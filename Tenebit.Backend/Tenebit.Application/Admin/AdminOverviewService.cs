using Tenebit.Application.Abstractions;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Admin;

/// <summary>
/// Read side of the platform admin panel: cross-tenant, read-only, and deliberately data-minimised.
///
/// Two rules govern everything here:
/// 1. Cross-tenant reads are explicit. The platform-admin JWT carries no organization_id, so it can never
///    authenticate against a tenant endpoint (see PlatformAdminClaims / TenebitEndpoints); this service is
///    reachable only from the isolated /api/admin group.
/// 2. Customer personal data never leaves. Assets, people and locations are returned as counts and
///    breakdowns; account identifiers are masked by <see cref="PiiMasking"/> before serialisation. A
///    stolen admin session therefore yields statistics, not a customer database.
/// </summary>
public sealed class AdminOverviewService
{
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationUserRepository _users;
    private readonly IAssetRepository _assets;
    private readonly IPersonRepository _people;
    private readonly ILocationRepository _locations;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IAdminRepository _admin;
    private readonly IPaymentGateway _paymentGateway;

    public AdminOverviewService(
        IOrganizationRepository organizations,
        IOrganizationUserRepository users,
        IAssetRepository assets,
        IPersonRepository people,
        ILocationRepository locations,
        ISubscriptionRepository subscriptions,
        IAdminRepository admin,
        IPaymentGateway paymentGateway)
    {
        _organizations = organizations;
        _users = users;
        _assets = assets;
        _people = people;
        _locations = locations;
        _subscriptions = subscriptions;
        _admin = admin;
        _paymentGateway = paymentGateway;
    }

    /// <summary>
    /// Normalises a requested window. Callers may pass an explicit from/to pair or nothing at all; the
    /// range is clamped to a year so a stray query cannot ask the database for an unbounded scan.
    /// </summary>
    public static (DateOnly From, DateOnly To) ResolveRange(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var end = to ?? today;
        var start = from ?? end.AddDays(-29);
        if (end < start) (start, end) = (end, start);
        if (end.DayNumber - start.DayNumber > 365) start = end.AddDays(-365);
        return (start, end);
    }

    public async Task<AdminDashboard> GetDashboardAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var (start, end) = ResolveRange(from, to);
        var totals = await _admin.GetTotalsAsync(cancellationToken);

        var assetsSeries = await _admin.GetAssetsCreatedPerDayAsync(start, end, null, cancellationToken);
        var organizationsSeries = await _admin.GetOrganizationsCreatedPerDayAsync(start, end, cancellationToken);
        var loginSeries = await _admin.GetLoginsPerDayAsync(start, end, true, cancellationToken);
        var failedSeries = await _admin.GetLoginsPerDayAsync(start, end, false, cancellationToken);
        var loginsInRange = await _admin.CountLoginsAsync(start, end, true, cancellationToken);
        var failedInRange = await _admin.CountLoginsAsync(start, end, false, cancellationToken);
        var plans = await _admin.GetPlanDistributionAsync(cancellationToken);

        var organizations = await ListOrganizationsAsync(cancellationToken);
        var newest = organizations.OrderByDescending(x => x.CreatedAt).Take(8).ToArray();

