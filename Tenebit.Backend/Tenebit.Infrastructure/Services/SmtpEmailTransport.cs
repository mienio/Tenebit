using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Tenebit.Infrastructure.Services;

internal sealed class SmtpEmailTransport : IEmailTransport
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailTransport> _logger;

    public SmtpEmailTransport(IConfiguration configuration, ILogger<SmtpEmailTransport> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, string messageId, CancellationToken cancellationToken)
    {
        var enabled = _configuration.GetValue("Email:Enabled", false);
        var host = _configuration["Email:Host"];

        if (!enabled || string.IsNullOrWhiteSpace(host))
        {
            _logger.LogInformation("Email delivery disabled or not configured; message {MessageId} treated as delivered in this environment.", messageId);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            _configuration["Email:FromName"] ?? "Tenebit",
            _configuration["Email:FromAddress"] ?? "no-reply@tenebit.app"));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };
        message.MessageId = $"{messageId}@mailer.tenebit.app";

        using var client = new SmtpClient
        {
            Timeout = Math.Clamp(_configuration.GetValue("Email:TimeoutMilliseconds", 30_000), 5_000, 120_000)
        };
        var port = _configuration.GetValue("Email:Port", 587);
        var useSsl = _configuration.GetValue("Email:UseSsl", true);
        await client.ConnectAsync(host, port, useSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None, cancellationToken);

        var username = _configuration["Email:Username"];
        if (!string.IsNullOrWhiteSpace(username))
        {
            await client.AuthenticateAsync(username, _configuration["Email:Password"] ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
