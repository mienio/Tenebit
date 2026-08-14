using Tenebit.Application.Abstractions;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Organizations;

namespace Tenebit.Application.Evidence;

/// <summary>Redaguje materiał dowodowy starszy niż retencja ustawiona dla organizacji, pomijając rekordy objęte legal hold.</summary>
public sealed class EvidenceRetentionService
{
    private readonly IOrganizationRepository _organizations;
    private readonly IAssetEvidenceRepository _evidence;
    private readonly IActivityLogRepository _activity;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public EvidenceRetentionService(IOrganizationRepository organizations, IAssetEvidenceRepository evidence, IActivityLogRepository activity, IClock clock, IUnitOfWork unitOfWork)
    {
        _organizations = organizations;
        _evidence = evidence;
        _activity = activity;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        foreach (var organization in await _organizations.ListAllAsync(cancellationToken))
        {
            await ProcessOrganizationAsync(organization, cancellationToken);
        }
    }

    private async Task ProcessOrganizationAsync(Organization organization, CancellationToken cancellationToken)
    {
        if (!organization.DefaultEvidenceRetentionMonths.HasValue) return;

        var now = _clock.UtcNow;
        var cutoff = now.AddMonths(-organization.DefaultEvidenceRetentionMonths.Value);
        var due = (await _evidence.ListByOrganizationAsync(organization.Id, cancellationToken))
            .Where(e => !e.LegalHold && e.RedactedAt is null && e.UploadedAt <= cutoff)
            .ToList();
        if (due.Count == 0) return;

        var hasChanges = false;
        foreach (var item in due)
        {
            if (!item.Redact(now)) continue;
            _activity.Add(new ActivityLog(organization.Id, "asset_evidence.redacted", "asset_evidence", item.Id, "system", item.FileName, now));
            hasChanges = true;
        }

        if (hasChanges)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
