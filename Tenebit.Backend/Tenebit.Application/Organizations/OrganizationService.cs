using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;

namespace Tenebit.Application.Organizations;

public sealed class OrganizationService
{
    private readonly IOrganizationRepository _organizations;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionService _permissions;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public OrganizationService(IOrganizationRepository organizations, IActivityLogRepository activity, ICurrentUser currentUser, IPermissionService permissions, IClock clock, IUnitOfWork unitOfWork)
    {
        _organizations = organizations;
        _activity = activity;
        _currentUser = currentUser;
        _permissions = permissions;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<OrganizationResponse>> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var access = await _permissions.EnsureAsync(PermissionModules.Settings, PermissionActions.View, cancellationToken);
        if (access.IsFailure) return Result<OrganizationResponse>.Failure(access.Error!);

        var organization = await _organizations.GetAsync(_currentUser.OrganizationId, cancellationToken);
        return organization is null
            ? Result<OrganizationResponse>.Failure(Error.NotFound("Organizacja nie istnieje."))
            : Result<OrganizationResponse>.Success(new OrganizationResponse(organization.Id, organization.Name, organization.Country, organization.Language, organization.Currency, organization.TimeZone, organization.LogoUrl));
    }

    public async Task<Result<OrganizationResponse>> UpdateCurrentAsync(UpdateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var access = await _permissions.EnsureAsync(PermissionModules.Settings, PermissionActions.Manage, cancellationToken);
        if (access.IsFailure) return Result<OrganizationResponse>.Failure(access.Error!);

        try
        {
            var organization = await _organizations.GetAsync(_currentUser.OrganizationId, cancellationToken);
            if (organization is null) return Result<OrganizationResponse>.Failure(Error.NotFound("Organizacja nie istnieje."));
            organization.UpdateProfile(request.Name, request.Country, request.Language, request.Currency, request.TimeZone, request.LogoUrl);
            _activity.Add(new ActivityLog(organization.Id, "organization.updated", "organization", organization.Id, _currentUser.Subject, organization.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<OrganizationResponse>.Success(new OrganizationResponse(organization.Id, organization.Name, organization.Country, organization.Language, organization.Currency, organization.TimeZone, organization.LogoUrl));
        }
        catch (DomainException ex)
        {
            return Result<OrganizationResponse>.Failure(Error.Validation(ex.Message));
        }
    }
}
