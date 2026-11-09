namespace PagUai.Api.Modulos.Cobrancas;

public enum StatusCobranca
{
    Pendente,
    Paga
}

/// <summary>
/// Cobrança emitida por um lojista (por exemplo, o Seu Geraldo) para receber de um cliente.
/// </summary>
public class Cobranca
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContaRecebedoraId { get; set; }
    public decimal Valor { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public StatusCobranca Status { get; set; } = StatusCobranca.Pendente;
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
    public DateTime? PagaEm { get; set; }
    public Guid? TransferenciaId { get; set; }
}
