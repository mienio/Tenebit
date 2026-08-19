using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assets;

namespace Tenebit.Application.Assets;

public enum AssetReturnDisposition
{
    DirectToStock,
    InspectionRequired,
    ReturnToVendor,
    Disposed
}

/// <summary>Zwrot fizyczny aktywa wg polityki kategorii (DirectToStock/InspectionRequired/ReturnToVendor/Dispose) -
/// wydzielone z <see cref="Tenebit.Application.Assignments.AssignmentService"/>, żeby ta sama logika mogła być
/// reużyta przez offboarding bez duplikacji.</summary>
public sealed class AssetReturnDispositionService
{
    private readonly IAssetInspectionRepository _inspections;

    public AssetReturnDispositionService(IAssetInspectionRepository inspections)
    {
        _inspections = inspections;
    }

    /// <summary>Stosuje politykę zwrotu do aktywa (zmienia jego status, ewentualnie tworzy AssetInspection) i
    /// zwraca jaka dyspozycja została zastosowana, żeby wołający mógł odpowiednio rozliczyć swoją pozycję
    /// (np. Assignment.AssignmentAsset albo OffboardingItem).</summary>
    public AssetReturnDisposition ApplyPhysicalReturn(Asset asset, AssetCategory? category, string? returnLocation, Guid organizationId,
        DateTimeOffset now, string createdBy, Guid? assignmentId, Guid? offboardingItemId)
    {
        var disposition = category?.PostReturnDisposition ?? PostReturnDisposition.Reuse;
        switch (disposition)
        {
            case PostReturnDisposition.ReturnToVendor:
                asset.ReleaseAssignment(AssetStatus.InTransit, returnLocation);
                return AssetReturnDisposition.ReturnToVendor;
            case PostReturnDisposition.Dispose:
                asset.ReleaseAssignment(AssetStatus.InService, returnLocation);
                return AssetReturnDisposition.Disposed;
            default: // Reuse
                if (category?.ReturnHandlingMode == ReturnHandlingMode.InspectionRequired)
                {
                    asset.ReleaseAssignment(AssetStatus.InService, returnLocation);
                    _inspections.Add(new AssetInspection(organizationId, asset.Id, assignmentId, now, createdBy, offboardingItemId));
                    return AssetReturnDisposition.InspectionRequired;
                }

                asset.ReleaseAssignment(AssetStatus.InStock, returnLocation);
                return AssetReturnDisposition.DirectToStock;
        }
    }
}
