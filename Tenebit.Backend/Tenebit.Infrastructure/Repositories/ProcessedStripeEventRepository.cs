using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Subscriptions;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class ProcessedStripeEventRepository : IProcessedStripeEventRepository
{
    private readonly TenebitDbContext _db;
    public ProcessedStripeEventRepository(TenebitDbContext db) => _db = db;

    public Task<bool> ExistsAsync(string eventId, CancellationToken cancellationToken) =>
        _db.ProcessedStripeEvents.AnyAsync(x => x.EventId == eventId, cancellationToken);

    public void Add(ProcessedStripeEvent processedEvent) => _db.ProcessedStripeEvents.Add(processedEvent);
}
