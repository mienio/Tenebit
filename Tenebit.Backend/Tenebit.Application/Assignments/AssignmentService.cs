using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Application.Evidence;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Application.Identity;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Procedures;
using Tenebit.Domain.Reservations;

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
    private readonly IEquipmentReservationRepository _reservations;
    private readonly IAssetEvidenceRepository _evidence;
    private readonly AssetEvidenceService _evidenceService;
    private readonly Assets.AssetReturnDispositionService _disposition;
    private readonly AssignmentResponseBuilder _responseBuilder;
    private readonly AssignmentProtocolModelBuilder _protocolModelBuilder;
    private readonly ManagerScopeService _managerScope;

    // Roles in TenebitRoles.AssignmentViewers that see the whole organization; Manager alone is
    // scoped to its own team's assignments by ManagerScopeService (audyt AUD3-006).
    private static readonly string[] OrgWideRoles = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Hr];

    public AssignmentService(IAssignmentRepository assignments, IAssetRepository assets, IAssetCategoryRepository categories, IAssetInspectionRepository inspections, IPersonRepository people, IProcedureRepository procedures, ITeamRepository teams, IOrganizationRepository organizations, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IPdfProtocolGenerator pdfGenerator, IEmailSender emailSender, IAppLinkBuilder linkBuilder, IEquipmentReservationRepository reservations, IAssetEvidenceRepository evidence, AssetEvidenceService evidenceService, Assets.AssetReturnDispositionService disposition, AssignmentResponseBuilder responseBuilder, AssignmentProtocolModelBuilder protocolModelBuilder, ManagerScopeService managerScope)
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
        _reservations = reservations;
        _evidence = evidence;
        _evidenceService = evidenceService;
        _disposition = disposition;
        _responseBuilder = responseBuilder;
        _protocolModelBuilder = protocolModelBuilder;
        _managerScope = managerScope;
    }

    public async Task<Result<IReadOnlyList<AssignmentResponse>>> ListAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssignmentViewers);
        if (access.IsFailure) return Result<IReadOnlyList<AssignmentResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var assignments = await _assignments.ListAsync(organizationId, cancellationToken);
        var visibleIds = await _managerScope.ResolveVisiblePersonIdsAsync(_currentUser, OrgWideRoles, cancellationToken);
        if (visibleIds is not null) assignments = assignments.Where(a => visibleIds.Contains(a.PersonId)).ToList();
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var assets = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);
        var procedures = await _procedures.ListAsync(organizationId, null, cancellationToken);
        var evidence = await _evidence.ListByOrganizationAsync(organizationId, cancellationToken);
        return Result<IReadOnlyList<AssignmentResponse>>.Success(assignments.Select(x => AssignmentResponseBuilder.Map(x, people, assets, procedures, evidence)).ToList());
    }

    public async Task<Result<PagedResult<AssignmentResponse>>> ListPagedAsync(string? search, AssignmentStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssignmentViewers);
        if (access.IsFailure) return Result<PagedResult<AssignmentResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var assets = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);
        var procedures = await _procedures.ListAsync(organizationId, null, cancellationToken);
        var evidence = await _evidence.ListByOrganizationAsync(organizationId, cancellationToken);

        var visibleIds = await _managerScope.ResolveVisiblePersonIdsAsync(_currentUser, OrgWideRoles, cancellationToken);
        if (visibleIds is null)
        {
            var (items, total) = await _assignments.ListPagedAsync(organizationId, search, status, page, pageSize, cancellationToken);
            return Result<PagedResult<AssignmentResponse>>.Success(new PagedResult<AssignmentResponse>(items.Select(x => AssignmentResponseBuilder.Map(x, people, assets, procedures, evidence)).ToList(), total, page, pageSize));
        }

        var all = (await _assignments.ListAsync(organizationId, cancellationToken)).Where(a => visibleIds.Contains(a.PersonId)).ToList();
        var page1 = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Result<PagedResult<AssignmentResponse>>.Success(new PagedResult<AssignmentResponse>(page1.Select(x => AssignmentResponseBuilder.Map(x, people, assets, procedures, evidence)).ToList(), all.Count, page, pageSize));
    }

    public async Task<Result<AssignmentResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssignmentViewers);
        if (access.IsFailure) return Result<AssignmentResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var visibleIds = await _managerScope.ResolveVisiblePersonIdsAsync(_currentUser, OrgWideRoles, cancellationToken);
        if (visibleIds is not null)
        {
            var assignment = await _assignments.GetAsync(organizationId, id, cancellationToken);
            if (assignment is null || !visibleIds.Contains(assignment.PersonId))
            {
                return Result<AssignmentResponse>.Failure(Error.NotFound("Wydanie nie istnieje."));
            }
        }

        return await _responseBuilder.BuildResponseAsync(organizationId, id, cancellationToken);
    }

    public async Task<Result<AssignmentResponse>> CreateAsync(CreateAssignmentRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Hr, TenebitRoles.Technician);
        if (access.IsFailure) return Result<AssignmentResponse>.Failure(access.Error!);

        try
        {
            var prepared = await PrepareAssignmentAsync(request, cancellationToken);
            if (prepared.IsFailure) return Result<AssignmentResponse>.Failure(prepared.Error!);

            var (assignment, person, assets, procedures) = prepared.Value!;
            var organizationId = _currentUser.OrganizationId;
            var rawToken = IssueAcceptanceToken(assignment, _clock.UtcNow);

            _assignments.Add(assignment);
            _activity.Add(new ActivityLog(organizationId, "assignment.created", "assignment", assignment.Id, _currentUser.Subject, person.FullName, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await SendAssignmentNotificationAsync(request, assignment, person, assets, procedures, organizationId, rawToken, cancellationToken);

            return await _responseBuilder.BuildResponseAsync(_currentUser.OrganizationId, assignment.Id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssignmentResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    // Spec 6.4: wydanie ze zdjęciami. Wszystko (wydanie + zdjęcia) trafia do bazy w jednej transakcji,
    // a e-mail z linkiem do akceptacji jest wysyłany dopiero po udanym zapisie. Błąd dowolnego zdjęcia
    // (format, rozmiar, limit, sanityzacja) wycofuje całą operację — wydanie nie zostaje utworzone.
    public async Task<Result<AssignmentResponse>> CreateWithEvidenceAsync(
        CreateAssignmentRequest request,
        IReadOnlyDictionary<string, EvidenceManifestEntry> evidenceManifest,
        IReadOnlyList<EvidenceFileInput> files,
        CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Hr, TenebitRoles.Technician);
        if (access.IsFailure) return Result<AssignmentResponse>.Failure(access.Error!);

        if (files.Count == 0) return Result<AssignmentResponse>.Failure(Error.Validation("Dodaj co najmniej jedno zdjęcie."));

        try
        {
            var organizationId = _currentUser.OrganizationId;

            // Walidacja manifestu przed jakąkolwiek mutacją.
            var requestedAssetIds = request.Assets.Select(x => x.AssetId).ToHashSet();
            var uploads = new List<EvidenceUploadInput>(files.Count);
            foreach (var file in files)
            {
                if (!evidenceManifest.TryGetValue(file.FieldName, out var entry))
                {
                    return Result<AssignmentResponse>.Failure(Error.Validation("Brak wpisu manifestu dla przesłanego pliku."));
                }

                if (!requestedAssetIds.Contains(entry.AssetId))
                {
                    return Result<AssignmentResponse>.Failure(Error.Validation("Zdjęcie dotyczy aktywa spoza wydania."));
                }

                uploads.Add(new EvidenceUploadInput(entry.AssetId, file.FileName, file.ContentType, file.Content, entry.Caption, _currentUser.Subject, EvidenceUploadSource.AuthenticatedUser));
            }

            var prepared = await PrepareAssignmentAsync(request, cancellationToken);
            if (prepared.IsFailure) return Result<AssignmentResponse>.Failure(prepared.Error!);

            var (assignment, person, assets, procedures) = prepared.Value!;
            assignment.EnableEvidenceIntegrity();
            var rawToken = IssueAcceptanceToken(assignment, _clock.UtcNow);

            var evidenceResult = await _evidenceService.PrepareEvidenceBatchAsync(organizationId, assignment.Id, EvidencePhase.Issue, uploads, cancellationToken);
            if (evidenceResult.IsFailure) return Result<AssignmentResponse>.Failure(evidenceResult.Error!);

            _assignments.Add(assignment);
            foreach (var evidence in evidenceResult.Value!)
            {
                _activity.Add(new ActivityLog(organizationId, "asset_evidence.uploaded", "asset_evidence", evidence.Id, _currentUser.Subject, evidence.FileName, _clock.UtcNow));
            }
            _activity.Add(new ActivityLog(organizationId, "assignment.created", "assignment", assignment.Id, _currentUser.Subject, person.FullName, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await SendAssignmentNotificationAsync(request, assignment, person, assets, procedures, organizationId, rawToken, cancellationToken);

            return await _responseBuilder.BuildResponseAsync(_currentUser.OrganizationId, assignment.Id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssignmentResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    private async Task<Result<PreparedAssignment>> PrepareAssignmentAsync(CreateAssignmentRequest request, CancellationToken cancellationToken)
    {
        if (request.Assets.Count == 0) return Result<PreparedAssignment>.Failure(Error.Validation("Dodaj co najmniej jedno aktywo do wydania."));
        var organizationId = _currentUser.OrganizationId;
        var person = await _people.GetAsync(organizationId, request.PersonId, cancellationToken);
        if (person is null) return Result<PreparedAssignment>.Failure(Error.Validation("Wybrany pracownik nie istnieje."));
        if (!person.CanReceiveNewObligations) return Result<PreparedAssignment>.Failure(Error.Validation("Nowe wydanie można utworzyć tylko dla aktywnej osoby."));

        var uniqueAssetIds = request.Assets.Select(x => x.AssetId).Distinct().ToArray();
        if (uniqueAssetIds.Length != request.Assets.Count) return Result<PreparedAssignment>.Failure(Error.Validation("To samo aktywo nie może wystąpić dwa razy w jednym wydaniu."));

        var assets = await _assets.GetByIdsAsync(organizationId, uniqueAssetIds, cancellationToken);
        if (assets.Count != uniqueAssetIds.Length) return Result<PreparedAssignment>.Failure(Error.Validation("Niektóre aktywa nie istnieją."));
        if (assets.Any(x => x.Status is AssetStatus.Assigned or AssetStatus.Disposed or AssetStatus.Lost or AssetStatus.PendingReturn))
        {
            return Result<PreparedAssignment>.Failure(Error.Conflict("Co najmniej jedno aktywo nie jest dostępne do wydania."));
        }

        var procedureIds = request.ProcedureIds.Distinct().ToArray();
        var procedures = await _procedures.GetByIdsAsync(organizationId, procedureIds, cancellationToken);
        if (procedures.Count != procedureIds.Length) return Result<PreparedAssignment>.Failure(Error.Validation("Niektóre procedury nie istnieją."));

        var assignment = new Assignment(organizationId, person.Id, AssignmentProtocolModelBuilder.CreateProtocolNumber(_clock.UtcNow), _clock.UtcNow, request.DueDate, request.Notes, _currentUser.Subject);
        foreach (var requestedAsset in request.Assets)
        {
            assignment.AddAsset(requestedAsset.AssetId, requestedAsset.IssueCondition);
            assets.First(x => x.Id == requestedAsset.AssetId).AssignTo(person.Id);
        }

        foreach (var procedure in procedures.Where(x => x.RequiresAcceptance && x.Status == ProcedureStatus.Published))
        {
            assignment.AddProcedureAcceptance(organizationId, procedure.Id, person.Id, _clock.UtcNow);
        }

        return Result<PreparedAssignment>.Success(new PreparedAssignment(assignment, person, assets, procedures));
    }

    // AUD-001: identyfikator wydania nie może sam być credentialem — link niesie osobny, losowy token
    // (hash trzymany na Assignment), z TTL i możliwością odświeżenia przez RegenerateAcceptanceLinkAsync.
    private string IssueAcceptanceToken(Assignment assignment, DateTimeOffset now)
    {
        var generated = PublicTokenService.Generate();
        var expiresAt = assignment.DueDate.HasValue
            ? new DateTimeOffset(assignment.DueDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(30)
            : now.AddDays(90);
        assignment.SetPublicToken(generated.TokenHash, expiresAt);
        return generated.RawToken;
    }

    public async Task<Result<AssignmentAcceptanceLinkResponse>> RegenerateAcceptanceLinkAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Hr, TenebitRoles.Technician);
        if (access.IsFailure) return Result<AssignmentAcceptanceLinkResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var assignment = await _assignments.GetAsync(organizationId, id, cancellationToken);
        if (assignment is null) return Result<AssignmentAcceptanceLinkResponse>.Failure(Error.NotFound("Wydanie nie istnieje."));

        var rawToken = IssueAcceptanceToken(assignment, _clock.UtcNow);
        _activity.Add(new ActivityLog(organizationId, "assignment.acceptance_link_regenerated", "assignment", assignment.Id, _currentUser.Subject, assignment.ProtocolNumber, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AssignmentAcceptanceLinkResponse>.Success(new AssignmentAcceptanceLinkResponse(_linkBuilder.BuildAssignmentAcceptanceLink(rawToken)));
    }

    private async Task SendAssignmentNotificationAsync(CreateAssignmentRequest request, Assignment assignment, Domain.People.Person person, IReadOnlyList<Asset> assets, IReadOnlyList<Procedure> procedures, Guid organizationId, string rawToken, CancellationToken cancellationToken)
    {
        try
        {
            var acceptedAssets = assets.Where(x => request.Assets.Any(item => item.AssetId == x.Id)).ToList();
            var requiredProcedures = procedures.Where(x => x.RequiresAcceptance && x.Status == ProcedureStatus.Published).Select(x => x.Title).ToList();
            var link = _linkBuilder.BuildAssignmentAcceptanceLink(rawToken);
            var organization = await _organizations.GetAsync(organizationId, cancellationToken);
            var (subject, html) = EmailTemplates.NewAssignmentNotification(organization?.Language, person.FirstName, assignment.ProtocolNumber, acceptedAssets.Select(x => x.Name), requiredProcedures, link);
            await _emailSender.SendAsync(person.Email, subject, html, cancellationToken);
        }
        catch (Exception ex)
        {
            _activity.Add(new ActivityLog(organizationId, "assignment.email_failed", "assignment", assignment.Id, _currentUser.Subject, ex.Message, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
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

            // Employee has no elevated visibility here — it may only accept its own assignment.
            // Privileged roles (Owner/Admin/AssetOperator/Hr) may accept on behalf of someone else
            // (audyt AUD3-004: employee could accept another employee's assignment given only its GUID).
            if (!_currentUser.HasAnyRole(TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Hr))
            {
                var currentPerson = string.IsNullOrEmpty(_currentUser.Email) ? null : await _people.FindByEmailAsync(organizationId, _currentUser.Email, cancellationToken);
                if (currentPerson is null || assignment.PersonId != currentPerson.Id)
                {
                    return Result<AssignmentResponse>.Failure(Error.Forbidden("Nie możesz zaakceptować cudzego wydania."));
                }
            }

            var evidence = assignment.IntegrityVersion >= 2 ? await _evidence.ListByAssignmentAsync(organizationId, id, cancellationToken) : null;
            assignment.Accept(_clock.UtcNow, _currentUser.IpAddress, evidence);
            _activity.Add(new ActivityLog(organizationId, "assignment.accepted", "assignment", assignment.Id, _currentUser.Subject, assignment.ProtocolNumber, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await _responseBuilder.BuildResponseAsync(_currentUser.OrganizationId, id, cancellationToken);
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
            await CompleteLinkedReservationAsync(organizationId, assignment, now, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await _responseBuilder.BuildResponseAsync(_currentUser.OrganizationId, id, cancellationToken);
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

            var now = _clock.UtcNow;
            var changed = ApplyAssetReturn(assignment, asset, category, request.Resolution, request.ReturnCondition, request.ReturnLocation, request.Notes, organizationId, now);
            if (changed)
            {
                _activity.Add(new ActivityLog(organizationId, "assignment.asset_returned", "assignment", assignment.Id, _currentUser.Subject, assignment.ProtocolNumber, _clock.UtcNow));
                await CompleteLinkedReservationAsync(organizationId, assignment, now, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await _responseBuilder.BuildResponseAsync(_currentUser.OrganizationId, assignmentId, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssignmentResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    // Spec 6.5: zwrot pojedynczego aktywa ze zdjęciami w jednej transakcji. Błąd zapisu dowolnego
    // zdjęcia pozostawia aktywo jako niezwrócone.
    public async Task<Result<AssignmentResponse>> ReturnAssetWithEvidenceAsync(Guid assignmentId, Guid assetId, ReturnAssignmentAssetItemRequest request, IReadOnlyList<EvidenceFileInput> files, CancellationToken cancellationToken)
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

            // Idempotencja: aktywo już rozliczone — nie zmieniamy stanu ani nie dodajemy zdjęć.
            if (assignment.Assets.First(x => x.AssetId == assetId).ReturnResolution is not null)
            {
                return await _responseBuilder.BuildResponseAsync(_currentUser.OrganizationId, assignmentId, cancellationToken);
            }

            var uploads = new List<EvidenceUploadInput>(files.Count);
            foreach (var file in files)
            {
                uploads.Add(new EvidenceUploadInput(assetId, file.FileName, file.ContentType, file.Content, null, _currentUser.Subject, EvidenceUploadSource.AuthenticatedUser));
            }

            var evidenceResult = await _evidenceService.PrepareEvidenceBatchAsync(organizationId, assignmentId, EvidencePhase.Return, uploads, cancellationToken);
            if (evidenceResult.IsFailure) return Result<AssignmentResponse>.Failure(evidenceResult.Error!);

            var now = _clock.UtcNow;
            var changed = ApplyAssetReturn(assignment, asset, category, request.Resolution, request.ReturnCondition, request.ReturnLocation, request.Notes, organizationId, now);
            if (changed)
            {
                _activity.Add(new ActivityLog(organizationId, "assignment.asset_returned", "assignment", assignment.Id, _currentUser.Subject, assignment.ProtocolNumber, _clock.UtcNow));
                await CompleteLinkedReservationAsync(organizationId, assignment, now, cancellationToken);
            }

            foreach (var evidence in evidenceResult.Value!)
            {
                _activity.Add(new ActivityLog(organizationId, "asset_evidence.uploaded", "asset_evidence", evidence.Id, _currentUser.Subject, evidence.FileName, _clock.UtcNow));
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await _responseBuilder.BuildResponseAsync(_currentUser.OrganizationId, assignmentId, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssignmentResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>Spec 8.8/8.12: gdy wszystkie pozycje wydania powiązanego z rezerwacją zostaną rozliczone
    /// (Assignment.Status przechodzi na Returned), powiązana rezerwacja automatycznie kończy się jako Completed.</summary>
    private async Task CompleteLinkedReservationAsync(Guid organizationId, Assignment assignment, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (assignment.Status != AssignmentStatus.Returned) return;

        var reservation = await _reservations.GetByAssignmentIdAsync(organizationId, assignment.Id, cancellationToken);
        if (reservation is null || reservation.Status != EquipmentReservationStatus.CheckedOut) return;

        reservation.Complete(now);
        _activity.Add(new ActivityLog(organizationId, "reservation.completed", "equipment_reservation", reservation.Id, _currentUser.Subject, reservation.Purpose, now));
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

    public async Task<Result<byte[]>> GetProtocolPdfAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssignmentViewers);
        if (access.IsFailure) return Result<byte[]>.Failure(access.Error!);

        return await GeneratePdfAsync(_currentUser.OrganizationId, id, cancellationToken);
    }

    /// <summary>Wspólna weryfikacja tokenu dla wszystkich publicznych endpointów wydania — ten sam wzorzec co
    /// OffboardingService.ResolveByTokenAsync: zawsze generyczny NotFound, żeby nie ujawniać czy token
    /// istniał/wygasł/został odwołany.</summary>
    private async Task<Result<Assignment>> ResolveByTokenAsync(string token, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var candidate = await _assignments.FindByPublicTokenHashAsync(TokenHasher.Hash(token), cancellationToken);
        if (candidate is not null && PublicTokenService.Verify(token, candidate.PublicTokenHash, candidate.PublicTokenExpiresAt ?? DateTimeOffset.MinValue, candidate.PublicTokenRevokedAt, now))
        {
            return Result<Assignment>.Success(candidate);
        }

        return Result<Assignment>.Failure(Error.NotFound("Wydanie nie istnieje."));
    }

    public async Task<Result<(Guid OrganizationId, Guid AssignmentId)>> ResolvePublicTokenAsync(string token, CancellationToken cancellationToken)
    {
        var resolved = await ResolveByTokenAsync(token, cancellationToken);
        return resolved.IsFailure
            ? Result<(Guid, Guid)>.Failure(resolved.Error!)
            : Result<(Guid, Guid)>.Success((resolved.Value!.OrganizationId, resolved.Value!.Id));
    }

    public async Task<Result<byte[]>> GetPublicProtocolPdfAsync(string token, CancellationToken cancellationToken)
    {
        var resolved = await ResolveByTokenAsync(token, cancellationToken);
        if (resolved.IsFailure) return Result<byte[]>.Failure(resolved.Error!);
        return await GeneratePdfAsync(resolved.Value!.OrganizationId, resolved.Value!.Id, cancellationToken);
    }

    public async Task<Result<PublicAssignmentResponse>> GetPublicAsync(string token, CancellationToken cancellationToken)
    {
        var resolved = await ResolveByTokenAsync(token, cancellationToken);
        if (resolved.IsFailure) return Result<PublicAssignmentResponse>.Failure(resolved.Error!);
        var assignment = resolved.Value!;
        return Result<PublicAssignmentResponse>.Success(await _responseBuilder.MapPublicAsync(assignment.OrganizationId, assignment, cancellationToken));
    }

    public async Task<Result<PublicAssignmentResponse>> AcceptPublicAsync(string token, CancellationToken cancellationToken)
    {
        var resolved = await ResolveByTokenAsync(token, cancellationToken);
        if (resolved.IsFailure) return Result<PublicAssignmentResponse>.Failure(resolved.Error!);
        var assignment = resolved.Value!;

        try
        {
            var evidence = assignment.IntegrityVersion >= 2 ? await _evidence.ListByAssignmentAsync(assignment.OrganizationId, assignment.Id, cancellationToken) : null;
            assignment.Accept(_clock.UtcNow, _currentUser.IpAddress, evidence);
            _activity.Add(new ActivityLog(assignment.OrganizationId, "assignment.accepted", "assignment", assignment.Id, "public-link", assignment.ProtocolNumber, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<PublicAssignmentResponse>.Success(await _responseBuilder.MapPublicAsync(assignment.OrganizationId, assignment, cancellationToken));
        }
        catch (DomainException ex)
        {
            return Result<PublicAssignmentResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<ProcedureDocument>> GetPublicProcedureDocumentAsync(string token, Guid procedureId, Guid documentId, CancellationToken cancellationToken)
    {
        var resolved = await ResolveByTokenAsync(token, cancellationToken);
        if (resolved.IsFailure) return Result<ProcedureDocument>.Failure(resolved.Error!);
        var assignment = resolved.Value!;
        if (assignment.ProcedureAcceptances.All(x => x.ProcedureId != procedureId))
        {
            return Result<ProcedureDocument>.Failure(Error.NotFound("Plik procedury nie istnieje."));
        }

        var document = await _procedures.GetDocumentAsync(assignment.OrganizationId, procedureId, documentId, cancellationToken);
        return document is null
            ? Result<ProcedureDocument>.Failure(Error.NotFound("Plik procedury nie istnieje."))
            : Result<ProcedureDocument>.Success(document);
    }

    private async Task<Result<byte[]>> GeneratePdfAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken)
    {
        var model = await _protocolModelBuilder.BuildAsync(organizationId, assignmentId, cancellationToken);
        if (model.IsFailure) return Result<byte[]>.Failure(model.Error!);
        return Result<byte[]>.Success(_pdfGenerator.GenerateHandoverProtocol(model.Value!));
    }

    private sealed record PreparedAssignment(Assignment Assignment, Domain.People.Person Person, IReadOnlyList<Asset> Assets, IReadOnlyList<Procedure> Procedures);
}
