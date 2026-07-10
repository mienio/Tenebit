using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.People;

namespace Tenebit.Application.People;

public sealed class PeopleService
{
    private readonly IPersonRepository _people;
    private readonly ITeamRepository _teams;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public PeopleService(IPersonRepository people, ITeamRepository teams, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork)
    {
        _people = people;
        _teams = teams;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<PersonResponse>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        var teams = await _teams.ListAsync(_currentUser.OrganizationId, cancellationToken);
        var people = await _people.ListAsync(_currentUser.OrganizationId, search, cancellationToken);
        return people.Select(person => Map(person, teams)).ToList();
    }

    public async Task<Result<PersonResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var person = await _people.GetAsync(_currentUser.OrganizationId, id, cancellationToken);
        if (person is null) return Result<PersonResponse>.Failure(Error.NotFound("Pracownik nie istnieje."));
        var teams = await _teams.ListAsync(_currentUser.OrganizationId, cancellationToken);
        return Result<PersonResponse>.Success(Map(person, teams));
    }

    public async Task<Result<PersonResponse>> CreateAsync(CreatePersonRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<PersonResponse>.Failure(access.Error!);

        try
        {
            var organizationId = _currentUser.OrganizationId;
            if (await _people.EmailExistsAsync(organizationId, request.Email, null, cancellationToken))
            {
                return Result<PersonResponse>.Failure(Error.Conflict("Osoba z tym adresem e-mail już istnieje."));
            }

            var person = new Person(organizationId, request.FirstName, request.LastName, request.Email);
            person.Update(request.FirstName, request.LastName, request.Email, request.Phone, request.EmployeeNumber, request.RelationType, request.JobTitle, request.TeamId, request.ManagerId, request.Location, request.CostCenter);
            _people.Add(person);
            _activity.Add(new ActivityLog(organizationId, "person.created", "person", person.Id, _currentUser.Subject, person.FullName, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await GetAsync(person.Id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<PersonResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<PersonResponse>> UpdateAsync(Guid id, UpdatePersonRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr);
        if (access.IsFailure) return Result<PersonResponse>.Failure(access.Error!);

        try
        {
            var organizationId = _currentUser.OrganizationId;
            var person = await _people.GetAsync(organizationId, id, cancellationToken);
            if (person is null) return Result<PersonResponse>.Failure(Error.NotFound("Pracownik nie istnieje."));

            if (await _people.EmailExistsAsync(organizationId, request.Email, id, cancellationToken))
            {
                return Result<PersonResponse>.Failure(Error.Conflict("Osoba z tym adresem e-mail już istnieje."));
            }

            person.Update(request.FirstName, request.LastName, request.Email, request.Phone, request.EmployeeNumber, request.RelationType, request.JobTitle, request.TeamId, request.ManagerId, request.Location, request.CostCenter);
            if (request.IsActive) person.Activate(); else person.Deactivate();

            _activity.Add(new ActivityLog(organizationId, "person.updated", "person", person.Id, _currentUser.Subject, person.FullName, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await GetAsync(id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<PersonResponse>.Failure(Error.Validation(ex.Message));
        }
    }


    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr);
        if (access.IsFailure) return Result.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var person = await _people.GetAsync(organizationId, id, cancellationToken);
        if (person is null) return Result.Failure(Error.NotFound("Pracownik nie istnieje."));

        if (await _people.HasBlockingRelationsAsync(organizationId, id, cancellationToken))
        {
            return Result.Failure(Error.Conflict("Nie można usunąć pracownika, bo ma przypisany sprzęt, historię wydań albo jest przełożonym. Najpierw zwróć sprzęt albo usuń powiązania."));
        }

        _people.Remove(person);
        _activity.Add(new ActivityLog(organizationId, "person.deleted", "person", person.Id, _currentUser.Subject, person.FullName, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static PersonResponse Map(Person person, IReadOnlyList<Team> teams)
    {
        var team = person.TeamId.HasValue ? teams.FirstOrDefault(x => x.Id == person.TeamId.Value) : null;
        return new PersonResponse(person.Id, person.FirstName, person.LastName, person.FullName, person.Email, person.Phone, person.EmployeeNumber, person.RelationType, person.JobTitle, person.TeamId, team?.Name, person.ManagerId, person.Location, person.CostCenter, person.IsActive);
    }
}
