namespace PagUai.Api.Modulos.Contas;

/// <summary>
/// Conta digital de um cliente ou lojista da PagUai (dados fictícios).
/// </summary>
public class Conta
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Titular { get; set; } = string.Empty;

    /// <summary>Documento fictício, apenas para fins didáticos.</summary>
    public string Documento { get; set; } = string.Empty;

    /// <summary>Chave do PIX simulado (sem nenhuma ligação com o sistema real do Banco Central).</summary>
    public string ChavePix { get; set; } = string.Empty;

    public decimal Saldo { get; set; }
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
}
