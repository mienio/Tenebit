namespace Tenebit.Application.Abstractions;

/// <summary>Provides the tenant attached to the current authenticated request. Background/public flows return Guid.Empty.</summary>
public interface ITenantContext
{
    Guid OrganizationId { get; }
}
