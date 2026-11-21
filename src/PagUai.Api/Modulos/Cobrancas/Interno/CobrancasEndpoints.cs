using PagUai.Api.Compartilhado;
using PagUai.Api.Modulos.Contas.Contratos;
using PagUai.Api.Modulos.Pagamentos.Contratos;

namespace PagUai.Api.Modulos.Cobrancas.Interno;

public record CriarCobrancaRequest(Guid ContaRecebedoraId, decimal Valor, string Descricao);
public record PagarCobrancaRequest(Guid ContaPagadoraId);
public record CobrancaResponse(Guid Id, Guid ContaRecebedoraId, decimal Valor, string Descricao, string Status, DateTime CriadaEm, DateTime? PagaEm);

public static class CobrancasEndpoints
{
    public static RouteGroupBuilder MapCobrancas(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/cobrancas").WithTags("Cobranças");

        grupo.MapPost("/", async (CriarCobrancaRequest req, CobrancasDbContext db, IContas contas, CancellationToken ct) =>
        {
            if (req.Valor <= 0)
            {
                throw ErroNegocio.Invalido("O valor da cobrança deve ser maior que zero.");
            }

            _ = await contas.ObterAsync(req.ContaRecebedoraId, ct) ?? throw ErroNegocio.NaoEncontrado("Conta recebedora");

            var cobranca = new Cobranca
            {
                ContaRecebedoraId = req.ContaRecebedoraId,
                Valor = req.Valor,
                Descricao = string.IsNullOrWhiteSpace(req.Descricao) ? "Cobrança PagUai" : req.Descricao.Trim()
            };
            db.Cobrancas.Add(cobranca);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/cobrancas/{cobranca.Id}", ParaResponse(cobranca));
        })
        .WithName("CriarCobranca")
        .WithSummary("Cria uma cobrança para um lojista receber");

        grupo.MapGet("/{id:guid}", async (Guid id, CobrancasDbContext db, CancellationToken ct) =>
        {
            var cobranca = await db.Cobrancas.FindAsync([id], ct) ?? throw ErroNegocio.NaoEncontrado("Cobrança");
            return ParaResponse(cobranca);
        })
        .WithName("ConsultarCobranca")
        .WithSummary("Consulta uma cobrança");

        // Sprint 1: Cobranças usa os CONTRATOS de Contas e de Pagamentos, não mais o serviço interno.
        grupo.MapPost("/{id:guid}/pagar", async (Guid id, PagarCobrancaRequest req, CobrancasDbContext db,
            IContas contas, IPagamentos pagamentos, UnidadeDeTrabalho unidade, CancellationToken ct) =>
        {
            var cobranca = await unidade.ExecutarAsync(async () =>
            {
                var cobranca = await db.Cobrancas.FindAsync([id], ct) ?? throw ErroNegocio.NaoEncontrado("Cobrança");
                if (cobranca.Status == StatusCobranca.Paga)
                {
                    throw new ErroNegocio(StatusCodes.Status409Conflict, "Cobrança já paga", "Esta cobrança já foi paga.");
                }

                var recebedor = await contas.ObterAsync(cobranca.ContaRecebedoraId, ct) ?? throw ErroNegocio.NaoEncontrado("Conta recebedora");

                // Uma transação para tudo: a transferência e a baixa da cobrança.
                var transferencia = await pagamentos.TransferirAsync(req.ContaPagadoraId, recebedor.ChavePix, cobranca.Valor,
                    $"Pagamento de cobrança ({cobranca.Descricao})", ct);
                if (!transferencia.Concluida)
                {
                    throw ErroNegocio.Invalido("Pagamento reprovado pelo antifraude.");
                }

                cobranca.Status = StatusCobranca.Paga;
                cobranca.PagaEm = DateTime.UtcNow;
                cobranca.TransferenciaId = transferencia.Id;
                await db.SaveChangesAsync(ct);
                return cobranca;
            }, ct);
            return Results.Ok(ParaResponse(cobranca));
        })
        .WithName("PagarCobranca")
        .WithSummary("Paga uma cobrança a partir de uma conta");

        return grupo;
    }

    private static CobrancaResponse ParaResponse(Cobranca c) =>
        new(c.Id, c.ContaRecebedoraId, c.Valor, c.Descricao, c.Status.ToString(), c.CriadaEm, c.PagaEm);
}
