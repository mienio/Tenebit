using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.People;

namespace Tenebit.Application.People;

public sealed class PersonRelationTypeService
{
    private readonly IPersonRelationTypeRepository _relationTypes;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public PersonRelationTypeService(IPersonRelationTypeRepository relationTypes, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork)
    {
        _relationTypes = relationTypes;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<PersonRelationTypeResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var items = await _relationTypes.ListAsync(_currentUser.OrganizationId, cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<Result<PersonRelationTypeResponse>> CreateAsync(CreatePersonRelationTypeRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr);
        if (access.IsFailure) return Result<PersonRelationTypeResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            if (await _relationTypes.NameExistsAsync(organizationId, request.Name, null, cancellationToken))
            {
                return Result<PersonRelationTypeResponse>.Failure(Error.Conflict("Typ relacji o tej nazwie już istnieje."));
            }
            var existing = await _relationTypes.ListAsync(organizationId, cancellationToken);
            var sortOrder = existing.Count == 0 ? 10 : existing.Max(x => x.SortOrder) + 10;
            var relationType = new PersonRelationType(organizationId, request.Name, sortOrder);
            _relationTypes.Add(relationType);
            _activity.Add(new ActivityLog(organizationId, "person_relation_type.created", "person_relation_type", relationType.Id, _currentUser.Subject, relationType.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<PersonRelationTypeResponse>.Success(Map(relationType));
        }
        catch (DomainException ex) { return Result<PersonRelationTypeResponse>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result<PersonRelationTypeResponse>> UpdateAsync(Guid id, UpdatePersonRelationTypeRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr);
        if (access.IsFailure) return Result<PersonRelationTypeResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var relationType = await _relationTypes.GetAsync(organizationId, id, cancellationToken);
            if (relationType is null) return Result<PersonRelationTypeResponse>.Failure(Error.NotFound("Typ relacji nie istnieje."));
            if (await _relationTypes.NameExistsAsync(organizationId, request.Name, id, cancellationToken))
            {
                return Result<PersonRelationTypeResponse>.Failure(Error.Conflict("Typ relacji o tej nazwie już istnieje."));
            }
            relationType.Update(request.Name);
            _activity.Add(new ActivityLog(organizationId, "person_relation_type.updated", "person_relation_type", relationType.Id, _currentUser.Subject, relationType.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<PersonRelationTypeResponse>.Success(Map(relationType));
        }
        catch (DomainException ex) { return Result<PersonRelationTypeResponse>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr);
        if (access.IsFailure) return access;
        var organizationId = _currentUser.OrganizationId;
        var relationType = await _relationTypes.GetAsync(organizationId, id, cancellationToken);
        if (relationType is null) return Result.Failure(Error.NotFound("Typ relacji nie istnieje."));
        if (await _relationTypes.IsUsedAsync(organizationId, relationType.Name, cancellationToken))
        {
            return Result.Failure(Error.Conflict("Nie można usunąć typu relacji przypisanego do osób."));
        }
        _relationTypes.Remove(relationType);
        _activity.Add(new ActivityLog(organizationId, "person_relation_type.deleted", "person_relation_type", relationType.Id, _currentUser.Subject, relationType.Name, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static PersonRelationTypeResponse Map(PersonRelationType relationType) => new(relationType.Id, relationType.Name);
}
