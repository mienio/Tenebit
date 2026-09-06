namespace Tenebit.Domain.Subscriptions;

/// <summary>Records a Paddle webhook notification_id once it has been applied, so a retried delivery of
/// the same event is a no-op instead of reapplying (and re-logging) the same state change twice
/// (audyt P0.6).</summary>
public sealed class ProcessedPaddleEvent
{
    private ProcessedPaddleEvent() { }

    public ProcessedPaddleEvent(string eventId, DateTimeOffset processedAt)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        ProcessedAt = processedAt;
    }

    public Guid Id { get; private set; }
    public string EventId { get; private set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; private set; }
}
