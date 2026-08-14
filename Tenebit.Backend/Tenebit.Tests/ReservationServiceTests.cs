using Tenebit.Application.Abstractions;
using Tenebit.Application.Assignments;
using Tenebit.Application.Reservations;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Common;
using Tenebit.Domain.People;
using Tenebit.Domain.Reservations;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class ReservationServiceTests
{
    private static (ReservationService Service, FakeCurrentUser User, InMemoryEquipmentReservationRepository Reservations,
        InMemoryPersonRepository People, InMemoryAssetRepository Assets, InMemoryActivityLogRepository Activity, FakeClock Clock,
        InMemoryAssignmentRepository Assignments, InMemoryAssetCategoryRepository Categories) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var reservations = new InMemoryEquipmentReservationRepository();
        var kits = new InMemoryEquipmentKitDefinitionRepository();
        var assets = new InMemoryAssetRepository();
        var people = new InMemoryPersonRepository();
        var assignments = new InMemoryAssignmentRepository();
        var categories = new InMemoryAssetCategoryRepository();
        var inspections = new InMemoryAssetInspectionRepository();
        var procedures = new InMemoryProcedureRepository();
        var teams = new InMemoryTeamRepository();
        var organizations = new InMemoryOrganizationRepository();
        var activity = new InMemoryActivityLogRepository();
        var clock = new FakeClock();
        var unitOfWork = new FakeUnitOfWork();
        var availability = new AssetAvailabilityService(assets, assignments, reservations);
        var assignmentService = new AssignmentService(assignments, assets, categories, inspections, people, procedures, teams,
            organizations, activity, currentUser, clock, unitOfWork, new FakePdfProtocolGenerator(), new FakeEmailSender(),
            new FakeAppLinkBuilder(), reservations);
        var service = new ReservationService(reservations, kits, assets, people, activity, availability, assignmentService, currentUser, clock, unitOfWork);
        return (service, currentUser, reservations, people, assets, activity, clock, assignments, categories);
    }

    private static Person AddPerson(InMemoryPersonRepository people, Guid organizationId, string email = "jan.kowalski@acme.test", Guid? managerId = null)
    {
        var person = new Person(organizationId, "Jan", "Kowalski", email);
        if (managerId.HasValue)
        {
            person.Update("Jan", "Kowalski", email, null, null, "Pracownik", null, null, managerId, null, null);
        }

        people.Add(person);
        return person;
    }

    private static Asset AddReservableAsset(FakeCurrentUser user, InMemoryAssetRepository assets, Guid categoryId)
    {
        var asset = new Asset(user.OrganizationId, categoryId, "Laptop", $"AT-{Guid.NewGuid():N}"[..8]);
        asset.SetReservationSettings(true, null, null);
        assets.Add(asset);
        return asset;
    }

    private static CreateReservationRequest CreateRequest(Guid categoryId, DateTimeOffset? startAt = null) =>
        new(startAt ?? DateTimeOffset.UtcNow.AddDays(3), DateTimeOffset.UtcNow.AddDays(6), "Delegacja", null, null,
            [new ReservationItemRequest(categoryId, null, 1)]);

    [Fact]
    public async Task CreateSubmitApprove_FullFlow()
    {
        var (service, user, _, people, assets, activity, _, _, _) = CreateService();
        var person = AddPerson(people, user.OrganizationId);
        user.Email = person.Email;
        var categoryId = Guid.NewGuid();
        var asset = AddReservableAsset(user, assets, categoryId);

        var created = await service.CreateAsync(CreateRequest(categoryId), CancellationToken.None);
        Assert.True(created.IsSuccess);
        Assert.Equal(EquipmentReservationStatus.Draft, created.Value!.Reservation.Status);
        Assert.Contains(activity.Logs, x => x.Action == "reservation.created");

        var submitted = await service.SubmitMyAsync(created.Value.Reservation.Id, CancellationToken.None);
        Assert.True(submitted.IsSuccess);
        Assert.Equal(EquipmentReservationStatus.PendingApproval, submitted.Value!.Reservation.Status);

        var item = submitted.Value.Items.Single();
        var approved = await service.ApproveAsync(created.Value.Reservation.Id,
            new ApproveReservationRequest([new ReservationAllocationRequest(item.Id, asset.Id)]), CancellationToken.None);

        Assert.True(approved.IsSuccess);
        Assert.Equal(EquipmentReservationStatus.Approved, approved.Value!.Reservation.Status);
        Assert.Equal(EquipmentReservationItemStatus.Allocated, approved.Value.Items.Single().Status);
        Assert.Contains(activity.Logs, x => x.Action == "reservation.approved");
    }

    [Fact]
    public async Task ApproveAsync_RejectsOverlappingReservationForSameAsset()
    {
        var (service, user, _, people, assets, _, _, _, _) = CreateService();
        var person = AddPerson(people, user.OrganizationId);
        user.Email = person.Email;
        var categoryId = Guid.NewGuid();
        var asset = AddReservableAsset(user, assets, categoryId);

        var first = await service.CreateAsync(CreateRequest(categoryId), CancellationToken.None);
        await service.SubmitMyAsync(first.Value!.Reservation.Id, CancellationToken.None);
        var firstItem = first.Value.Items.Single();
        var firstApprove = await service.ApproveAsync(first.Value.Reservation.Id,
            new ApproveReservationRequest([new ReservationAllocationRequest(firstItem.Id, asset.Id)]), CancellationToken.None);
        Assert.True(firstApprove.IsSuccess);

        var second = await service.CreateAsync(CreateRequest(categoryId), CancellationToken.None);
        await service.SubmitMyAsync(second.Value!.Reservation.Id, CancellationToken.None);
        var secondItem = second.Value.Items.Single();
        var secondApprove = await service.ApproveAsync(second.Value.Reservation.Id,
            new ApproveReservationRequest([new ReservationAllocationRequest(secondItem.Id, asset.Id)]), CancellationToken.None);

        Assert.True(secondApprove.IsFailure);
        Assert.Equal(409, secondApprove.Error!.StatusCode);
    }

    [Fact]
    public async Task ApproveAsync_ConflictDoesNotAllocateAnyItem()
    {
        var (service, user, reservations, people, assets, _, clock, _, _) = CreateService();
        var person = AddPerson(people, user.OrganizationId);
        user.Email = person.Email;
        var categoryA = Guid.NewGuid();
        var categoryB = Guid.NewGuid();
        var assetA = AddReservableAsset(user, assets, categoryA);
        var assetB = AddReservableAsset(user, assets, categoryB);

        // Rezerwujemy assetB w innym, zatwierdzonym wniosku o nachodzącym terminie.
        var other = await service.CreateAsync(CreateRequest(categoryB), CancellationToken.None);
        await service.SubmitMyAsync(other.Value!.Reservation.Id, CancellationToken.None);
        var otherItem = other.Value.Items.Single();
        var otherApprove = await service.ApproveAsync(other.Value.Reservation.Id,
            new ApproveReservationRequest([new ReservationAllocationRequest(otherItem.Id, assetB.Id)]), CancellationToken.None);
        Assert.True(otherApprove.IsSuccess);

        var request = new CreateReservationRequest(clock.UtcNow.AddDays(3), clock.UtcNow.AddDays(6), "Delegacja", null, null,
            [new ReservationItemRequest(categoryA, null, 1), new ReservationItemRequest(categoryB, null, 1)]);
        var reservation = await service.CreateAsync(request, CancellationToken.None);
        await service.SubmitMyAsync(reservation.Value!.Reservation.Id, CancellationToken.None);
        var itemA = reservation.Value.Items.Single(x => x.RequestedCategoryId == categoryA);
        var itemB = reservation.Value.Items.Single(x => x.RequestedCategoryId == categoryB);

        var approve = await service.ApproveAsync(reservation.Value.Reservation.Id, new ApproveReservationRequest([
            new ReservationAllocationRequest(itemA.Id, assetA.Id),
            new ReservationAllocationRequest(itemB.Id, assetB.Id),
        ]), CancellationToken.None);

        Assert.True(approve.IsFailure);
        Assert.Equal(409, approve.Error!.StatusCode);

        var stored = reservations.Reservations.Single(x => x.Id == reservation.Value.Reservation.Id);
        Assert.Equal(EquipmentReservationStatus.PendingApproval, stored.Status);
        Assert.All(stored.Items, x => Assert.Equal(EquipmentReservationItemStatus.Requested, x.Status));
    }

    [Fact]
    public async Task ApproveAsync_ManagerCanOnlyApproveDirectReports()
    {
        var (service, user, reservations, people, assets, _, clock, _, _) = CreateService();

        var manager = new Person(user.OrganizationId, "Anna", "Kierownik", "anna@acme.test");
        people.Add(manager);
        var employee = new Person(user.OrganizationId, "Jan", "Kowalski", "jan@acme.test");
        employee.Update("Jan", "Kowalski", "jan@acme.test", null, null, "Pracownik", null, null, manager.Id, null, null);
        people.Add(employee);
        var stranger = new Person(user.OrganizationId, "Piotr", "Obcy", "piotr@acme.test");
        people.Add(stranger);

        var categoryId = Guid.NewGuid();
        var asset = AddReservableAsset(user, assets, categoryId);

        user.Email = "anna@acme.test";
        user.Roles = ["manager"];

        var now = clock.UtcNow;
        var employeeReservation = new EquipmentReservation(user.OrganizationId, employee.Id, now.AddDays(3), now.AddDays(6), "Cel", null, null);
        var employeeItem = employeeReservation.AddItem(categoryId, 1, null);
        employeeReservation.Submit(now);
        reservations.Add(employeeReservation);

        var strangerReservation = new EquipmentReservation(user.OrganizationId, stranger.Id, now.AddDays(3), now.AddDays(6), "Cel", null, null);
        var strangerItem = strangerReservation.AddItem(categoryId, 1, null);
        strangerReservation.Submit(now);
        reservations.Add(strangerReservation);

        var strangerApprove = await service.ApproveAsync(strangerReservation.Id,
            new ApproveReservationRequest([new ReservationAllocationRequest(strangerItem.Id, asset.Id)]), CancellationToken.None);
        Assert.True(strangerApprove.IsFailure);
        Assert.Equal(403, strangerApprove.Error!.StatusCode);

        var employeeApprove = await service.ApproveAsync(employeeReservation.Id,
            new ApproveReservationRequest([new ReservationAllocationRequest(employeeItem.Id, asset.Id)]), CancellationToken.None);
        Assert.True(employeeApprove.IsSuccess);
    }

    [Fact]
    public async Task CreateAsync_BlocksWhenPersonIsNotActive()
    {
        var (service, user, _, people, assets, _, clock, _, _) = CreateService();
        var person = AddPerson(people, user.OrganizationId);
        user.Email = person.Email;
        var categoryId = Guid.NewGuid();
        AddReservableAsset(user, assets, categoryId);

        person.StartOffboarding(clock.UtcNow.AddDays(5));

        var result = await service.CreateAsync(CreateRequest(categoryId), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ApproveAsync_MapsConcurrencyExceptionToConflict()
    {
        var currentUser = new FakeCurrentUser();
        var reservations = new InMemoryEquipmentReservationRepository();
        var kits = new InMemoryEquipmentKitDefinitionRepository();
        var assets = new InMemoryAssetRepository();
        var people = new InMemoryPersonRepository();
        var assignments = new InMemoryAssignmentRepository();
        var categories = new InMemoryAssetCategoryRepository();
        var activity = new InMemoryActivityLogRepository();
        var clock = new FakeClock();
        var availability = new AssetAvailabilityService(assets, assignments, reservations);
        var assignmentService = new AssignmentService(assignments, assets, categories, new InMemoryAssetInspectionRepository(), people,
            new InMemoryProcedureRepository(), new InMemoryTeamRepository(), new InMemoryOrganizationRepository(), activity, currentUser,
            clock, new FakeUnitOfWork(), new FakePdfProtocolGenerator(), new FakeEmailSender(), new FakeAppLinkBuilder(), reservations);
        var service = new ReservationService(reservations, kits, assets, people, activity, availability, assignmentService, currentUser, clock, new ThrowingConcurrencyUnitOfWork());

        var person = AddPerson(people, currentUser.OrganizationId);
        var categoryId = Guid.NewGuid();
        var asset = AddReservableAsset(currentUser, assets, categoryId);

        var now = clock.UtcNow;
        var reservation = new EquipmentReservation(currentUser.OrganizationId, person.Id, now.AddDays(3), now.AddDays(6), "Cel", null, null);
        var item = reservation.AddItem(categoryId, 1, null);
        reservation.Submit(now);
        reservations.Add(reservation);

        var approve = await service.ApproveAsync(reservation.Id,
            new ApproveReservationRequest([new ReservationAllocationRequest(item.Id, asset.Id)]), CancellationToken.None);

        Assert.True(approve.IsFailure);
        Assert.Equal(409, approve.Error!.StatusCode);
    }

    [Fact]
    public async Task CheckoutAsync_CreatesAssignmentWithDueDateAndAssets()
    {
        var (service, user, _, people, assets, activity, _, assignments, _) = CreateService();
        var person = AddPerson(people, user.OrganizationId);
        user.Email = person.Email;
        var categoryId = Guid.NewGuid();
        var asset = AddReservableAsset(user, assets, categoryId);

        var created = await service.CreateAsync(CreateRequest(categoryId), CancellationToken.None);
        await service.SubmitMyAsync(created.Value!.Reservation.Id, CancellationToken.None);
        var item = created.Value.Items.Single();
        var approved = await service.ApproveAsync(created.Value.Reservation.Id,
            new ApproveReservationRequest([new ReservationAllocationRequest(item.Id, asset.Id)]), CancellationToken.None);
        Assert.True(approved.IsSuccess);

        var checkedOut = await service.CheckoutAsync(created.Value.Reservation.Id, CancellationToken.None);

        Assert.True(checkedOut.IsSuccess);
        Assert.Equal(EquipmentReservationStatus.CheckedOut, checkedOut.Value!.Reservation.Status);
        Assert.Contains(activity.Logs, x => x.Action == "reservation.checked_out");

        var assignment = assignments.Assignments.Single();
        Assert.Equal(person.Id, assignment.PersonId);
        Assert.Equal(DateOnly.FromDateTime(approved.Value!.Reservation.EndAt.UtcDateTime), assignment.DueDate);
        Assert.Contains(assignment.Assets, x => x.AssetId == asset.Id);
    }

    [Fact]
    public async Task CheckoutAsync_RejectsWhenItemHasNoAssignedAsset()
    {
        var (service, user, reservations, people, assets, _, clock, _, _) = CreateService();
        var person = AddPerson(people, user.OrganizationId);
        user.Email = person.Email;
        var categoryId = Guid.NewGuid();
        AddReservableAsset(user, assets, categoryId);

        // Symulujemy wniosek zatwierdzony domenowo bez alokacji pozycji (edge case niedopuszczalny przez
        // normalny przepływ ApproveAsync, ale checkout musi go bezpiecznie odrzucić).
        var now = clock.UtcNow;
        var reservation = new EquipmentReservation(user.OrganizationId, person.Id, now.AddDays(3), now.AddDays(6), "Cel", null, null);
        reservation.AddItem(categoryId, 1, null);
        reservation.Submit(now);
        reservation.Approve(now, user.Subject);
        reservations.Add(reservation);

        var result = await service.CheckoutAsync(reservation.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(400, result.Error!.StatusCode);
    }

    [Fact]
    public async Task CheckoutAsync_DetectsConflictAndDoesNotCreateAssignment()
    {
        var (service, user, _, people, assets, _, _, assignments, _) = CreateService();
        var person = AddPerson(people, user.OrganizationId);
        user.Email = person.Email;
        var categoryId = Guid.NewGuid();
        var asset = AddReservableAsset(user, assets, categoryId);

        var created = await service.CreateAsync(CreateRequest(categoryId), CancellationToken.None);
        await service.SubmitMyAsync(created.Value!.Reservation.Id, CancellationToken.None);
        var item = created.Value.Items.Single();
        var approved = await service.ApproveAsync(created.Value.Reservation.Id,
            new ApproveReservationRequest([new ReservationAllocationRequest(item.Id, asset.Id)]), CancellationToken.None);
        Assert.True(approved.IsSuccess);

        // Aktywo staje się niedostępne pomiędzy zatwierdzeniem a wydaniem (np. oznaczone jako uszkodzone).
        asset.ChangeStatus(AssetStatus.Damaged);

        var result = await service.CheckoutAsync(created.Value.Reservation.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode);
        Assert.Empty(assignments.Assignments);
    }

    [Fact]
    public async Task ReturnAsync_FullReturnOfLinkedAssignmentCompletesReservation()
    {
        var (service, user, reservations, people, assets, activity, _, assignments, categories) = CreateService();
        var person = AddPerson(people, user.OrganizationId);
        user.Email = person.Email;
        var categoryId = Guid.NewGuid();
        var asset = AddReservableAsset(user, assets, categoryId);

        var created = await service.CreateAsync(CreateRequest(categoryId), CancellationToken.None);
        await service.SubmitMyAsync(created.Value!.Reservation.Id, CancellationToken.None);
        var item = created.Value.Items.Single();
        await service.ApproveAsync(created.Value.Reservation.Id,
            new ApproveReservationRequest([new ReservationAllocationRequest(item.Id, asset.Id)]), CancellationToken.None);

        var checkedOut = await service.CheckoutAsync(created.Value.Reservation.Id, CancellationToken.None);
        Assert.True(checkedOut.IsSuccess);
        var assignmentId = assignments.Assignments.Single().Id;

        var assignmentService = new AssignmentService(assignments, assets, categories, new InMemoryAssetInspectionRepository(), people,
            new InMemoryProcedureRepository(), new InMemoryTeamRepository(), new InMemoryOrganizationRepository(), activity, user,
            new FakeClock(), new FakeUnitOfWork(), new FakePdfProtocolGenerator(), new FakeEmailSender(), new FakeAppLinkBuilder(), reservations);

        var returned = await assignmentService.ReturnAsync(assignmentId, new ReturnAssignmentRequest("Sprawny", null), CancellationToken.None);

        Assert.True(returned.IsSuccess);
        var stored = reservations.Reservations.Single(x => x.Id == created.Value.Reservation.Id);
        Assert.Equal(EquipmentReservationStatus.Completed, stored.Status);
        Assert.Contains(activity.Logs, x => x.Action == "reservation.completed");
    }

    [Fact]
    public async Task GetCalendarAsync_FlagsOverdueDueTodayAndConflicting()
    {
        var (service, user, reservations, people, assets, _, clock, _, _) = CreateService();
        var person = AddPerson(people, user.OrganizationId);
        var categoryId = Guid.NewGuid();
        var asset = AddReservableAsset(user, assets, categoryId);
        var now = clock.UtcNow;

        // Zaległy zwrot: termin minął, wciąż CheckedOut.
        var overdue = new EquipmentReservation(user.OrganizationId, person.Id, now.AddDays(-10), now.AddDays(-1), "Cel", null, null);
        var overdueItem = overdue.AddItem(categoryId, 1, null);
        overdue.Submit(now.AddDays(-10));
        overdue.Approve(now.AddDays(-10), user.Subject);
        overdueItem.Allocate(asset.Id);
        overdue.MarkCheckedOut(Guid.NewGuid(), now.AddDays(-9));
        reservations.Add(overdue);

        // Do wydania dzisiaj.
        var dueToday = new EquipmentReservation(user.OrganizationId, person.Id, now, now.AddDays(2), "Cel", null, null);
        dueToday.AddItem(categoryId, 1, null);
        dueToday.Submit(now);
        reservations.Add(dueToday);

        // Dwie zatwierdzone rezerwacje tego samego aktywa z nachodzącym terminem = konflikt.
        var conflictA = new EquipmentReservation(user.OrganizationId, person.Id, now.AddDays(1), now.AddDays(5), "Cel", null, null);
        var conflictAItem = conflictA.AddItem(categoryId, 1, null);
        conflictA.Submit(now);
        conflictA.Approve(now, user.Subject);
        conflictAItem.Allocate(asset.Id);
        reservations.Add(conflictA);

        var conflictB = new EquipmentReservation(user.OrganizationId, person.Id, now.AddDays(2), now.AddDays(6), "Cel", null, null);
        var conflictBItem = conflictB.AddItem(categoryId, 1, null);
        conflictB.Submit(now);
        conflictB.Approve(now, user.Subject);
        conflictBItem.Allocate(asset.Id);
        reservations.Add(conflictB);

        var result = await service.GetCalendarAsync(now.AddDays(-30), now.AddDays(30), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var items = result.Value!;
        Assert.True(items.Single(x => x.Id == overdue.Id).IsOverdue);
        Assert.True(items.Single(x => x.Id == dueToday.Id).IsDueToday);
        Assert.True(items.Single(x => x.Id == conflictA.Id).IsConflicting);
        Assert.True(items.Single(x => x.Id == conflictB.Id).IsConflicting);
    }

    private sealed class ThrowingConcurrencyUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new ConcurrencyException("Symulowany konflikt wersji.");
    }
}
