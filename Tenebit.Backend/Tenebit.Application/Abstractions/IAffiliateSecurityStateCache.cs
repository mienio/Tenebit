namespace Tenebit.Application.Abstractions;

/// <summary>Mirrors <see cref="IUserSecurityStateCache"/> against a fully separate cache/table pair for
/// affiliates - kept apart on purpose (spec §4.1/§12.1) so a lookup bug can never confuse an affiliate
/// session for a tenant one.</summary>
public interface IAffiliateSecurityStateCache
{
    bool TryGet(Guid affiliateId, out AffiliateSecurityState state);
    void Set(Guid affiliateId, AffiliateSecurityState state, TimeSpan ttl);
    void Remove(Guid affiliateId);
}
