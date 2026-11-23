using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PagUai.Mensageria;
using PagUai.Testes;

namespace PagUai.Notificacoes.Tests;

/// <summary>
/// Sprint 3: Notificações reage a PagamentoAprovado. O e-mail é trocado por um falso, que só anota;
/// o banco é de verdade (veja tests/Comum). Retry e dead letter estão em PagUai.Mensageria.Tests.
/// </summary>
public class AoAprovarPagamentoTests : IAsyncLifetime
{
    private readonly EmailFalso _email = new();
    private DbContextOptions<NotificacoesDbContext> _opcoes = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = new DbContextOptionsBuilder<NotificacoesDbContext>();
        NotificacoesDbContext.Configurar(builder, await AmbientePostgres.ConexaoAsync("notificacoes_testes"));
        _opcoes = builder.Options;
        await using var db = new NotificacoesDbContext(_opcoes);
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task Entregar(EventoRecebido evento)
    {
        await using var db = new NotificacoesDbContext(_opcoes);
        await new AoAprovarPagamento(db, _email, NullLogger<AoAprovarPagamento>.Instance).TratarAsync(evento, TestContext.Current.CancellationToken);
    }

    private static EventoRecebido PagamentoAprovado(decimal valor, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Tipo = "PagamentoAprovado",
        Versao = 1,
        OcorridoEm = DateTimeOffset.UtcNow,
        Dados = JsonSerializer.SerializeToElement(new
        {
            transferenciaId = Guid.NewGuid(), contaOrigemId = Guid.NewGuid(), titularOrigem = "Ana Clara Mendes",
            contaDestinoId = Guid.NewGuid(), titularDestino = "Loja do Seu Geraldo", chavePixDestino = "loja.geraldo@paguai.local",
            valor, descricao = "PIX", aprovadoEm = DateTime.UtcNow,
            campoNovoQueEsteServicoNaoConhece = "ignorado" // leitor tolerante
        }, Json.Opcoes)
    };

    [Fact]
    public async Task Avisa_quem_pagou_e_quem_recebeu()
    {
        await Entregar(PagamentoAprovado(1500m));

        Assert.Equal(
            ["Você enviou R$ 1.500,00 para Loja do Seu Geraldo.", "Você recebeu R$ 1.500,00 de Ana Clara Mendes."],
            _email.Textos);
    }

    /// <summary>O RabbitMQ entrega "pelo menos uma vez". O Seu Geraldo não pode receber o mesmo aviso duas vezes.</summary>
    [Fact]
    public async Task Mesmo_evento_duas_vezes_nao_manda_o_email_de_novo()
    {
        var evento = PagamentoAprovado(50m);

        await Entregar(evento);
        await Entregar(evento);

        Assert.Equal(2, _email.Textos.Count);
        await using var db = new NotificacoesDbContext(_opcoes);
        Assert.Equal(2, await db.Notificacoes.CountAsync(n => n.EventoId == evento.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>Se o e-mail falhar, o evento NÃO é marcado como processado: a próxima tentativa envia.</summary>
    [Fact]
    public async Task Falha_no_email_nao_marca_o_evento_como_processado()
    {
        var evento = PagamentoAprovado(50m);
        _email.Falhar = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Entregar(evento));

        _email.Falhar = false;
        await Entregar(evento);
        Assert.Equal(2, _email.Textos.Count);
    }

    [Fact]
    public async Task SimularFalha_faz_o_envio_falhar()
    {
        var smtp = new EnviadorSmtp(Options.Create(new NotificacaoOptions { SimularFalha = true }), NullLogger<EnviadorSmtp>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            smtp.EnviarAsync("Ana", "ana@teste.local", "assunto", "texto", TestContext.Current.CancellationToken));
    }

    private sealed class EmailFalso : IEnviadorDeEmail
    {
        public List<string> Textos { get; } = [];
        public bool Falhar { get; set; }

        public Task EnviarAsync(string destinatario, string endereco, string assunto, string texto, CancellationToken ct)
        {
            if (Falhar)
            {
                throw new InvalidOperationException("Servidor de e-mail fora do ar.");
            }

            Textos.Add(texto);
            return Task.CompletedTask;
        }
    }
}
