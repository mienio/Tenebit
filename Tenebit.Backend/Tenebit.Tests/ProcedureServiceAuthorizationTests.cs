using Tenebit.Application.Common;
using Tenebit.Application.Procedures;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public sealed class ProcedureServiceAuthorizationTests
{
    private static (ProcedureService Service, FakeCurrentUser User, InMemoryPersonRepository People, InMemoryTeamRepository Teams, InMemoryProcedureRepository Procedures, InMemoryAssignmentRepository Assignments) CreateService()
    {
        var user = new FakeCurrentUser();
        var people = new InMemoryPersonRepository();
        var teams = new InMemoryTeamRepository();
        var procedures = new InMemoryProcedureRepository();
        var assignments = new InMemoryAssignmentRepository();
        var service = new ProcedureService(
            procedures,
            assignments,
            people,
            new InMemoryActivityLogRepository(),
            user,
            new FakeClock(),
            new FakeUnitOfWork(),
            new ManagerScopeService(people, teams));
        return (service, user, people, teams, procedures, assignments);
    }

    [Fact]
    public async Task Manager_ListAndGet_OnlyExposeProceduresAssignedToManagedPeople()
    {
        var (service, user, people, teams, procedures, assignments) = CreateService();
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
        var (service, user, _, _, _, _) = CreateService();
        user.Roles = [TenebitRoles.Manager];

        var result = await service.CreateAsync(new CreateProcedureRequest("Policy", "1.0", "HR", null, null, true), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Employee_CannotDownloadProcedureAssignedToAnotherPerson()
    {
        var (service, user, people, _, procedures, assignments) = CreateService();
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
}
