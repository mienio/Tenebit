using Tenebit.Application.Abstractions;
using Tenebit.Application.Assignments;
using Tenebit.Application.Common;
using Tenebit.Application.Offboarding;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Offboarding;

namespace Tenebit.Application.Protocols;

/// <summary>
/// Renderuje protokoły zdawczo-odbiorcze jako PDF.
///
/// Autoryzacja nie jest tu powtarzana: ścieżki zalogowane pytają najpierw <see cref="AssignmentService"/>
/// albo <see cref="OffboardingService"/> o ten sam rekord, więc zakres menedżera i role działają dokładnie
/// tak, jak na liście - a ta klasa dokłada wyłącznie pola, których odpowiedź API nie niesie (numery
/// seryjne, wartości, dane organizacji). Ścieżka publiczna dostaje organizację i wydanie już rozwiązane
/// z tokenu capability przez endpoint.
/// </summary>
public sealed class ProtocolPdfService
{
    private readonly AssignmentService _assignmentService;
    private readonly OffboardingService _offboardingService;
    private readonly IAssignmentRepository _assignments;
    private readonly IOffboardingCaseRepository _offboardingCases;
    private readonly IOffboardingItemRepository _offboardingItems;
    private readonly IPersonRepository _people;
    private readonly IAssetRepository _assets;
    private readonly IProcedureRepository _procedures;
    private readonly IOrganizationRepository _organizations;
    private readonly IProtocolPdfGenerator _generator;
    private readonly ICurrentUser _currentUser;

    public ProtocolPdfService(
        AssignmentService assignmentService,
        OffboardingService offboardingService,
        IAssignmentRepository assignments,
        IOffboardingCaseRepository offboardingCases,
        IOffboardingItemRepository offboardingItems,
        IPersonRepository people,
        IAssetRepository assets,
        IProcedureRepository procedures,
        IOrganizationRepository organizations,
        IProtocolPdfGenerator generator,
        ICurrentUser currentUser)
    {
        _assignmentService = assignmentService;
        _offboardingService = offboardingService;
        _assignments = assignments;
        _offboardingCases = offboardingCases;
        _offboardingItems = offboardingItems;
        _people = people;
        _assets = assets;
        _procedures = procedures;
        _organizations = organizations;
        _generator = generator;
        _currentUser = currentUser;
    }

    public async Task<Result<ProtocolFile>> GetAssignmentProtocolAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var access = await _assignmentService.GetAsync(assignmentId, cancellationToken);
        if (access.IsFailure) return Result<ProtocolFile>.Failure(access.Error!);

