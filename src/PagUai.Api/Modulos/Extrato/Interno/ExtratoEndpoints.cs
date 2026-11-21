using PagUai.Api.Compartilhado;
using PagUai.Api.Modulos.Contas.Contratos;
using PagUai.Api.Modulos.Pagamentos.Contratos;

namespace PagUai.Api.Modulos.Extrato.Interno;

public record LancamentoResponse(DateTime Data, string Tipo, decimal Valor, string Descricao);
public record ExtratoResponse(Guid ContaId, string Titular, decimal SaldoAtual, DateTime De, DateTime Ate, IReadOnlyList<LancamentoResponse> Lancamentos);

/// <summary>
/// Sprint 1: o Extrato virou um módulo SÓ DE LEITURA. Não tem tabelas: monta a resposta
/// perguntando a Contas (titular) e a Pagamentos (saldo e lançamentos), pelos contratos públicos.
/// </summary>
public static class ExtratoEndpoints
{
    public static RouteHandlerBuilder MapExtrato(this IEndpointRouteBuilder app)
    {
        // VULNERABILIDADE PROPOSITAL (Sprints 11 e 12): não há autenticação nem verificação de dono da conta.
        // Qualquer pessoa que souber o id consegue ver o extrato de outra conta (BOLA, OWASP API1).
        // NÃO use este código em produção.
        return app.MapGet("/contas/{id:guid}/extrato", async (Guid id, DateTime? de, DateTime? ate,
            IContas contas, IPagamentos pagamentos, CancellationToken ct) =>
        {
            var conta = await contas.ObterAsync(id, ct) ?? throw ErroNegocio.NaoEncontrado("Conta");

            var fim = (ate ?? DateTime.UtcNow).ToUniversalTime();
            var inicio = (de ?? fim.AddDays(-30)).ToUniversalTime();
            if (inicio > fim)
            {
                throw ErroNegocio.Invalido("A data inicial deve ser anterior à data final.");
            }

            var saldo = await pagamentos.ObterSaldoAsync(id, ct) ?? 0m;
            var movimentos = await pagamentos.ListarMovimentosAsync(id, inicio, fim, ct);

            return new ExtratoResponse(conta.Id, conta.Titular, saldo, inicio, fim,
                movimentos.Select(m => new LancamentoResponse(m.Data, m.Tipo, m.Valor, m.Descricao)).ToList());
        })
        .WithTags("Extrato")
        .WithName("ConsultarExtrato")
        .WithSummary("Consulta o extrato de uma conta (padrão: últimos 30 dias)");
    }
}
