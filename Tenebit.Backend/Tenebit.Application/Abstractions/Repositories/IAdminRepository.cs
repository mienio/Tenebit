using Tenebit.Domain.Identity;

namespace Tenebit.Application.Abstractions;

public sealed record LoginEventEntry(
    Guid Id,
    Guid? OrganizationId,
    string? OrganizationName,
    Guid? UserId,
    string Email,
    bool Succeeded,
    string? FailureReason,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset CreatedAt);

public sealed record AdminUserEntry(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    bool OrganizationSuspended,
    string Email,
    string DisplayName,
    bool IsActive,
    bool IsEmailVerified,
    bool IsTwoFactorEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<string> Roles);

public sealed record DailyCount(DateOnly Day, int Count);

public sealed record PlatformTotals(
    int Organizations,
    int SuspendedOrganizations,
    int Users,
    int ActiveUsers,
    int Assets,
    int People,
    int Locations,
    int Licenses);

/// <summary>
/// Cross-tenant read/write access used exclusively by the platform admin panel. Kept in its own
/// repository so no tenant-facing service can accidentally take a dependency on unscoped queries.
/// </summary>
public interface IAdminRepository
{
    Task<PlatformTotals> GetTotalsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Daily counts of newly created rows within [from, to] inclusive, oldest first, with missing days
    /// filled as zero so the chart gets a dense series.
    /// </summary>
    Task<IReadOnlyList<DailyCount>> GetAssetsCreatedPerDayAsync(DateOnly from, DateOnly to, Guid? organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DailyCount>> GetOrganizationsCreatedPerDayAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<IReadOnlyList<DailyCount>> GetLoginsPerDayAsync(DateOnly from, DateOnly to, bool succeeded, CancellationToken cancellationToken);
    Task<int> CountLoginsAsync(DateOnly from, DateOnly to, bool succeeded, CancellationToken cancellationToken);

    /// <summary>Status/category breakdowns for one organization - counts only, never row contents.</summary>
    Task<IReadOnlyList<(string Label, int Count)>> GetAssetStatusBreakdownAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<(string Label, int Count)>> GetAssetCategoryBreakdownAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<(string Label, int Count)>> GetPeopleStatusBreakdownAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<(string PlanKey, int Count)>> GetPlanDistributionAsync(CancellationToken cancellationToken);

    Task<(IReadOnlyList<AdminUserEntry> Items, int Total)> ListUsersAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<AdminUserEntry?> GetUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<LoginEventEntry> Items, int Total)> ListLoginEventsAsync(string? search, bool? succeededOnly, Guid? organizationId, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// When each organization's name was last reviewed for terms-of-service compliance, derived from the
    /// admin audit trail. Stored as audit entries rather than a column so the "who checked this, and
    /// when" history is preserved and no migration is needed to record a review.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetReviewedOrganizationsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminAuditLog>> ListAdminAuditAsync(int limit, CancellationToken cancellationToken);
    void AddAdminAudit(AdminAuditLog entry);

    void AddLoginEvent(LoginEvent loginEvent);

    /// <summary>Counts moderation actions taken in the trailing window - drives the blast-radius cap.</summary>
    Task<int> CountRecentModerationActionsAsync(DateTimeOffset since, CancellationToken cancellationToken);
}
