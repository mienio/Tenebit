using Tenebit.Domain.Affiliates;

namespace Tenebit.Application.Abstractions;

public interface IAffiliateMessageThreadRepository
{
    Task<AffiliateMessageThread?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AffiliateMessageThread>> ListByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AffiliateMessageThread>> ListAllAsync(CancellationToken cancellationToken);
    Task<int> CountUnreadByAdminAsync(CancellationToken cancellationToken);
    void Add(AffiliateMessageThread thread);
}

public interface IAffiliateMessageRepository
{
    Task<IReadOnlyList<AffiliateMessage>> ListByThreadAsync(Guid threadId, CancellationToken cancellationToken);
    void Add(AffiliateMessage message);
}
