using Tenebit.Domain.Evidence;

namespace Tenebit.Tests;

public class AssetEvidenceRedactionTests
{
    private static byte[] Content(int size = 32)
    {
        var bytes = new byte[size];
        bytes[0] = 0xFF;
        return bytes;
    }

    private static AssetEvidence CreateEvidence(DateTimeOffset uploadedAt) =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, EvidencePhase.Issue, "photo.jpg", "image/jpeg", Content(), "a".PadLeft(64, '0'), null, "system", EvidenceUploadSource.AuthenticatedUser, uploadedAt);

    [Fact]
    public void Redact_RemovesContentAndSizeButKeepsAuditTrail()
    {
        var item = CreateEvidence(DateTimeOffset.UtcNow.AddYears(-1));

        var redacted = item.Redact(DateTimeOffset.UtcNow);

        Assert.True(redacted);
        Assert.Empty(item.Content);
        Assert.Equal(0, item.SizeBytes);
        Assert.NotNull(item.RedactedAt);
        Assert.Equal("photo.jpg", item.FileName);
        Assert.Equal("a".PadLeft(64, '0'), item.Sha256);
    }

    [Fact]
    public void Redact_IsIdempotent()
    {
        var item = CreateEvidence(DateTimeOffset.UtcNow.AddYears(-1));
        var firstRedactedAt = DateTimeOffset.UtcNow;
        item.Redact(firstRedactedAt);

        var secondCallResult = item.Redact(DateTimeOffset.UtcNow.AddMinutes(5));

        Assert.False(secondCallResult);
        Assert.Equal(firstRedactedAt, item.RedactedAt);
    }

    [Fact]
    public void Redact_SkipsRecordsUnderLegalHold()
    {
        var item = CreateEvidence(DateTimeOffset.UtcNow.AddYears(-1));
        item.SetLegalHold(true);

        var result = item.Redact(DateTimeOffset.UtcNow);

        Assert.False(result);
        Assert.NotEmpty(item.Content);
        Assert.Null(item.RedactedAt);
    }

    [Fact]
    public void SetLegalHold_CanBeToggled()
    {
        var item = CreateEvidence(DateTimeOffset.UtcNow);

        item.SetLegalHold(true);
        Assert.True(item.LegalHold);

        item.SetLegalHold(false);
        Assert.False(item.LegalHold);
    }
}
