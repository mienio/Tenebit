using Tenebit.Application.Abstractions;
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
        InMemoryPersonRepository People, InMemoryAssetRepository Assets, InMemoryActivityLogRepository Activity, FakeClock Clock) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var reservations = new InMemoryEquipmentReservationRepository();
        var kits = new InMemoryEquipmentKitDefinitionRepository();
        var assets = new InMemoryAssetRepository();
        var people = new InMemoryPersonRepository();
        var assignments = new InMemoryAssignmentRepository();
        var activity = new InMemoryActivityLogRepository();
        var clock = new FakeClock();
        var unitOfWork = new FakeUnitOfWork();
        var availability = new AssetAvailabilityService(assets, assignments, reservations);
        var service = new ReservationService(reservations, kits, assets, people, activity, availability, currentUser, clock, unitOfWork);
        return (service, currentUser, reservations, people, assets, activity, clock);
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
        var (service, user, _, people, assets, activity, _) = CreateService();
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
        var (service, user, _, people, assets, _, _) = CreateService();
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
        var (service, user, reservations, people, assets, _, clock) = CreateService();
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
        var (service, user, reservations, people, assets, _, clock) = CreateService();

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
        var (service, user, _, people, assets, _, clock) = CreateService();
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
        var activity = new InMemoryActivityLogRepository();
        var clock = new FakeClock();
        var availability = new AssetAvailabilityService(assets, assignments, reservations);
        var service = new ReservationService(reservations, kits, assets, people, activity, availability, currentUser, clock, new ThrowingConcurrencyUnitOfWork());

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

    private sealed class ThrowingConcurrencyUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new ConcurrencyException("Symulowany konflikt wersji.");
    }
}
