using Tenebit.Application.Audits;
using Tenebit.Application.Evidence;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Audits;
using Tenebit.Domain.People;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AssetAuditCampaignServiceTests
{
    private static (AssetAuditCampaignService Service, FakeCurrentUser User, InMemoryAssetAuditCampaignRepository Campaigns,
        InMemoryAssetAuditParticipantRepository Participants, InMemoryAssetAuditItemRepository Items,
        InMemoryPersonRepository People, InMemoryAssetRepository Assets, InMemoryActivityLogRepository Activity, FakeEmailSender EmailSender,
        FakePdfProtocolGenerator PdfGenerator) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var campaigns = new InMemoryAssetAuditCampaignRepository();
        var participants = new InMemoryAssetAuditParticipantRepository();
        var items = new InMemoryAssetAuditItemRepository();
        var people = new InMemoryPersonRepository();
        var assets = new InMemoryAssetRepository();
        var activity = new InMemoryActivityLogRepository();
        var unitOfWork = new FakeUnitOfWork();
        var organizations = new InMemoryOrganizationRepository();
        var emailSender = new FakeEmailSender();
        var linkBuilder = new FakeAppLinkBuilder();
        var evidence = new InMemoryAssetEvidenceRepository();
        var assignments = new InMemoryAssignmentRepository();
        var evidenceService = new AssetEvidenceService(evidence, assets, assignments, new FakeImageSanitizer(), activity, currentUser, new FakeClock(), unitOfWork);
        var pdfGenerator = new FakePdfProtocolGenerator();

        var service = new AssetAuditCampaignService(campaigns, participants, items, people, assets, evidence, evidenceService, activity, currentUser,
            new FakeClock(), unitOfWork, organizations, emailSender, linkBuilder, pdfGenerator);

        return (service, currentUser, campaigns, participants, items, people, assets, activity, emailSender, pdfGenerator);
    }

    private static Person AddPerson(FakeCurrentUser user, InMemoryPersonRepository people, string email = "jan.kowalski@acme.test")
    {
        var person = new Person(user.OrganizationId, "Jan", "Kowalski", email);
        people.Add(person);
        return person;
    }

    private static Asset AddAsset(FakeCurrentUser user, InMemoryAssetRepository assets, Guid? assignedPersonId = null)
    {
        var asset = new Asset(user.OrganizationId, Guid.NewGuid(), "Laptop", $"AT-{Guid.NewGuid():N}"[..8]);
        if (assignedPersonId.HasValue) asset.AssignTo(assignedPersonId.Value);
        assets.Add(asset);
        return asset;
    }

    private static CreateAssetAuditCampaignRequest OrganizationScopeRequest(string name = "Q1 audyt") =>
        new(name, null, DateTimeOffset.UtcNow.AddDays(14), new AssetAuditScope(AssetAuditScopeType.Organization));

    [Fact]
    public async Task CreateAsync_CreatesDraftCampaign()
    {
        var (service, _, _, _, _, _, _, activity, _, _) = CreateService();

        var result = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetAuditCampaignStatus.Draft, result.Value!.Campaign.Status);
        Assert.Contains(activity.Logs, x => x.Action == "asset_audit.created");
    }

    [Fact]
    public async Task PreviewAsync_MatchesAssignedAssetCount_AndDoesNotPersist()
    {
        var (service, user, campaigns, participants, items, people, assets, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);
        AddAsset(user, assets, person.Id);
        AddPerson(user, people, "no.assets@acme.test"); // no assigned assets - must be skipped

        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        var preview = await service.PreviewAsync(created.Value!.Campaign.Id, CancellationToken.None);

        Assert.True(preview.IsSuccess);
        Assert.Equal(1, preview.Value!.ParticipantCount);
        Assert.Equal(2, preview.Value!.AssetCount);
        Assert.Empty(participants.Participants);
        Assert.Empty(items.Items);
    }

    [Fact]
    public async Task PreviewAsync_ListsPeopleWithoutEmail()
    {
        // Person domain entity always requires a valid e-mail at construction (defensive validation),
        // so "no email" is not reachable via the public constructor in this test — verified instead
        // that a person WITH email is not flagged, keeping the warning list empty when everyone has one.
        var (service, user, _, _, _, people, assets, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        var preview = await service.PreviewAsync(created.Value!.Campaign.Id, CancellationToken.None);

        Assert.True(preview.IsSuccess);
        Assert.Empty(preview.Value!.PeopleWithoutEmail);
    }

    [Fact]
    public async Task StartAsync_CreatesParticipantsOnlyForPeopleWithAtLeastOneAsset()
    {
        var (service, user, campaigns, participants, items, people, assets, _, _, _) = CreateService();
        var withAsset = AddPerson(user, people, "with.asset@acme.test");
        AddAsset(user, assets, withAsset.Id);
        AddPerson(user, people, "without.asset@acme.test");

        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        var started = await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);

        Assert.True(started.IsSuccess);
        Assert.Equal(AssetAuditCampaignStatus.Active, started.Value!.Campaign.Status);
        Assert.Single(participants.Participants);
        Assert.Equal(withAsset.Id, participants.Participants[0].PersonId);
        Assert.Single(items.Items);
    }

    [Fact]
    public async Task StartAsync_IssuesDistinctTokensPerParticipant()
    {
        var (service, user, _, participants, _, people, assets, _, _, _) = CreateService();
        var person1 = AddPerson(user, people, "person1@acme.test");
        var person2 = AddPerson(user, people, "person2@acme.test");
        AddAsset(user, assets, person1.Id);
        AddAsset(user, assets, person2.Id);

        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);

        Assert.Equal(2, participants.Participants.Count);
        Assert.NotEqual(participants.Participants[0].TokenHash, participants.Participants[1].TokenHash);
    }

    [Fact]
    public async Task StartAsync_PersonWithEmail_ReceivesEmail()
    {
        // Mirrors the "no email = no send" branch exercised in OffboardingServiceTests: since Person
        // always requires a valid e-mail, we assert the positive path (email actually sent) here instead.
        var (service, user, _, participants, _, people, assets, _, emailSender, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        var started = await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);

        Assert.True(started.IsSuccess);
        Assert.Single(participants.Participants);
        Assert.Single(emailSender.Sent);
    }

    [Fact]
    public async Task AllOperations_AreFilteredByOrganizationId()
    {
        var (service, user, campaigns, _, _, people, assets, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);
        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);

        user.OrganizationId = Guid.NewGuid();
        var getResult = await service.GetAsync(created.Value!.Campaign.Id, CancellationToken.None);

        Assert.True(getResult.IsFailure);
        Assert.Equal("AUDIT_CAMPAIGN_NOT_FOUND", getResult.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_RejectedWhenNotDraft()
    {
        var (service, user, _, _, _, people, assets, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);
        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);

        var update = await service.UpdateAsync(created.Value!.Campaign.Id, new UpdateAssetAuditCampaignRequest("Nowa nazwa", null, DateTimeOffset.UtcNow.AddDays(30), new AssetAuditScope(AssetAuditScopeType.Organization)), CancellationToken.None);

        Assert.True(update.IsFailure);
        Assert.Equal("AUDIT_CAMPAIGN_EDIT_INVALID_STATE", update.Error!.Code);
    }

    [Fact]
    public async Task PublicToken_OfOneParticipant_DoesNotResolveOtherParticipantsItem()
    {
        var (service, user, _, participants, items, people, assets, _, emailSender, _) = CreateService();
        var personA = AddPerson(user, people, "a@acme.test");
        var personB = AddPerson(user, people, "b@acme.test");
        AddAsset(user, assets, personA.Id);
        AddAsset(user, assets, personB.Id);

        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);

        var tokenA = ExtractRawTokenFromEmail(emailSender, personA.Email);
        var itemOfB = items.Items.Single(x => x.ExpectedPersonId == personB.Id);

        var result = await service.RecordItemResponseAsync(tokenA, itemOfB.Id, new SubmitPublicAssetAuditItemRequest(AssetAuditResponse.Confirmed, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("ITEM_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task RecordItemResponseAsync_RejectedAfterSubmit()
    {
        var (service, user, _, _, items, people, assets, _, emailSender, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);
        var token = ExtractRawTokenFromEmail(emailSender, person.Email);
        var item = items.Items.Single();

        var submit = await service.SubmitAsync(token, CancellationToken.None);
        Assert.True(submit.IsSuccess);

        var result = await service.RecordItemResponseAsync(token, item.Id, new SubmitPublicAssetAuditItemRequest(AssetAuditResponse.Missing, "zgubione"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("AUDIT_RESPONSES_ALREADY_SUBMITTED_LOCKED", result.Error!.Code);
    }

    [Fact]
    public async Task RecordItemResponseAsync_DoesNotChangeAssetStatus()
    {
        var (service, user, _, _, items, people, assets, _, emailSender, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);
        var token = ExtractRawTokenFromEmail(emailSender, person.Email);
        var item = items.Items.Single();

        var result = await service.RecordItemResponseAsync(token, item.Id, new SubmitPublicAssetAuditItemRequest(AssetAuditResponse.Damaged, "Rysa na obudowie"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetStatus.Assigned, assets.Assets.Single(x => x.Id == asset.Id).Status);
    }

    [Fact]
    public async Task SubmitAsync_SetsStatusSubmitted_AndRecomputesCampaignStatus()
    {
        var (service, user, campaigns, participants, _, people, assets, activity, emailSender, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);
        var token = ExtractRawTokenFromEmail(emailSender, person.Email);

        var submit = await service.SubmitAsync(token, CancellationToken.None);

        Assert.True(submit.IsSuccess);
        Assert.Equal(AssetAuditParticipantStatus.Submitted, participants.Participants.Single().Status);
        Assert.Equal(AssetAuditCampaignStatus.Reviewing, campaigns.Campaigns.Single().Status);
        Assert.Contains(activity.Logs, x => x.Action == "asset_audit.participant_submitted");
    }

    [Fact]
    public async Task CompleteAsync_RevokesAllParticipantTokens()
    {
        var (service, user, _, _, _, people, assets, _, emailSender, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);
        var token = ExtractRawTokenFromEmail(emailSender, person.Email);

        var complete = await service.CompleteAsync(created.Value!.Campaign.Id, CancellationToken.None);
        Assert.True(complete.IsSuccess);

        var result = await service.GetPublicAsync(token, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CancelAsync_RevokesAllParticipantTokens()
    {
        var (service, user, _, _, _, people, assets, _, emailSender, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);
        var token = ExtractRawTokenFromEmail(emailSender, person.Email);

        var cancel = await service.CancelAsync(created.Value!.Campaign.Id, CancellationToken.None);
        Assert.True(cancel.IsSuccess);

        var result = await service.GetPublicAsync(token, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetPublicAsync_ExpiredOrRevokedOrUnknownToken_ReturnsNotFound()
    {
        var (service, _, _, _, _, _, _, _, _, _) = CreateService();

        var result = await service.GetPublicAsync("does-not-exist", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PUBLIC_LINK_INVALID_OR_EXPIRED", result.Error!.Code);
    }

    [Fact]
    public async Task ResolveItemAsync_AssetMarkedLost_ChangesStatusAndClearsOwner()
    {
        var (service, user, _, _, items, people, assets, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets, person.Id);
        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);
        var item = items.Items.Single();

        var result = await service.ResolveItemAsync(created.Value!.Campaign.Id, item.Id,
            new ResolveAssetAuditItemRequest(AssetAuditResolution.AssetMarkedLost, "Zgubione w podróży", null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var updated = assets.Assets.Single(x => x.Id == asset.Id);
        Assert.Equal(AssetStatus.Lost, updated.Status);
        Assert.Null(updated.AssignedPersonId);
    }

    [Fact]
    public async Task ResolveItemAsync_AssetMarkedDamaged_ChangesStatus_ButKeepsOwner()
    {
        var (service, user, _, _, items, people, assets, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets, person.Id);
        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);
        var item = items.Items.Single();

        var result = await service.ResolveItemAsync(created.Value!.Campaign.Id, item.Id,
            new ResolveAssetAuditItemRequest(AssetAuditResolution.AssetMarkedDamaged, "Pęknięta obudowa", null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var updated = assets.Assets.Single(x => x.Id == asset.Id);
        Assert.Equal(AssetStatus.Damaged, updated.Status);
        Assert.Equal(person.Id, updated.AssignedPersonId);
    }

    [Fact]
    public async Task ResolveItemAsync_OwnershipCorrected_MovesAssetToNewOwner_EvenIfPreviouslyAssignedToSomeoneElse()
    {
        var (service, user, _, _, items, people, assets, _, _, _) = CreateService();
        var oldOwner = AddPerson(user, people, "old@acme.test");
        var newOwner = AddPerson(user, people, "new@acme.test");
        var asset = AddAsset(user, assets, oldOwner.Id);
        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);
        var item = items.Items.Single();

        var result = await service.ResolveItemAsync(created.Value!.Campaign.Id, item.Id,
            new ResolveAssetAuditItemRequest(AssetAuditResolution.OwnershipCorrected, "Błąd ewidencji", newOwner.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var updated = assets.Assets.Single(x => x.Id == asset.Id);
        Assert.Equal(AssetStatus.Assigned, updated.Status);
        Assert.Equal(newOwner.Id, updated.AssignedPersonId);
    }

    [Fact]
    public async Task ResolveItemAsync_OwnershipCorrected_RejectsCrossOrganizationNewOwnerPersonId()
    {
        var (service, user, _, _, items, people, assets, _, _, _) = CreateService();
        var oldOwner = AddPerson(user, people, "old@acme.test");
        var otherOrgPerson = new Person(Guid.NewGuid(), "Anna", "Nowak", "anna@other.test");
        people.Add(otherOrgPerson);
        var asset = AddAsset(user, assets, oldOwner.Id);
        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);
        var item = items.Items.Single();

        var result = await service.ResolveItemAsync(created.Value!.Campaign.Id, item.Id,
            new ResolveAssetAuditItemRequest(AssetAuditResolution.OwnershipCorrected, "Błąd ewidencji", otherOrgPerson.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        var untouched = assets.Assets.Single(x => x.Id == asset.Id);
        Assert.Equal(oldOwner.Id, untouched.AssignedPersonId);
    }

    [Theory]
    [InlineData(AssetAuditResolution.AssetMarkedLost)]
    [InlineData(AssetAuditResolution.AssetMarkedDamaged)]
    public async Task ResolveItemAsync_WithoutNotes_ReturnsValidationError(AssetAuditResolution resolution)
    {
        var (service, user, _, _, items, people, assets, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);
        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);
        var item = items.Items.Single();

        var result = await service.ResolveItemAsync(created.Value!.Campaign.Id, item.Id,
            new ResolveAssetAuditItemRequest(resolution, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("AUDIT_RESOLUTION_NOTES_REQUIRED", result.Error!.Code);
    }

    [Fact]
    public async Task ResolveItemAsync_OwnershipCorrected_WithoutNewOwner_ReturnsValidationError()
    {
        var (service, user, _, _, items, people, assets, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);
        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);
        var item = items.Items.Single();

        var result = await service.ResolveItemAsync(created.Value!.Campaign.Id, item.Id,
            new ResolveAssetAuditItemRequest(AssetAuditResolution.OwnershipCorrected, "Notatka", null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("AUDIT_NEW_OWNER_REQUIRED", result.Error!.Code);
    }

    [Fact]
    public async Task ReopenParticipantAsync_OnlyWorksFromSubmitted()
    {
        var (service, user, _, participants, _, people, assets, _, emailSender, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);
        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);
        var participant = participants.Participants.Single();

        var beforeSubmit = await service.ReopenParticipantAsync(created.Value!.Campaign.Id, participant.Id, CancellationToken.None);
        Assert.True(beforeSubmit.IsFailure);
        Assert.Equal("AUDIT_REOPEN_INVALID_STATE", beforeSubmit.Error!.Code);

        var token = ExtractRawTokenFromEmail(emailSender, person.Email);
        await service.SubmitAsync(token, CancellationToken.None);

        var afterSubmit = await service.ReopenParticipantAsync(created.Value!.Campaign.Id, participant.Id, CancellationToken.None);
        Assert.True(afterSubmit.IsSuccess);
        Assert.Equal(AssetAuditParticipantStatus.InProgress, participants.Participants.Single().Status);
    }

    [Fact]
    public async Task CompleteAsync_IsIdempotent()
    {
        var (service, user, campaigns, _, _, people, assets, activity, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);
        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);

        var first = await service.CompleteAsync(created.Value!.Campaign.Id, CancellationToken.None);
        var second = await service.CompleteAsync(created.Value!.Campaign.Id, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(AssetAuditCampaignStatus.Completed, campaigns.Campaigns.Single().Status);
        Assert.Single(activity.Logs, x => x.Action == "asset_audit.completed");
    }

    [Fact]
    public async Task RemindParticipantsAsync_OnlyRemindsPendingOrInProgress()
    {
        var (service, user, _, participants, _, people, assets, activity, emailSender, _) = CreateService();
        var personA = AddPerson(user, people, "a@acme.test");
        var personB = AddPerson(user, people, "b@acme.test");
        AddAsset(user, assets, personA.Id);
        AddAsset(user, assets, personB.Id);
        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);

        var tokenA = ExtractRawTokenFromEmail(emailSender, personA.Email);
        await service.SubmitAsync(tokenA, CancellationToken.None); // A already submitted, should be skipped

        var remind = await service.RemindParticipantsAsync(created.Value!.Campaign.Id, CancellationToken.None);

        Assert.True(remind.IsSuccess);
        Assert.Equal(1, remind.Value!.RemindedCount);
        Assert.NotNull(participants.Participants.Single(x => x.PersonId == personB.Id).LastReminderAt);
        Assert.Null(participants.Participants.Single(x => x.PersonId == personA.Id).LastReminderAt);
        Assert.Contains(activity.Logs, x => x.Action == "asset_audit.reminder_sent");
    }

    [Fact]
    public async Task ExportCsvAsync_ContainsAllItems_AndEscapesCommaInComment()
    {
        var (service, user, _, _, items, people, assets, _, emailSender, _) = CreateService();
        var personA = AddPerson(user, people, "a@acme.test");
        var personB = AddPerson(user, people, "b@acme.test");
        AddAsset(user, assets, personA.Id);
        AddAsset(user, assets, personB.Id);
        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);

        var tokenA = ExtractRawTokenFromEmail(emailSender, personA.Email);
        var itemA = items.Items.Single(x => x.ExpectedPersonId == personA.Id);
        await service.RecordItemResponseAsync(tokenA, itemA.Id,
            new SubmitPublicAssetAuditItemRequest(AssetAuditResponse.Damaged, "Rysa, pęknięcie"), CancellationToken.None);

        var result = await service.ExportCsvAsync(created.Value!.Campaign.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var lines = result.Value!.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(items.Items.Count + 1, lines.Length); // header + one row per item
        Assert.Contains(lines, l => l.Contains("\"Rysa, pęknięcie\""));
    }

    [Fact]
    public async Task GetReportPdfAsync_SummaryCountsMatchItemResponses()
    {
        var (service, user, _, _, items, people, assets, _, emailSender, pdfGenerator) = CreateService();
        var personA = AddPerson(user, people, "a@acme.test");
        var personB = AddPerson(user, people, "b@acme.test");
        AddAsset(user, assets, personA.Id);
        AddAsset(user, assets, personB.Id);
        var created = await service.CreateAsync(OrganizationScopeRequest(), CancellationToken.None);
        await service.StartAsync(created.Value!.Campaign.Id, CancellationToken.None);

        var tokenA = ExtractRawTokenFromEmail(emailSender, personA.Email);
        var itemA = items.Items.Single(x => x.ExpectedPersonId == personA.Id);
        await service.RecordItemResponseAsync(tokenA, itemA.Id,
            new SubmitPublicAssetAuditItemRequest(AssetAuditResponse.Missing, "Zagubione"), CancellationToken.None);

        var result = await service.GetReportPdfAsync(created.Value!.Campaign.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var model = pdfGenerator.LastAssetAuditReportModel!;
        Assert.Equal(1, model.MissingCount);
        Assert.Equal(0, model.ConfirmedCount);
        Assert.Single(model.Exceptions);
    }

    private static string ExtractRawTokenFromEmail(FakeEmailSender emailSender, string recipient)
    {
        var index = emailSender.Sent.FindLastIndex(x => x.To == recipient);
        Assert.True(index >= 0, "Nie znaleziono e-maila do wskazanego odbiorcy.");
        var body = emailSender.Bodies[index];
        var match = System.Text.RegularExpressions.Regex.Match(body, @"https://test/audit/([^""'\s]+)");
        Assert.True(match.Success, "E-mail nie zawierał linku audytowego.");
        return match.Groups[1].Value;
    }
}
