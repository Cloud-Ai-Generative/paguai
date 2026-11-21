using Microsoft.EntityFrameworkCore;
using PagUai.Api.Compartilhado;

namespace PagUai.Api.Modulos.Contas.Interno;

public record AbrirContaRequest(string Titular, string Documento, string ChavePix, decimal DepositoInicial);

/// <summary>Sprint 1: a resposta não traz mais o saldo. Consulte <c>GET /saldos/{contaId}</c> ou o extrato.</summary>
public record ContaResponse(Guid Id, string Titular, string ChavePix, DateTime CriadaEm);

public static class ContasEndpoints
{
    public static RouteGroupBuilder MapContas(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/contas").WithTags("Contas");

        grupo.MapPost("/", async (AbrirContaRequest req, ServicoDeContas contas, CancellationToken ct) =>
        {
            var conta = await contas.AbrirAsync(req.Titular, req.Documento, req.ChavePix, req.DepositoInicial, ct);
            return Results.Created($"/contas/{conta.Id}", ParaResponse(conta));
        })
        .WithName("AbrirConta")
        .WithSummary("Abre uma nova conta digital (o depósito inicial vira saldo no módulo de Pagamentos)");

        grupo.MapGet("/", async (ContasDbContext db, CancellationToken ct) =>
            await db.Contas.OrderBy(c => c.Titular)
                .Select(c => new ContaResponse(c.Id, c.Titular, c.ChavePix, c.CriadaEm))
                .ToListAsync(ct))
        .WithName("ListarContas")
        .WithSummary("Lista todas as contas");

        grupo.MapGet("/{id:guid}", async (Guid id, ContasDbContext db, CancellationToken ct) =>
        {
            var conta = await db.Contas.FindAsync([id], ct) ?? throw ErroNegocio.NaoEncontrado("Conta");
            return ParaResponse(conta);
        })
        .WithName("ConsultarConta")
        .WithSummary("Consulta uma conta pelo id");

        return grupo;
    }

    private static ContaResponse ParaResponse(Conta c) => new(c.Id, c.Titular, c.ChavePix, c.CriadaEm);
}
