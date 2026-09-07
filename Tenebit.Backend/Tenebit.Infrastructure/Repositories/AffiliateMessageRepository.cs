using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Affiliates;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AffiliateMessageThreadRepository : IAffiliateMessageThreadRepository
{
    private readonly TenebitDbContext _db;
    public AffiliateMessageThreadRepository(TenebitDbContext db) => _db = db;

    public Task<AffiliateMessageThread?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.AffiliateMessageThreads.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AffiliateMessageThread>> ListByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken) =>
        await _db.AffiliateMessageThreads
            .Where(x => x.AffiliateId == affiliateId)
            .OrderByDescending(x => x.LastMessageAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AffiliateMessageThread>> ListAllAsync(CancellationToken cancellationToken) =>
        await _db.AffiliateMessageThreads
            .OrderByDescending(x => x.LastMessageAt)
            .ToListAsync(cancellationToken);

    public Task<int> CountUnreadByAdminAsync(CancellationToken cancellationToken) =>
        _db.AffiliateMessageThreads.CountAsync(x => x.UnreadByAdmin, cancellationToken);

    public void Add(AffiliateMessageThread thread) => _db.AffiliateMessageThreads.Add(thread);
}

public sealed class AffiliateMessageRepository : IAffiliateMessageRepository
{
    private readonly TenebitDbContext _db;
    public AffiliateMessageRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<AffiliateMessage>> ListByThreadAsync(Guid threadId, CancellationToken cancellationToken) =>
        await _db.AffiliateMessages
            .Where(x => x.ThreadId == threadId)
            .OrderBy(x => x.SentAt)
            .ToListAsync(cancellationToken);

    public void Add(AffiliateMessage message) => _db.AffiliateMessages.Add(message);
}
