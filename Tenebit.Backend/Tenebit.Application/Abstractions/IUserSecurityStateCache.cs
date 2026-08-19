namespace Tenebit.Application.Abstractions;

public sealed record UserSecurityState(Guid OrganizationId, Guid SecurityStamp, bool IsActive, bool IsEmailVerified);

public interface IUserSecurityStateCache
{
    bool TryGet(Guid userId, out UserSecurityState state);
    void Set(Guid userId, UserSecurityState state, TimeSpan ttl);
    void Remove(Guid userId);
}
