using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assignments;

namespace Tenebit.Application.Assignments;

/// <summary>Buduje dane wejściowe do generatora PDF protokołu wydania — wydzielone z AssignmentService
/// (audyt P2 #4), analogicznie do OffboardingProtocolModelBuilder. Sam generator (IPdfProtocolGenerator)
/// zostaje wywoływany przez AssignmentService, ta klasa odpowiada wyłącznie za zebranie i zmapowanie danych.</summary>
public sealed class AssignmentProtocolModelBuilder
{
    private readonly IAssignmentRepository _assignments;
    private readonly IPersonRepository _people;
    private readonly ITeamRepository _teams;
    private readonly IOrganizationRepository _organizations;
    private readonly IAssetRepository _assets;
    private readonly IProcedureRepository _procedures;

    public AssignmentProtocolModelBuilder(IAssignmentRepository assignments, IPersonRepository people, ITeamRepository teams,
        IOrganizationRepository organizations, IAssetRepository assets, IProcedureRepository procedures)
    {
        _assignments = assignments;
        _people = people;
        _teams = teams;
        _organizations = organizations;
        _assets = assets;
        _procedures = procedures;
    }

    public async Task<Result<ProtocolPdfModel>> BuildAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(organizationId, assignmentId, cancellationToken);
        if (assignment is null) return Result<ProtocolPdfModel>.Failure(Error.NotFound("Wydanie nie istnieje."));

        var person = await _people.GetAsync(organizationId, assignment.PersonId, cancellationToken);
        var team = person?.TeamId.HasValue == true ? await _teams.GetAsync(organizationId, person.TeamId!.Value, cancellationToken) : null;
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);

        var assetIds = assignment.Assets.Select(x => x.AssetId).ToArray();
        var assets = await _assets.GetByIdsAsync(organizationId, assetIds, cancellationToken);
        var procedureIds = assignment.ProcedureAcceptances.Select(x => x.ProcedureId).ToArray();
        var procedures = await _procedures.GetByIdsAsync(organizationId, procedureIds, cancellationToken);

        var assetRows = assignment.Assets.Select(item =>
        {
            var asset = assets.FirstOrDefault(x => x.Id == item.AssetId);
            return new ProtocolPdfAssetRow(asset?.Name ?? "—", asset?.AssetTag ?? "—", asset?.SerialNumber, item.IssueCondition, item.ReturnCondition);
        }).ToList();

        var procedureTitles = procedures.Where(x => x.RequiresAcceptance).Select(x => x.Title).ToList();

        var model = new ProtocolPdfModel(
            organization?.Name ?? "Tenebit",
            organization?.LogoUrl,
            organization?.Country ?? "PL",
            assignment.ProtocolNumber,
            assignment.IssuedAt,
            assignment.DueDate,
            assignment.AcceptedAt,
            assignment.ReturnedAt,
            person?.FullName ?? "—",
            person?.JobTitle,
            team?.Name,
            assetRows,
            procedureTitles,
            assignment.Notes);

        return Result<ProtocolPdfModel>.Success(model);
    }

    public static string CreateProtocolNumber(DateTimeOffset now) => $"TEN-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
