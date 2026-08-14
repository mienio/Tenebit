using Tenebit.Domain.Common;
using Tenebit.Domain.Offboarding;

namespace Tenebit.Tests;

public class OffboardingCaseTests
{
    private static OffboardingCase CreateCase(DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        return new OffboardingCase(Guid.NewGuid(), Guid.NewGuid(), at.AddDays(14), at.AddDays(21),
            "Magazyn główny", "Notatka", null, true, true, true, "admin@acme.test", at);
    }

    private static OffboardingItem CreateRequiredItem(Guid caseId, Guid organizationId) =>
        new(organizationId, caseId, OffboardingItemType.AssetReturn, "Laptop", true, Guid.NewGuid(), null, null, OffboardingItemAutomationMode.Manual, 0);

    [Fact]
    public void Start_TransitionsFromDraftToActive()
    {
        var offboardingCase = CreateCase();

        offboardingCase.Start(DateTimeOffset.UtcNow);

        Assert.Equal(OffboardingCaseStatus.Active, offboardingCase.Status);
        Assert.NotNull(offboardingCase.StartedAt);
    }

    [Fact]
    public void Start_ThrowsWhenNotDraft()
    {
        var offboardingCase = CreateCase();
        offboardingCase.Start(DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => offboardingCase.Start(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Complete_ThrowsWhenRequiredItemIsStillOpen()
    {
        var now = DateTimeOffset.UtcNow;
        var offboardingCase = CreateCase(now);
        offboardingCase.Start(now);
        offboardingCase.MarkPersonDeactivated(now);
        var openItem = CreateRequiredItem(offboardingCase.Id, offboardingCase.OrganizationId);

        offboardingCase.RecomputeStatus([openItem], now);

        Assert.Throws<DomainException>(() => offboardingCase.Complete(now, "admin@acme.test", "PROT-1"));
    }

    [Fact]
    public void Complete_ThrowsWhenPersonNotDeactivatedDespiteResolvedItems()
    {
        var now = DateTimeOffset.UtcNow;
        var offboardingCase = CreateCase(now);
        offboardingCase.Start(now);
        var item = CreateRequiredItem(offboardingCase.Id, offboardingCase.OrganizationId);
        item.MarkReceived(now, "admin@acme.test");
        item.CompleteInspection(now, "admin@acme.test");

        offboardingCase.RecomputeStatus([item], now);

        Assert.Equal(OffboardingCaseStatus.ReadyToClose, offboardingCase.Status);
        Assert.Throws<DomainException>(() => offboardingCase.Complete(now, "admin@acme.test", "PROT-1"));
        Assert.Equal(OffboardingCaseStatus.ReadyToClose, offboardingCase.Status);
    }

    [Fact]
    public void Cancel_SucceedsBeforePersonDeactivation()
    {
        var offboardingCase = CreateCase();
        offboardingCase.Start(DateTimeOffset.UtcNow);

        offboardingCase.Cancel(DateTimeOffset.UtcNow, "Rezygnacja z procesu");

        Assert.Equal(OffboardingCaseStatus.Cancelled, offboardingCase.Status);
        Assert.Equal("Rezygnacja z procesu", offboardingCase.CancellationReason);
    }

    [Fact]
    public void Cancel_IsBlockedAfterPersonDeactivation()
    {
        var now = DateTimeOffset.UtcNow;
        var offboardingCase = CreateCase(now);
        offboardingCase.Start(now);
        offboardingCase.MarkPersonDeactivated(now);

        Assert.Throws<DomainException>(() => offboardingCase.Cancel(now, "Za późno"));
        Assert.Equal(OffboardingCaseStatus.Active, offboardingCase.Status);
    }

    [Fact]
    public void Complete_IsIdempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var offboardingCase = CreateCase(now);
        offboardingCase.Start(now);
        var item = CreateRequiredItem(offboardingCase.Id, offboardingCase.OrganizationId);
        item.MarkReceived(now, "admin@acme.test");
        item.CompleteInspection(now, "admin@acme.test");
        offboardingCase.MarkPersonDeactivated(now);
        offboardingCase.RecomputeStatus([item], now);

        offboardingCase.Complete(now, "admin@acme.test", "PROT-1");
        var completedAtFirstCall = offboardingCase.CompletedAt;

        offboardingCase.Complete(now.AddMinutes(5), "someone-else@acme.test", "PROT-2");

        Assert.Equal(OffboardingCaseStatus.Completed, offboardingCase.Status);
        Assert.Equal(completedAtFirstCall, offboardingCase.CompletedAt);
        Assert.Equal("admin@acme.test", offboardingCase.CompletedBy);
        Assert.Equal("PROT-1", offboardingCase.FinalProtocolNumber);
    }

    [Fact]
    public void Complete_SucceedsWhenReadyAndPersonDeactivated()
    {
        var now = DateTimeOffset.UtcNow;
        var offboardingCase = CreateCase(now);
        offboardingCase.Start(now);
        var item = CreateRequiredItem(offboardingCase.Id, offboardingCase.OrganizationId);
        item.MarkReceived(now, "admin@acme.test");
        item.CompleteInspection(now, "admin@acme.test");
        offboardingCase.MarkPersonDeactivated(now);
        offboardingCase.RecomputeStatus([item], now);

        offboardingCase.Complete(now, "admin@acme.test", "PROT-1");

        Assert.Equal(OffboardingCaseStatus.Completed, offboardingCase.Status);
        Assert.NotNull(offboardingCase.PublicTokenRevokedAt);
    }

    [Fact]
    public void RestoreEmployment_ThrowsWhenPersonNotDeactivated()
    {
        var offboardingCase = CreateCase();
        offboardingCase.Start(DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => offboardingCase.RestoreEmployment(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RestoreEmployment_CancelsCaseAfterDeactivation()
    {
        var now = DateTimeOffset.UtcNow;
        var offboardingCase = CreateCase(now);
        offboardingCase.Start(now);
        offboardingCase.MarkPersonDeactivated(now);

        offboardingCase.RestoreEmployment(now);

        Assert.Equal(OffboardingCaseStatus.Cancelled, offboardingCase.Status);
        Assert.Equal("Przywrócenie zatrudnienia", offboardingCase.CancellationReason);
    }
}
