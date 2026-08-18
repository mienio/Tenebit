using Tenebit.Application.Abstractions;
using Tenebit.Application.Assets;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.People;

namespace Tenebit.Application.People;

public sealed class PeopleService
{
    private readonly IPersonRepository _people;
    private readonly ITeamRepository _teams;
    private readonly IAssetRepository _assets;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ManagerScopeService _managerScope;
    private readonly LocationReferenceResolver _locationResolver;

    // Roles in TenebitRoles.PeopleViewers that see the whole organization; Manager alone is scoped
    // to its own team by ManagerScopeService (audyt AUD3-006).
    private static readonly string[] OrgWideRoles = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.AssetOperator, TenebitRoles.Auditor];

    public PeopleService(IPersonRepository people, ITeamRepository teams, IAssetRepository assets, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, ManagerScopeService managerScope, LocationReferenceResolver locationResolver)
    {
        _people = people;
        _teams = teams;
        _assets = assets;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _managerScope = managerScope;
        _locationResolver = locationResolver;
    }

    public async Task<Result<IReadOnlyList<PersonResponse>>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.PeopleViewers);
        if (access.IsFailure) return Result<IReadOnlyList<PersonResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var teams = await _teams.ListAsync(organizationId, cancellationToken);
        var scope = await _managerScope.ResolveAsync(_currentUser, OrgWideRoles, cancellationToken);
        var people = scope is null
            ? await _people.ListAsync(organizationId, search, cancellationToken)
            : await _people.ListScopedAsync(organizationId, search, scope.PersonIds, cancellationToken);
        return Result<IReadOnlyList<PersonResponse>>.Success(people.Select(person => Map(person, teams)).ToList());
    }

    public async Task<Result<PagedResult<PersonResponse>>> ListPagedAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.PeopleViewers);
        if (access.IsFailure) return Result<PagedResult<PersonResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var teams = await _teams.ListAsync(organizationId, cancellationToken);
        var scope = await _managerScope.ResolveAsync(_currentUser, OrgWideRoles, cancellationToken);
        var (items, total) = scope is null
            ? await _people.ListPagedAsync(organizationId, search, page, pageSize, cancellationToken)
            : await _people.ListPagedScopedAsync(organizationId, search, page, pageSize, scope.PersonIds, cancellationToken);
        return Result<PagedResult<PersonResponse>>.Success(new PagedResult<PersonResponse>(items.Select(person => Map(person, teams)).ToList(), total, page, pageSize));
    }

    public async Task<Result<PersonResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.PeopleViewers);
        if (access.IsFailure) return Result<PersonResponse>.Failure(access.Error!);

        var person = await _people.GetAsync(_currentUser.OrganizationId, id, cancellationToken);
        if (person is null) return Result<PersonResponse>.Failure(Error.NotFound("Pracownik nie istnieje."));

        var scope = await _managerScope.ResolveAsync(_currentUser, OrgWideRoles, cancellationToken);
        if (scope is not null && !scope.ContainsPerson(person.Id))
        {
            return Result<PersonResponse>.Failure(Error.NotFound("Pracownik nie istnieje."));
        }

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

            var referenceError = await ValidateReferencesAsync(organizationId, request.TeamId, request.ManagerId, null, cancellationToken);
            if (referenceError is not null) return Result<PersonResponse>.Failure(referenceError);

            var locationResult = await _locationResolver.ResolveAsync(organizationId, request.Location, cancellationToken);
            if (locationResult.IsFailure) return Result<PersonResponse>.Failure(locationResult.Error!);
            var person = new Person(organizationId, request.FirstName, request.LastName, request.Email);
            person.Update(request.FirstName, request.LastName, request.Email, request.Phone, request.EmployeeNumber, request.RelationType, request.JobTitle, request.TeamId, request.ManagerId, locationResult.Value!.FullPath, request.CostCenter);
            person.SetLocation(locationResult.Value!.Id, locationResult.Value.FullPath);
            person.SetPreferredLanguage(request.PreferredLanguage);
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

            var referenceError = await ValidateReferencesAsync(organizationId, request.TeamId, request.ManagerId, id, cancellationToken);
            if (referenceError is not null) return Result<PersonResponse>.Failure(referenceError);
            var locationResult = await _locationResolver.ResolveAsync(organizationId, request.Location, cancellationToken);
            if (locationResult.IsFailure) return Result<PersonResponse>.Failure(locationResult.Error!);

            person.Update(request.FirstName, request.LastName, request.Email, request.Phone, request.EmployeeNumber, request.RelationType, request.JobTitle, request.TeamId, request.ManagerId, locationResult.Value!.FullPath, request.CostCenter);
            person.SetLocation(locationResult.Value!.Id, locationResult.Value.FullPath);
            person.SetPreferredLanguage(request.PreferredLanguage);
            if (!request.IsActive)
            {
                person.Deactivate(_clock.UtcNow);
            }
            else if (person.EmploymentStatus == EmploymentStatus.Inactive)
            {
                person.Activate();
            }

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

    public async Task<Result<PersonResponse>> StartOffboardingAsync(Guid id, StartOffboardingRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr);
        if (access.IsFailure) return Result<PersonResponse>.Failure(access.Error!);

        try
        {
            var organizationId = _currentUser.OrganizationId;
            var person = await _people.GetAsync(organizationId, id, cancellationToken);
            if (person is null) return Result<PersonResponse>.Failure(Error.NotFound("Pracownik nie istnieje."));

            person.StartOffboarding(request.EmploymentEndsAt);

            var assets = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);
            foreach (var asset in assets.Where(a => a.AssignedPersonId == person.Id && a.Status == AssetStatus.Assigned))
            {
                asset.MarkPendingReturn();
            }

            _activity.Add(new ActivityLog(organizationId, "person.offboarding_started", "person", person.Id, _currentUser.Subject, person.FullName, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await GetAsync(id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<PersonResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    private async Task<Error?> ValidateReferencesAsync(Guid organizationId, Guid? teamId, Guid? managerId, Guid? excludingPersonId, CancellationToken cancellationToken)
    {
        if (teamId.HasValue && await _teams.GetAsync(organizationId, teamId.Value, cancellationToken) is null)
        {
            return Error.Validation("Wybrany zespół nie istnieje.");
        }

        if (managerId.HasValue)
        {
            if (managerId.Value == excludingPersonId)
            {
                return Error.Validation("Osoba nie może być swoim własnym przełożonym.");
            }

            if (await _people.GetAsync(organizationId, managerId.Value, cancellationToken) is null)
            {
                return Error.Validation("Wybrany przełożony nie istnieje.");
            }
        }

        return null;
    }

    private static PersonResponse Map(Person person, IReadOnlyList<Team> teams)
    {
        var team = person.TeamId.HasValue ? teams.FirstOrDefault(x => x.Id == person.TeamId.Value) : null;
        return new PersonResponse(person.Id, person.FirstName, person.LastName, person.FullName, person.Email, person.Phone, person.EmployeeNumber, person.RelationType, person.JobTitle, person.TeamId, team?.Name, person.ManagerId, person.Location, person.CostCenter, person.IsActive, person.EmploymentStatus, person.EmploymentEndsAt, person.DeactivatedAt, person.PreferredLanguage);
    }
}
