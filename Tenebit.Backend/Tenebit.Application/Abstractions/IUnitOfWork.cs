namespace Tenebit.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Runs a security/business workflow in one database transaction without taking a tenant-wide advisory lock.</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);

    /// <summary>Runs a workflow in one transaction while serializing only the specified resources inside a tenant.
    /// Resource ids are locked in deterministic order to avoid deadlocks.</summary>
    Task<T> ExecuteWithResourceLocksAsync<T>(Guid organizationId, string resourceType, IReadOnlyCollection<Guid> resourceIds, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}
