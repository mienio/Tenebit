using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.Procedures;

namespace Tenebit.Application.Assignments;

public sealed class AssignmentService
{
    private readonly IAssignmentRepository _assignments;
    private readonly IAssetRepository _assets;
    private readonly IPersonRepository _people;
    private readonly IProcedureRepository _procedures;
    private readonly ITeamRepository _teams;
    private readonly IOrganizationRepository _organizations;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPdfProtocolGenerator _pdfGenerator;
    private readonly IEmailSender _emailSender;
    private readonly IAppLinkBuilder _linkBuilder;

    public AssignmentService(IAssignmentRepository assignments, IAssetRepository assets, IPersonRepository people, IProcedureRepository procedures, ITeamRepository teams, IOrganizationRepository organizations, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IPdfProtocolGenerator pdfGenerator, IEmailSender emailSender, IAppLinkBuilder linkBuilder)
    {
        _assignments = assignments;
        _assets = assets;
        _people = people;
        _procedures = procedures;
        _teams = teams;
        _organizations = organizations;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _pdfGenerator = pdfGenerator;
        _emailSender = emailSender;
        _linkBuilder = linkBuilder;
    }

    public async Task<IReadOnlyList<AssignmentResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        var assignments = await _assignments.ListAsync(organizationId, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var assets = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);
        var procedures = await _procedures.ListAsync(organizationId, null, cancellationToken);
        return assignments.Select(x => Map(x, people, assets, procedures)).ToList();
    }

    public async Task<Result<AssignmentResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        var assignment = await _assignments.GetAsync(organizationId, id, cancellationToken);
        if (assignment is null) return Result<AssignmentResponse>.Failure(Error.NotFound("Wydanie nie istnieje."));
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var assets = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);
        var procedures = await _procedures.ListAsync(organizationId, null, cancellationToken);
        return Result<AssignmentResponse>.Success(Map(assignment, people, assets, procedures));
    }

    public async Task<Result<AssignmentResponse>> CreateAsync(CreateAssignmentRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Hr);
        if (access.IsFailure) return Result<AssignmentResponse>.Failure(access.Error!);

        try
        {
            if (request.Assets.Count == 0) return Result<AssignmentResponse>.Failure(Error.Validation("Dodaj co najmniej jedno aktywo do wydania."));
            var organizationId = _currentUser.OrganizationId;
            var person = await _people.GetAsync(organizationId, request.PersonId, cancellationToken);
            if (person is null) return Result<AssignmentResponse>.Failure(Error.Validation("Wybrany pracownik nie istnieje."));

            var uniqueAssetIds = request.Assets.Select(x => x.AssetId).Distinct().ToArray();
            if (uniqueAssetIds.Length != request.Assets.Count) return Result<AssignmentResponse>.Failure(Error.Validation("To samo aktywo nie może wystąpić dwa razy w jednym wydaniu."));

            var assets = await _assets.GetByIdsAsync(organizationId, uniqueAssetIds, cancellationToken);
            if (assets.Count != uniqueAssetIds.Length) return Result<AssignmentResponse>.Failure(Error.Validation("Niektóre aktywa nie istnieją."));
            if (assets.Any(x => x.Status is AssetStatus.Assigned or AssetStatus.Disposed or AssetStatus.Lost))
            {
                return Result<AssignmentResponse>.Failure(Error.Conflict("Co najmniej jedno aktywo nie jest dostępne do wydania."));
            }

            var procedureIds = request.ProcedureIds.Distinct().ToArray();
            var procedures = await _procedures.GetByIdsAsync(organizationId, procedureIds, cancellationToken);
            if (procedures.Count != procedureIds.Length) return Result<AssignmentResponse>.Failure(Error.Validation("Niektóre procedury nie istnieją."));

            var assignment = new Assignment(organizationId, person.Id, CreateProtocolNumber(_clock.UtcNow), _clock.UtcNow, request.DueDate, request.Notes, _currentUser.Subject);
            foreach (var requestedAsset in request.Assets)
            {
                assignment.AddAsset(requestedAsset.AssetId, requestedAsset.IssueCondition);
                assets.First(x => x.Id == requestedAsset.AssetId).AssignTo(person.Id);
            }

            foreach (var procedure in procedures.Where(x => x.RequiresAcceptance))
            {
                assignment.AddProcedureAcceptance(organizationId, procedure.Id, person.Id, _clock.UtcNow);
            }

            _assignments.Add(assignment);
            _activity.Add(new ActivityLog(organizationId, "assignment.created", "assignment", assignment.Id, _currentUser.Subject, person.FullName, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            try
            {
                var acceptedAssets = assets.Where(x => request.Assets.Any(item => item.AssetId == x.Id)).ToList();
                var requiredProcedures = procedures.Where(x => x.RequiresAcceptance).Select(x => x.Title).ToList();
                var link = _linkBuilder.BuildAssignmentAcceptanceLink(organizationId, assignment.Id);
                var html = BuildAssignmentEmailHtml(person.FirstName, assignment.ProtocolNumber, acceptedAssets.Select(x => x.Name), requiredProcedures, link);
                await _emailSender.SendAsync(person.Email, $"Nowy sprzęt do odebrania — {assignment.ProtocolNumber}", html, cancellationToken);
            }
            catch (Exception ex)
            {
                _activity.Add(new ActivityLog(organizationId, "assignment.email_failed", "assignment", assignment.Id, _currentUser.Subject, ex.Message, _clock.UtcNow));
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return await GetAsync(assignment.Id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssignmentResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<AssignmentResponse>> AcceptAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Hr, TenebitRoles.Employee);
        if (access.IsFailure) return Result<AssignmentResponse>.Failure(access.Error!);

        try
        {
            var organizationId = _currentUser.OrganizationId;
            var assignment = await _assignments.GetAsync(organizationId, id, cancellationToken);
            if (assignment is null) return Result<AssignmentResponse>.Failure(Error.NotFound("Wydanie nie istnieje."));
            assignment.Accept(_clock.UtcNow);
            _activity.Add(new ActivityLog(organizationId, "assignment.accepted", "assignment", assignment.Id, _currentUser.Subject, assignment.ProtocolNumber, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await GetAsync(id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssignmentResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<AssignmentResponse>> ReturnAsync(Guid id, ReturnAssignmentRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Hr);
        if (access.IsFailure) return Result<AssignmentResponse>.Failure(access.Error!);

        try
        {
            var organizationId = _currentUser.OrganizationId;
            var assignment = await _assignments.GetAsync(organizationId, id, cancellationToken);
            if (assignment is null) return Result<AssignmentResponse>.Failure(Error.NotFound("Wydanie nie istnieje."));

            var assetIds = assignment.Assets.Select(x => x.AssetId).ToArray();
            var assets = await _assets.GetByIdsAsync(organizationId, assetIds, cancellationToken);
            foreach (var asset in assets) asset.ReturnToStock(request.DestinationLocation);

            assignment.Return(_clock.UtcNow, request.ReturnCondition);
            _activity.Add(new ActivityLog(organizationId, "assignment.returned", "assignment", assignment.Id, _currentUser.Subject, assignment.ProtocolNumber, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await GetAsync(id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssignmentResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public Task<Result<byte[]>> GetProtocolPdfAsync(Guid id, CancellationToken cancellationToken) =>
        BuildProtocolPdfAsync(_currentUser.OrganizationId, id, cancellationToken);

    public Task<Result<byte[]>> GetPublicProtocolPdfAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken) =>
        BuildProtocolPdfAsync(organizationId, assignmentId, cancellationToken);

    public async Task<Result<PublicAssignmentResponse>> GetPublicAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(organizationId, assignmentId, cancellationToken);
        if (assignment is null) return Result<PublicAssignmentResponse>.Failure(Error.NotFound("Wydanie nie istnieje."));
        return Result<PublicAssignmentResponse>.Success(await MapPublicAsync(organizationId, assignment, cancellationToken));
    }

    public async Task<Result<PublicAssignmentResponse>> AcceptPublicAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(organizationId, assignmentId, cancellationToken);
        if (assignment is null) return Result<PublicAssignmentResponse>.Failure(Error.NotFound("Wydanie nie istnieje."));

        try
        {
            assignment.Accept(_clock.UtcNow);
            _activity.Add(new ActivityLog(organizationId, "assignment.accepted", "assignment", assignment.Id, "public-link", assignment.ProtocolNumber, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<PublicAssignmentResponse>.Success(await MapPublicAsync(organizationId, assignment, cancellationToken));
        }
        catch (DomainException ex)
        {
            return Result<PublicAssignmentResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    private async Task<PublicAssignmentResponse> MapPublicAsync(Guid organizationId, Assignment assignment, CancellationToken cancellationToken)
    {
        var person = await _people.GetAsync(organizationId, assignment.PersonId, cancellationToken);
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        var assetIds = assignment.Assets.Select(x => x.AssetId).ToArray();
        var assets = await _assets.GetByIdsAsync(organizationId, assetIds, cancellationToken);
        var procedureIds = assignment.ProcedureAcceptances.Select(x => x.ProcedureId).ToArray();
        var procedures = await _procedures.GetByIdsAsync(organizationId, procedureIds, cancellationToken);

        var assetRows = assignment.Assets.Select(item =>
        {
            var asset = assets.FirstOrDefault(x => x.Id == item.AssetId);
            return new PublicAssignmentAssetResponse(asset?.Name ?? "—", asset?.AssetTag ?? "—", item.IssueCondition);
        }).ToList();
        var procedureTitles = procedures.Where(x => x.RequiresAcceptance).Select(x => x.Title).ToList();

        return new PublicAssignmentResponse(
            organization?.Name ?? "Tenebit",
            assignment.ProtocolNumber,
            assignment.Status,
            person?.FirstName ?? "—",
            assetRows,
            procedureTitles);
    }

    private async Task<Result<byte[]>> BuildProtocolPdfAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(organizationId, assignmentId, cancellationToken);
        if (assignment is null) return Result<byte[]>.Failure(Error.NotFound("Wydanie nie istnieje."));

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

        return Result<byte[]>.Success(_pdfGenerator.GenerateHandoverProtocol(model));
    }

    private static AssignmentResponse Map(Assignment assignment, IReadOnlyList<Domain.People.Person> people, IReadOnlyList<Asset> assets, IReadOnlyList<Procedure> procedures)
    {
        var person = people.FirstOrDefault(x => x.Id == assignment.PersonId);
        var items = assignment.Assets.Select(item =>
        {
            var asset = assets.FirstOrDefault(x => x.Id == item.AssetId);
            return new AssignmentAssetResponse(item.AssetId, asset?.Name, asset?.AssetTag, item.IssueCondition, item.ReturnCondition);
        }).ToList();

        var acceptances = assignment.ProcedureAcceptances.Select(acceptance =>
        {
            var procedure = procedures.FirstOrDefault(x => x.Id == acceptance.ProcedureId);
            return new ProcedureAcceptanceResponse(acceptance.Id, acceptance.ProcedureId, procedure?.Title, acceptance.Status, acceptance.SentAt, acceptance.AcceptedAt);
        }).ToList();

        return new AssignmentResponse(assignment.Id, assignment.PersonId, person?.FullName, assignment.Status, assignment.IssuedAt, assignment.DueDate, assignment.AcceptedAt, assignment.ReturnedAt, assignment.ProtocolNumber, assignment.Notes, items, acceptances);
    }

    private static string CreateProtocolNumber(DateTimeOffset now) => $"TEN-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private static string BuildAssignmentEmailHtml(string firstName, string protocolNumber, IEnumerable<string> assetNames, IReadOnlyList<string> procedureTitles, string acceptanceLink)
    {
        var assetsHtml = string.Join("", assetNames.Select(name => $"<li>{System.Net.WebUtility.HtmlEncode(name)}</li>"));
        var proceduresHtml = procedureTitles.Count == 0
            ? ""
            : $"<p><strong>Procedury i regulaminy do zapoznania:</strong></p><ul>{string.Join("", procedureTitles.Select(title => $"<li>{System.Net.WebUtility.HtmlEncode(title)}</li>"))}</ul>";

        return $"""
            <div style="font-family: sans-serif; max-width: 480px; margin: 0 auto;">
                <h2>Witaj, {System.Net.WebUtility.HtmlEncode(firstName)}!</h2>
                <p>Otrzymujesz nowy sprzęt do odebrania. Numer protokołu: <strong>{System.Net.WebUtility.HtmlEncode(protocolNumber)}</strong></p>
                <p><strong>Sprzęt:</strong></p>
                <ul>{assetsHtml}</ul>
                {proceduresHtml}
                <p><a href="{acceptanceLink}" style="display:inline-block;padding:12px 24px;background:#111827;color:#fff;text-decoration:none;border-radius:8px;">Zobacz i potwierdź odbiór</a></p>
                <p style="color:#687385;font-size:13px;">Jeśli przycisk nie działa, skopiuj ten link do przeglądarki: {acceptanceLink}</p>
            </div>
            """;
    }
}
