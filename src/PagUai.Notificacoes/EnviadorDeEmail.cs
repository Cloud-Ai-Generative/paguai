using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace PagUai.Notificacoes;

public class NotificacaoOptions
{
    /// <summary>Servidor SMTP (no Docker Compose, o Mailpit). Vazio = só grava no banco e no log.</summary>
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 1025;
    public string Remetente { get; set; } = "nao-responda@paguai.local";

    /// <summary>
    /// Força uma falha no envio, para você ver retry e dead letter (vídeo 3.3).
    /// Até a Sprint 2, esta falha desfazia o PIX. Agora o PIX já terminou quando o e-mail é tentado.
    /// </summary>
    public bool SimularFalha { get; set; }
}

public interface IEnviadorDeEmail
{
    Task EnviarAsync(string destinatario, string endereco, string assunto, string texto, CancellationToken ct);
}

public class EnviadorSmtp(IOptions<NotificacaoOptions> options, ILogger<EnviadorSmtp> logger) : IEnviadorDeEmail
{
    private readonly NotificacaoOptions _opcoes = options.Value;

    public async Task EnviarAsync(string destinatario, string endereco, string assunto, string texto, CancellationToken ct)
    {
        if (_opcoes.SimularFalha)
        {
            throw new InvalidOperationException("Falha simulada no servidor de e-mail (Notificacoes__SimularFalha=true).");
        }

        if (string.IsNullOrWhiteSpace(_opcoes.SmtpHost))
        {
            logger.LogInformation("SMTP não configurado. E-mail para {Endereco} não enviado: {Texto}", endereco, texto);
            return;
        }

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress("PagUai", _opcoes.Remetente));
        email.To.Add(new MailboxAddress(destinatario, endereco));
        email.Subject = assunto;
        email.Body = new TextPart("plain") { Text = texto };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_opcoes.SmtpHost, _opcoes.SmtpPort, MailKit.Security.SecureSocketOptions.None, ct);
        await smtp.SendAsync(email, ct);
        await smtp.DisconnectAsync(true, ct);
    }
}
