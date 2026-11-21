namespace PagUai.Api.Modulos.Notificacoes.Contratos;

// ── CONTRATO PÚBLICO do módulo Notificações ────────────────────────────────────

public interface INotificacoes
{
    Task NotificarAsync(Guid contaId, string destinatario, string mensagem, CancellationToken ct);
}
