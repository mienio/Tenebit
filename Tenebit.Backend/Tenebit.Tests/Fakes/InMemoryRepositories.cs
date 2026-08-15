using Tenebit.Application.Abstractions;
using Tenebit.Domain.Alerts;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Identity;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.Reservations;

namespace Tenebit.Tests.Fakes;

public sealed class InMemoryOrganizationRepository : IOrganizationRepository
{
    public List<Organization> Organizations { get; } = [];

    public Task<Organization?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Organizations.FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<Organization>> ListAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Organization>>(Organizations);

    public void Add(Organization organization) => Organizations.Add(organization);
}

public sealed class InMemoryOrganizationUserRepository : IOrganizationUserRepository
{
    public List<OrganizationUser> Users { get; } = [];

    public Task<IReadOnlyList<OrganizationUser>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OrganizationUser>>(Users.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<OrganizationUser?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<OrganizationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(x => x.Id == id));

    public Task<bool> EmailExistsAsync(Guid organizationId, string email, Guid? excludingId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.Any(x => x.OrganizationId == organizationId && x.Email == email.Trim().ToLowerInvariant() && (!excludingId.HasValue || x.Id != excludingId.Value)));

    public Task<OrganizationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(x => x.Email == email.Trim().ToLowerInvariant()));

    public void Add(OrganizationUser user) => Users.Add(user);
}

public sealed class InMemoryAssetCategoryRepository : IAssetCategoryRepository
{
    public List<AssetCategory> Categories { get; } = [];

    public Task<IReadOnlyList<AssetCategory>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetCategory>>(Categories.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<AssetCategory?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Categories.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingCategoryId, CancellationToken cancellationToken) =>
        Task.FromResult(Categories.Any(x => x.OrganizationId == organizationId && x.Name == name && (!excludingCategoryId.HasValue || x.Id != excludingCategoryId.Value)));

    public bool IsUsed { get; set; }

    public Task<bool> IsUsedAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(IsUsed);

    public void Add(AssetCategory category) => Categories.Add(category);
    public void Remove(AssetCategory category) => Categories.Remove(category);
}

public sealed class InMemoryEquipmentReservationRepository : IEquipmentReservationRepository
{
    private static readonly EquipmentReservationStatus[] HoldingStatuses =
    [
        EquipmentReservationStatus.Approved,
        EquipmentReservationStatus.ReadyForPickup,
        EquipmentReservationStatus.CheckedOut
    ];

    private static readonly EquipmentReservationStatus[] CalendarStatuses =
    [
        EquipmentReservationStatus.PendingApproval,
        EquipmentReservationStatus.Approved,
        EquipmentReservationStatus.ReadyForPickup,
        EquipmentReservationStatus.CheckedOut
    ];

    public List<EquipmentReservation> Reservations { get; } = [];

    public Task<IReadOnlyList<EquipmentReservation>> ListOpenAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EquipmentReservation>>(Reservations
            .Where(x => x.OrganizationId == organizationId && CalendarStatuses.Contains(x.Status))
            .ToList());

