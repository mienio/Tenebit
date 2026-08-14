using Tenebit.Domain.Audits;
using Tenebit.Domain.Common;

namespace Tenebit.Tests;

public class AssetAuditCampaignTests
{
    private static AssetAuditCampaign CreateCampaign(DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        return new AssetAuditCampaign(Guid.NewGuid(), "Audyt Q1", "Opis", at.AddDays(14), "{}", "admin@acme.test", at);
    }

    [Fact]
    public void Start_TransitionsFromDraftToActive()
    {
        var campaign = CreateCampaign();

        campaign.Start(DateTimeOffset.UtcNow);

        Assert.Equal(AssetAuditCampaignStatus.Active, campaign.Status);
        Assert.NotNull(campaign.StartedAt);
    }

    [Fact]
    public void Start_ThrowsWhenNotDraft()
    {
        var campaign = CreateCampaign();
        campaign.Start(DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => campaign.Start(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Complete_IsIdempotent()
    {
        var campaign = CreateCampaign();
        campaign.Start(DateTimeOffset.UtcNow);

        campaign.Complete(DateTimeOffset.UtcNow, "admin");
        campaign.Complete(DateTimeOffset.UtcNow.AddMinutes(5), "someone-else");

        Assert.Equal(AssetAuditCampaignStatus.Completed, campaign.Status);
    }

    [Fact]
    public void Complete_ThrowsFromDraft()
    {
        var campaign = CreateCampaign();

        Assert.Throws<DomainException>(() => campaign.Complete(DateTimeOffset.UtcNow, "admin"));
    }

    [Fact]
    public void Cancel_ThrowsAfterCompleted()
    {
        var campaign = CreateCampaign();
        campaign.Start(DateTimeOffset.UtcNow);
        campaign.Complete(DateTimeOffset.UtcNow, "admin");

        Assert.Throws<DomainException>(() => campaign.Cancel(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Cancel_IsIdempotent()
    {
        var campaign = CreateCampaign();

        campaign.Cancel(DateTimeOffset.UtcNow);
        campaign.Cancel(DateTimeOffset.UtcNow);

        Assert.Equal(AssetAuditCampaignStatus.Cancelled, campaign.Status);
    }

    [Fact]
    public void ExtendDueDate_RejectsEarlierDate()
    {
        var campaign = CreateCampaign();

        Assert.Throws<DomainException>(() => campaign.ExtendDueDate(campaign.DueDate.AddDays(-1)));
    }

    [Fact]
    public void ExtendDueDate_RejectsSameDate()
    {
        var campaign = CreateCampaign();

        Assert.Throws<DomainException>(() => campaign.ExtendDueDate(campaign.DueDate));
    }

    [Fact]
    public void ExtendDueDate_AcceptsLaterDate()
    {
        var campaign = CreateCampaign();
        var newDueDate = campaign.DueDate.AddDays(7);

        campaign.ExtendDueDate(newDueDate);

        Assert.Equal(newDueDate, campaign.DueDate);
    }

    [Fact]
    public void RecomputeStatus_MovesToReviewingWhenAllParticipantsSubmitted()
    {
        var campaign = CreateCampaign();
        campaign.Start(DateTimeOffset.UtcNow);
        var p1 = new AssetAuditParticipant(campaign.OrganizationId, campaign.Id, Guid.NewGuid(), "a@acme.test");
        var p2 = new AssetAuditParticipant(campaign.OrganizationId, campaign.Id, Guid.NewGuid(), "b@acme.test");
        p1.Submit(DateTimeOffset.UtcNow);
        p2.Submit(DateTimeOffset.UtcNow);

        campaign.RecomputeStatus([p1, p2]);

        Assert.Equal(AssetAuditCampaignStatus.Reviewing, campaign.Status);
    }

    [Fact]
    public void RecomputeStatus_StaysActiveWhenNotAllParticipantsSubmitted()
    {
        var campaign = CreateCampaign();
        campaign.Start(DateTimeOffset.UtcNow);
        var p1 = new AssetAuditParticipant(campaign.OrganizationId, campaign.Id, Guid.NewGuid(), "a@acme.test");
        var p2 = new AssetAuditParticipant(campaign.OrganizationId, campaign.Id, Guid.NewGuid(), "b@acme.test");
        p1.Submit(DateTimeOffset.UtcNow);

        campaign.RecomputeStatus([p1, p2]);

        Assert.Equal(AssetAuditCampaignStatus.Active, campaign.Status);
    }
}

public class AssetAuditParticipantTests
{
    private static AssetAuditParticipant CreateParticipant() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "employee@acme.test");

    [Fact]
    public void Submit_ThrowsWhenAlreadySubmitted()
    {
        var participant = CreateParticipant();
        participant.Submit(DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => participant.Submit(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Reopen_WorksOnlyFromSubmitted()
    {
        var participant = CreateParticipant();

        Assert.Throws<DomainException>(() => participant.Reopen(DateTimeOffset.UtcNow));

        participant.Submit(DateTimeOffset.UtcNow);
        participant.Reopen(DateTimeOffset.UtcNow);

        Assert.Equal(AssetAuditParticipantStatus.InProgress, participant.Status);
    }

    [Fact]
    public void SetToken_ThenRevoke()
    {
        var participant = CreateParticipant();

        participant.SetToken("hash", DateTimeOffset.UtcNow.AddDays(7));
        Assert.Equal("hash", participant.TokenHash);

        participant.RevokeToken(DateTimeOffset.UtcNow);
        Assert.NotNull(participant.TokenRevokedAt);
    }
}

public class AssetAuditItemTests
{
    private static AssetAuditItem CreateItem() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Biuro 1");

    [Theory]
    [InlineData(AssetAuditResolution.AssetMarkedLost)]
    [InlineData(AssetAuditResolution.AssetMarkedDamaged)]
    [InlineData(AssetAuditResolution.OwnershipCorrected)]
    public void Resolve_RequiresNotesForOwnershipOrStatusChanges(AssetAuditResolution resolution)
    {
        var item = CreateItem();

        Assert.Throws<DomainException>(() => item.Resolve(resolution, null, "admin", DateTimeOffset.UtcNow));

        item.Resolve(resolution, "Uzasadnienie", "admin", DateTimeOffset.UtcNow);
        Assert.Equal(resolution, item.Resolution);
    }

    [Theory]
    [InlineData(AssetAuditResolution.Accepted)]
    [InlineData(AssetAuditResolution.Dismissed)]
    public void Resolve_DoesNotRequireNotes(AssetAuditResolution resolution)
    {
        var item = CreateItem();

        item.Resolve(resolution, null, "admin", DateTimeOffset.UtcNow);

        Assert.Equal(resolution, item.Resolution);
        Assert.Null(item.ResolutionNotes);
    }

    [Fact]
    public void Resolve_ThrowsOnSecondCall()
    {
        var item = CreateItem();
        item.Resolve(AssetAuditResolution.Accepted, null, "admin", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => item.Resolve(AssetAuditResolution.Dismissed, null, "admin", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RecordResponse_UpdatesResponseAndComment()
    {
        var item = CreateItem();

        item.RecordResponse(AssetAuditResponse.Damaged, "Rysa na obudowie", DateTimeOffset.UtcNow);

        Assert.Equal(AssetAuditResponse.Damaged, item.Response);
        Assert.Equal("Rysa na obudowie", item.Comment);
        Assert.NotNull(item.RespondedAt);
    }
}
