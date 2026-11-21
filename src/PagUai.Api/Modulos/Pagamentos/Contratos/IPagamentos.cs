namespace PagUai.Api.Modulos.Pagamentos.Contratos;

// ── CONTRATO PÚBLICO do módulo Pagamentos ──────────────────────────────────────
// Pagamentos é o DONO do saldo e dos lançamentos. Ninguém mais grava essas tabelas.

public record TransferenciaRealizada(Guid Id, Guid ContaOrigemId, Guid ContaDestinoId, decimal Valor, bool Concluida, string Status, DateTime CriadaEm);

public record Movimento(Guid Id, DateTime Data, string Tipo, decimal Valor, string Descricao);

/// <summary>Uma página de movimentos. <c>ProximoCursor</c> é null quando não há mais itens.</summary>
public record PaginaDeMovimentos(IReadOnlyList<Movimento> Itens, string? ProximoCursor);

public interface IPagamentos
{
    /// <summary>Transfere por chave PIX simulada: passa pelo antifraude, movimenta os saldos e notifica as duas contas.</summary>
    Task<TransferenciaRealizada> TransferirAsync(Guid contaOrigemId, string chavePixDestino, decimal valor, string descricao, CancellationToken ct);

    Task<decimal?> ObterSaldoAsync(Guid contaId, CancellationToken ct);

    /// <summary>
    /// Movimentos do período, do mais recente para o mais antigo, em páginas.
    /// O cursor marca onde a página anterior parou; assim, lançamentos novos não desalinham a navegação.
    /// </summary>
    Task<PaginaDeMovimentos> ListarMovimentosAsync(Guid contaId, DateTime de, DateTime ate, int limite, string? cursor, CancellationToken ct);
}
