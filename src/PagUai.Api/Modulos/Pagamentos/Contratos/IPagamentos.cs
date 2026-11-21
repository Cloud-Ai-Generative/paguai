namespace PagUai.Api.Modulos.Pagamentos.Contratos;

// ── CONTRATO PÚBLICO do módulo Pagamentos ──────────────────────────────────────
// Pagamentos é o DONO do saldo e dos lançamentos. Ninguém mais grava essas tabelas.

public record TransferenciaRealizada(Guid Id, Guid ContaOrigemId, Guid ContaDestinoId, decimal Valor, bool Concluida, string Status, DateTime CriadaEm);

public record Movimento(DateTime Data, string Tipo, decimal Valor, string Descricao);

public interface IPagamentos
{
    /// <summary>Transfere por chave PIX simulada: passa pelo antifraude, movimenta os saldos e notifica as duas contas.</summary>
    Task<TransferenciaRealizada> TransferirAsync(Guid contaOrigemId, string chavePixDestino, decimal valor, string descricao, CancellationToken ct);

    Task<decimal?> ObterSaldoAsync(Guid contaId, CancellationToken ct);

    Task<IReadOnlyList<Movimento>> ListarMovimentosAsync(Guid contaId, DateTime de, DateTime ate, CancellationToken ct);
}
