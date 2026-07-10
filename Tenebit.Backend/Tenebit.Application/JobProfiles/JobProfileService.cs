using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.JobProfiles;

namespace Tenebit.Application.JobProfiles;

public sealed class JobProfileService
{
    private readonly IJobProfileRepository _profiles;
    private readonly IAssetCategoryRepository _categories;
    private readonly IProcedureRepository _procedures;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public JobProfileService(IJobProfileRepository profiles, IAssetCategoryRepository categories, IProcedureRepository procedures, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork)
    {
        _profiles = profiles;
        _categories = categories;
        _procedures = procedures;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<JobProfileResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var profiles = await _profiles.ListAsync(_currentUser.OrganizationId, cancellationToken);
        return profiles.Select(Map).ToList();
    }

    public async Task<Result<JobProfileResponse>> CreateAsync(SaveJobProfileRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.ProcedureManager);
        if (access.IsFailure) return Result<JobProfileResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var validation = await ValidateReferencesAsync(organizationId, request, cancellationToken);
            if (validation.IsFailure) return Result<JobProfileResponse>.Failure(validation.Error!);
            if (await _profiles.NameExistsAsync(organizationId, request.Name, null, cancellationToken)) return Result<JobProfileResponse>.Failure(Error.Conflict("Zestaw stanowiskowy o tej nazwie już istnieje."));
            var profile = new JobProfile(organizationId, request.Name, request.Description, request.DefaultManagerId);
            profile.SetAssetCategories(request.AssetCategoryIds);
            profile.SetProcedures(request.ProcedureIds);
            _profiles.Add(profile);
            _activity.Add(new ActivityLog(organizationId, "job_profile.created", "job_profile", profile.Id, _currentUser.Subject, profile.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<JobProfileResponse>.Success(Map(profile));
        }
        catch (DomainException ex) { return Result<JobProfileResponse>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result<JobProfileResponse>> UpdateAsync(Guid id, SaveJobProfileRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.ProcedureManager);
        if (access.IsFailure) return Result<JobProfileResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var profile = await _profiles.GetAsync(organizationId, id, cancellationToken);
            if (profile is null) return Result<JobProfileResponse>.Failure(Error.NotFound("Zestaw stanowiskowy nie istnieje."));
            var validation = await ValidateReferencesAsync(organizationId, request, cancellationToken);
            if (validation.IsFailure) return Result<JobProfileResponse>.Failure(validation.Error!);
            if (await _profiles.NameExistsAsync(organizationId, request.Name, id, cancellationToken)) return Result<JobProfileResponse>.Failure(Error.Conflict("Zestaw stanowiskowy o tej nazwie już istnieje."));
            profile.Update(request.Name, request.Description, request.DefaultManagerId);
            profile.SetAssetCategories(request.AssetCategoryIds);
            profile.SetProcedures(request.ProcedureIds);
            _activity.Add(new ActivityLog(organizationId, "job_profile.updated", "job_profile", profile.Id, _currentUser.Subject, profile.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<JobProfileResponse>.Success(Map(profile));
        }
        catch (DomainException ex) { return Result<JobProfileResponse>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return access;
        var organizationId = _currentUser.OrganizationId;
        var profile = await _profiles.GetAsync(organizationId, id, cancellationToken);
        if (profile is null) return Result.Failure(Error.NotFound("Zestaw stanowiskowy nie istnieje."));
        _profiles.Remove(profile);
        _activity.Add(new ActivityLog(organizationId, "job_profile.deleted", "job_profile", profile.Id, _currentUser.Subject, profile.Name, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> ValidateReferencesAsync(Guid organizationId, SaveJobProfileRequest request, CancellationToken cancellationToken)
    {
        var categories = await _categories.ListAsync(organizationId, cancellationToken);
        if (request.AssetCategoryIds.Any(id => categories.All(x => x.Id != id))) return Result.Failure(Error.Validation("Zestaw zawiera kategorię, która nie istnieje."));
        var procedures = await _procedures.GetByIdsAsync(organizationId, request.ProcedureIds.Distinct().ToArray(), cancellationToken);
        if (procedures.Count != request.ProcedureIds.Distinct().Count()) return Result.Failure(Error.Validation("Zestaw zawiera procedurę, która nie istnieje."));
        return Result.Success();
    }

    private static JobProfileResponse Map(JobProfile profile) => new(profile.Id, profile.Name, profile.Description, profile.DefaultManagerId, profile.AssetCategories.Select(x => x.AssetCategoryId).ToArray(), profile.Procedures.Select(x => x.ProcedureId).ToArray(), profile.CreatedAt);
}