        return new AdminDashboard(
            totals.Organizations,
            totals.SuspendedOrganizations,
            totals.Users,
            totals.ActiveUsers,
            totals.Assets,
            totals.People,
            totals.Locations,
            totals.Licenses,
            loginsInRange,
            failedInRange,
            organizations.Count(x => x.ReviewedAt is null),
            start.ToString("yyyy-MM-dd"),
            end.ToString("yyyy-MM-dd"),
            ToSeries("Nowe aktywa", assetsSeries),
            ToSeries("Nowe organizacje", organizationsSeries),
            ToSeries("Udane logowania", loginSeries),
            ToSeries("Nieudane logowania", failedSeries),
            plans.Select(x => new AdminPlanSlice(SubscriptionPlan.FromKey(x.PlanKey)?.Name ?? x.PlanKey, x.Count))
                .OrderByDescending(x => x.Count)
                .ToArray(),
            newest);
    }

    private static AdminSeries ToSeries(string label, IReadOnlyList<DailyCount> points) =>
        new(label, points.Select(x => new AdminSeriesPoint(x.Day.ToString("yyyy-MM-dd"), x.Count)).ToArray());

    public async Task<IReadOnlyList<AdminOrganizationSummary>> ListOrganizationsAsync(CancellationToken cancellationToken)
    {
        var organizations = await _organizations.ListAllAsync(cancellationToken);
        var reviewed = await _admin.GetReviewedOrganizationsAsync(cancellationToken);
        var result = new List<AdminOrganizationSummary>(organizations.Count);
        foreach (var organization in organizations)
        {
            result.Add(await BuildSummaryAsync(organization, reviewed, cancellationToken));
        }

        return result;
    }

    public async Task<AdminOrganizationDetail?> GetOrganizationDetailAsync(
        Guid organizationId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        if (organization is null) return null;

        var (start, end) = ResolveRange(from, to);
        var reviewed = await _admin.GetReviewedOrganizationsAsync(cancellationToken);
        var summary = await BuildSummaryAsync(organization, reviewed, cancellationToken);
        var users = await _users.ListAsync(organizationId, cancellationToken);

        var lastLogins = await BuildLastLoginLookupAsync(organizationId, users.Select(u => u.Id).ToHashSet(), cancellationToken);
        var assetsByStatus = await _admin.GetAssetStatusBreakdownAsync(organizationId, cancellationToken);
        var assetsByCategory = await _admin.GetAssetCategoryBreakdownAsync(organizationId, cancellationToken);
        var peopleByStatus = await _admin.GetPeopleStatusBreakdownAsync(organizationId, cancellationToken);
        var assetsCreated = await _admin.GetAssetsCreatedPerDayAsync(start, end, organizationId, cancellationToken);

        return new AdminOrganizationDetail(
            summary,
            users.Select(u => new AdminUserSummary(
                u.Id,
                PiiMasking.Email(u.Email),
                PiiMasking.PersonName(u.DisplayName),
                u.IsActive,
                u.IsEmailVerified,
                u.IsTwoFactorEnabled,
                u.CreatedAt,
                lastLogins.TryGetValue(u.Id, out var lastLogin) ? lastLogin : null,
                u.Roles.Select(r => r.Role).ToArray())).ToArray(),
            assetsByStatus.Select(x => new AdminCountSlice(x.Label, x.Count)).ToArray(),
            assetsByCategory.Select(x => new AdminCountSlice(x.Label, x.Count)).ToArray(),
            peopleByStatus.Select(x => new AdminCountSlice(x.Label, x.Count)).ToArray(),
            summary.LocationCount,
            ToSeries("Nowe aktywa", assetsCreated));
    }

    /// <summary>
    /// What an organization has actually paid, as evidence (billing disputes, chargebacks) - pulled live
    /// from Stripe on every call rather than mirrored locally, since Stripe's own invoice is the record
    /// that matters and this stays correct even if it changes on Stripe's side (refund, etc.) after the
    /// fact. Returns an empty (zero-total) result, not null, for an organization that has never had a
    /// Stripe customer - null only means the organization itself doesn't exist.
    /// </summary>
    public async Task<AdminOrganizationPayments?> GetOrganizationPaymentsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        if (organization is null) return null;

        var subscription = await _subscriptions.GetByOrganizationAsync(organizationId, cancellationToken);
        if (subscription is null || string.IsNullOrWhiteSpace(subscription.StripeCustomerId))
            return new AdminOrganizationPayments(0m, "EUR", []);

        var invoices = await _paymentGateway.ListInvoicesAsync(subscription.StripeCustomerId, cancellationToken);
        var currency = invoices.Count > 0 ? invoices[0].Currency : "EUR";

        return new AdminOrganizationPayments(
            invoices.Sum(x => x.AmountPaid),
            currency,
            invoices.Select(x => new AdminPaymentEntry(
                x.Id, x.Number, x.AmountPaid, x.AmountDue, x.Currency, x.Status, x.Created, x.HostedInvoiceUrl, x.InvoicePdfUrl)).ToArray());
    }

    private async Task<Dictionary<Guid, DateTimeOffset>> BuildLastLoginLookupAsync(
        Guid organizationId, HashSet<Guid> userIds, CancellationToken cancellationToken)
    {
        var lastLogins = new Dictionary<Guid, DateTimeOffset>();
        var (loginEvents, _) = await _admin.ListLoginEventsAsync(null, true, organizationId, 1, 500, cancellationToken);
        foreach (var loginEvent in loginEvents)
        {
            // Events arrive newest-first, so the first hit per user is their latest sign-in.
            if (loginEvent.UserId is { } userId && userIds.Contains(userId) && !lastLogins.ContainsKey(userId))
            {
                lastLogins[userId] = loginEvent.CreatedAt;
            }
        }

        return lastLogins;
    }

    public async Task<AdminPage<AdminUserListItem>> ListUsersAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var size = Math.Clamp(pageSize, 1, 200);
        var (items, total) = await _admin.ListUsersAsync(search, page, size, cancellationToken);
        return new AdminPage<AdminUserListItem>(
            items.Select(x => new AdminUserListItem(
                x.Id, x.OrganizationId, x.OrganizationName, x.OrganizationSuspended,
                PiiMasking.Email(x.Email), PiiMasking.PersonName(x.DisplayName),
                x.IsActive, x.IsEmailVerified, x.IsTwoFactorEnabled, x.CreatedAt, x.LastLoginAt, x.Roles)).ToArray(),
            total, Math.Max(page, 1), size);
    }

    public async Task<AdminPage<AdminLoginEntry>> ListLoginsAsync(
        string? search, bool? succeededOnly, Guid? organizationId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var size = Math.Clamp(pageSize, 1, 200);
        var (items, total) = await _admin.ListLoginEventsAsync(search, succeededOnly, organizationId, page, size, cancellationToken);
        return new AdminPage<AdminLoginEntry>(
            items.Select(x => new AdminLoginEntry(
                x.Id, x.OrganizationId, x.OrganizationName, x.UserId, PiiMasking.Email(x.Email), x.Succeeded,
                x.FailureReason, x.IpAddress, x.UserAgent, x.CreatedAt)).ToArray(),
            total, Math.Max(page, 1), size);
    }

    public async Task<IReadOnlyList<AdminAuditEntry>> ListAdminAuditAsync(int limit, CancellationToken cancellationToken)
    {
        var entries = await _admin.ListAdminAuditAsync(limit, cancellationToken);
        return entries
            .Select(x => new AdminAuditEntry(
                x.Id, x.Action, x.TargetType, x.TargetId,
                // Audit targets record who was acted on, so an e-mail-shaped label is masked on the way out.
                PiiMasking.AuditLabel(x.TargetLabel),
                x.Details, x.IpAddress, x.CreatedAt))
            .ToArray();
    }

    private async Task<AdminOrganizationSummary> BuildSummaryAsync(
        Organization organization,
        IReadOnlyDictionary<Guid, DateTimeOffset> reviewed,
        CancellationToken cancellationToken)
    {
        var assetCount = await _assets.CountAsync(organization.Id, cancellationToken);
        var peopleCount = await _people.CountAsync(organization.Id, cancellationToken);
        var locationCount = await _locations.CountAsync(organization.Id, cancellationToken);
        var users = await _users.ListAsync(organization.Id, cancellationToken);
        var subscription = await _subscriptions.GetByOrganizationAsync(organization.Id, cancellationToken);
        var plan = subscription is not null ? SubscriptionPlan.FromKey(subscription.PlanKey) : null;

        return new AdminOrganizationSummary(
            organization.Id,
            organization.Name,
            organization.Country,
            organization.CreatedAt,
            plan?.Name ?? "Free",
            subscription?.Status.ToString() ?? "none",
            subscription?.CurrentPeriodEnd,
            assetCount,
            peopleCount,
            locationCount,
            users.Count,
            organization.IsSuspended,
            organization.SuspendedAt,
            organization.SuspendedReason,
            reviewed.TryGetValue(organization.Id, out var reviewedAt) ? reviewedAt : null);
    }
}
