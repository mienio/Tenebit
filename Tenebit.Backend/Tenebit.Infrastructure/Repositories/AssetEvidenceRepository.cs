using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Evidence;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AssetEvidenceRepository : IAssetEvidenceRepository
{
    private readonly TenebitDbContext _db;
    public AssetEvidenceRepository(TenebitDbContext db) => _db = db;

    // Intentionally loads Content for the return evidence workflow. The query is narrowly scoped to one offboarding item.
    public async Task<IReadOnlyList<AssetEvidence>> ListContentByOffboardingItemAsync(Guid organizationId, Guid offboardingItemId, CancellationToken cancellationToken) =>
        await _db.AssetEvidence
            .Where(x => x.OrganizationId == organizationId && x.OffboardingItemId == offboardingItemId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AssetEvidenceMetadata>> ListMetadataByAssetAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken) =>
        await MetadataQuery(organizationId, x => x.AssetId == assetId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AssetEvidenceMetadata>> ListMetadataByAssignmentIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> assignmentIds, CancellationToken cancellationToken)
    {
        if (assignmentIds.Count == 0) return [];
        return await _db.AssetEvidence.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.AssignmentId.HasValue && assignmentIds.Contains(x.AssignmentId.Value))
            .Select(AssetEvidenceMetadataProjection.Select)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AssetEvidenceMetadata>> ListMetadataByAssignmentAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken) =>
        await MetadataQuery(organizationId, x => x.AssignmentId == assignmentId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AssetEvidenceRetentionCandidate>> ListRetentionCandidatesAsync(Guid organizationId, DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken) =>
        await _db.AssetEvidence.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && !x.LegalHold && x.RedactedAt == null && x.UploadedAt <= cutoff)
            .OrderBy(x => x.UploadedAt)
            .Select(x => new AssetEvidenceRetentionCandidate(x.Id, x.FileName))
            .Take(Math.Clamp(batchSize, 1, 2_000))
            .ToListAsync(cancellationToken);

    public async Task<int> RedactAsync(Guid organizationId, IReadOnlyCollection<Guid> evidenceIds, DateTimeOffset redactedAt, CancellationToken cancellationToken)
    {
        if (evidenceIds.Count == 0) return 0;
        return await _db.AssetEvidence
            .Where(x => x.OrganizationId == organizationId && evidenceIds.Contains(x.Id) && !x.LegalHold && x.RedactedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Content, Array.Empty<byte>())
                .SetProperty(x => x.SizeBytes, 0L)
                .SetProperty(x => x.RedactedAt, redactedAt), cancellationToken);
    }

    public Task<AssetEvidence?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.AssetEvidence.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<int> CountAsync(Guid organizationId, Guid assetId, EvidencePhase phase, CancellationToken cancellationToken) =>
        _db.AssetEvidence.CountAsync(x => x.OrganizationId == organizationId && x.AssetId == assetId && x.Phase == phase, cancellationToken);

    public void Add(AssetEvidence evidence) => _db.AssetEvidence.Add(evidence);
    public void Remove(AssetEvidence evidence) => _db.AssetEvidence.Remove(evidence);

    // Filtr musi trafic na encje, PRZED projekcja. Gdy `.Where(...)` szedl na juz zrzutowany
    // AssetEvidenceMetadata, EF nie umial przetlumaczyc wyrazenia i rzucal InvalidOperationException
    // w runtime - co wywalalo tworzenie wydania (500) i cala publiczna strone akceptacji dla
    // pracownika. Sasiednie ListMetadataByAssignmentIdsAsync zawsze filtrowalo poprawnie i dlatego
    // dzialalo; teraz wszystkie sciezki robia to tak samo.
    private IQueryable<AssetEvidenceMetadata> MetadataQuery(
        Guid organizationId,
        System.Linq.Expressions.Expression<Func<AssetEvidence, bool>> filter) =>
        _db.AssetEvidence.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Where(filter)
            .Select(AssetEvidenceMetadataProjection.Select);
}
