using Tenebit.Domain.Alerts;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Identity;
using Tenebit.Domain.JobProfiles;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Domain.Settings;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Abstractions;

public interface IAssetRepository
{
    Task<IReadOnlyList<Asset>> ListAsync(Guid organizationId, string? search, AssetStatus? status, string? location, CancellationToken cancellationToken);
    Task<IReadOnlyList<Asset>> GetByIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task<Asset?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<bool> AssetTagExistsAsync(Guid organizationId, string assetTag, Guid? excludingAssetId, CancellationToken cancellationToken);
    void Add(Asset asset);
    void Remove(Asset asset);
}

public interface IAssetCategoryRepository
{
    Task<IReadOnlyList<AssetCategory>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<AssetCategory?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingCategoryId, CancellationToken cancellationToken);
    Task<bool> IsUsedAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    void Add(AssetCategory category);
    void Remove(AssetCategory category);
}

public interface IPersonRepository
{
    Task<IReadOnlyList<Person>> ListAsync(Guid organizationId, string? search, CancellationToken cancellationToken);
    Task<Person?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<Person?> FindByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(Guid organizationId, string email, Guid? excludingPersonId, CancellationToken cancellationToken);
    Task<bool> HasBlockingRelationsAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken);
    void Add(Person person);
    void Remove(Person person);
}

public interface ITeamRepository
{
    Task<IReadOnlyList<Team>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Team?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingTeamId, CancellationToken cancellationToken);
    void Add(Team team);
}

public interface IProcedureRepository
{
    Task<IReadOnlyList<Procedure>> ListAsync(Guid organizationId, string? search, CancellationToken cancellationToken);
    Task<IReadOnlyList<Procedure>> GetByIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task<Procedure?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<ProcedureDocument?> GetDocumentAsync(Guid organizationId, Guid procedureId, Guid documentId, CancellationToken cancellationToken);
    void Add(Procedure procedure);
    void AddDocument(ProcedureDocument document);
    void RemoveDocument(ProcedureDocument document);
}

public interface IAssignmentRepository
{
    Task<IReadOnlyList<Assignment>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Assignment?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    void Add(Assignment assignment);
}

public interface IOrganizationRepository
{
    Task<Organization?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Organization>> ListAllAsync(CancellationToken cancellationToken);
    void Add(Organization organization);
}

public interface IActivityLogRepository
{
    Task<IReadOnlyList<ActivityLog>> ListAsync(Guid organizationId, int limit, CancellationToken cancellationToken);
    Task<(IReadOnlyList<ActivityLog> Items, int Total)> ListPagedAsync(Guid organizationId, int page, int pageSize, string? entityType, Guid? entityId, string? search, CancellationToken cancellationToken);
    void Add(ActivityLog log);
}

public interface IOrganizationUserRepository
{
    Task<IReadOnlyList<OrganizationUser>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<OrganizationUser?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<OrganizationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(Guid organizationId, string email, Guid? excludingId, CancellationToken cancellationToken);
    Task<OrganizationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    void Add(OrganizationUser user);
}

public interface IExternalLoginRepository
{
    Task<OrganizationUser?> FindLinkedUserAsync(string provider, string providerUserId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid organizationUserId, string provider, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListProvidersAsync(Guid organizationUserId, CancellationToken cancellationToken);
    Task<ExternalLogin?> FindAsync(Guid organizationUserId, string provider, CancellationToken cancellationToken);
    void Add(ExternalLogin externalLogin);
    void Remove(ExternalLogin externalLogin);
}

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken);
    void Add(PasswordResetToken token);
}

public interface IEmailVerificationTokenRepository
{
    Task<EmailVerificationToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken);
    void Add(EmailVerificationToken token);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken);
    void Add(RefreshToken token);
}

public interface IDeviceTrustTokenRepository
{
    Task<DeviceTrustToken?> FindValidAsync(Guid organizationUserId, string tokenHash, DateTimeOffset now, CancellationToken cancellationToken);
    void Add(DeviceTrustToken token);
}

public interface IJobProfileRepository
{
    Task<IReadOnlyList<JobProfile>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<JobProfile?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingId, CancellationToken cancellationToken);
    void Add(JobProfile profile);
    void Remove(JobProfile profile);
}

public interface IAssetStatusSettingRepository
{
    Task<IReadOnlyList<AssetStatusSetting>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<AssetStatusSetting?> GetByKeyAsync(Guid organizationId, string statusKey, CancellationToken cancellationToken);
    void Add(AssetStatusSetting setting);
}

public interface ISubscriptionRepository
{
    Task<OrganizationSubscription?> GetByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
    void Add(OrganizationSubscription subscription);
}

public interface ISentAlertRepository
{
    Task<bool> ExistsAsync(Guid organizationId, string alertKey, Guid entityId, CancellationToken cancellationToken);
    void Add(SentAlert alert);
}
