using Tenebit.Domain.Settings;

namespace Tenebit.Application.Abstractions;

public interface IRoleLabelRepository
{
    Task<IReadOnlyList<RoleLabel>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<RoleLabel?> FindAsync(Guid organizationId, string roleKey, CancellationToken cancellationToken);
    void Add(RoleLabel label);
}
