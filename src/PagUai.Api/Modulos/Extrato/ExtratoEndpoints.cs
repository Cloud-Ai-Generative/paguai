using Microsoft.EntityFrameworkCore;
using PagUai.Api.Infra;

namespace PagUai.Api.Modulos.Extrato;

public record LancamentoResponse(DateTime Data, string Tipo, decimal Valor, string Descricao);
public record ExtratoResponse(Guid ContaId, string Titular, decimal SaldoAtual, DateTime De, DateTime Ate, IReadOnlyList<LancamentoResponse> Lancamentos);

public static class ExtratoEndpoints
{
    public static RouteHandlerBuilder MapExtrato(this IEndpointRouteBuilder app)
    {
        // VULNERABILIDADE PROPOSITAL (Sprints 11 e 12): não há autenticação nem verificação de dono da conta.
        // Qualquer pessoa que souber o id consegue ver o extrato de outra conta (BOLA, OWASP API1).
        // NÃO use este código em produção.
        return app.MapGet("/contas/{id:guid}/extrato", async (Guid id, DateTime? de, DateTime? ate, PagUaiDbContext db, CancellationToken ct) =>
        {
            var conta = await db.Contas.FindAsync([id], ct) ?? throw ErroNegocio.NaoEncontrado("Conta");

            var fim = (ate ?? DateTime.UtcNow).ToUniversalTime();
            var inicio = (de ?? fim.AddDays(-30)).ToUniversalTime();
            if (inicio > fim)
            {
                throw ErroNegocio.Invalido("A data inicial deve ser anterior à data final.");
            }

            var lancamentos = await db.Lancamentos
                .Where(l => l.ContaId == id && l.Data >= inicio && l.Data <= fim)
                .OrderByDescending(l => l.Data)
                .Select(l => new LancamentoResponse(l.Data, l.Tipo.ToString(), l.Valor, l.Descricao))
                .ToListAsync(ct);

            return new ExtratoResponse(conta.Id, conta.Titular, conta.Saldo, inicio, fim, lancamentos);
        })
        .WithTags("Extrato")
        .WithName("ConsultarExtrato")
        .WithSummary("Consulta o extrato de uma conta (padrão: últimos 30 dias)");
    }
}
