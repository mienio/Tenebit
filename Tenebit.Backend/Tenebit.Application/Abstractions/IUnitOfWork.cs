namespace Tenebit.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Runs <paramref name="action"/> inside a DB transaction serialized per-organization (Postgres
    /// advisory lock), so a check-then-act sequence (e.g. "count rows against a plan limit, then insert") is
    /// atomic across concurrent requests instead of racing (audyt P1.11).</summary>
    Task<T> ExecuteWithOrganizationLockAsync<T>(Guid organizationId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}
