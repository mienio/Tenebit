namespace Tenebit.Domain.Evidence;

public sealed record AssetEvidenceIntegrityEntry(Guid Id, EvidencePhase Phase, string Sha256);
