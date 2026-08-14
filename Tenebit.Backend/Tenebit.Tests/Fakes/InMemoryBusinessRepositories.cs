using System.Security.Cryptography;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audits;
using Tenebit.Domain.Dashboards;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Licenses;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Domain.Settings;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Tests.Fakes;

public sealed class InMemoryAssetRepository : IAssetRepository
{
    public List<Asset> Assets { get; } = [];

    public Task<IReadOnlyList<Asset>> ListAsync(Guid organizationId, string? search, AssetStatus? status, string? location, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Asset>>(Assets
            .Where(x => x.OrganizationId == organizationId && (!status.HasValue || x.Status == status.Value))
            .ToList());

    public Task<(IReadOnlyList<Asset> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, AssetStatus? status, string? location, Guid? teamId, bool unassignedOnly, DateOnly? warrantyFrom, DateOnly? warrantyTo, string? sortKey, bool sortDesc, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = Assets.Where(x => x.OrganizationId == organizationId).ToList();
        return Task.FromResult<(IReadOnlyList<Asset>, int)>((rows, rows.Count));
    }

    public Task<IReadOnlyList<Asset>> GetByIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Asset>>(Assets.Where(x => x.OrganizationId == organizationId && ids.Contains(x.Id)).ToList());

    public Task<Asset?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Assets.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<bool> AssetTagExistsAsync(Guid organizationId, string assetTag, Guid? excludingAssetId, CancellationToken cancellationToken) =>
        Task.FromResult(Assets.Any(x => x.OrganizationId == organizationId && x.AssetTag == assetTag && (!excludingAssetId.HasValue || x.Id != excludingAssetId.Value)));

    public void Add(Asset asset) => Assets.Add(asset);
    public void Remove(Asset asset) => Assets.Remove(asset);
}

public sealed class InMemoryPersonRepository : IPersonRepository
{
    public List<Person> People { get; } = [];

    public Task<IReadOnlyList<Person>> ListAsync(Guid organizationId, string? search, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Person>>(People.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<(IReadOnlyList<Person> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = People.Where(x => x.OrganizationId == organizationId).ToList();
        return Task.FromResult<(IReadOnlyList<Person>, int)>((rows, rows.Count));
    }

    public Task<Person?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(People.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<Person?> FindByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken) =>
        Task.FromResult(People.FirstOrDefault(x => x.OrganizationId == organizationId && x.Email == email));

    public Task<bool> EmailExistsAsync(Guid organizationId, string email, Guid? excludingPersonId, CancellationToken cancellationToken) =>
        Task.FromResult(People.Any(x => x.OrganizationId == organizationId && x.Email == email && (!excludingPersonId.HasValue || x.Id != excludingPersonId.Value)));

    public bool HasBlockingRelations { get; set; }

    public Task<bool> HasBlockingRelationsAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken) =>
        Task.FromResult(HasBlockingRelations);

    public void Add(Person person) => People.Add(person);
    public void Remove(Person person) => People.Remove(person);
}

public sealed class InMemoryTeamRepository : ITeamRepository
{
    public List<Team> Teams { get; } = [];
    public List<Person> People { get; } = [];
    public List<Asset> Assets { get; } = [];

    public Task<IReadOnlyList<Team>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Team>>(Teams.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<Team?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Teams.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingTeamId, CancellationToken cancellationToken) =>
        Task.FromResult(Teams.Any(x => x.OrganizationId == organizationId && x.Name == name && (!excludingTeamId.HasValue || x.Id != excludingTeamId.Value)));

    public Task<bool> IsUsedAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(People.Any(x => x.OrganizationId == organizationId && x.TeamId == id) || Assets.Any(x => x.OrganizationId == organizationId && x.TeamId == id));

    public void Add(Team team) => Teams.Add(team);
    public void Remove(Team team) => Teams.Remove(team);
}

public sealed class InMemoryPersonRelationTypeRepository : IPersonRelationTypeRepository
{
    public List<PersonRelationType> RelationTypes { get; } = [];
    public List<Person> People { get; } = [];

    public Task<IReadOnlyList<PersonRelationType>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PersonRelationType>>(RelationTypes.Where(x => x.OrganizationId == organizationId).OrderBy(x => x.SortOrder).ToList());

    public Task<PersonRelationType?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(RelationTypes.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingId, CancellationToken cancellationToken) =>
        Task.FromResult(RelationTypes.Any(x => x.OrganizationId == organizationId && x.Name == name.Trim() && (!excludingId.HasValue || x.Id != excludingId.Value)));

