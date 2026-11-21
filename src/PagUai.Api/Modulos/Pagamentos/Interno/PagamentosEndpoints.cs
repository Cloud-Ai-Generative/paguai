using PagUai.Api.Compartilhado;
using PagUai.Api.Modulos.Pagamentos.Contratos;

namespace PagUai.Api.Modulos.Pagamentos.Interno;

public record TransferirRequest(Guid ContaOrigemId, string ChavePixDestino, decimal Valor);
public record TransferenciaResponse(Guid Id, Guid ContaOrigemId, string ChavePixDestino, decimal Valor, string Status, DateTime CriadaEm);
public record SaldoResponse(Guid ContaId, decimal Saldo);

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
            return new SaldoResponse(contaId, saldo);
        })
        .WithTags("Pagamentos (PIX simulado)")
        .WithName("ConsultarSaldo")
        .WithSummary("Consulta o saldo de uma conta (o saldo pertence ao contexto de Pagamentos)");
    }
}
