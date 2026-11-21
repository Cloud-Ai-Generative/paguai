using PagUai.Api.Compartilhado;
using PagUai.Api.Modulos.Pagamentos.Contratos;

namespace PagUai.Api.Modulos.Pagamentos.Interno;

public record TransferirRequest(Guid ContaOrigemId, string ChavePixDestino, decimal Valor);
public record TransferenciaResponse(Guid Id, Guid ContaOrigemId, string ChavePixDestino, decimal Valor, string Status, DateTime CriadaEm);
public record SaldoResponse(Guid ContaId, decimal Saldo, DateTime ConsultadoEm);
public record MovimentoResponse(Guid Id, DateTime Data, string Tipo, decimal Valor, string Descricao);
public record PaginaDeMovimentosResponse(Guid ContaId, IReadOnlyList<MovimentoResponse> Itens, string? ProximoCursor);

public static class PagamentosEndpoints
{
    public static void MapPagamentos(this IEndpointRouteBuilder app)
    {
        app.MapPost("/transferencias", async (TransferirRequest req, IPagamentos pagamentos, CancellationToken ct) =>
        {
            var t = await pagamentos.TransferirAsync(req.ContaOrigemId, req.ChavePixDestino, req.Valor, "PIX", ct);
            return Results.Created($"/transferencias/{t.Id}",
                new TransferenciaResponse(t.Id, t.ContaOrigemId, req.ChavePixDestino, t.Valor, t.Status, t.CriadaEm));
        })
        .WithTags("Pagamentos (PIX simulado)")
        .WithName("Transferir")
        .WithSummary("Transfere por chave PIX simulada (passa pelo antifraude e notifica as duas contas)");

        app.MapGet("/saldos/{contaId:guid}", async (Guid contaId, IPagamentos pagamentos, CancellationToken ct) =>
        {
            var saldo = await pagamentos.ObterSaldoAsync(contaId, ct) ?? throw ErroNegocio.NaoEncontrado("Saldo da conta");
            return new SaldoResponse(contaId, saldo, DateTime.UtcNow);
        })
        .WithTags("Pagamentos (PIX simulado)")
        .WithName("ConsultarSaldo")
        .WithSummary("Consulta o saldo de uma conta (o saldo pertence ao contexto de Pagamentos)");

        // Sprint 2: usado pelo serviço de Extrato, que agora roda fora do monólito.
        app.MapGet("/pagamentos/contas/{contaId:guid}/movimentos", async (Guid contaId, DateTime de, DateTime ate,
            int? limite, string? cursor, IPagamentos pagamentos, CancellationToken ct) =>
        {
            var tamanho = Math.Clamp(limite ?? 100, 1, 500);
            var pagina = await pagamentos.ListarMovimentosAsync(contaId, de.ToUniversalTime(), ate.ToUniversalTime(), tamanho, cursor, ct);
            return new PaginaDeMovimentosResponse(contaId,
                pagina.Itens.Select(m => new MovimentoResponse(m.Id, m.Data, m.Tipo, m.Valor, m.Descricao)).ToList(),
                pagina.ProximoCursor);
        })
        .WithTags("Pagamentos (uso interno)")
        .WithName("ListarMovimentos")
        .WithSummary("Movimentos de uma conta no período, paginados por cursor (usado pelo serviço de Extrato)");
    }
}
