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
    private readonly IAssetCategoryRepository _categories;
    private readonly IAssetInspectionRepository _inspections;
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
    private readonly Assets.AssetReturnDispositionService _disposition;

    public AssignmentService(IAssignmentRepository assignments, IAssetRepository assets, IAssetCategoryRepository categories, IAssetInspectionRepository inspections, IPersonRepository people, IProcedureRepository procedures, ITeamRepository teams, IOrganizationRepository organizations, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IPdfProtocolGenerator pdfGenerator, IEmailSender emailSender, IAppLinkBuilder linkBuilder)
    {
        _assignments = assignments;
        _assets = assets;
        _categories = categories;
        _inspections = inspections;
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
        _disposition = new Assets.AssetReturnDispositionService(inspections);
    }

    public async Task<Result<IReadOnlyList<AssignmentResponse>>> ListAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssignmentViewers);
        if (access.IsFailure) return Result<IReadOnlyList<AssignmentResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var assignments = await _assignments.ListAsync(organizationId, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var assets = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);
        var procedures = await _procedures.ListAsync(organizationId, null, cancellationToken);
        return Result<IReadOnlyList<AssignmentResponse>>.Success(assignments.Select(x => Map(organizationId, x, people, assets, procedures)).ToList());
    }

    public async Task<Result<PagedResult<AssignmentResponse>>> ListPagedAsync(string? search, AssignmentStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssignmentViewers);
        if (access.IsFailure) return Result<PagedResult<AssignmentResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var (items, total) = await _assignments.ListPagedAsync(organizationId, search, status, page, pageSize, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var assets = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);
        var procedures = await _procedures.ListAsync(organizationId, null, cancellationToken);
        return Result<PagedResult<AssignmentResponse>>.Success(new PagedResult<AssignmentResponse>(items.Select(x => Map(organizationId, x, people, assets, procedures)).ToList(), total, page, pageSize));
    }

    public async Task<Result<AssignmentResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssignmentViewers);
        if (access.IsFailure) return Result<AssignmentResponse>.Failure(access.Error!);

        return await BuildResponseAsync(id, cancellationToken);
    }

    // Used after a mutation (create/accept/return) that already ran its own, narrower role check —
    // building the response here must not re-apply the broader read-role gate from GetAsync, or an
    // "employee" accepting their own assignment would succeed the accept but then get a 403 back.
    private async Task<Result<AssignmentResponse>> BuildResponseAsync(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        var assignment = await _assignments.GetAsync(organizationId, id, cancellationToken);
        if (assignment is null) return Result<AssignmentResponse>.Failure(Error.NotFound("Wydanie nie istnieje."));
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var assets = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);
        var procedures = await _procedures.ListAsync(organizationId, null, cancellationToken);
        return Result<AssignmentResponse>.Success(Map(organizationId, assignment, people, assets, procedures));
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
            if (!person.CanReceiveNewObligations) return Result<AssignmentResponse>.Failure(Error.Validation("Nowe wydanie można utworzyć tylko dla aktywnej osoby."));

            var uniqueAssetIds = request.Assets.Select(x => x.AssetId).Distinct().ToArray();
            if (uniqueAssetIds.Length != request.Assets.Count) return Result<AssignmentResponse>.Failure(Error.Validation("To samo aktywo nie może wystąpić dwa razy w jednym wydaniu."));

            var assets = await _assets.GetByIdsAsync(organizationId, uniqueAssetIds, cancellationToken);
            if (assets.Count != uniqueAssetIds.Length) return Result<AssignmentResponse>.Failure(Error.Validation("Niektóre aktywa nie istnieją."));
            if (assets.Any(x => x.Status is AssetStatus.Assigned or AssetStatus.Disposed or AssetStatus.Lost or AssetStatus.PendingReturn))
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

            foreach (var procedure in procedures.Where(x => x.RequiresAcceptance && x.Status == ProcedureStatus.Published))
            {
                assignment.AddProcedureAcceptance(organizationId, procedure.Id, person.Id, _clock.UtcNow);
            }

            _assignments.Add(assignment);
            _activity.Add(new ActivityLog(organizationId, "assignment.created", "assignment", assignment.Id, _currentUser.Subject, person.FullName, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            try
            {
                var acceptedAssets = assets.Where(x => request.Assets.Any(item => item.AssetId == x.Id)).ToList();
                var requiredProcedures = procedures.Where(x => x.RequiresAcceptance && x.Status == ProcedureStatus.Published).Select(x => x.Title).ToList();
                var link = _linkBuilder.BuildAssignmentAcceptanceLink(organizationId, assignment.Id);
                var organization = await _organizations.GetAsync(organizationId, cancellationToken);
                var (subject, html) = EmailTemplates.NewAssignmentNotification(organization?.Language, person.FirstName, assignment.ProtocolNumber, acceptedAssets.Select(x => x.Name), requiredProcedures, link);
                await _emailSender.SendAsync(person.Email, subject, html, cancellationToken);
            }
            catch (Exception ex)
            {
                _activity.Add(new ActivityLog(organizationId, "assignment.email_failed", "assignment", assignment.Id, _currentUser.Subject, ex.Message, _clock.UtcNow));
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return await BuildResponseAsync(assignment.Id, cancellationToken);
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
            assignment.Accept(_clock.UtcNow, _currentUser.IpAddress);
            _activity.Add(new ActivityLog(organizationId, "assignment.accepted", "assignment", assignment.Id, _currentUser.Subject, assignment.ProtocolNumber, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildResponseAsync(id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssignmentResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<AssignmentResponse>> ReturnAsync(Guid id, ReturnAssignmentRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Hr, TenebitRoles.Technician);
        if (access.IsFailure) return Result<AssignmentResponse>.Failure(access.Error!);

        try
        {
            var organizationId = _currentUser.OrganizationId;
            var assignment = await _assignments.GetAsync(organizationId, id, cancellationToken);
            if (assignment is null) return Result<AssignmentResponse>.Failure(Error.NotFound("Wydanie nie istnieje."));

            var assetIds = assignment.Assets.Select(x => x.AssetId).ToArray();
            var assets = await _assets.GetByIdsAsync(organizationId, assetIds, cancellationToken);
            var categories = await _categories.ListAsync(organizationId, cancellationToken);
            var perAssetConditions = request.Assets?.ToDictionary(x => x.AssetId, x => x.ReturnCondition);
            var now = _clock.UtcNow;

            foreach (var asset in assets)
            {
                var condition = perAssetConditions is not null && perAssetConditions.TryGetValue(asset.Id, out var perAsset) && !string.IsNullOrWhiteSpace(perAsset)
                    ? perAsset
                    : request.ReturnCondition;
                var category = categories.FirstOrDefault(x => x.Id == asset.CategoryId);
                ApplyAssetReturn(assignment, asset, category, ReturnResolution.Returned, condition, request.DestinationLocation, null, organizationId, now);
            }

            _activity.Add(new ActivityLog(organizationId, "assignment.returned", "assignment", assignment.Id, _currentUser.Subject, assignment.ProtocolNumber, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildResponseAsync(id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssignmentResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<AssignmentResponse>> ReturnAssetAsync(Guid assignmentId, Guid assetId, ReturnAssignmentAssetItemRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Hr, TenebitRoles.Technician);
        if (access.IsFailure) return Result<AssignmentResponse>.Failure(access.Error!);

        try
        {
            var organizationId = _currentUser.OrganizationId;
            var assignment = await _assignments.GetAsync(organizationId, assignmentId, cancellationToken);
            if (assignment is null) return Result<AssignmentResponse>.Failure(Error.NotFound("Wydanie nie istnieje."));

            if (assignment.Assets.All(x => x.AssetId != assetId))
            {
                return Result<AssignmentResponse>.Failure(Error.NotFound("To aktywo nie należy do tego wydania."));
            }

            var asset = await _assets.GetAsync(organizationId, assetId, cancellationToken);
            if (asset is null) return Result<AssignmentResponse>.Failure(Error.NotFound("Aktywo nie istnieje."));
            var category = await _categories.GetAsync(organizationId, asset.CategoryId, cancellationToken);

            var changed = ApplyAssetReturn(assignment, asset, category, request.Resolution, request.ReturnCondition, request.ReturnLocation, request.Notes, organizationId, _clock.UtcNow);
            if (changed)
            {
                _activity.Add(new ActivityLog(organizationId, "assignment.asset_returned", "assignment", assignment.Id, _currentUser.Subject, assignment.ProtocolNumber, _clock.UtcNow));
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildResponseAsync(assignmentId, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssignmentResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    private bool ApplyAssetReturn(Assignment assignment, Asset asset, AssetCategory? category, ReturnResolution resolution, string? returnCondition, string? returnLocation, string? notes, Guid organizationId, DateTimeOffset now)
    {
        var item = assignment.Assets.FirstOrDefault(x => x.AssetId == asset.Id);
        if (item is null || item.ReturnResolution is not null)
        {
            return false;
        }

        assignment.ReturnAsset(asset.Id, resolution, now, returnCondition, returnLocation, _currentUser.Subject, notes);
        ApplyReturnResolutionToAsset(assignment, asset, category, resolution, returnLocation, organizationId, now);
        return true;
    }

    private void ApplyReturnResolutionToAsset(Assignment assignment, Asset asset, AssetCategory? category, ReturnResolution resolution, string? returnLocation, Guid organizationId, DateTimeOffset now)
    {
        switch (resolution)
        {
            case ReturnResolution.Returned:
                _disposition.ApplyPhysicalReturn(asset, category, returnLocation, organizationId, now, _currentUser.Subject, assignment.Id, null);
                break;
            case ReturnResolution.Damaged:
                asset.ReleaseAssignment(AssetStatus.Damaged, returnLocation);
                break;
            case ReturnResolution.Missing:
                asset.ReleaseAssignment(AssetStatus.Lost);
                break;
            case ReturnResolution.Retained:
                asset.ChangeStatus(AssetStatus.Retired);
                break;
            case ReturnResolution.WrittenOff:
                asset.ReleaseAssignment(AssetStatus.Disposed);
                break;
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
            assignment.Accept(_clock.UtcNow, _currentUser.IpAddress);
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

    public async Task<Result<ProcedureDocument>> GetPublicProcedureDocumentAsync(Guid organizationId, Guid assignmentId, Guid procedureId, Guid documentId, CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(organizationId, assignmentId, cancellationToken);
        if (assignment is null) return Result<ProcedureDocument>.Failure(Error.NotFound("Wydanie nie istnieje."));
        if (assignment.ProcedureAcceptances.All(x => x.ProcedureId != procedureId))
        {
            return Result<ProcedureDocument>.Failure(Error.NotFound("Plik procedury nie istnieje."));
        }

        var document = await _procedures.GetDocumentAsync(organizationId, procedureId, documentId, cancellationToken);
        return document is null
            ? Result<ProcedureDocument>.Failure(Error.NotFound("Plik procedury nie istnieje."))
            : Result<ProcedureDocument>.Success(document);
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

    private AssignmentResponse Map(Guid organizationId, Assignment assignment, IReadOnlyList<Domain.People.Person> people, IReadOnlyList<Asset> assets, IReadOnlyList<Procedure> procedures)
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

        var acceptanceLink = _linkBuilder.BuildAssignmentAcceptanceLink(organizationId, assignment.Id);
        return new AssignmentResponse(assignment.Id, assignment.PersonId, person?.FullName, assignment.Status, assignment.IssuedAt, assignment.DueDate, assignment.AcceptedAt, assignment.ReturnedAt, assignment.ProtocolNumber, assignment.Notes, items, acceptances, acceptanceLink, assignment.AcceptedIp, assignment.AcceptanceHash, assignment.VerifyIntegrity());
    }

    private static string CreateProtocolNumber(DateTimeOffset now) => $"TEN-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
