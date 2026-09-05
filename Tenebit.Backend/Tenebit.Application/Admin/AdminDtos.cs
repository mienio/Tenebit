namespace Tenebit.Application.Admin;

// Contracts for the platform admin panel. Read PiiMasking first: these types intentionally carry no
// customer personal data - no asset names, no people's names, no full e-mail addresses. Anything that
// could identify a customer's employee is either aggregated into a count or masked before it is
// serialised, so an attacker holding an admin token gets statistics, not a database dump.

public sealed record AdminOrganizationSummary(
    Guid Id,
    string Name,
    string Country,
    DateTimeOffset CreatedAt,
    string PlanName,
    string SubscriptionStatus,
    DateTimeOffset? CurrentPeriodEnd,
    int AssetCount,
    int PeopleCount,
    int LocationCount,
    int UserCount,
    bool IsSuspended,
    DateTimeOffset? SuspendedAt,
    string? SuspendedReason,
    /// <summary>When the name was last checked against the terms of service; null means "not yet reviewed".</summary>
    DateTimeOffset? ReviewedAt);

/// <summary>A user account as the panel sees it: identifiable enough to moderate, never contactable.</summary>
public sealed record AdminUserSummary(
    Guid Id,
    string MaskedEmail,
    string Initials,
    bool IsActive,
    bool IsEmailVerified,
    bool IsTwoFactorEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<string> Roles);

public sealed record AdminCountSlice(string Label, int Count);

/// <summary>
/// Per-organization drill-down. Asset, people and location records are represented purely as counts and
/// status breakdowns - the panel can show how big an organization is and whether it looks abusive,
/// without ever exposing what it owns or who works there.
/// </summary>
public sealed record AdminOrganizationDetail(
    AdminOrganizationSummary Summary,
    IReadOnlyList<AdminUserSummary> Users,
    IReadOnlyList<AdminCountSlice> AssetsByStatus,
    IReadOnlyList<AdminCountSlice> AssetsByCategory,
    IReadOnlyList<AdminCountSlice> PeopleByStatus,
    int LocationCount,
    AdminSeries AssetsCreated);

public sealed record AdminSeries(string Label, IReadOnlyList<AdminSeriesPoint> Points);

public sealed record AdminSeriesPoint(string Day, int Count);

public sealed record AdminPlanSlice(string Plan, int Count);

public sealed record AdminDashboard(
    int Organizations,
    int SuspendedOrganizations,
    int Users,
    int ActiveUsers,
    int Assets,
    int People,
    int Locations,
    int Licenses,
    int LoginsInRange,
    int FailedLoginsInRange,
    int PendingReview,
    string RangeFrom,
    string RangeTo,
    AdminSeries AssetsCreated,
    AdminSeries OrganizationsCreated,
    AdminSeries Logins,
    AdminSeries FailedLogins,
    IReadOnlyList<AdminPlanSlice> Plans,
    IReadOnlyList<AdminOrganizationSummary> NewestOrganizations);

public sealed record AdminLoginEntry(
    Guid Id,
    Guid? OrganizationId,
    string? OrganizationName,
    Guid? UserId,
    string MaskedEmail,
    bool Succeeded,
    string? FailureReason,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset CreatedAt);

public sealed record AdminUserListItem(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    bool OrganizationSuspended,
    string MaskedEmail,
    string Initials,
    bool IsActive,
    bool IsEmailVerified,
    bool IsTwoFactorEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<string> Roles);

public sealed record AdminAuditEntry(
    Guid Id,
    string Action,
    string? TargetType,
    Guid? TargetId,
    string? TargetLabel,
    string? Details,
    string? IpAddress,
    DateTimeOffset CreatedAt);

public sealed record AdminPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>One Stripe invoice for an organization - a financial record of the organization itself, not
/// personal data of anyone who works there, so (unlike the rest of this file) it is not masked.</summary>
public sealed record AdminPaymentEntry(
    string Id,
    string? Number,
    decimal AmountPaid,
    decimal AmountDue,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt,
    string? HostedInvoiceUrl,
    string? InvoicePdfUrl);

/// <summary>An organization's payment history, pulled live from Stripe (Tenebit keeps no local copy) -
/// see AdminOverviewService.GetOrganizationPaymentsAsync.</summary>
public sealed record AdminOrganizationPayments(
    decimal TotalPaid,
    string Currency,
    IReadOnlyList<AdminPaymentEntry> Invoices);
