using Microsoft.EntityFrameworkCore;
using PagUai.Mensageria;

namespace PagUai.Extrato;

// Os campos de cada evento que o Extrato usa (contratos/eventos.asyncapi.yaml). O resto é ignorado.
public record ContaAberta(Guid ContaId, string Titular, decimal DepositoInicial, DateTime AbertaEm);

public record PagamentoAprovado(Guid ContaOrigemId, string TitularOrigem, Guid ContaDestinoId, string TitularDestino,
    decimal Valor, string Descricao, DateTime AprovadoEm);

/// <summary>A conta ainda não chegou ao Extrato. Pode ser só ordem de chegada: vale tentar de novo.</summary>
public class ContaAindaDesconhecida(Guid contaId) : Exception($"A conta {contaId} ainda não está no Extrato.");

/// <summary>
/// Mantém o modelo de leitura em dia. Cada evento é aplicado numa única gravação (uma transação),
/// junto com o registro de "evento processado": ou tudo entra, ou nada entra.
/// </summary>
public class AtualizarExtrato(ExtratoDbContext db, ILogger<AtualizarExtrato> logger) : IManipuladorDeEventos
{
    public static readonly ConfiguracaoDaFila Fila = new("extrato", ["conta.aberta", "pagamento.aprovado"]);

    public async Task TratarAsync(EventoRecebido evento, CancellationToken ct)
    {
        if (await db.EventosProcessados.AnyAsync(e => e.EventoId == evento.Id, ct))
        {
            logger.LogInformation("Evento {Id} já aplicado ao extrato. Ignorado.", evento.Id);
            return;
        }

        switch (evento.Tipo)
        {
            case "ContaAberta":
                AplicarContaAberta(evento.Id, evento.Ler<ContaAberta>());
                break;
            case "PagamentoAprovado":
                await AplicarPagamentoAsync(evento.Id, evento.Ler<PagamentoAprovado>(), ct);
                break;
            default:
                throw new MensagemInvalida($"O Extrato não trata eventos do tipo {evento.Tipo}.");
        }

        db.EventosProcessados.Add(new EventoProcessado { EventoId = evento.Id, Tipo = evento.Tipo });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Evento {Tipo} {Id} aplicado ao extrato.", evento.Tipo, evento.Id);
    }

    private void AplicarContaAberta(Guid eventoId, ContaAberta conta)
    {
        var aberta = DateTime.SpecifyKind(conta.AbertaEm, DateTimeKind.Utc);
        db.Contas.Add(new ContaDoExtrato { Id = conta.ContaId, Titular = conta.Titular, Saldo = conta.DepositoInicial, SaldoEm = aberta });
        if (conta.DepositoInicial > 0)
        {
            db.Lancamentos.Add(new LancamentoDoExtrato
            {
                ContaId = conta.ContaId, EventoId = eventoId, Data = aberta,
                Tipo = "Credito", Valor = conta.DepositoInicial, Descricao = "Depósito inicial"
            });
        }
    }

    private async Task AplicarPagamentoAsync(Guid eventoId, PagamentoAprovado pagamento, CancellationToken ct)
    {
        var origem = await db.Contas.FindAsync([pagamento.ContaOrigemId], ct) ?? throw new ContaAindaDesconhecida(pagamento.ContaOrigemId);
        var destino = await db.Contas.FindAsync([pagamento.ContaDestinoId], ct) ?? throw new ContaAindaDesconhecida(pagamento.ContaDestinoId);
        var quando = DateTime.SpecifyKind(pagamento.AprovadoEm, DateTimeKind.Utc);

        origem.Saldo -= pagamento.Valor;
        origem.SaldoEm = quando;
        destino.Saldo += pagamento.Valor;
        destino.SaldoEm = quando;

        db.Lancamentos.Add(new LancamentoDoExtrato
        {
            ContaId = origem.Id, EventoId = eventoId, Data = quando,
            Tipo = "Debito", Valor = pagamento.Valor, Descricao = $"{pagamento.Descricao} para {pagamento.TitularDestino}"
        });
        db.Lancamentos.Add(new LancamentoDoExtrato
        {
            ContaId = destino.Id, EventoId = eventoId, Data = quando,
            Tipo = "Credito", Valor = pagamento.Valor, Descricao = $"{pagamento.Descricao} de {pagamento.TitularOrigem}"
        });
    }
}
