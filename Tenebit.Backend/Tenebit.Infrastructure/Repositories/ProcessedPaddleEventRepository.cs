using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Subscriptions;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class ProcessedPaddleEventRepository : IProcessedPaddleEventRepository
{
    private readonly TenebitDbContext _db;
    public ProcessedPaddleEventRepository(TenebitDbContext db) => _db = db;

    public Task<bool> ExistsAsync(string eventId, CancellationToken cancellationToken) =>
        _db.ProcessedPaddleEvents.AnyAsync(x => x.EventId == eventId, cancellationToken);

    public void Add(ProcessedPaddleEvent processedEvent) => _db.ProcessedPaddleEvents.Add(processedEvent);
}