    public Task<bool> IsUsedAsync(Guid organizationId, string name, CancellationToken cancellationToken) =>
        Task.FromResult(People.Any(x => x.OrganizationId == organizationId && x.RelationType == name));

    public void Add(PersonRelationType relationType) => RelationTypes.Add(relationType);
    public void Remove(PersonRelationType relationType) => RelationTypes.Remove(relationType);
}

public sealed class InMemoryProcedureRepository : IProcedureRepository
{
    public List<Procedure> Procedures { get; } = [];

    public Task<IReadOnlyList<Procedure>> ListAsync(Guid organizationId, string? search, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Procedure>>(Procedures.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<(IReadOnlyList<Procedure> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = Procedures.Where(x => x.OrganizationId == organizationId).ToList();
        return Task.FromResult<(IReadOnlyList<Procedure>, int)>((rows, rows.Count));
    }

    public Task<IReadOnlyList<Procedure>> GetByIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Procedure>>(Procedures.Where(x => x.OrganizationId == organizationId && ids.Contains(x.Id)).ToList());

    public Task<Procedure?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Procedures.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<ProcedureDocument?> GetDocumentAsync(Guid organizationId, Guid procedureId, Guid documentId, CancellationToken cancellationToken) =>
        Task.FromResult(Procedures.FirstOrDefault(x => x.Id == procedureId)?.Documents.FirstOrDefault(x => x.Id == documentId));

    public void Add(Procedure procedure) => Procedures.Add(procedure);
    public void AddDocument(ProcedureDocument document) { }
    public void RemoveDocument(ProcedureDocument document) { }
}

public sealed class InMemoryAssignmentRepository : IAssignmentRepository
{
    public List<Assignment> Assignments { get; } = [];

    public Task<IReadOnlyList<Assignment>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Assignment>>(Assignments.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<(IReadOnlyList<Assignment> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, AssignmentStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = Assignments.Where(x => x.OrganizationId == organizationId).ToList();
        return Task.FromResult<(IReadOnlyList<Assignment>, int)>((rows, rows.Count));
    }

    public Task<Assignment?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Assignments.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public void Add(Assignment assignment) => Assignments.Add(assignment);
}

public sealed class InMemorySubscriptionRepository : ISubscriptionRepository
{
    public List<OrganizationSubscription> Subscriptions { get; } = [];

    public Task<OrganizationSubscription?> GetByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(Subscriptions.FirstOrDefault(x => x.OrganizationId == organizationId));

    public Task<OrganizationSubscription?> GetByStripeCustomerAsync(string stripeCustomerId, CancellationToken cancellationToken) =>
        Task.FromResult(Subscriptions.FirstOrDefault(x => x.StripeCustomerId == stripeCustomerId));

    public void Add(OrganizationSubscription subscription) => Subscriptions.Add(subscription);
}

public sealed class InMemoryDashboardLayoutRepository : IDashboardLayoutRepository
{
    public List<DashboardLayout> Layouts { get; } = [];

    public Task<DashboardLayout?> GetAsync(Guid organizationUserId, CancellationToken cancellationToken) =>
        Task.FromResult(Layouts.FirstOrDefault(x => x.OrganizationUserId == organizationUserId));

    public void Add(DashboardLayout layout) => Layouts.Add(layout);
}

public sealed class InMemoryDashboardSnapshotRepository : IDashboardSnapshotRepository
{
    public List<DashboardSnapshot> Snapshots { get; } = [];

    public Task<DashboardSnapshot?> GetForDateAsync(Guid organizationId, DateOnly date, CancellationToken cancellationToken) =>
        Task.FromResult(Snapshots.FirstOrDefault(x => x.OrganizationId == organizationId && x.SnapshotDate == date));

    public Task<DashboardSnapshot?> GetClosestOnOrBeforeAsync(Guid organizationId, DateOnly onOrBefore, CancellationToken cancellationToken) =>
        Task.FromResult(Snapshots
            .Where(x => x.OrganizationId == organizationId && x.SnapshotDate <= onOrBefore)
            .OrderByDescending(x => x.SnapshotDate)
            .FirstOrDefault());

    public void Add(DashboardSnapshot snapshot) => Snapshots.Add(snapshot);
}

public sealed class InMemoryLicenseRepository : ILicenseRepository
{
    public List<License> Licenses { get; } = [];

    public Task<IReadOnlyList<License>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<License>>(Licenses.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<License?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Licenses.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public void Add(License license) => Licenses.Add(license);
    public void Remove(License license) => Licenses.Remove(license);
}

public sealed class InMemoryOffboardingCaseRepository : IOffboardingCaseRepository
{
    private static readonly OffboardingCaseStatus[] ClosedStatuses = [OffboardingCaseStatus.Completed, OffboardingCaseStatus.Cancelled];

    public List<OffboardingCase> Cases { get; } = [];

    public Task<(IReadOnlyList<OffboardingCase> Items, int Total)> ListPagedAsync(Guid organizationId, OffboardingCaseStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = Cases.Where(x => x.OrganizationId == organizationId && (!status.HasValue || x.Status == status.Value)).ToList();
        return Task.FromResult<(IReadOnlyList<OffboardingCase>, int)>((rows, rows.Count));
    }

    public Task<OffboardingCase?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Cases.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<OffboardingCase?> FindOpenByPersonAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken) =>
        Task.FromResult(Cases.FirstOrDefault(x => x.OrganizationId == organizationId && x.PersonId == personId && !ClosedStatuses.Contains(x.Status)));

    public Task<IReadOnlyList<OffboardingCase>> ListWithPublicTokenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OffboardingCase>>(Cases.Where(x => x.PublicTokenHash != null && x.PublicTokenRevokedAt == null).ToList());

    public void Add(OffboardingCase offboardingCase) => Cases.Add(offboardingCase);
}

public sealed class InMemoryOffboardingItemRepository : IOffboardingItemRepository
{
    public List<OffboardingItem> Items { get; } = [];

    public Task<IReadOnlyList<OffboardingItem>> ListByCaseAsync(Guid organizationId, Guid offboardingCaseId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OffboardingItem>>(Items
            .Where(x => x.OrganizationId == organizationId && x.OffboardingCaseId == offboardingCaseId)
            .OrderBy(x => x.SortOrder)
            .ToList());

    public Task<OffboardingItem?> GetAsync(Guid organizationId, Guid offboardingCaseId, Guid itemId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.OrganizationId == organizationId && x.OffboardingCaseId == offboardingCaseId && x.Id == itemId));

    public void Add(OffboardingItem item) => Items.Add(item);
}

public sealed class InMemoryAssetAuditCampaignRepository : IAssetAuditCampaignRepository
{
    public List<AssetAuditCampaign> Campaigns { get; } = [];

    public Task<(IReadOnlyList<AssetAuditCampaign> Items, int Total)> ListPagedAsync(Guid organizationId, AssetAuditCampaignStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = Campaigns.Where(x => x.OrganizationId == organizationId && (!status.HasValue || x.Status == status.Value)).ToList();
        return Task.FromResult<(IReadOnlyList<AssetAuditCampaign>, int)>((rows, rows.Count));
    }

    public Task<AssetAuditCampaign?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Campaigns.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public void Add(AssetAuditCampaign campaign) => Campaigns.Add(campaign);
}

public sealed class InMemoryAssetAuditParticipantRepository : IAssetAuditParticipantRepository
{
    public List<AssetAuditParticipant> Participants { get; } = [];

    public Task<IReadOnlyList<AssetAuditParticipant>> ListByCampaignAsync(Guid organizationId, Guid campaignId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetAuditParticipant>>(Participants.Where(x => x.OrganizationId == organizationId && x.CampaignId == campaignId).ToList());

    public Task<AssetAuditParticipant?> GetAsync(Guid organizationId, Guid campaignId, Guid participantId, CancellationToken cancellationToken) =>
        Task.FromResult(Participants.FirstOrDefault(x => x.OrganizationId == organizationId && x.CampaignId == campaignId && x.Id == participantId));

    public void Add(AssetAuditParticipant participant) => Participants.Add(participant);

    public Task<IReadOnlyList<AssetAuditParticipant>> ListWithActiveTokenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetAuditParticipant>>(Participants.Where(x => x.TokenHash != null && x.TokenRevokedAt == null).ToList());
}

public sealed class InMemoryAssetAuditItemRepository : IAssetAuditItemRepository
{
    public List<AssetAuditItem> Items { get; } = [];

    public Task<IReadOnlyList<AssetAuditItem>> ListByCampaignAsync(Guid organizationId, Guid campaignId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetAuditItem>>(Items.Where(x => x.OrganizationId == organizationId && x.CampaignId == campaignId).ToList());

    public Task<IReadOnlyList<AssetAuditItem>> ListByParticipantAsync(Guid organizationId, Guid participantId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetAuditItem>>(Items.Where(x => x.OrganizationId == organizationId && x.ParticipantId == participantId).ToList());

    public void Add(AssetAuditItem item) => Items.Add(item);
}

public sealed class FakePaymentGateway : IPaymentGateway
{
    public bool IsConfigured { get; set; } = true;
    public string NextCustomerId { get; set; } = "cus_fake";
    public string NextCheckoutUrl { get; set; } = "https://checkout.stripe.com/fake-session";
    public string NextPortalUrl { get; set; } = "https://billing.stripe.com/fake-portal";
    public PaymentWebhookEvent? NextWebhookEvent { get; set; }
    public bool ThrowOnParseWebhookEvent { get; set; }

    public Task<string> CreateCustomerAsync(string email, Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(NextCustomerId);

    public Task<string> CreateCheckoutSessionAsync(string customerId, Guid organizationId, string successUrl, string cancelUrl, CancellationToken cancellationToken) =>
        Task.FromResult(NextCheckoutUrl);

    public Task<string> CreateBillingPortalSessionAsync(string customerId, string returnUrl, CancellationToken cancellationToken) =>
        Task.FromResult(NextPortalUrl);

    public PaymentWebhookEvent? ParseWebhookEvent(string payload, string signatureHeader)
    {
        if (ThrowOnParseWebhookEvent) throw new InvalidOperationException("Invalid Stripe webhook signature.");
        return NextWebhookEvent;
    }
}

public sealed class FakePdfProtocolGenerator : IPdfProtocolGenerator
{
    public byte[] GenerateHandoverProtocol(ProtocolPdfModel model) => [1, 2, 3];
    public byte[] GenerateOffboardingProtocol(OffboardingProtocolPdfModel model) => [1, 2, 3];
    public AssetAuditReportPdfModel? LastAssetAuditReportModel { get; private set; }

    public byte[] GenerateAssetAuditReport(AssetAuditReportPdfModel model)
    {
        LastAssetAuditReportModel = model;
        return [1, 2, 3];
    }
}

public sealed class InMemoryAssetEvidenceRepository : IAssetEvidenceRepository
{
    public List<AssetEvidence> Items { get; } = [];

    public Task<IReadOnlyList<AssetEvidence>> ListByAssetAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetEvidence>>(Items.Where(x => x.OrganizationId == organizationId && x.AssetId == assetId).ToList());

    public Task<IReadOnlyList<AssetEvidence>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetEvidence>>(Items.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<AssetEvidence?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<int> CountAsync(Guid organizationId, Guid assetId, EvidencePhase phase, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Count(x => x.OrganizationId == organizationId && x.AssetId == assetId && x.Phase == phase));

    public void Add(AssetEvidence evidence) => Items.Add(evidence);
    public void Remove(AssetEvidence evidence) => Items.Remove(evidence);
}

public sealed class InMemoryAssetStatusSettingRepository : IAssetStatusSettingRepository
{
    public List<AssetStatusSetting> Items { get; } = [];

    public Task<IReadOnlyList<AssetStatusSetting>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetStatusSetting>>(Items.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<AssetStatusSetting?> GetByKeyAsync(Guid organizationId, string statusKey, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.OrganizationId == organizationId && x.StatusKey == statusKey));

    public void Add(AssetStatusSetting setting) => Items.Add(setting);
}

public sealed class FakeImageSanitizer : IImageSanitizer
{
    public SanitizedImage StripMetadata(DetectedImageFormat format, byte[] content)
    {
        var contentType = format switch
        {
            DetectedImageFormat.Png => "image/png",
            DetectedImageFormat.Webp => "image/webp",
            _ => "image/jpeg",
        };
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(content));
        return new SanitizedImage(content, contentType, content.LongLength, sha256);
    }
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; set; } = true;
    public Guid OrganizationId { get; set; } = Guid.NewGuid();
    public string Subject { get; set; } = Guid.NewGuid().ToString();
    public string Email { get; set; } = "tester@acme.test";
    public string Language { get; set; } = "pl";
    public string IpAddress { get; set; } = "127.0.0.1";
    public IReadOnlyCollection<string> Roles { get; set; } = ["owner"];
}
