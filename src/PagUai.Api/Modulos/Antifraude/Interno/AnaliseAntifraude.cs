namespace PagUai.Api.Modulos.Antifraude.Interno;

public enum DecisaoAntifraude
{
    Aprovada,
    Reprovada
}

public class AnaliseAntifraude
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PagamentoId { get; set; }

    /// <summary>
    /// Sprint 1: o Antifraude guarda a conta de origem na PRÓPRIA tabela. Na Sprint 0 ele contava
    /// as transferências lendo a tabela de outro módulo.
    /// </summary>
    public Guid ContaOrigemId { get; set; }

    public decimal Valor { get; set; }
    public int Score { get; set; }
    public DecisaoAntifraude Decisao { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateTime AnalisadaEm { get; set; } = DateTime.UtcNow;
}
