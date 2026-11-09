namespace PagUai.Api.Modulos.Extrato;

public enum TipoLancamento
{
    Credito,
    Debito
}

/// <summary>
/// Linha do extrato de uma conta.
/// </summary>
public class Lancamento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContaId { get; set; }
    public TipoLancamento Tipo { get; set; }
    public decimal Valor { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public DateTime Data { get; set; } = DateTime.UtcNow;
}
