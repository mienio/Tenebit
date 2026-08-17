using Tenebit.Application.Assets;
using Tenebit.Application.Offboarding;
using Tenebit.Domain.People;
using Tenebit.Domain.Reservations;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class OffboardingReservationCancellationTests
{
    private static (OffboardingService Service, FakeCurrentUser User, InMemoryEquipmentReservationRepository Reservations,
        InMemoryPersonRepository People, FakeClock Clock) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var cases = new InMemoryOffboardingCaseRepository();
        var items = new InMemoryOffboardingItemRepository();
        var people = new InMemoryPersonRepository();
        var assets = new InMemoryAssetRepository();
        var categories = new InMemoryAssetCategoryRepository();
        var assignments = new InMemoryAssignmentRepository();
        var licenses = new InMemoryLicenseRepository();
        var activity = new InMemoryActivityLogRepository();
        var inspections = new InMemoryAssetInspectionRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock();
        var reservations = new InMemoryEquipmentReservationRepository();

        var inspectionService = new AssetInspectionService(inspections, assets, activity, currentUser, clock, unitOfWork);
        var disposition = new AssetReturnDispositionService(inspections);
        var organizations = new InMemoryOrganizationRepository();
        var emailSender = new FakeEmailSender();
        var linkBuilder = new FakeAppLinkBuilder();
        var evidence = new InMemoryAssetEvidenceRepository();
        var evidenceService = new Tenebit.Application.Evidence.AssetEvidenceService(evidence, assets, assignments, new FakeImageSanitizer(), activity, currentUser, clock, unitOfWork);
        var responseBuilder = new OffboardingResponseBuilder(cases, items, people, organizations, assets, evidence, reservations, clock);
        var protocolModelBuilder = new OffboardingProtocolModelBuilder(organizations, people, items, assets, evidence, licenses);

        var service = new OffboardingService(cases, items, people, assets, categories, assignments, licenses, activity, currentUser, clock, unitOfWork,
            new OffboardingScheduledActionsService(cases, items, licenses, activity, new FakeUnitOfWork()), disposition, inspectionService, inspections,
            organizations, emailSender, linkBuilder, evidenceService, new FakePdfProtocolGenerator(), reservations,
            new InMemoryAssetAuditCampaignRepository(), new InMemoryAssetAuditItemRepository(), responseBuilder, protocolModelBuilder);

        return (service, currentUser, reservations, people, clock);
    }

    [Fact]
    public async Task StartAsync_CancelFutureReservations_CancelsFutureApprovedAndRejectsPending()
    {
        var (service, user, reservations, people, clock) = CreateService();
        var person = new Person(user.OrganizationId, "Jan", "Kowalski", "jan@acme.test");
        people.Add(person);

        var now = clock.UtcNow;

        var approvedReservation = new EquipmentReservation(user.OrganizationId, person.Id, now.AddDays(10), now.AddDays(12), "Delegacja", null, null);
        var approvedItem = approvedReservation.AddItem(Guid.NewGuid(), 1, null);
        approvedReservation.Submit(now);
        approvedReservation.Approve(now, "admin");
        approvedItem.Allocate(Guid.NewGuid());
        reservations.Add(approvedReservation);

        var pendingReservation = new EquipmentReservation(user.OrganizationId, person.Id, now.AddDays(10), now.AddDays(12), "Wyjazd", null, null);
        pendingReservation.AddItem(Guid.NewGuid(), 1, null);
        pendingReservation.Submit(now);
        reservations.Add(pendingReservation);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, now.AddDays(14), now.AddDays(21), null, null, null, false, true, false), CancellationToken.None);
        Assert.True(created.IsSuccess);

        var started = await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(NotifyEmployee: false), CancellationToken.None);

        Assert.True(started.IsSuccess, started.Error?.Message);
        Assert.Equal(EquipmentReservationStatus.Cancelled, approvedReservation.Status);
        Assert.Equal(EquipmentReservationStatus.Rejected, pendingReservation.Status);
    }
}
