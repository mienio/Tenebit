using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Procedures;

namespace Tenebit.Application.Assignments;

/// <summary>Buduje response DTO dla wydań (widok wewnętrzny i publiczny kanał pracownika) — wydzielone
/// z AssignmentService (audyt P2 #4), analogicznie do OffboardingResponseBuilder.</summary>
public sealed class AssignmentResponseBuilder
{
    private readonly IAssignmentRepository _assignments;
    private readonly IPersonRepository _people;
    private readonly IAssetRepository _assets;
    private readonly IProcedureRepository _procedures;
    private readonly IAssetEvidenceRepository _evidence;
    private readonly IOrganizationRepository _organizations;

    public AssignmentResponseBuilder(IAssignmentRepository assignments, IPersonRepository people, IAssetRepository assets,
        IProcedureRepository procedures, IAssetEvidenceRepository evidence, IOrganizationRepository organizations)
    {
        _assignments = assignments;
        _people = people;
        _assets = assets;
        _procedures = procedures;
        _evidence = evidence;
        _organizations = organizations;
    }

    // Used after a mutation (create/accept/return) that already ran its own, narrower role check —
    // callers must not re-apply the broader read-role gate from GetAsync, or an "employee" accepting
    // their own assignment would succeed the accept but then get a 403 back.
    public async Task<Result<AssignmentResponse>> BuildResponseAsync(Guid organizationId, Guid id, CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(organizationId, id, cancellationToken);
        if (assignment is null) return Result<AssignmentResponse>.Failure(Error.NotFound("Wydanie nie istnieje."));
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var assets = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);
        var procedures = await _procedures.ListAsync(organizationId, null, cancellationToken);
        var evidence = await _evidence.ListByAssignmentAsync(organizationId, id, cancellationToken);
        return Result<AssignmentResponse>.Success(Map(assignment, people, assets, procedures, evidence));
    }

    public async Task<PublicAssignmentResponse> MapPublicAsync(Guid organizationId, Assignment assignment, CancellationToken cancellationToken)
    {
        var person = await _people.GetAsync(organizationId, assignment.PersonId, cancellationToken);
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        var assetIds = assignment.Assets.Select(x => x.AssetId).ToArray();
        var assets = await _assets.GetByIdsAsync(organizationId, assetIds, cancellationToken);
        var procedureIds = assignment.ProcedureAcceptances.Select(x => x.ProcedureId).ToArray();
        var procedures = await _procedures.GetByIdsAsync(organizationId, procedureIds, cancellationToken);

        // Zdjęcia wydania są pokazywane pracownikowi przed potwierdzeniem (spec 6.2/6.7) — wyłącznie faza Issue.
        var evidence = await _evidence.ListByAssignmentAsync(organizationId, assignment.Id, cancellationToken);
        var issueEvidenceByAsset = evidence
            .Where(x => x.Phase == EvidencePhase.Issue)
            .GroupBy(x => x.AssetId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Id).Select(x => x.Id).ToList());

        var assetRows = assignment.Assets.Select(item =>
        {
            var asset = assets.FirstOrDefault(x => x.Id == item.AssetId);
            var evidenceIds = issueEvidenceByAsset.TryGetValue(item.AssetId, out var ids) ? ids : [];
            return new PublicAssignmentAssetResponse(asset?.Name ?? "—", asset?.AssetTag ?? "—", item.IssueCondition, item.AssetId, evidenceIds);
        }).ToList();
        var procedureRows = procedures
            .Where(x => x.RequiresAcceptance)
            .Select(x => new PublicAssignmentProcedureResponse(
                x.Id,
                x.Title,
                x.Version,
                x.Documents.Select(doc => new PublicAssignmentDocumentResponse(doc.Id, doc.FileName)).ToList()))
            .ToList();

        return new PublicAssignmentResponse(
            organization?.Name ?? "Tenebit",
            assignment.ProtocolNumber,
            assignment.Status,
            person?.FirstName ?? "—",
            assetRows,
            procedureRows);
    }

    public static AssignmentResponse Map(Assignment assignment, IReadOnlyList<Domain.People.Person> people, IReadOnlyList<Asset> assets, IReadOnlyList<Procedure> procedures, IReadOnlyList<AssetEvidence>? evidence = null)
    {
        var person = people.FirstOrDefault(x => x.Id == assignment.PersonId);
        var items = assignment.Assets.Select(item =>
        {
            var asset = assets.FirstOrDefault(x => x.Id == item.AssetId);
            return new AssignmentAssetResponse(item.AssetId, asset?.Name, asset?.AssetTag, item.IssueCondition, item.ReturnCondition, item.ReturnedAt, item.ReturnLocation, item.ReturnedBy, item.ReturnResolution, item.ReturnNotes);
        }).ToList();

        var acceptances = assignment.ProcedureAcceptances.Select(acceptance =>
        {
            var procedure = procedures.FirstOrDefault(x => x.Id == acceptance.ProcedureId);
            return new ProcedureAcceptanceResponse(acceptance.Id, acceptance.ProcedureId, procedure?.Title, acceptance.Status, acceptance.SentAt, acceptance.AcceptedAt, acceptance.ConfirmedIp, acceptance.ConfirmationHash, acceptance.VerifyIntegrity());
        }).ToList();

        var assignmentEvidence = evidence?.Where(x => x.AssignmentId == assignment.Id).ToList();
        return new AssignmentResponse(assignment.Id, assignment.PersonId, person?.FullName, assignment.Status, assignment.IssuedAt, assignment.DueDate, assignment.AcceptedAt, assignment.ReturnedAt, assignment.ProtocolNumber, assignment.Notes, items, acceptances, assignment.AcceptedIp, assignment.AcceptanceHash, assignment.VerifyIntegrity(assignmentEvidence));
    }
}
