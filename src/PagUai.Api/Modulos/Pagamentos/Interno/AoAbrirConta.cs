using PagUai.Api.Compartilhado;
using PagUai.Api.Modulos.Contas.Contratos;

namespace PagUai.Api.Modulos.Pagamentos.Interno;

/// <summary>
/// Reage ao evento <see cref="ContaAberta"/> do módulo Contas: cria o saldo e registra o depósito inicial.
/// Pagamentos conhece o contrato de Contas; Contas não conhece Pagamentos.
/// </summary>
public class AoAbrirConta(PagamentosDbContext db) : IManipulador<ContaAberta>
{
    public async Task TratarAsync(ContaAberta evento, CancellationToken ct)
    {
        db.Saldos.Add(new Saldo { ContaId = evento.ContaId, Valor = evento.DepositoInicial });
        if (evento.DepositoInicial > 0)
        {
            db.Lancamentos.Add(new Lancamento
            {
                ContaId = evento.ContaId,
                Tipo = TipoLancamento.Credito,
                Valor = evento.DepositoInicial,
                Descricao = "Depósito inicial"
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