        return await RenderAssignmentAsync(_currentUser.OrganizationId, assignmentId, cancellationToken);
    }

    /// <summary>Egzemplarz dla pracownika, pobierany ze strony publicznej. Endpoint zweryfikował już token
    /// capability, więc identyfikatory są zaufane i nie ma tu drugiej kontroli ról.</summary>
    public Task<Result<ProtocolFile>> GetPublicAssignmentProtocolAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken) =>
        RenderAssignmentAsync(organizationId, assignmentId, cancellationToken);

    public async Task<Result<ProtocolFile>> GetOffboardingProtocolAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var access = await _offboardingService.GetAsync(caseId, cancellationToken);
        if (access.IsFailure) return Result<ProtocolFile>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var offboardingCase = await _offboardingCases.GetAsync(organizationId, caseId, cancellationToken);
        if (offboardingCase is null) return Result<ProtocolFile>.Failure(Error.NotFound("Sprawa offboardingowa nie istnieje."));

        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        if (organization is null) return Result<ProtocolFile>.Failure(Error.NotFound("Organizacja nie istnieje."));

        var person = await _people.GetAsync(organizationId, offboardingCase.PersonId, cancellationToken);
        var items = await _offboardingItems.ListByCaseAsync(organizationId, caseId, cancellationToken);
        var assetIds = items.Where(x => x.AssetId.HasValue).Select(x => x.AssetId!.Value).Distinct().ToArray();
        var assets = await _assets.GetByIdsAsync(organizationId, assetIds, cancellationToken);
        var assetsById = assets.ToDictionary(x => x.Id);
        var labels = ProtocolLabels.For(organization.Language);

        var lines = items
            .OrderBy(x => x.SortOrder)
            .Select(item =>
            {
                Asset? asset = item.AssetId.HasValue && assetsById.TryGetValue(item.AssetId.Value, out var found) ? found : null;
                return new ProtocolLine(
                    Name: item.Label,
                    AssetTag: asset?.AssetTag,
                    SerialNumber: asset?.SerialNumber,
                    Condition: item.ResolutionNotes,
                    Value: asset?.PurchasePrice,
                    Currency: asset?.Currency,
                    Status: DescribeItemStatus(item.Status));
            })
            .ToList();

        var protocolNumber = string.IsNullOrWhiteSpace(offboardingCase.FinalProtocolNumber)
            ? offboardingCase.Id.ToString()[..8].ToUpperInvariant()
            : offboardingCase.FinalProtocolNumber;

        var document = new ProtocolDocument(
            Kind: ProtocolKind.Return,
            OrganizationName: organization.Name,
            ProtocolNumber: protocolNumber,
            Person: DescribeParty(person),
            IssuedAt: offboardingCase.StartedAt ?? offboardingCase.CreatedAt,
            ConfirmedAt: offboardingCase.CompletedAt,
            ConfirmationHash: null,
            SignerName: null,
            SignatureImage: null,
            Lines: lines,
            Procedures: [],
            Notes: offboardingCase.Notes,
            Labels: labels);

        return Result<ProtocolFile>.Success(new ProtocolFile(_generator.Render(document), FileName(ProtocolKind.Return, protocolNumber)));
    }

    private async Task<Result<ProtocolFile>> RenderAssignmentAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(organizationId, assignmentId, cancellationToken);
        if (assignment is null) return Result<ProtocolFile>.Failure(Error.NotFound("Wydanie nie istnieje."));

        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        if (organization is null) return Result<ProtocolFile>.Failure(Error.NotFound("Organizacja nie istnieje."));

        var person = await _people.GetAsync(organizationId, assignment.PersonId, cancellationToken);
        var assets = await _assets.GetByIdsAsync(organizationId, assignment.Assets.Select(x => x.AssetId).Distinct().ToArray(), cancellationToken);
        var assetsById = assets.ToDictionary(x => x.Id);
        var procedures = await _procedures.GetByIdsAsync(organizationId, assignment.ProcedureAcceptances.Select(x => x.ProcedureId).Distinct().ToArray(), cancellationToken);
        var labels = ProtocolLabels.For(organization.Language);

        var lines = assignment.Assets
            .Select(item =>
            {
                Asset? asset = assetsById.TryGetValue(item.AssetId, out var found) ? found : null;
                return new ProtocolLine(
                    Name: asset?.Name ?? item.AssetId.ToString(),
                    AssetTag: asset?.AssetTag,
                    SerialNumber: asset?.SerialNumber,
                    // Po zwrocie liczy się stan zwrotu - to on jest przedmiotem ewentualnego sporu.
                    Condition: item.ReturnCondition ?? item.IssueCondition,
                    Value: asset?.PurchasePrice,
                    Currency: asset?.Currency,
                    Status: item.ReturnResolution?.ToString());
            })
            .ToList();

        var document = new ProtocolDocument(
            Kind: assignment.Status is AssignmentStatus.Returned or AssignmentStatus.PartiallyReturned ? ProtocolKind.Return : ProtocolKind.Handover,
            OrganizationName: organization.Name,
            ProtocolNumber: assignment.ProtocolNumber,
            Person: DescribeParty(person),
            IssuedAt: assignment.IssuedAt,
            ConfirmedAt: assignment.AcceptedAt,
            ConfirmationHash: assignment.AcceptanceHash,
            SignerName: assignment.SignerName,
            SignatureImage: assignment.SignatureImage,
            Lines: lines,
            Procedures: procedures.Select(x => $"{x.Title} ({x.Version})").ToList(),
            Notes: assignment.Notes,
            Labels: labels);

        return Result<ProtocolFile>.Success(new ProtocolFile(_generator.Render(document), FileName(document.Kind, assignment.ProtocolNumber)));
    }

    private static ProtocolParty DescribeParty(Domain.People.Person? person) => person is null
        ? new ProtocolParty("-", null, null, null)
        : new ProtocolParty($"{person.FirstName} {person.LastName}".Trim(), person.EmployeeNumber, person.JobTitle, person.Email);

    private static string DescribeItemStatus(OffboardingItemStatus status) => status switch
    {
        OffboardingItemStatus.Returned => "Zwrócone",
        OffboardingItemStatus.Received => "Odebrane",
        OffboardingItemStatus.Released => "Zwolnione",
        OffboardingItemStatus.Missing => "Brak",
        OffboardingItemStatus.Damaged => "Uszkodzone",
        OffboardingItemStatus.Retained => "Zatrzymane",
        OffboardingItemStatus.Waived => "Odstąpiono",
        OffboardingItemStatus.Inspecting => "W ocenie",
        OffboardingItemStatus.EmployeeAcknowledged => "Potwierdzone przez pracownika",
        _ => "Oczekuje"
    };

    // Nazwa pliku trafia do nagłówka Content-Disposition, więc numer protokołu jest ograniczony do
    // znaków bezpiecznych w nazwie pliku.
    private static string FileName(ProtocolKind kind, string protocolNumber)
    {
        var safe = new string(protocolNumber.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        var prefix = kind == ProtocolKind.Return ? "protokol-zwrotu" : "protokol-przekazania";
        return string.IsNullOrEmpty(safe) ? $"{prefix}.pdf" : $"{prefix}-{safe}.pdf";
    }
}
