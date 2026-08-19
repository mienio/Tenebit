using Tenebit.Application.Abstractions;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Organizations;

namespace Tenebit.Application.Evidence;

/// <summary>Redaguje materiał dowodowy starszy niż retencja ustawiona dla organizacji, pomijając rekordy objęte legal hold.</summary>
public sealed class EvidenceRetentionService
{
    private const int BatchSize = 500;
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

        var cutoff = _clock.UtcNow.AddMonths(-organization.DefaultEvidenceRetentionMonths.Value);
        while (!cancellationToken.IsCancellationRequested)
        {
            var processed = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                // SQL performs the cutoff/legal-hold filter and returns metadata only. Work is capped so a
                // backlog cannot create a huge parameter list, allocation spike or one hours-long transaction.
                var due = await _evidence.ListRetentionCandidatesAsync(organization.Id, cutoff, BatchSize, ct);
                if (due.Count == 0) return 0;

                var now = _clock.UtcNow;
                var ids = due.Select(x => x.Id).ToArray();
                var redacted = await _evidence.RedactAsync(organization.Id, ids, now, ct);
                if (redacted == 0) return due.Count;

                foreach (var item in due)
                {
                    _activity.Add(new ActivityLog(organization.Id, "asset_evidence.redacted", "asset_evidence", item.Id, "system", item.FileName, now));
                }

                await _unitOfWork.SaveChangesAsync(ct);
                return due.Count;
            }, cancellationToken);

            if (processed < BatchSize) return;
        }
    }
}
