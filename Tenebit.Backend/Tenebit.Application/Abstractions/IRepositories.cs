using Tenebit.Domain.Alerts;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Audits;
using Tenebit.Domain.Dashboards;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Identity;
using Tenebit.Domain.JobProfiles;
using Tenebit.Domain.Licenses;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Domain.Reservations;
using Tenebit.Domain.Settings;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Abstractions;

public interface IAssetRepository
{
    Task<IReadOnlyList<Asset>> ListAsync(Guid organizationId, string? search, AssetStatus? status, string? location, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Asset> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, AssetStatus? status, string? location, Guid? teamId, bool unassignedOnly, DateOnly? warrantyFrom, DateOnly? warrantyTo, string? sortKey, bool sortDesc, int page, int pageSize, CancellationToken cancellationToken);
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

public interface IAssetInspectionRepository
{
    Task<AssetInspection?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<AssetInspection?> GetPendingByAssetAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken);
    void Add(AssetInspection inspection);
}

public interface IPersonRepository
{
    Task<IReadOnlyList<Person>> ListAsync(Guid organizationId, string? search, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Person> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, int page, int pageSize, CancellationToken cancellationToken);
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
    Task<bool> IsUsedAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    void Add(Team team);
    void Remove(Team team);
}

public interface IPersonRelationTypeRepository
{
    Task<IReadOnlyList<PersonRelationType>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<PersonRelationType?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingId, CancellationToken cancellationToken);
    Task<bool> IsUsedAsync(Guid organizationId, string name, CancellationToken cancellationToken);
    void Add(PersonRelationType relationType);
    void Remove(PersonRelationType relationType);
}

public interface IProcedureRepository
{
    Task<IReadOnlyList<Procedure>> ListAsync(Guid organizationId, string? search, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Procedure> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, int page, int pageSize, CancellationToken cancellationToken);
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
    Task<(IReadOnlyList<Assignment> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, AssignmentStatus? status, int page, int pageSize, CancellationToken cancellationToken);
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
    Task<(IReadOnlyList<ActivityLog> Items, int Total)> ListPagedAsync(Guid organizationId, int page, int pageSize, string? entityType, Guid? entityId, string? search, DateTimeOffset? from, DateTimeOffset? to, IReadOnlyCollection<string>? actorSubjects, string? action, CancellationToken cancellationToken);
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

public interface ITwoFactorRecoveryCodeRepository
{
    Task<IReadOnlyList<TwoFactorRecoveryCode>> ListAsync(Guid organizationUserId, CancellationToken cancellationToken);
    void AddRange(IEnumerable<TwoFactorRecoveryCode> codes);
    void RemoveAll(IEnumerable<TwoFactorRecoveryCode> codes);
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
    Task<OrganizationSubscription?> GetByStripeCustomerAsync(string stripeCustomerId, CancellationToken cancellationToken);
    void Add(OrganizationSubscription subscription);
}

public interface ISentAlertRepository
{
    Task<SentAlert?> GetAsync(Guid organizationId, string alertKey, Guid entityId, string recipientEmail, CancellationToken cancellationToken);
    void Add(SentAlert alert);
}

public interface IDashboardLayoutRepository
{
    Task<DashboardLayout?> GetAsync(Guid organizationUserId, CancellationToken cancellationToken);
    void Add(DashboardLayout layout);
}

public interface IDashboardSnapshotRepository
{
    Task<DashboardSnapshot?> GetForDateAsync(Guid organizationId, DateOnly date, CancellationToken cancellationToken);
    Task<DashboardSnapshot?> GetClosestOnOrBeforeAsync(Guid organizationId, DateOnly onOrBefore, CancellationToken cancellationToken);
    void Add(DashboardSnapshot snapshot);
}

public interface ILicenseRepository
{
    Task<IReadOnlyList<License>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<License?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    void Add(License license);
    void Remove(License license);
}

public interface IOffboardingCaseRepository
{
    Task<(IReadOnlyList<OffboardingCase> Items, int Total)> ListPagedAsync(Guid organizationId, OffboardingCaseStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<OffboardingCase?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<OffboardingCase?> FindOpenByPersonAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OffboardingCase>> ListWithPublicTokenAsync(CancellationToken cancellationToken);
    void Add(OffboardingCase offboardingCase);
}

public interface IOffboardingItemRepository
{
    Task<IReadOnlyList<OffboardingItem>> ListByCaseAsync(Guid organizationId, Guid offboardingCaseId, CancellationToken cancellationToken);
    Task<OffboardingItem?> GetAsync(Guid organizationId, Guid offboardingCaseId, Guid itemId, CancellationToken cancellationToken);
    void Add(OffboardingItem item);
}

public interface IAssetAuditCampaignRepository
{
    Task<(IReadOnlyList<AssetAuditCampaign> Items, int Total)> ListPagedAsync(Guid organizationId, AssetAuditCampaignStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<AssetAuditCampaign?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    void Add(AssetAuditCampaign campaign);
}

public interface IAssetAuditParticipantRepository
{
    Task<IReadOnlyList<AssetAuditParticipant>> ListByCampaignAsync(Guid organizationId, Guid campaignId, CancellationToken cancellationToken);
    Task<AssetAuditParticipant?> GetAsync(Guid organizationId, Guid campaignId, Guid participantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetAuditParticipant>> ListWithActiveTokenAsync(CancellationToken cancellationToken);
    void Add(AssetAuditParticipant participant);
}

public interface IAssetAuditItemRepository
{
    Task<IReadOnlyList<AssetAuditItem>> ListByCampaignAsync(Guid organizationId, Guid campaignId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetAuditItem>> ListByParticipantAsync(Guid organizationId, Guid participantId, CancellationToken cancellationToken);
    void Add(AssetAuditItem item);
}

public interface IAssetEvidenceRepository
{
    Task<IReadOnlyList<AssetEvidence>> ListByAssetAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetEvidence>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetEvidence>> ListByAssignmentAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken);
    Task<AssetEvidence?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<int> CountAsync(Guid organizationId, Guid assetId, EvidencePhase phase, CancellationToken cancellationToken);
    void Add(AssetEvidence evidence);
    void Remove(AssetEvidence evidence);
}

public interface IRolePermissionRepository
{
    Task<IReadOnlyList<RolePermission>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<RolePermission?> FindAsync(Guid organizationId, string roleKey, string permissionKey, CancellationToken cancellationToken);
    void Add(RolePermission permission);
    void Remove(RolePermission permission);
}

public interface IEquipmentKitDefinitionRepository
{
    Task<IReadOnlyList<EquipmentKitDefinition>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<EquipmentKitDefinition?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    void Add(EquipmentKitDefinition kitDefinition);
}

public interface IEquipmentReservationRepository
{
    Task<IReadOnlyList<EquipmentReservation>> ListApprovedOverlappingAsync(Guid organizationId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
    Task<IReadOnlyList<EquipmentReservation>> ListByRequesterAsync(Guid organizationId, Guid requesterPersonId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<EquipmentReservation> Items, int Total)> ListPagedAsync(Guid organizationId, EquipmentReservationStatus? status, IReadOnlyCollection<Guid>? requesterPersonIds, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<EquipmentReservation>> ListForCalendarAsync(Guid organizationId, DateTimeOffset from, DateTimeOffset to, IReadOnlyCollection<Guid>? requesterPersonIds, CancellationToken cancellationToken);
    Task<EquipmentReservation?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<EquipmentReservation?> GetByAssignmentIdAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken);
    void Add(EquipmentReservation reservation);
}
