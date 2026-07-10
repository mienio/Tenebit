using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Identity;
using Tenebit.Domain.Organizations;

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

public sealed class InMemoryActivityLogRepository : IActivityLogRepository
{
    public List<ActivityLog> Logs { get; } = [];

    public Task<IReadOnlyList<ActivityLog>> ListAsync(Guid organizationId, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ActivityLog>>(Logs.Where(x => x.OrganizationId == organizationId).Take(limit).ToList());

    public Task<(IReadOnlyList<ActivityLog> Items, int Total)> ListPagedAsync(Guid organizationId, int page, int pageSize, string? entityType, Guid? entityId, string? search, CancellationToken cancellationToken) =>
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

public sealed class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject)> Sent { get; } = [];

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        Sent.Add((to, subject));
        return Task.CompletedTask;
    }
}

public sealed class FakeAppLinkBuilder : IAppLinkBuilder
{
    public string BuildAssignmentAcceptanceLink(Guid organizationId, Guid assignmentId) => $"https://test/accept/{organizationId}/{assignmentId}";
    public string BuildAssetScanLink(Guid organizationId, Guid assetId) => $"https://test/scan/{organizationId}/{assetId}";
    public string BuildPasswordResetLink(string rawToken) => $"https://test/reset-password?token={rawToken}";
    public string BuildEmailVerificationLink(string rawToken) => $"https://test/verify-email?token={rawToken}";
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
