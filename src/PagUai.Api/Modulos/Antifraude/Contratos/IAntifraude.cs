namespace PagUai.Api.Modulos.Antifraude.Contratos;

// ── CONTRATO PÚBLICO do módulo Antifraude ──────────────────────────────────────

public record ParecerAntifraude(bool Aprovado, int Score, string Motivo);

public interface IAntifraude
{
    /// <summary>Analisa um pagamento antes de o dinheiro sair da conta.</summary>
    Task<ParecerAntifraude> AnalisarAsync(Guid pagamentoId, Guid contaOrigemId, decimal valor, CancellationToken ct);
}
