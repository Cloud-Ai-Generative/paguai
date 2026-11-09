namespace PagUai.Api.Modulos.Transferencias;

public record TransferirRequest(Guid ContaOrigemId, string ChavePixDestino, decimal Valor);
public record TransferenciaResponse(Guid Id, Guid ContaOrigemId, string ChavePixDestino, decimal Valor, string Status, DateTime CriadaEm);

public static class TransferenciasEndpoints
{
    public static RouteGroupBuilder MapTransferencias(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/transferencias").WithTags("Transferências (PIX simulado)");

        grupo.MapPost("/", async (TransferirRequest req, TransferenciaService servico, CancellationToken ct) =>
        {
            var t = await servico.TransferirAsync(req.ContaOrigemId, req.ChavePixDestino, req.Valor, "PIX", ct);
            return Results.Created($"/transferencias/{t.Id}", ParaResponse(t));
        })
        .WithName("Transferir")
        .WithSummary("Transfere por chave PIX simulada (passa pelo antifraude e notifica as duas contas)");

        return grupo;
    }

    public static TransferenciaResponse ParaResponse(Transferencia t) =>
        new(t.Id, t.ContaOrigemId, t.ChavePixDestino, t.Valor, t.Status.ToString(), t.CriadaEm);
}
