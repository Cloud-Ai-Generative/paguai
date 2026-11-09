namespace PagUai.Api.Modulos.Notificacoes;

public class Notificacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContaId { get; set; }
    public string Mensagem { get; set; } = string.Empty;
    public DateTime EnviadaEm { get; set; } = DateTime.UtcNow;
}
