using Tenebit.Domain.Identity;

namespace Tenebit.Tests;

public sealed class RefreshTokenFamilyTests
{
    [Fact]
    public void SuccessorKeepsFamilyAndReferencesParent()
    {
        var userId = Guid.NewGuid();
        var root = new RefreshToken(userId, "root-hash", DateTimeOffset.UtcNow.AddDays(1));
        var child = new RefreshToken(userId, "child-hash", DateTimeOffset.UtcNow.AddDays(1), root.FamilyId, root.Id);
        root.MarkRotated(child.Id, DateTimeOffset.UtcNow);
        Assert.Equal(root.Id, root.FamilyId);
        Assert.Equal(root.FamilyId, child.FamilyId);
        Assert.Equal(root.Id, child.ParentTokenId);
        Assert.Equal(child.Id, root.ReplacedByTokenId);
        Assert.Equal("rotated", root.RevocationReason);
    }
}
