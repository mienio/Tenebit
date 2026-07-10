using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var enabled = _configuration.GetValue("Email:Enabled", false);
        var host = _configuration["Email:Host"];

        if (!enabled || string.IsNullOrWhiteSpace(host))
        {
            _logger.LogInformation("Email wysyłka wyłączona lub niekonfigurowana — pomijam wysyłkę do {To}, temat: {Subject}", to, subject);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_configuration["Email:FromName"] ?? "Tenebit", _configuration["Email:FromAddress"] ?? "no-reply@tenebit.app"));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        var port = _configuration.GetValue("Email:Port", 587);
        var useSsl = _configuration.GetValue("Email:UseSsl", true);
        await client.ConnectAsync(host, port, useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken);

        var username = _configuration["Email:Username"];
        if (!string.IsNullOrWhiteSpace(username))
        {
            await client.AuthenticateAsync(username, _configuration["Email:Password"], cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
