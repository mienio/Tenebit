using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

/// <summary>
/// Immediate transport used only by non-transactional mail call sites. Security-sensitive capability/recovery
/// e-mails use <see cref="IEmailOutboxWriter"/> and the durable outbox dispatcher instead.
/// </summary>
internal sealed class SmtpEmailSender : IEmailSender
{
    private readonly IEmailTransport _transport;

    public SmtpEmailSender(IEmailTransport transport) => _transport = transport;

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken) =>
        _transport.SendAsync(to, subject, htmlBody, Guid.NewGuid().ToString("N"), cancellationToken);
}
