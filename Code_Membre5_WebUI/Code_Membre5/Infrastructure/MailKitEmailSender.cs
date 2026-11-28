#if MAILKIT
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Pressing.Membre5.Demo.Application;

namespace Pressing.Membre5.Demo.Infrastructure;

// Installer le package NuGet MailKit avant d'utiliser cette classe.
public sealed class ConfigurationSmtp
{
    public string Hote { get; init; } = "";
    public int Port { get; init; } = 587;
    public string AdresseExpediteur { get; init; } = "";
    public string MotDePasseApplication { get; init; } = "";
    public bool UtiliserSslDirect { get; init; }
}

public sealed class MailKitEmailSender(ConfigurationSmtp configuration) : IEmailSender
{
    public async Task EnvoyerAsync(string destinataire, string sujet, string corps, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(configuration.AdresseExpediteur));
        message.To.Add(MailboxAddress.Parse(destinataire));
        message.Subject = sujet;
        message.Body = new TextPart("plain") { Text = corps };

        using var smtp = new SmtpClient();
        var socket = configuration.UtiliserSslDirect ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await smtp.ConnectAsync(configuration.Hote, configuration.Port, socket, ct);
        await smtp.AuthenticateAsync(configuration.AdresseExpediteur, configuration.MotDePasseApplication, ct);
        await smtp.SendAsync(message, ct);
        await smtp.DisconnectAsync(true, ct);
    }
}
#endif
