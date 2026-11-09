using Microsoft.EntityFrameworkCore;
using PagUai.Api.Infra;

namespace PagUai.Api.Modulos.Contas;

public record AbrirContaRequest(string Titular, string Documento, string ChavePix, decimal DepositoInicial);
public record ContaResponse(Guid Id, string Titular, string ChavePix, decimal Saldo, DateTime CriadaEm);

public static class ContasEndpoints
{
    public static RouteGroupBuilder MapContas(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/contas").WithTags("Contas");

        grupo.MapPost("/", async (AbrirContaRequest req, PagUaiDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Titular) || string.IsNullOrWhiteSpace(req.Documento) || string.IsNullOrWhiteSpace(req.ChavePix))
            {
                throw ErroNegocio.Invalido("Titular, documento e chave PIX são obrigatórios.");
            }

            if (req.DepositoInicial < 0)
            {
                throw ErroNegocio.Invalido("O depósito inicial não pode ser negativo.");
            }

            if (await db.Contas.AnyAsync(c => c.ChavePix == req.ChavePix || c.Documento == req.Documento, ct))
            {
                throw new ErroNegocio(StatusCodes.Status409Conflict, "Conta já existe", "Já existe uma conta com este documento ou chave PIX.");
            }

            var conta = new Conta
            {
                Titular = req.Titular.Trim(),
                Documento = req.Documento.Trim(),
                ChavePix = req.ChavePix.Trim(),
                Saldo = req.DepositoInicial
            };
            db.Contas.Add(conta);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/contas/{conta.Id}", ParaResponse(conta));
        })
        .WithName("AbrirConta")
        .WithSummary("Abre uma nova conta digital");

        grupo.MapGet("/", async (PagUaiDbContext db, CancellationToken ct) =>
            await db.Contas.OrderBy(c => c.Titular)
                .Select(c => new ContaResponse(c.Id, c.Titular, c.ChavePix, c.Saldo, c.CriadaEm))
                .ToListAsync(ct))
        .WithName("ListarContas")
        .WithSummary("Lista todas as contas");

        grupo.MapGet("/{id:guid}", async (Guid id, PagUaiDbContext db, CancellationToken ct) =>
        {
            var conta = await db.Contas.FindAsync([id], ct) ?? throw ErroNegocio.NaoEncontrado("Conta");
            return ParaResponse(conta);
        })
        .WithName("ConsultarConta")
        .WithSummary("Consulta uma conta pelo id");

        return grupo;
    }

    private static ContaResponse ParaResponse(Conta c) => new(c.Id, c.Titular, c.ChavePix, c.Saldo, c.CriadaEm);
}
