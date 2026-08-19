using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;

namespace Tenebit.Application.Assets;

public sealed class AssetInspectionService
{
    private readonly IAssetInspectionRepository _inspections;
    private readonly IAssetRepository _assets;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AssetAuthorizationService _assetAuthorization;

    public AssetInspectionService(IAssetInspectionRepository inspections, IAssetRepository assets, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, AssetAuthorizationService assetAuthorization)
    {
        _inspections = inspections;
        _assets = assets;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _assetAuthorization = assetAuthorization;
    }

    public async Task<Result<AssetInspectionResponse>> GetPendingForAssetAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<AssetInspectionResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var assetAccess = await _assetAuthorization.EnsureCanViewAsync(assetId, cancellationToken);
        if (assetAccess.IsFailure) return Result<AssetInspectionResponse>.Failure(assetAccess.Error!);
        var inspection = await _inspections.GetPendingByAssetAsync(organizationId, assetId, cancellationToken);
        if (inspection is null) return Result<AssetInspectionResponse>.Failure(Error.NotFound("Brak oczekującej kontroli dla tego aktywa."));
        return Result<AssetInspectionResponse>.Success(Map(inspection));
    }

    public async Task<Result<AssetInspectionResponse>> CompleteAsync(Guid id, CompleteAssetInspectionRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Technician);
        if (access.IsFailure) return Result<AssetInspectionResponse>.Failure(access.Error!);

        try
        {
            var organizationId = _currentUser.OrganizationId;
            var inspection = await _inspections.GetAsync(organizationId, id, cancellationToken);
            if (inspection is null) return Result<AssetInspectionResponse>.Failure(Error.NotFound("Kontrola nie istnieje."));

            if (inspection.IsCompleted)
            {
                return Result<AssetInspectionResponse>.Success(Map(inspection));
            }

            var asset = await _assets.GetAsync(organizationId, inspection.AssetId, cancellationToken);
            if (asset is null) return Result<AssetInspectionResponse>.Failure(Error.NotFound("Aktywo nie istnieje."));

            var now = _clock.UtcNow;
            inspection.Complete(request.Outcome, request.SerialNumberMatched, request.AccessoriesComplete, request.DataWiped, request.FunctionalTestPassed, request.DamageAssessmentNotes, request.Notes, now, _currentUser.Subject);

            asset.ChangeStatus(request.Outcome switch
            {
                InspectionOutcome.ReadyForReuse => AssetStatus.InStock,
                InspectionOutcome.Damaged => AssetStatus.Damaged,
                InspectionOutcome.Retired => AssetStatus.Retired,
                InspectionOutcome.Disposed => AssetStatus.Disposed,
                _ => asset.Status
            });

            _activity.Add(new ActivityLog(organizationId, "asset.inspection_completed", "asset", asset.Id, _currentUser.Subject, asset.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<AssetInspectionResponse>.Success(Map(inspection));
        }
        catch (DomainException ex)
        {
            return Result<AssetInspectionResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    private static AssetInspectionResponse Map(AssetInspection inspection) => new(
        inspection.Id,
        inspection.AssetId,
        inspection.AssignmentId,
        inspection.CreatedAt,
        inspection.CreatedBy,
        inspection.SerialNumberMatched,
        inspection.AccessoriesComplete,
        inspection.DataWiped,
        inspection.FunctionalTestPassed,
        inspection.DamageAssessmentNotes,
        inspection.Outcome,
        inspection.Notes,
        inspection.CompletedAt,
        inspection.CompletedBy);
}
