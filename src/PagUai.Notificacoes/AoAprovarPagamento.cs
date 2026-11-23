using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PagUai.Mensageria;

namespace PagUai.Notificacoes;

/// <summary>
/// Os campos de PagamentoAprovado que ESTE serviço usa (contratos/eventos.asyncapi.yaml).
/// O evento traz mais campos; eles são ignorados (leitor tolerante).
/// </summary>
public record PagamentoAprovado(Guid TransferenciaId, Guid ContaOrigemId, string TitularOrigem,
    Guid ContaDestinoId, string TitularDestino, decimal Valor);

/// <summary>Avisa quem pagou e quem recebeu, por e-mail.</summary>
public class AoAprovarPagamento(
    NotificacoesDbContext db,
    IEnviadorDeEmail email,
    ILogger<AoAprovarPagamento> logger) : IManipuladorDeEventos
{
    public static readonly ConfiguracaoDaFila Fila = new("notificacoes", ["pagamento.aprovado"]);

    private static readonly CultureInfo Real = CultureInfo.GetCultureInfo("pt-BR");

    public async Task TratarAsync(EventoRecebido evento, CancellationToken ct)
    {
        if (evento.Tipo != "PagamentoAprovado")
        {
            throw new MensagemInvalida($"A fila {Fila.Nome} não trata eventos do tipo {evento.Tipo}.");
        }

        // IDEMPOTÊNCIA: o mesmo evento pode chegar mais de uma vez. Já foi tratado? Não faz nada.
        if (await db.EventosProcessados.AnyAsync(e => e.EventoId == evento.Id, ct))
        {
            logger.LogInformation("Evento {Id} já processado. Ignorado, para não enviar o e-mail de novo.", evento.Id);
            return;
        }

        var pagamento = evento.Ler<PagamentoAprovado>();
        var valor = $"R$ {pagamento.Valor.ToString("N2", Real)}"; // R$ 1.500,00
        var avisos = new[]
        {
            (Conta: pagamento.ContaOrigemId, Nome: pagamento.TitularOrigem, Texto: $"Você enviou {valor} para {pagamento.TitularDestino}."),
            (Conta: pagamento.ContaDestinoId, Nome: pagamento.TitularDestino, Texto: $"Você recebeu {valor} de {pagamento.TitularOrigem}.")
        };

        foreach (var aviso in avisos)
        {
            var endereco = $"{aviso.Conta:N}@clientes.paguai.local";
            await email.EnviarAsync(aviso.Nome, endereco, "PagUai: nova movimentação na sua conta", aviso.Texto, ct);
            db.Notificacoes.Add(new Notificacao { EventoId = evento.Id, ContaId = aviso.Conta, Destinatario = endereco, Mensagem = aviso.Texto });
            logger.LogInformation("E-mail enviado para {Titular}: {Texto}", aviso.Nome, aviso.Texto);
        }

        // Registro do evento e notificações na mesma gravação (uma transação).
        // Limite que fica: se o processo cair DEPOIS de enviar e ANTES desta linha, o e-mail sai de novo
        // na próxima entrega. Um servidor de e-mail não participa da transação do banco.
        db.EventosProcessados.Add(new EventoProcessado { EventoId = evento.Id, Tipo = evento.Tipo });
        await db.SaveChangesAsync(ct);
    }
}
