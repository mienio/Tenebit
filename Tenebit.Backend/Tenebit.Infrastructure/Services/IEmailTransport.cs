namespace Tenebit.Infrastructure.Services;

public interface IEmailTransport
{
    Task SendAsync(string to, string subject, string htmlBody, string messageId, CancellationToken cancellationToken);
}
