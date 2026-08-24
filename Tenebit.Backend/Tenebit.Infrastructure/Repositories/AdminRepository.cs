using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Admin;
using Tenebit.Domain.Identity;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

/// <summary>
/// Every query here calls IgnoreQueryFilters() explicitly. The tenant filter would already pass for an
/// admin request (the admin token carries no organization_id, so CurrentTenantOrganizationId is empty),
/// but relying on that would make cross-tenant reads an accident of configuration rather than a stated
/// intent - and it would silently return nothing if the filter's fallback ever changed.
/// </summary>
public sealed class AdminRepository : IAdminRepository
{
    private readonly TenebitDbContext _db;

    public AdminRepository(TenebitDbContext db) => _db = db;

    public async Task<PlatformTotals> GetTotalsAsync(CancellationToken cancellationToken) => new(
        Organizations: await _db.Organizations.IgnoreQueryFilters().CountAsync(cancellationToken),
        SuspendedOrganizations: await _db.Organizations.IgnoreQueryFilters().CountAsync(x => x.IsSuspended, cancellationToken),
        Users: await _db.OrganizationUsers.IgnoreQueryFilters().CountAsync(cancellationToken),
        ActiveUsers: await _db.OrganizationUsers.IgnoreQueryFilters().CountAsync(x => x.IsActive, cancellationToken),
        Assets: await _db.Assets.IgnoreQueryFilters().CountAsync(cancellationToken),
        People: await _db.People.IgnoreQueryFilters().CountAsync(cancellationToken),
        Locations: await _db.Locations.IgnoreQueryFilters().CountAsync(cancellationToken),
        Licenses: await _db.Licenses.IgnoreQueryFilters().CountAsync(cancellationToken));

    public Task<IReadOnlyList<DailyCount>> GetAssetsCreatedPerDayAsync(DateOnly from, DateOnly to, Guid? organizationId, CancellationToken cancellationToken)
    {
        var query = _db.Assets.IgnoreQueryFilters().AsQueryable();
        if (organizationId is { } id) query = query.Where(x => x.OrganizationId == id);
        return CountPerDayAsync(query.Select(x => x.CreatedAt), from, to, cancellationToken);
    }

    public Task<IReadOnlyList<DailyCount>> GetOrganizationsCreatedPerDayAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        CountPerDayAsync(_db.Organizations.IgnoreQueryFilters().Select(x => x.CreatedAt), from, to, cancellationToken);

    public Task<IReadOnlyList<DailyCount>> GetLoginsPerDayAsync(DateOnly from, DateOnly to, bool succeeded, CancellationToken cancellationToken) =>
        CountPerDayAsync(
            _db.LoginEvents.IgnoreQueryFilters().Where(x => x.Succeeded == succeeded).Select(x => x.CreatedAt),
            from, to, cancellationToken);

    public Task<int> CountLoginsAsync(DateOnly from, DateOnly to, bool succeeded, CancellationToken cancellationToken)
    {
        var (start, end) = ToUtcBounds(from, to);
        return _db.LoginEvents.IgnoreQueryFilters()
            .CountAsync(x => x.Succeeded == succeeded && x.CreatedAt >= start && x.CreatedAt < end, cancellationToken);
    }

