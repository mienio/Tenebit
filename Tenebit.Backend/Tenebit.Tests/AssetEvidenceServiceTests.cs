using Tenebit.Application.Common;
using Tenebit.Application.Evidence;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Evidence;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AssetEvidenceServiceTests
{
    private static byte[] JpegBytes(int size = 32) => Fill([0xFF, 0xD8, 0xFF], size);
    private static byte[] PngBytes(int size = 32) => Fill([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], size);
    private static byte[] WebpBytes(int size = 32) => Fill([(byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P'], size);

    private static byte[] Fill(byte[] prefix, int size)
    {
        var bytes = new byte[Math.Max(size, prefix.Length)];
        prefix.CopyTo(bytes, 0);
        return bytes;
    }

    private static (AssetEvidenceService Service, FakeCurrentUser User, InMemoryAssetEvidenceRepository Evidence, InMemoryAssetRepository Assets, InMemoryAssignmentRepository Assignments, InMemoryActivityLogRepository Activity, FakeClock Clock) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var evidence = new InMemoryAssetEvidenceRepository();
        var assets = new InMemoryAssetRepository();
        var assignments = new InMemoryAssignmentRepository();
        var activity = new InMemoryActivityLogRepository();
        var clock = new FakeClock();

        var service = new AssetEvidenceService(evidence, assets, assignments, new FakeImageSanitizer(), activity, currentUser, clock, new FakeUnitOfWork());
        return (service, currentUser, evidence, assets, assignments, activity, clock);
    }

    private static Asset AddAsset(FakeCurrentUser user, InMemoryAssetRepository assets)
    {
        var asset = new Asset(user.OrganizationId, Guid.NewGuid(), "Laptop", $"AT-{Guid.NewGuid():N}"[..8]);
        assets.Add(asset);
        return asset;
    }

    [Fact]
    public async Task UploadAsync_SucceedsForValidJpeg()
    {
        var (service, user, _, assets, _, activity, _) = CreateService();
        var asset = AddAsset(user, assets);

        var result = await service.UploadAsync(asset.Id, new UploadAssetEvidenceRequest(EvidencePhase.Issue, null, null), "photo.jpg", "image/jpeg", JpegBytes(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(activity.Logs, x => x.Action == "asset_evidence.uploaded");
    }

    [Fact]
    public async Task UploadAsync_SucceedsForValidPngAndWebp()
    {
        var (service, user, _, assets, _, _, _) = CreateService();
        var asset = AddAsset(user, assets);

        var pngResult = await service.UploadAsync(asset.Id, new UploadAssetEvidenceRequest(EvidencePhase.Issue, null, null), "photo.png", "image/png", PngBytes(), CancellationToken.None);
        var webpResult = await service.UploadAsync(asset.Id, new UploadAssetEvidenceRequest(EvidencePhase.Return, null, null), "photo.webp", "image/webp", WebpBytes(), CancellationToken.None);

        Assert.True(pngResult.IsSuccess);
        Assert.True(webpResult.IsSuccess);
    }

    [Fact]
    public async Task UploadAsync_RejectsDisallowedDeclaredContentType()
    {
        var (service, user, _, assets, _, _, _) = CreateService();
        var asset = AddAsset(user, assets);

        var result = await service.UploadAsync(asset.Id, new UploadAssetEvidenceRequest(EvidencePhase.Issue, null, null), "file.pdf", "application/pdf", JpegBytes(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("EVIDENCE_UNSUPPORTED_IMAGE_FORMAT", result.Error!.Code);
    }

    [Fact]
    public async Task UploadAsync_RejectsSpoofedHeaderWithInvalidMagicBytes()
    {
        var (service, user, _, assets, _, _, _) = CreateService();
        var asset = AddAsset(user, assets);
        var bogus = new byte[32]; // all zeros, no valid image signature

        var result = await service.UploadAsync(asset.Id, new UploadAssetEvidenceRequest(EvidencePhase.Issue, null, null), "file.jpg", "image/jpeg", bogus, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("EVIDENCE_INVALID_IMAGE_FILE", result.Error!.Code);
    }

    [Fact]
    public async Task UploadAsync_RejectsOversizedContent()
    {
        var (service, user, _, assets, _, _, _) = CreateService();
        var asset = AddAsset(user, assets);
        var big = JpegBytes(5 * 1024 * 1024 + 1);

        var result = await service.UploadAsync(asset.Id, new UploadAssetEvidenceRequest(EvidencePhase.Issue, null, null), "photo.jpg", "image/jpeg", big, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("EVIDENCE_PHOTO_TOO_LARGE", result.Error!.Code);
    }

    [Fact]
    public async Task UploadAsync_RejectsSixthUploadForSameAssetAndPhase()
    {
        var (service, user, _, assets, _, _, _) = CreateService();
        var asset = AddAsset(user, assets);

        for (var i = 0; i < 5; i++)
        {
            var ok = await service.UploadAsync(asset.Id, new UploadAssetEvidenceRequest(EvidencePhase.Issue, null, null), $"photo{i}.jpg", "image/jpeg", JpegBytes(), CancellationToken.None);
            Assert.True(ok.IsSuccess);
        }

        var result = await service.UploadAsync(asset.Id, new UploadAssetEvidenceRequest(EvidencePhase.Issue, null, null), "photo6.jpg", "image/jpeg", JpegBytes(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("EVIDENCE_PHOTO_LIMIT_REACHED", result.Error!.Code);
    }

    [Fact]
    public async Task UploadAsync_ReturnsNotFoundForAssetInDifferentOrganization()
    {
        var (service, _, _, assets, _, _, _) = CreateService();
        var otherAsset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "Laptop", "AT-OTHER1");
        assets.Add(otherAsset);

        var result = await service.UploadAsync(otherAsset.Id, new UploadAssetEvidenceRequest(EvidencePhase.Issue, null, null), "photo.jpg", "image/jpeg", JpegBytes(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("ASSET_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task UploadAsync_ReturnsNotFoundWhenAssignmentDoesNotBelongToOrganization()
    {
        var (service, user, _, assets, assignments, _, _) = CreateService();
        var asset = AddAsset(user, assets);
        var foreignAssignment = new Assignment(Guid.NewGuid(), Guid.NewGuid(), "PROT-1", DateTimeOffset.UtcNow, null, null, "system");
        assignments.Add(foreignAssignment);

        var result = await service.UploadAsync(asset.Id, new UploadAssetEvidenceRequest(EvidencePhase.Issue, foreignAssignment.Id, null), "photo.jpg", "image/jpeg", JpegBytes(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("ASSIGNMENT_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task UploadAsync_ForbiddenForNonPrivilegedRole()
    {
        var (service, user, _, assets, _, _, _) = CreateService();
        var asset = AddAsset(user, assets);
        user.Roles = ["employee"];

        var result = await service.UploadAsync(asset.Id, new UploadAssetEvidenceRequest(EvidencePhase.Issue, null, null), "photo.jpg", "image/jpeg", JpegBytes(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FORBIDDEN", result.Error!.Code);
    }

    [Fact]
    public async Task LockAsync_ForbiddenForNonPrivilegedRole()
    {
        var (service, user, evidence, assets, _, _, _) = CreateService();
        var asset = AddAsset(user, assets);
        var item = new AssetEvidence(user.OrganizationId, asset.Id, null, EvidencePhase.Issue, "photo.jpg", "image/jpeg", JpegBytes(), "a".PadLeft(64, '0'), null, user.Subject, EvidenceUploadSource.AuthenticatedUser, DateTimeOffset.UtcNow);
        evidence.Add(item);
        user.Roles = ["employee"];

        var result = await service.LockAsync(item.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FORBIDDEN", result.Error!.Code);
    }

    [Fact]
    public async Task DeleteAsync_ForbiddenForNonPrivilegedRole()
    {
        var (service, user, evidence, assets, _, _, _) = CreateService();
        var asset = AddAsset(user, assets);
        var item = new AssetEvidence(user.OrganizationId, asset.Id, null, EvidencePhase.Issue, "photo.jpg", "image/jpeg", JpegBytes(), "a".PadLeft(64, '0'), null, user.Subject, EvidenceUploadSource.AuthenticatedUser, DateTimeOffset.UtcNow);
        evidence.Add(item);
        user.Roles = ["employee"];

        var result = await service.DeleteAsync(item.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FORBIDDEN", result.Error!.Code);
    }

    [Fact]
    public async Task LockAsync_SetsLockedAtAndIsIdempotent()
    {
        var (service, user, evidence, assets, _, activity, clock) = CreateService();
        var asset = AddAsset(user, assets);
        var item = new AssetEvidence(user.OrganizationId, asset.Id, null, EvidencePhase.Issue, "photo.jpg", "image/jpeg", JpegBytes(), "a".PadLeft(64, '0'), null, user.Subject, EvidenceUploadSource.AuthenticatedUser, DateTimeOffset.UtcNow);
        evidence.Add(item);

        var first = await service.LockAsync(item.Id, CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.NotNull(first.Value!.LockedAt);
        var lockedAt = first.Value!.LockedAt;

        clock.UtcNow = clock.UtcNow.AddMinutes(5);
        var second = await service.LockAsync(item.Id, CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Equal(lockedAt, second.Value!.LockedAt);
        Assert.Equal(1, activity.Logs.Count(x => x.Action == "asset_evidence.locked"));
    }

    [Fact]
    public async Task DeleteAsync_SucceedsForUnlockedRecord()
    {
        var (service, user, evidence, assets, _, _, _) = CreateService();
        var asset = AddAsset(user, assets);
        var item = new AssetEvidence(user.OrganizationId, asset.Id, null, EvidencePhase.Issue, "photo.jpg", "image/jpeg", JpegBytes(), "a".PadLeft(64, '0'), null, user.Subject, EvidenceUploadSource.AuthenticatedUser, DateTimeOffset.UtcNow);
        evidence.Add(item);

        var result = await service.DeleteAsync(item.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(evidence.Items);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsValidationErrorForLockedRecordAndDoesNotRemoveIt()
    {
        var (service, user, evidence, assets, _, _, clock) = CreateService();
        var asset = AddAsset(user, assets);
        var item = new AssetEvidence(user.OrganizationId, asset.Id, null, EvidencePhase.Issue, "photo.jpg", "image/jpeg", JpegBytes(), "a".PadLeft(64, '0'), null, user.Subject, EvidenceUploadSource.AuthenticatedUser, DateTimeOffset.UtcNow);
        item.Lock(clock.UtcNow);
        evidence.Add(item);

        var result = await service.DeleteAsync(item.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("EVIDENCE_LOCKED_CANNOT_DELETE", result.Error!.Code);
        Assert.Single(evidence.Items);
    }

    [Fact]
    public async Task SetLegalHoldAsync_ForbiddenForNonPrivilegedRole()
    {
        var (service, user, evidence, assets, _, _, _) = CreateService();
        var asset = AddAsset(user, assets);
        var item = new AssetEvidence(user.OrganizationId, asset.Id, null, EvidencePhase.Issue, "photo.jpg", "image/jpeg", JpegBytes(), "a".PadLeft(64, '0'), null, user.Subject, EvidenceUploadSource.AuthenticatedUser, DateTimeOffset.UtcNow);
        evidence.Add(item);
        user.Roles = ["employee"];

        var result = await service.SetLegalHoldAsync(item.Id, true, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FORBIDDEN", result.Error!.Code);
    }

    [Fact]
    public async Task SetLegalHoldAsync_SetsAndClearsLegalHoldAndIsIdempotent()
    {
        var (service, user, evidence, assets, _, activity, _) = CreateService();
        var asset = AddAsset(user, assets);
        var item = new AssetEvidence(user.OrganizationId, asset.Id, null, EvidencePhase.Issue, "photo.jpg", "image/jpeg", JpegBytes(), "a".PadLeft(64, '0'), null, user.Subject, EvidenceUploadSource.AuthenticatedUser, DateTimeOffset.UtcNow);
        evidence.Add(item);

        var first = await service.SetLegalHoldAsync(item.Id, true, CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.True(first.Value!.LegalHold);

        var second = await service.SetLegalHoldAsync(item.Id, true, CancellationToken.None);
        Assert.True(second.IsSuccess);
        Assert.Equal(1, activity.Logs.Count(x => x.Action == "asset_evidence.legal_hold_set"));

        var cleared = await service.SetLegalHoldAsync(item.Id, false, CancellationToken.None);
        Assert.True(cleared.IsSuccess);
        Assert.False(cleared.Value!.LegalHold);
        Assert.Equal(1, activity.Logs.Count(x => x.Action == "asset_evidence.legal_hold_cleared"));
    }

    [Fact]
    public async Task ListByAssetAsync_OnlyReturnsOrgScopedEvidence()
    {
        var (service, user, evidence, assets, _, _, _) = CreateService();
        var asset = AddAsset(user, assets);
        var ownItem = new AssetEvidence(user.OrganizationId, asset.Id, null, EvidencePhase.Issue, "photo.jpg", "image/jpeg", JpegBytes(), "a".PadLeft(64, '0'), null, user.Subject, EvidenceUploadSource.AuthenticatedUser, DateTimeOffset.UtcNow);
        var foreignAsset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "Laptop", "AT-FOREIGN1");
        var foreignItem = new AssetEvidence(foreignAsset.OrganizationId, foreignAsset.Id, null, EvidencePhase.Issue, "photo.jpg", "image/jpeg", JpegBytes(), "b".PadLeft(64, '0'), null, "someone", EvidenceUploadSource.AuthenticatedUser, DateTimeOffset.UtcNow);
        evidence.Add(ownItem);
        evidence.Add(foreignItem);

        var result = await service.ListByAssetAsync(asset.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal(ownItem.Id, result.Value![0].Id);
    }
}
