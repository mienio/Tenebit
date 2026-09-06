using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Abstractions;

public interface IProcessedPaddleEventRepository
{
    Task<bool> ExistsAsync(string eventId, CancellationToken cancellationToken);
    void Add(ProcessedPaddleEvent processedEvent);
}
