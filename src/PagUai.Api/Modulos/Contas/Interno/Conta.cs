namespace PagUai.Api.Modulos.Contas.Interno;

/// <summary>
/// Cadastro de uma conta digital (dados fictícios).
/// Sprint 1: o SALDO saiu daqui. Movimentar dinheiro é responsabilidade do contexto de Pagamentos.
/// </summary>
public class Conta
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Titular { get; set; } = string.Empty;

    /// <summary>Documento fictício, apenas para fins didáticos.</summary>
    public string Documento { get; set; } = string.Empty;

    /// <summary>Chave do PIX simulado (sem nenhuma ligação com o sistema real do Banco Central).</summary>
    public string ChavePix { get; set; } = string.Empty;

    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
}
