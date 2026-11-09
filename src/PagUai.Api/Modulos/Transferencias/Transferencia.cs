namespace PagUai.Api.Modulos.Transferencias;

public enum StatusTransferencia
{
    Concluida,
    ReprovadaAntifraude
}

/// <summary>
/// Transferência entre contas usando o PIX simulado da PagUai.
/// </summary>
public class Transferencia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContaOrigemId { get; set; }
    public Guid? ContaDestinoId { get; set; }
    public string ChavePixDestino { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public StatusTransferencia Status { get; set; }
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
}