    public async Task<IReadOnlyList<(string Label, int Count)>> GetAssetStatusBreakdownAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var rows = await _db.Assets.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId)
            .GroupBy(x => x.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return rows.Select(x => (x.Key.ToString(), x.Count)).OrderByDescending(x => x.Count).ToArray();
    }

    public async Task<IReadOnlyList<(string Label, int Count)>> GetAssetCategoryBreakdownAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        // Category names are the organization's own taxonomy labels (e.g. "Laptop"), not personal data,
        // and they are what makes a size breakdown meaningful.
        var rows = await (
            from asset in _db.Assets.IgnoreQueryFilters().Where(x => x.OrganizationId == organizationId)
            join category in _db.AssetCategories.IgnoreQueryFilters() on asset.CategoryId equals category.Id
            group category by category.Name into grouped
            select new { grouped.Key, Count = grouped.Count() }
        ).ToListAsync(cancellationToken);
        return rows.Select(x => (x.Key, x.Count)).OrderByDescending(x => x.Count).ToArray();
    }

    public async Task<IReadOnlyList<(string Label, int Count)>> GetPeopleStatusBreakdownAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var rows = await _db.People.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId)
            .GroupBy(x => x.EmploymentStatus)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return rows.Select(x => (x.Key.ToString(), x.Count)).OrderByDescending(x => x.Count).ToArray();
    }

    private static (DateTimeOffset Start, DateTimeOffset End) ToUtcBounds(DateOnly from, DateOnly to) => (
        new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
        // Exclusive upper bound one day past `to`, so the range includes everything that happened on `to`.
        new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

    /// <summary>
    /// Groups server-side by date and returns a dense series (missing days filled with zero) so the
    /// chart does not have to guess where the gaps are.
    /// </summary>
    private static async Task<IReadOnlyList<DailyCount>> CountPerDayAsync(
        IQueryable<DateTimeOffset> source, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from) (from, to) = (to, from);
        var (start, end) = ToUtcBounds(from, to);

        // Grouped in memory rather than in SQL: neither DateTimeOffset.Date nor .UtcDateTime.Date has a
        // Npgsql translation, and the alternatives (raw date_trunc SQL) would bypass the query filters
        // this repository deliberately controls. The window is capped at one year and only the timestamp
        // column is fetched, so the row count stays proportional to activity in the selected range.
        var timestamps = await source
            .Where(x => x >= start && x < end)
            .ToListAsync(cancellationToken);

        var lookup = timestamps
            .GroupBy(x => DateOnly.FromDateTime(x.UtcDateTime))
            .ToDictionary(g => g.Key, g => g.Count());
        var span = to.DayNumber - from.DayNumber + 1;
        var result = new List<DailyCount>(span);
        for (var offset = 0; offset < span; offset++)
        {
            var day = from.AddDays(offset);
            result.Add(new DailyCount(day, lookup.TryGetValue(day, out var count) ? count : 0));
        }

        return result;
    }

    public async Task<IReadOnlyList<(string PlanKey, int Count)>> GetPlanDistributionAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.Subscriptions.IgnoreQueryFilters()
            .GroupBy(x => x.PlanKey)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return rows.Select(x => (x.Key, x.Count)).ToArray();
    }

    public async Task<(IReadOnlyList<AdminUserEntry> Items, int Total)> ListUsersAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.OrganizationUsers.IgnoreQueryFilters().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            // Organization names live on another table; resolving the matching ids first keeps this a
            // single-table predicate instead of a join EF cannot translate here.
            var matchingOrganizations = await _db.Organizations.IgnoreQueryFilters()
                .Where(o => EF.Functions.ILike(o.Name, term))
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);

            query = query.Where(u => EF.Functions.ILike(u.Email, term)
                || EF.Functions.ILike(u.DisplayName, term)
                || matchingOrganizations.Contains(u.OrganizationId));
        }

        var total = await query.CountAsync(cancellationToken);
        var users = await query
            .Include(u => u.Roles)
            .OrderByDescending(u => u.CreatedAt)
            .Skip((Math.Max(page, 1) - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (await ComposeUserEntriesAsync(users, cancellationToken), total);
    }

    public async Task<AdminUserEntry?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.OrganizationUsers.IgnoreQueryFilters()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return null;
        return (await ComposeUserEntriesAsync([user], cancellationToken)).SingleOrDefault();
    }

    /// <summary>
    /// Joins accounts to their organization and last sign-in with two extra set-based queries rather than
    /// one LINQ join. EF cannot translate the joined projection into this record (it falls back to an
    /// object-typed key and throws), and this shape keeps the number of round trips constant.
    /// </summary>
    private async Task<IReadOnlyList<AdminUserEntry>> ComposeUserEntriesAsync(
        IReadOnlyList<Domain.Identity.OrganizationUser> users, CancellationToken cancellationToken)
    {
        if (users.Count == 0) return [];

        var organizationIds = users.Select(u => u.OrganizationId).Distinct().ToArray();
        var organizations = await _db.Organizations.IgnoreQueryFilters()
            .Where(o => organizationIds.Contains(o.Id))
            .Select(o => new { o.Id, o.Name, o.IsSuspended })
            .ToDictionaryAsync(o => o.Id, cancellationToken);

        var userIds = users.Select(u => u.Id).ToArray();
        var lastLogins = await _db.LoginEvents.IgnoreQueryFilters()
            .Where(x => x.Succeeded && x.UserId != null && userIds.Contains(x.UserId.Value))
            .GroupBy(x => x.UserId!.Value)
            .Select(g => new { UserId = g.Key, Last = g.Max(x => x.CreatedAt) })
            .ToDictionaryAsync(x => x.UserId, x => x.Last, cancellationToken);

        return users.Select(user =>
        {
            organizations.TryGetValue(user.OrganizationId, out var organization);
            return new AdminUserEntry(
                user.Id,
                user.OrganizationId,
                organization?.Name ?? "—",
                organization?.IsSuspended ?? false,
                user.Email,
                user.DisplayName,
                user.IsActive,
                user.IsEmailVerified,
                user.IsTwoFactorEnabled,
                user.CreatedAt,
                lastLogins.TryGetValue(user.Id, out var last) ? last : null,
                user.Roles.Select(r => r.Role).ToArray());
        }).ToArray();
    }

    public async Task<(IReadOnlyList<LoginEventEntry> Items, int Total)> ListLoginEventsAsync(
        string? search, bool? succeededOnly, Guid? organizationId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.LoginEvents.IgnoreQueryFilters().AsQueryable();

        if (organizationId is { } orgId) query = query.Where(x => x.OrganizationId == orgId);
        if (succeededOnly is { } succeeded) query = query.Where(x => x.Succeeded == succeeded);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Email, term) || (x.IpAddress != null && EF.Functions.ILike(x.IpAddress, term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var events = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((Math.Max(page, 1) - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Same reason as above: the organization name is resolved with a second lookup instead of an
        // untranslatable left join over a nullable foreign key.
        var names = new Dictionary<Guid, string>();
        var referenced = events.Where(x => x.OrganizationId.HasValue).Select(x => x.OrganizationId!.Value).Distinct().ToArray();
        if (referenced.Length > 0)
        {
            names = await _db.Organizations.IgnoreQueryFilters()
                .Where(o => referenced.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken);
        }

        var items = events.Select(x => new LoginEventEntry(
            x.Id,
            x.OrganizationId,
            x.OrganizationId is { } id && names.TryGetValue(id, out var name) ? name : null,
            x.UserId,
            x.Email,
            x.Succeeded,
            x.FailureReason,
            x.IpAddress,
            x.UserAgent,
            x.CreatedAt)).ToArray();

        return (items, total);
    }

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetReviewedOrganizationsAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.AdminAuditLogs.IgnoreQueryFilters()
            .Where(x => x.Action == AdminActions.OrganizationReviewed && x.TargetId != null)
            .GroupBy(x => x.TargetId!.Value)
            .Select(g => new { OrganizationId = g.Key, Last = g.Max(x => x.CreatedAt) })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.OrganizationId, x => x.Last);
    }

    public async Task<IReadOnlyList<AdminAuditLog>> ListAdminAuditAsync(int limit, CancellationToken cancellationToken) =>
        await _db.AdminAuditLogs.IgnoreQueryFilters()
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

    public void AddAdminAudit(AdminAuditLog entry) => _db.AdminAuditLogs.Add(entry);

    public void AddLoginEvent(LoginEvent loginEvent) => _db.LoginEvents.Add(loginEvent);

    public Task<int> CountRecentModerationActionsAsync(DateTimeOffset since, CancellationToken cancellationToken) =>
        _db.AdminAuditLogs.IgnoreQueryFilters()
            .CountAsync(x => x.CreatedAt >= since
                && x.Action != AdminActions.SignedIn
                && x.Action != AdminActions.SignInFailed
                && x.Action != AdminActions.OrganizationReviewed, cancellationToken);
}
