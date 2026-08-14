using Tenebit.Domain.Common;
using Tenebit.Domain.Offboarding;

namespace Tenebit.Tests;

public class OffboardingItemTests
{
    private static OffboardingItem CreateItem(bool required = true) =>
        new(Guid.NewGuid(), Guid.NewGuid(), OffboardingItemType.AssetReturn, "Laptop", required, Guid.NewGuid(), null, null, OffboardingItemAutomationMode.Manual, 0);

    [Fact]
    public void MarkReceived_SetsReceivedStatus()
    {
        var item = CreateItem();
        var at = DateTimeOffset.UtcNow;

        item.MarkReceived(at, "operator@acme.test");

        Assert.Equal(OffboardingItemStatus.Received, item.Status);
        Assert.Equal(at, item.ReceivedAt);
        Assert.False(item.IsResolved);
    }

    [Fact]
    public void CompleteInspection_TransitionsToReturnedTerminalState()
    {
        var item = CreateItem();
        var at = DateTimeOffset.UtcNow;
        item.MarkReceived(at, "operator@acme.test");

        item.CompleteInspection(at, "operator@acme.test");

        Assert.Equal(OffboardingItemStatus.Returned, item.Status);
        Assert.True(item.IsResolved);
    }

    [Theory]
    [InlineData(OffboardingItemStatus.Missing)]
    [InlineData(OffboardingItemStatus.Damaged)]
    [InlineData(OffboardingItemStatus.Retained)]
    public void Resolve_SetsTerminalStatusWithNotesAndActor(OffboardingItemStatus status)
    {
        var item = CreateItem();
        var at = DateTimeOffset.UtcNow;

        item.Resolve(status, "Uzasadnienie", "admin@acme.test", at);

        Assert.Equal(status, item.Status);
        Assert.True(item.IsResolved);
        Assert.Equal("Uzasadnienie", item.ResolutionNotes);
    }

    [Fact]
    public void Resolve_ThrowsWhenNotesMissing()
    {
        var item = CreateItem();

        Assert.Throws<DomainException>(() => item.Resolve(OffboardingItemStatus.Missing, "", "admin@acme.test", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Resolve_ThrowsWhenActorMissing()
    {
        var item = CreateItem();

        Assert.Throws<DomainException>(() => item.Resolve(OffboardingItemStatus.Damaged, "Uszkodzony ekran", "", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Resolve_ThrowsForNonTerminalStatus()
    {
        var item = CreateItem();

        Assert.Throws<DomainException>(() => item.Resolve(OffboardingItemStatus.Received, "notatka", "admin@acme.test", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Waive_ThrowsWhenReasonIsEmpty()
    {
        var item = CreateItem();

        Assert.Throws<DomainException>(() => item.Waive("", "admin@acme.test", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Waive_SetsWaivedTerminalStatus()
    {
        var item = CreateItem();
        var at = DateTimeOffset.UtcNow;

        item.Waive("Organizacja odstępuje od zwrotu", "admin@acme.test", at);

        Assert.Equal(OffboardingItemStatus.Waived, item.Status);
        Assert.True(item.IsResolved);
        Assert.Equal("admin@acme.test", item.CompletedBy);
    }

    [Fact]
    public void Resolve_ThrowsWhenItemAlreadyResolved()
    {
        var item = CreateItem();
        item.Waive("powód", "admin@acme.test", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => item.Resolve(OffboardingItemStatus.Missing, "notatka", "admin@acme.test", DateTimeOffset.UtcNow));
    }
}
