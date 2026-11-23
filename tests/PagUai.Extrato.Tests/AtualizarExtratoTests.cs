using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PagUai.Mensageria;
using PagUai.Testes;

namespace PagUai.Extrato.Tests;

/// <summary>
/// Sprint 3: o modelo de leitura do Extrato, montado com eventos.
/// Os eventos são entregues direto ao manipulador, sem RabbitMQ: o que se testa aqui é a REAÇÃO a eles.
/// </summary>
public class AtualizarExtratoTests : IAsyncLifetime
{
    private DbContextOptions<ExtratoDbContext> _opcoes = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = new DbContextOptionsBuilder<ExtratoDbContext>();
        ExtratoDbContext.Configurar(builder, await AmbientePostgres.ConexaoAsync("extrato_projecao"));
        _opcoes = builder.Options;
        await using var db = new ExtratoDbContext(_opcoes);
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task Entregar(EventoRecebido evento)
    {
        await using var db = new ExtratoDbContext(_opcoes);
        await new AtualizarExtrato(db, NullLogger<AtualizarExtrato>.Instance).TratarAsync(evento, TestContext.Current.CancellationToken);
    }

    private static EventoRecebido Evento(string tipo, object dados, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Tipo = tipo,
        Versao = 1,
        OcorridoEm = DateTimeOffset.UtcNow,
        Dados = JsonSerializer.SerializeToElement(dados, PagUai.Mensageria.Json.Opcoes)
    };

    private static object ContaAberta(Guid id, string titular, decimal deposito) =>
        new { contaId = id, titular, chavePix = $"{id:N}@teste.local", depositoInicial = deposito, abertaEm = DateTime.UtcNow };

    private static object Pagamento(Guid origem, Guid destino, decimal valor) => new
    {
        transferenciaId = Guid.NewGuid(), contaOrigemId = origem, titularOrigem = "Ana", contaDestinoId = destino,
        titularDestino = "Loja do Seu Geraldo", chavePixDestino = "loja@teste.local", valor, descricao = "PIX", aprovadoEm = DateTime.UtcNow
    };

    private async Task<ContaDoExtrato> Conta(Guid id)
    {
        await using var db = new ExtratoDbContext(_opcoes);
        return await db.Contas.SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<List<LancamentoDoExtrato>> Lancamentos(Guid conta)
    {
        await using var db = new ExtratoDbContext(_opcoes);
        return await db.Lancamentos.Where(l => l.ContaId == conta).ToListAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ContaAberta_cria_a_conta_com_o_deposito_inicial()
    {
        var ana = Guid.NewGuid();

        await Entregar(Evento("ContaAberta", ContaAberta(ana, "Ana", 100m)));

        Assert.Equal(100m, (await Conta(ana)).Saldo);
        Assert.Equal("Depósito inicial", Assert.Single(await Lancamentos(ana)).Descricao);
    }

    [Fact]
    public async Task PagamentoAprovado_debita_um_e_credita_o_outro()
    {
        var ana = Guid.NewGuid();
        var loja = Guid.NewGuid();
        await Entregar(Evento("ContaAberta", ContaAberta(ana, "Ana", 100m)));
        await Entregar(Evento("ContaAberta", ContaAberta(loja, "Loja do Seu Geraldo", 0m)));

        await Entregar(Evento("PagamentoAprovado", Pagamento(ana, loja, 30m)));

        Assert.Equal(70m, (await Conta(ana)).Saldo);
        Assert.Equal(30m, (await Conta(loja)).Saldo);
        Assert.Contains(await Lancamentos(ana), l => l is { Tipo: "Debito", Valor: 30m, Descricao: "PIX para Loja do Seu Geraldo" });
        Assert.Contains(await Lancamentos(loja), l => l is { Tipo: "Credito", Valor: 30m, Descricao: "PIX de Ana" });
    }

    /// <summary>"Pelo menos uma vez": o mesmo evento pode chegar de novo. O efeito não pode dobrar.</summary>
    [Fact]
    public async Task Mesmo_evento_duas_vezes_nao_duplica_o_lancamento()
    {
        var ana = Guid.NewGuid();
        var loja = Guid.NewGuid();
        await Entregar(Evento("ContaAberta", ContaAberta(ana, "Ana", 100m)));
        await Entregar(Evento("ContaAberta", ContaAberta(loja, "Loja", 0m)));
        var pagamento = Evento("PagamentoAprovado", Pagamento(ana, loja, 30m));

        await Entregar(pagamento);
        await Entregar(pagamento);

        Assert.Equal(70m, (await Conta(ana)).Saldo);
        Assert.Equal(2, (await Lancamentos(ana)).Count);
    }

    /// <summary>Se o pagamento chegar antes da conta, o manipulador falha e o consumidor tenta de novo depois.</summary>
    [Fact]
    public async Task Pagamento_de_conta_ainda_desconhecida_falha_para_ser_tentado_de_novo()
    {
        await Assert.ThrowsAsync<ContaAindaDesconhecida>(() =>
            Entregar(Evento("PagamentoAprovado", Pagamento(Guid.NewGuid(), Guid.NewGuid(), 10m))));
    }

    [Fact]
    public async Task Tipo_desconhecido_e_mensagem_invalida()
    {
        await Assert.ThrowsAsync<MensagemInvalida>(() => Entregar(Evento("CobrancaCriada", new { id = 1 })));
    }
}