    public Task<IReadOnlyList<EquipmentReservation>> ListApprovedOverlappingAsync(Guid organizationId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EquipmentReservation>>(Reservations
            .Where(x => x.OrganizationId == organizationId && HoldingStatuses.Contains(x.Status) && x.StartAt < to && x.EndAt > from)
            .ToList());

    public Task<IReadOnlyList<EquipmentReservation>> ListForCalendarAsync(Guid organizationId, DateTimeOffset from, DateTimeOffset to, IReadOnlyCollection<Guid>? requesterPersonIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EquipmentReservation>>(Reservations
            .Where(x => x.OrganizationId == organizationId && CalendarStatuses.Contains(x.Status) && x.StartAt < to && x.EndAt > from
                && (requesterPersonIds == null || requesterPersonIds.Contains(x.RequesterPersonId)))
            .ToList());

    public Task<IReadOnlyList<EquipmentReservation>> ListByRequesterAsync(Guid organizationId, Guid requesterPersonId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EquipmentReservation>>(Reservations
            .Where(x => x.OrganizationId == organizationId && x.RequesterPersonId == requesterPersonId)
            .OrderByDescending(x => x.CreatedAt)
            .ToList());

    public Task<(IReadOnlyList<EquipmentReservation> Items, int Total)> ListPagedAsync(Guid organizationId, EquipmentReservationStatus? status, IReadOnlyCollection<Guid>? requesterPersonIds, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = Reservations
            .Where(x => x.OrganizationId == organizationId && (!status.HasValue || x.Status == status.Value)
                && (requesterPersonIds == null || requesterPersonIds.Contains(x.RequesterPersonId)))
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
        return Task.FromResult<(IReadOnlyList<EquipmentReservation>, int)>((rows, rows.Count));
    }

    public Task<EquipmentReservation?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Reservations.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<EquipmentReservation?> GetByAssignmentIdAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Reservations.FirstOrDefault(x => x.OrganizationId == organizationId && x.AssignmentId == assignmentId));

    public void Add(EquipmentReservation reservation) => Reservations.Add(reservation);
}

public sealed class InMemoryAssetInspectionRepository : IAssetInspectionRepository
{
    public List<AssetInspection> Inspections { get; } = [];

    public Task<AssetInspection?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Inspections.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<AssetInspection?> GetPendingByAssetAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken) =>
        Task.FromResult(Inspections
            .Where(x => x.OrganizationId == organizationId && x.AssetId == assetId && x.Outcome is null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault());

    public void Add(AssetInspection inspection) => Inspections.Add(inspection);
}

public sealed class InMemoryActivityLogRepository : IActivityLogRepository
{
    public List<ActivityLog> Logs { get; } = [];

    public Task<IReadOnlyList<ActivityLog>> ListAsync(Guid organizationId, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ActivityLog>>(Logs.Where(x => x.OrganizationId == organizationId).Take(limit).ToList());

    public Task<(IReadOnlyList<ActivityLog> Items, int Total)> ListPagedAsync(Guid organizationId, int page, int pageSize, string? entityType, Guid? entityId, string? search, DateTimeOffset? from, DateTimeOffset? to, IReadOnlyCollection<string>? actorSubjects, string? action, CancellationToken cancellationToken) =>
        Task.FromResult<(IReadOnlyList<ActivityLog>, int)>((Logs, Logs.Count));

    public void Add(ActivityLog log) => Logs.Add(log);
}

public sealed class InMemoryExternalLoginRepository : IExternalLoginRepository
{
    public List<ExternalLogin> Links { get; } = [];
    private readonly InMemoryOrganizationUserRepository _users;

    public InMemoryExternalLoginRepository(InMemoryOrganizationUserRepository users) => _users = users;

    public Task<OrganizationUser?> FindLinkedUserAsync(string provider, string providerUserId, CancellationToken cancellationToken)
    {
        var link = Links.FirstOrDefault(x => x.Provider == provider && x.ProviderUserId == providerUserId);
        return Task.FromResult(link is null ? null : _users.Users.FirstOrDefault(x => x.Id == link.OrganizationUserId));
    }

    public Task<bool> ExistsAsync(Guid organizationUserId, string provider, CancellationToken cancellationToken) =>
        Task.FromResult(Links.Any(x => x.OrganizationUserId == organizationUserId && x.Provider == provider));

    public Task<IReadOnlyList<string>> ListProvidersAsync(Guid organizationUserId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(Links.Where(x => x.OrganizationUserId == organizationUserId).Select(x => x.Provider).ToList());

    public Task<ExternalLogin?> FindAsync(Guid organizationUserId, string provider, CancellationToken cancellationToken) =>
        Task.FromResult(Links.FirstOrDefault(x => x.OrganizationUserId == organizationUserId && x.Provider == provider));

    public void Add(ExternalLogin externalLogin) => Links.Add(externalLogin);
    public void Remove(ExternalLogin externalLogin) => Links.Remove(externalLogin);
}

public sealed class InMemoryPasswordResetTokenRepository : IPasswordResetTokenRepository
{
    public List<PasswordResetToken> Tokens { get; } = [];

    public Task<PasswordResetToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.FirstOrDefault(x => x.TokenHash == tokenHash && x.IsValid(now)));

    public void Add(PasswordResetToken token) => Tokens.Add(token);
}

public sealed class InMemoryEmailVerificationTokenRepository : IEmailVerificationTokenRepository
{
    public List<EmailVerificationToken> Tokens { get; } = [];

    public Task<EmailVerificationToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.FirstOrDefault(x => x.TokenHash == tokenHash && x.IsValid(now)));

    public void Add(EmailVerificationToken token) => Tokens.Add(token);
}

public sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Tokens { get; } = [];

    public Task<RefreshToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.FirstOrDefault(x => x.TokenHash == tokenHash && x.IsValid(now)));

    public void Add(RefreshToken token) => Tokens.Add(token);
}

public sealed class InMemoryDeviceTrustTokenRepository : IDeviceTrustTokenRepository
{
    public List<DeviceTrustToken> Tokens { get; } = [];

    public Task<DeviceTrustToken?> FindValidAsync(Guid organizationUserId, string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.FirstOrDefault(x => x.OrganizationUserId == organizationUserId && x.TokenHash == tokenHash && x.IsValid(now)));

    public void Add(DeviceTrustToken token) => Tokens.Add(token);
}

public sealed class InMemoryTwoFactorRecoveryCodeRepository : ITwoFactorRecoveryCodeRepository
{
    public List<TwoFactorRecoveryCode> Codes { get; } = [];

