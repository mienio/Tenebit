namespace Tenebit.Application.Abstractions;

/// <summary>
/// Persists security-sensitive e-mail delivery into the database outbox. The caller owns the surrounding
/// business transaction and must commit the token/workflow state together with this enqueue operation.
/// Raw capability credentials may exist in the e-mail body, therefore the Infrastructure implementation
/// encrypts recipient/subject/body before persistence.
/// </summary>
public interface IEmailOutboxWriter
{
    Task EnqueueAsync(
        Guid organizationId,
        string recipient,
        string subject,
        string htmlBody,
        string purpose,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
