using Tenebit.Application.Common;
using Tenebit.Application.Procedures;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Domain.Subscriptions;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public sealed class ProcedureServiceAuthorizationTests
{
    private static (ProcedureService Service, FakeCurrentUser User, InMemoryPersonRepository People, InMemoryTeamRepository Teams, InMemoryProcedureRepository Procedures, InMemoryAssignmentRepository Assignments, InMemorySubscriptionRepository Subscriptions) CreateService()
    {
        var user = new FakeCurrentUser();
        var people = new InMemoryPersonRepository();
        var teams = new InMemoryTeamRepository();
        var procedures = new InMemoryProcedureRepository();
        var assignments = new InMemoryAssignmentRepository();
        var subscriptions = new InMemorySubscriptionRepository();
        var service = new ProcedureService(
            procedures,
            assignments,
            people,
            new InMemoryActivityLogRepository(),
            user,
            new FakeClock(),
            new FakeUnitOfWork(),
            new ManagerScopeService(people, teams),
            subscriptions);
        return (service, user, people, teams, procedures, assignments, subscriptions);
    }

    [Fact]
    public async Task Manager_ListAndGet_OnlyExposeProceduresAssignedToManagedPeople()
    {
        var (service, user, people, teams, procedures, assignments, _) = CreateService();
        user.Roles = [TenebitRoles.Manager];

        var manager = new Person(user.OrganizationId, "Anna", "Manager", "manager@acme.test");
        people.Add(manager);
        user.PersonId = manager.Id;
        var team = new Team(user.OrganizationId, "Team A", manager.Id, null);
        teams.Add(team);
        var teammate = new Person(user.OrganizationId, "Jan", "Team", "jan@acme.test");
        teammate.Update(teammate.FirstName, teammate.LastName, teammate.Email, null, null, "employee", null, team.Id, null, null, null);
        people.Add(teammate);
        var outsider = new Person(user.OrganizationId, "Ola", "Other", "ola@acme.test");
        people.Add(outsider);

        var visible = new Procedure(user.OrganizationId, "Visible", "1.0", "HR", true);
        var hidden = new Procedure(user.OrganizationId, "Hidden", "1.0", "HR", true);
        procedures.Add(visible);
        procedures.Add(hidden);

        var visibleAssignment = new Assignment(user.OrganizationId, teammate.Id, "TEN-1", DateTimeOffset.UtcNow, null, null, "test");
        visibleAssignment.AddProcedureAcceptance(user.OrganizationId, visible.Id, teammate.Id, DateTimeOffset.UtcNow);
        assignments.Add(visibleAssignment);
        var hiddenAssignment = new Assignment(user.OrganizationId, outsider.Id, "TEN-2", DateTimeOffset.UtcNow, null, null, "test");
        hiddenAssignment.AddProcedureAcceptance(user.OrganizationId, hidden.Id, outsider.Id, DateTimeOffset.UtcNow);
        assignments.Add(hiddenAssignment);

        var list = await service.ListAsync(null, CancellationToken.None);
        var hiddenGet = await service.GetAsync(hidden.Id, CancellationToken.None);

        Assert.True(list.IsSuccess);
        Assert.Contains(list.Value!, x => x.Id == visible.Id);
        Assert.DoesNotContain(list.Value!, x => x.Id == hidden.Id);
        Assert.True(hiddenGet.IsFailure);
    }

    [Fact]
    public async Task Manager_CannotCreateProcedure()
    {
        var (service, user, _, _, _, _, _) = CreateService();
        user.Roles = [TenebitRoles.Manager];

        var result = await service.CreateAsync(new CreateProcedureRequest("Policy", "1.0", "HR", null, null, true), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Employee_CannotDownloadProcedureAssignedToAnotherPerson()
    {
        var (service, user, people, _, procedures, assignments, _) = CreateService();
        user.Roles = [TenebitRoles.Employee];
        var self = new Person(user.OrganizationId, "Ela", "Self", "ela@acme.test");
        people.Add(self);
        user.PersonId = self.Id;
        var other = new Person(user.OrganizationId, "Ola", "Other", "ola@acme.test");
        people.Add(other);
        var procedure = new Procedure(user.OrganizationId, "Policy", "1.0", "HR", true);
        procedures.Add(procedure);
        var assignment = new Assignment(user.OrganizationId, other.Id, "TEN-3", DateTimeOffset.UtcNow, null, null, "test");
        assignment.AddProcedureAcceptance(user.OrganizationId, procedure.Id, other.Id, DateTimeOffset.UtcNow);
        assignments.Add(assignment);

        var result = await service.GetDocumentAsync(procedure.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task PublishedProcedure_CannotBeEditedOrHaveDocumentsChangedThroughService()
    {
        var (service, _, _, _, _, _, _) = CreateService();
        var created = await service.CreateAsync(new CreateProcedureRequest("Policy", "1.0", "HR", null, null, true), CancellationToken.None);
        Assert.True(created.IsSuccess);

        var procedureId = created.Value!.Id;
        var pdf = System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\nminimal-test");
        var attached = await service.AttachDocumentAsync(procedureId, "policy.pdf", "application/octet-stream", pdf, CancellationToken.None);
        Assert.True(attached.IsSuccess);
        var documentId = Assert.Single(attached.Value!.Documents).Id;

        var published = await service.PublishAsync(procedureId, CancellationToken.None);
        Assert.True(published.IsSuccess);

        var update = await service.UpdateAsync(procedureId, new UpdateProcedureRequest("Changed", "2.0", "HR", null, null, true), CancellationToken.None);
        var attachAgain = await service.AttachDocumentAsync(procedureId, "other.pdf", "application/pdf", pdf, CancellationToken.None);
        var remove = await service.RemoveDocumentAsync(procedureId, documentId, CancellationToken.None);

        Assert.True(update.IsFailure);
        Assert.True(attachAgain.IsFailure);
        Assert.True(remove.IsFailure);
    }

    [Fact]
    public async Task ProcedureUpload_RejectsArbitraryExecutablePayload()
    {
        var (service, _, _, _, _, _, _) = CreateService();
        var created = await service.CreateAsync(new CreateProcedureRequest("Policy", "1.0", "HR", null, null, true), CancellationToken.None);
        Assert.True(created.IsSuccess);

        var result = await service.AttachDocumentAsync(created.Value!.Id, "payload.exe", "application/octet-stream", [0x4D, 0x5A, 0x90, 0x00], CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_RejectsWhenAtSubscriptionResourceLimit()
    {
        var (service, user, _, _, procedures, _, subscriptions) = CreateService();
        subscriptions.Add(new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key));

        for (var i = 0; i < SubscriptionPlan.Free.AssetLimit; i++)
        {
            procedures.Add(new Procedure(user.OrganizationId, $"Policy {i}", "1.0", "HR", true));
        }

        var result = await service.CreateAsync(new CreateProcedureRequest("Policy over limit", "1.0", "HR", null, null, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Limit procedur przekroczony", result.Error!.Message);
    }
}
