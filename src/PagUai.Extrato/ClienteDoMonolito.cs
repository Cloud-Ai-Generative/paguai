using System.Net;
using System.Net.Http.Json;

namespace PagUai.Extrato;

// Formatos que o MONÓLITO devolve. São contratos de outra equipe: o Extrato só lê o que precisa.
public record ContaDoMonolito(Guid Id, string Titular, string ChavePix);
public record SaldoDoMonolito(Guid ContaId, decimal Saldo, DateTime ConsultadoEm);
public record MovimentoDoMonolito(Guid Id, DateTime Data, string Tipo, decimal Valor, string Descricao);
public record PaginaDoMonolito(Guid ContaId, IReadOnlyList<MovimentoDoMonolito> Itens, string? ProximoCursor);

/// <summary>O monólito respondeu que a conta não existe.</summary>
public class ContaNaoEncontrada : Exception;

/// <summary>O monólito recusou o cursor.</summary>
public class CursorInvalido : Exception;

/// <summary>
/// Chamadas HTTP do Extrato para o monólito (Contas e Pagamentos).
///
/// Antes, na Sprint 1, eram chamadas de método dentro do mesmo processo. Agora há uma REDE no
/// meio: cada consulta de extrato vira três requisições HTTP, que somam latência e podem falhar.
///
/// DOR PROPOSITAL (Sprint 9): nenhum timeout foi configurado. O HttpClient espera até 100 segundos
/// (o padrão do .NET) antes de desistir. Se o monólito ficar lento, o Extrato fica lento junto.
/// </summary>
public class ClienteDoMonolito(HttpClient http)
{
    public async Task<ContaDoMonolito> ObterContaAsync(Guid contaId, CancellationToken ct)
    {
        using var resposta = await http.GetAsync($"/contas/{contaId}", ct);
        if (resposta.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ContaNaoEncontrada();
        }

        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ContaDoMonolito>(ct))!;
    }

    public async Task<SaldoDoMonolito> ObterSaldoAsync(Guid contaId, CancellationToken ct)
    {
        using var resposta = await http.GetAsync($"/saldos/{contaId}", ct);
        if (resposta.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ContaNaoEncontrada();
        }

        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<SaldoDoMonolito>(ct))!;
    }

    public async Task<PaginaDoMonolito> ListarMovimentosAsync(Guid contaId, DateTime de, DateTime ate, int limite, string? cursor, CancellationToken ct)
    {
        var url = $"/pagamentos/contas/{contaId}/movimentos?de={Uri.EscapeDataString(de.ToString("O"))}&ate={Uri.EscapeDataString(ate.ToString("O"))}&limite={limite}";
        if (!string.IsNullOrEmpty(cursor))
        {
            url += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        using var resposta = await http.GetAsync(url, ct);
        if (resposta.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            throw new CursorInvalido();
        }

        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<PaginaDoMonolito>(ct))!;
    }
}
