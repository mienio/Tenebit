using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.People;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.People;

public sealed class TeamService
{
    private readonly ITeamRepository _teams;
    private readonly IPersonRepository _people;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public TeamService(ITeamRepository teams, IPersonRepository people, ISubscriptionRepository subscriptions, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork)
    {
        _teams = teams;
        _people = people;
        _subscriptions = subscriptions;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<TeamResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var teams = await _teams.ListAsync(_currentUser.OrganizationId, cancellationToken);
        return teams.Select(Map).ToList();
    }

    public async Task<Result<TeamResponse>> CreateAsync(CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr);
        if (access.IsFailure) return Result<TeamResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            if (await _teams.NameExistsAsync(organizationId, request.Name, null, cancellationToken)) return Result<TeamResponse>.Failure(Error.Conflict("Zespół o tej nazwie już istnieje."));
            if (request.ManagerId.HasValue && await _people.GetAsync(organizationId, request.ManagerId.Value, cancellationToken) is null)
            {
                return Result<TeamResponse>.Failure(Error.Validation("Wybrany przełożony nie istnieje."));
            }
            var team = new Team(organizationId, request.Name, request.ManagerId, request.CostCenter);

            var subscription = await _subscriptions.GetByOrganizationAsync(organizationId, cancellationToken);
            if (subscription is null)
            {
                subscription = new OrganizationSubscription(organizationId, SubscriptionPlan.Free.Key);
                _subscriptions.Add(subscription);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var limit = subscription.GetResourceLimit();

            var withinLimit = await _unitOfWork.ExecuteWithResourceLocksAsync(
                organizationId,
                "team-capacity",
                [organizationId],
                async ct =>
            {
                var currentCount = (await _teams.ListAsync(organizationId, ct)).Count;
                if (currentCount >= limit) return false;

                _teams.Add(team);
                _activity.Add(new ActivityLog(organizationId, "team.created", "team", team.Id, _currentUser.Subject, team.Name, _clock.UtcNow));
                await _unitOfWork.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);

            if (!withinLimit)
            {
                var plan = SubscriptionPlan.FromKey(subscription.PlanKey) ?? SubscriptionPlan.Free;
                return Result<TeamResponse>.Failure(Error.Validation($"Limit zespołów przekroczony. Plan {plan.Name} pozwala na {limit} zespołów. Przejdź na wyższy plan."));
            }

            return Result<TeamResponse>.Success(Map(team));
        }
        catch (DomainException ex) { return Result<TeamResponse>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result<TeamResponse>> UpdateAsync(Guid id, UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr);
        if (access.IsFailure) return Result<TeamResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var team = await _teams.GetAsync(organizationId, id, cancellationToken);
            if (team is null) return Result<TeamResponse>.Failure(Error.NotFound("Zespół nie istnieje."));
            if (await _teams.NameExistsAsync(organizationId, request.Name, id, cancellationToken)) return Result<TeamResponse>.Failure(Error.Conflict("Zespół o tej nazwie już istnieje."));
            if (request.ManagerId.HasValue && await _people.GetAsync(organizationId, request.ManagerId.Value, cancellationToken) is null)
            {
                return Result<TeamResponse>.Failure(Error.Validation("Wybrany przełożony nie istnieje."));
            }
            team.Update(request.Name, request.ManagerId, request.CostCenter);
            _activity.Add(new ActivityLog(organizationId, "team.updated", "team", team.Id, _currentUser.Subject, team.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<TeamResponse>.Success(Map(team));
        }
        catch (DomainException ex) { return Result<TeamResponse>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr);
        if (access.IsFailure) return access;
        var organizationId = _currentUser.OrganizationId;
        var team = await _teams.GetAsync(organizationId, id, cancellationToken);
        if (team is null) return Result.Failure(Error.NotFound("Zespół nie istnieje."));
        if (await _teams.IsUsedAsync(organizationId, id, cancellationToken))
        {
            return Result.Failure(Error.Conflict("Nie można usunąć zespołu przypisanego do osób lub aktywów."));
        }
        _teams.Remove(team);
        _activity.Add(new ActivityLog(organizationId, "team.deleted", "team", team.Id, _currentUser.Subject, team.Name, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static TeamResponse Map(Team team) => new(team.Id, team.Name, team.ManagerId, team.CostCenter);
}
