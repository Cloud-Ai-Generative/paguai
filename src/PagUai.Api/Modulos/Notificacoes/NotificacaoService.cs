using MailKit.Net.Smtp;
using MimeKit;
using PagUai.Api.Infra;

namespace PagUai.Api.Modulos.Notificacoes;

public class NotificacaoOptions
{
    /// <summary>Servidor SMTP (no Docker Compose, o Mailpit). Vazio = apenas grava no banco e no log.</summary>
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 1025;
    public string Remetente { get; set; } = "nao-responda@paguai.local";

    /// <summary>
    /// Força uma falha no envio.
    /// DOR PROPOSITAL (Sprint 3): como a notificação roda dentro da transação, a transferência inteira falha junto.
    /// </summary>
    public bool SimularFalha { get; set; }
}

public class NotificacaoService(PagUaiDbContext db, Microsoft.Extensions.Options.IOptions<NotificacaoOptions> options, ILogger<NotificacaoService> logger)
{
    private readonly NotificacaoOptions _opcoes = options.Value;

    public async Task NotificarAsync(Guid contaId, string destinatario, string mensagem, CancellationToken ct)
    {
        if (_opcoes.SimularFalha)
        {
            throw new InvalidOperationException("Falha simulada no serviço de notificações.");
        }

        db.Notificacoes.Add(new Notificacao { ContaId = contaId, Mensagem = mensagem });

        if (!string.IsNullOrWhiteSpace(_opcoes.SmtpHost))
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("PagUai", _opcoes.Remetente));
            email.To.Add(new MailboxAddress(destinatario, $"{contaId:N}@clientes.paguai.local"));
            email.Subject = "PagUai: nova movimentação na sua conta";
            email.Body = new TextPart("plain") { Text = mensagem };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_opcoes.SmtpHost, _opcoes.SmtpPort, MailKit.Security.SecureSocketOptions.None, ct);
            await smtp.SendAsync(email, ct);
            await smtp.DisconnectAsync(true, ct);
        }

        logger.LogInformation("Notificação para a conta {ContaId}: {Mensagem}", contaId, mensagem);
    }
}
