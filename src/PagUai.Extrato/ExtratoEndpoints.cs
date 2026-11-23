using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PagUai.Extrato;

// Respostas no formato do CONTRATO (contratos/extrato.openapi.yaml). Se mudar aqui, o teste
// ContratoTests falha até o contrato ser atualizado, revisado e aprovado.
public record LancamentoResponse(DateTime Data, string Tipo, decimal Valor, string Descricao);
public record ExtratoResponse(Guid ContaId, string Titular, decimal SaldoAtual, DateTime SaldoEm, DateTime De, DateTime Ate,
    IReadOnlyList<LancamentoResponse> Lancamentos, string? ProximoCursor);

public static class ExtratoEndpoints
{
    public const int LimitePadrao = 100;
    public const int LimiteMaximo = 200;
    public const int DiasPadrao = 30;
    public const int DiasMaximo = 90;

    public static void MapExtrato(this IEndpointRouteBuilder app)
    {
        // VULNERABILIDADE PROPOSITAL (Sprints 11 e 12), registrada também no contrato:
        // não há autenticação nem verificação de dono da conta (BOLA, OWASP API1).
        app.MapGet("/contas/{contaId:guid}/extrato", async (Guid contaId, DateTime? de, DateTime? ate, int? limite, string? cursor,
            ExtratoDbContext db, CancellationToken ct) =>
        {
            var tamanho = limite ?? LimitePadrao;
            if (tamanho is < 1 or > LimiteMaximo)
            {
                return Problemas.Criar(422, "parametro-invalido", "Parâmetro inválido", $"O limite deve estar entre 1 e {LimiteMaximo}.");
            }

            var fim = (ate ?? DateTime.UtcNow).ToUniversalTime();
            var inicio = (de ?? fim.AddDays(-DiasPadrao)).ToUniversalTime();
            if (inicio > fim)
            {
                return Problemas.Criar(422, "periodo-invalido", "Período inválido", "A data inicial deve ser anterior à data final.");
            }

            if (fim - inicio > TimeSpan.FromDays(DiasMaximo))
            {
                return Problemas.Criar(422, "periodo-invalido", "Período inválido", $"O período máximo é de {DiasMaximo} dias.");
            }

            var temCursor = CursorDoExtrato.TentarLer(cursor, out var dataCursor, out var idCursor);
            if (!temCursor && !string.IsNullOrEmpty(cursor))
            {
                return Problemas.Criar(422, "cursor-invalido", "Cursor inválido", "Use o valor de proximoCursor recebido na página anterior.");
            }

            try
            {
                // Sprint 3: uma consulta ao PRÓPRIO banco. Até a Sprint 2 eram três chamadas HTTP ao monólito.
                var conta = await db.Contas.AsNoTracking().FirstOrDefaultAsync(c => c.Id == contaId, ct);
                if (conta is null)
                {
                    return Problemas.Criar(404, "conta-nao-encontrada", "Conta não encontrada", "Não existe conta com este id.");
                }

                var consulta = db.Lancamentos.AsNoTracking().Where(l => l.ContaId == contaId && l.Data >= inicio && l.Data <= fim);
                if (temCursor)
                {
                    consulta = consulta.Where(l => l.Data < dataCursor || (l.Data == dataCursor && l.Id.CompareTo(idCursor) < 0));
                }

                var itens = await consulta
                    .OrderByDescending(l => l.Data).ThenByDescending(l => l.Id)
                    .Take(tamanho + 1)
                    .ToListAsync(ct);

                string? proximo = null;
                if (itens.Count > tamanho)
                {
                    itens.RemoveAt(itens.Count - 1);
                    proximo = CursorDoExtrato.Criar(itens[^1].Data, itens[^1].Id);
                }

                return Results.Ok(new ExtratoResponse(conta.Id, conta.Titular, conta.Saldo, conta.SaldoEm, inicio, fim,
                    itens.Select(l => new LancamentoResponse(l.Data, l.Tipo, l.Valor, l.Descricao)).ToList(), proximo));
            }
            catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException { InnerException: NpgsqlException })
            {
                // Falha parcial: o Extrato está no ar, mas o banco dele não responde.
                return Problemas.Criar(503, "dependencia-indisponivel", "Dependência indisponível",
                    "O banco do Extrato está indisponível. Tente novamente em instantes.");
            }
        })
        .WithName("consultarExtrato");
    }
}

/// <summary>O cursor é opaco para quem consome: a data e o id do último item, em Base64 seguro para URL.</summary>
public static class CursorDoExtrato
{
    public static string Criar(DateTime data, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{data.Ticks}:{id}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool TentarLer(string? cursor, out DateTime data, out Guid id)
    {
        data = default;
        id = default;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            var partes = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split(':');
            if (partes.Length == 2 && long.TryParse(partes[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) && Guid.TryParse(partes[1], out id))
            {
                data = new DateTime(ticks, DateTimeKind.Utc);
                return true;
            }
        }
        catch (FormatException)
        {
        }

        return false;
    }
}

/// <summary>Erros no formato Problem Details (RFC 9457), com um <c>type</c> para cada erro, como diz o contrato.</summary>
public static class Problemas
{
    public const string Base = "https://paguai.local/problemas/";

    public static IResult Criar(int status, string tipo, string titulo, string detalhe) =>
        Results.Problem(type: Base + tipo, title: titulo, statusCode: status, detail: detalhe);
}
