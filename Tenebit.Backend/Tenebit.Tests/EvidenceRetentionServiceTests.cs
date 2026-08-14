using Tenebit.Application.Evidence;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Organizations;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class EvidenceRetentionServiceTests
{
    private static (EvidenceRetentionService Service, FakeClock Clock, InMemoryOrganizationRepository Organizations, InMemoryAssetEvidenceRepository Evidence, InMemoryActivityLogRepository Activity) CreateService()
    {
        var organizations = new InMemoryOrganizationRepository();
        var evidence = new InMemoryAssetEvidenceRepository();
        var activity = new InMemoryActivityLogRepository();
        var clock = new FakeClock();
        var service = new EvidenceRetentionService(organizations, evidence, activity, clock, new FakeUnitOfWork());
        return (service, clock, organizations, evidence, activity);
    }

    private static Organization CreateOrganization(int? retentionMonths)
    {
        var org = new Organization("Acme", "PL", "pl", "PLN", "Europe/Warsaw");
        if (retentionMonths.HasValue)
        {
            org.UpdatePrivacySettings(PublicIpCaptureMode.Off, null, retentionMonths, null, null);
        }
        return org;
    }

    private static AssetEvidence CreateEvidence(Guid organizationId, DateTimeOffset uploadedAt)
    {
        var bytes = new byte[32];
        bytes[0] = 0xFF;
        return new AssetEvidence(organizationId, Guid.NewGuid(), null, EvidencePhase.Issue, "photo.jpg", "image/jpeg", bytes, "a".PadLeft(64, '0'), null, "system", EvidenceUploadSource.AuthenticatedUser, uploadedAt);
    }

    [Fact]
    public async Task RunAsync_RedactsEvidenceOlderThanRetentionPeriod()
    {
        var (service, clock, organizations, evidence, _) = CreateService();
        var org = CreateOrganization(12);
        organizations.Add(org);
        var item = CreateEvidence(org.Id, clock.UtcNow.AddMonths(-13));
        evidence.Add(item);

        await service.RunAsync(CancellationToken.None);

        Assert.NotNull(item.RedactedAt);
        Assert.Empty(item.Content);
    }

    [Fact]
    public async Task RunAsync_DoesNotRedactEvidenceWithinRetentionPeriod()
    {
        var (service, clock, organizations, evidence, _) = CreateService();
        var org = CreateOrganization(12);
        organizations.Add(org);
        var item = CreateEvidence(org.Id, clock.UtcNow.AddMonths(-1));
        evidence.Add(item);

        await service.RunAsync(CancellationToken.None);

        Assert.Null(item.RedactedAt);
        Assert.NotEmpty(item.Content);
    }

    [Fact]
    public async Task RunAsync_SkipsRecordsUnderLegalHold()
    {
        var (service, clock, organizations, evidence, _) = CreateService();
        var org = CreateOrganization(12);
        organizations.Add(org);
        var item = CreateEvidence(org.Id, clock.UtcNow.AddMonths(-13));
        item.SetLegalHold(true);
        evidence.Add(item);

        await service.RunAsync(CancellationToken.None);

        Assert.Null(item.RedactedAt);
        Assert.NotEmpty(item.Content);
    }

    [Fact]
    public async Task RunAsync_DoesNothingWhenOrganizationHasNoRetentionConfigured()
    {
        var (service, clock, organizations, evidence, _) = CreateService();
        var org = CreateOrganization(null);
        organizations.Add(org);
        var item = CreateEvidence(org.Id, clock.UtcNow.AddYears(-5));
        evidence.Add(item);

        await service.RunAsync(CancellationToken.None);

        Assert.Null(item.RedactedAt);
    }

    [Fact]
    public async Task RunAsync_WritesActivityLogEntryForRedaction()
    {
        var (service, clock, organizations, evidence, activity) = CreateService();
        var org = CreateOrganization(12);
        organizations.Add(org);
        var item = CreateEvidence(org.Id, clock.UtcNow.AddMonths(-13));
        evidence.Add(item);

        await service.RunAsync(CancellationToken.None);

        Assert.Single(activity.Logs, l => l.Action == "asset_evidence.redacted" && l.EntityId == item.Id && l.ActorSubject == "system");
    }

    [Fact]
    public async Task RunAsync_IsIdempotentAcrossTwoRunsAndDoesNotDuplicateActivityLog()
    {
        var (service, clock, organizations, evidence, activity) = CreateService();
        var org = CreateOrganization(12);
        organizations.Add(org);
        var item = CreateEvidence(org.Id, clock.UtcNow.AddMonths(-13));
        evidence.Add(item);

        await service.RunAsync(CancellationToken.None);
        var logCountAfterFirstRun = activity.Logs.Count(l => l.Action == "asset_evidence.redacted");
        await service.RunAsync(CancellationToken.None);

        Assert.Equal(1, logCountAfterFirstRun);
        Assert.Equal(logCountAfterFirstRun, activity.Logs.Count(l => l.Action == "asset_evidence.redacted"));
    }

    [Fact]
    public async Task RunAsync_IsTenantIsolated()
    {
        var (service, clock, organizations, evidence, _) = CreateService();
        var orgA = CreateOrganization(12);
        var orgB = CreateOrganization(12);
        organizations.Add(orgA);
        organizations.Add(orgB);
        var itemA = CreateEvidence(orgA.Id, clock.UtcNow.AddMonths(-13));
        var itemB = CreateEvidence(orgB.Id, clock.UtcNow.AddMonths(-13));
        evidence.Add(itemA);
        evidence.Add(itemB);

        await service.RunAsync(CancellationToken.None);

        Assert.NotNull(itemA.RedactedAt);
        Assert.NotNull(itemB.RedactedAt);
    }
}
