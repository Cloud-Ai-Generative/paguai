namespace PagUai.Api.Modulos.Antifraude;

public enum DecisaoAntifraude
{
    Aprovada,
    Reprovada
}

public class AnaliseAntifraude
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransferenciaId { get; set; }
    public int Score { get; set; }
    public DecisaoAntifraude Decisao { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateTime AnalisadaEm { get; set; } = DateTime.UtcNow;
}
