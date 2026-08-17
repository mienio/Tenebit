namespace Tenebit.Domain.Subscriptions;

/// <summary>Records a Stripe webhook EventId once it has been applied, so a retried delivery of the same
/// event (Stripe retries on timeout/5xx) is a no-op instead of reapplying the same state change twice
/// (audyt P0.6).</summary>
public sealed class ProcessedStripeEvent
{
    private ProcessedStripeEvent() { }

    public ProcessedStripeEvent(string eventId, DateTimeOffset processedAt)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        ProcessedAt = processedAt;
    }

    public Guid Id { get; private set; }
    public string EventId { get; private set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; private set; }
}