    public Task<IReadOnlyList<TwoFactorRecoveryCode>> ListAsync(Guid organizationUserId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TwoFactorRecoveryCode>>(Codes.Where(x => x.OrganizationUserId == organizationUserId).ToList());

    public void AddRange(IEnumerable<TwoFactorRecoveryCode> codes) => Codes.AddRange(codes);
    public void RemoveAll(IEnumerable<TwoFactorRecoveryCode> codes) { foreach (var code in codes.ToList()) Codes.Remove(code); }
}

public sealed class InMemorySentAlertRepository : ISentAlertRepository
{
    public List<SentAlert> Alerts { get; } = [];

    public Task<SentAlert?> GetAsync(Guid organizationId, string alertKey, Guid entityId, string recipientEmail, CancellationToken cancellationToken)
    {
        var normalized = recipientEmail.Trim().ToLowerInvariant();
        return Task.FromResult(Alerts.FirstOrDefault(x =>
            x.OrganizationId == organizationId && x.AlertKey == alertKey && x.EntityId == entityId && x.RecipientEmail == normalized));
    }

    public void Add(SentAlert alert) => Alerts.Add(alert);

    public Task<SentAlert?> GetLatestAsync(Guid organizationId, Guid entityId, string alertKeyPrefix, CancellationToken cancellationToken) =>
        Task.FromResult(Alerts
            .Where(x => x.OrganizationId == organizationId && x.EntityId == entityId && x.AlertKey.StartsWith(alertKeyPrefix))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault());

    public Task<(IReadOnlyList<SentAlert> Items, int Total)> ListPagedAsync(Guid organizationId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = Alerts.Where(x => x.OrganizationId == organizationId).OrderByDescending(x => x.CreatedAt);
        var total = query.Count();
        var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList().AsReadOnly();
        return Task.FromResult(((IReadOnlyList<SentAlert>)items, total));
    }
}

public sealed class InMemoryAlertRuleRepository : IAlertRuleRepository
{
    public List<AlertRule> Rules { get; } = [];

    public Task<IReadOnlyList<AlertRule>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AlertRule>>(Rules.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<AlertRule?> GetAsync(Guid organizationId, AlertType type, CancellationToken cancellationToken) =>
        Task.FromResult(Rules.FirstOrDefault(x => x.OrganizationId == organizationId && x.Type == type));

    public void Add(AlertRule rule) => Rules.Add(rule);

    public void Update(AlertRule rule)
    {
        var idx = Rules.FindIndex(x => x.OrganizationId == rule.OrganizationId && x.Type == rule.Type);
        if (idx >= 0) Rules[idx] = rule;
    }
}

public sealed class InMemoryAlertDigestSettingsRepository : IAlertDigestSettingsRepository
{
    public List<AlertDigestSettings> Settings { get; } = [];

    public Task<AlertDigestSettings?> GetAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(Settings.FirstOrDefault(x => x.OrganizationId == organizationId));

    public void Add(AlertDigestSettings settings) => Settings.Add(settings);

    public void Update(AlertDigestSettings settings)
    {
        var idx = Settings.FindIndex(x => x.OrganizationId == settings.OrganizationId);
        if (idx >= 0) Settings[idx] = settings;
    }
}

public sealed class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject)> Sent { get; } = [];
    public List<string> Bodies { get; } = [];
    public HashSet<string> FailFor { get; } = [];
    public int AttemptCount { get; private set; }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        AttemptCount++;
        if (FailFor.Contains(to)) throw new InvalidOperationException("Simulated SMTP failure");
        Sent.Add((to, subject));
        Bodies.Add(htmlBody);
        return Task.CompletedTask;
    }
}

public sealed class FakeAppLinkBuilder : IAppLinkBuilder
{
    public string BuildAssignmentAcceptanceLink(Guid organizationId, Guid assignmentId) => $"https://test/accept/{organizationId}/{assignmentId}";
    public string BuildAssetScanLink(Guid organizationId, Guid assetId) => $"https://test/scan/{organizationId}/{assetId}";
    public string BuildPasswordResetLink(string rawToken) => $"https://test/reset-password?token={rawToken}";
    public string BuildEmailVerificationLink(string rawToken) => $"https://test/verify-email?token={rawToken}";
    public string BuildOffboardingLink(string rawToken) => $"https://test/exit/{rawToken}";
    public string BuildAssetAuditLink(string rawToken) => $"https://test/audit/{rawToken}";
}

public sealed class FakeQrCodeGenerator : IQrCodeGenerator
{
    public string CreateAssetQrSvg(string payload) => "<svg/>";
    public string CreateTotpQrSvg(string otpAuthUri) => "<svg/>";
}

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(0);
}
