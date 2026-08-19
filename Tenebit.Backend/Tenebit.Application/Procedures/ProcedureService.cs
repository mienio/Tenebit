using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.Procedures;

namespace Tenebit.Application.Procedures;

public sealed class ProcedureService
{
    private static readonly string[] OrgWideReadRoles = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.AssetOperator, TenebitRoles.Auditor, TenebitRoles.ProcedureManager];
    private static readonly string[] ProcedureEditors = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.ProcedureManager];

    private readonly IProcedureRepository _procedures;
    private readonly IAssignmentRepository _assignments;
    private readonly IPersonRepository _people;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ManagerScopeService _managerScope;

    public ProcedureService(IProcedureRepository procedures, IAssignmentRepository assignments, IPersonRepository people, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, ManagerScopeService managerScope)
    {
        _procedures = procedures;
        _assignments = assignments;
        _people = people;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _managerScope = managerScope;
    }

    public async Task<Result<IReadOnlyList<ProcedureAcceptanceStatusResponse>>> GetAcceptanceStatusAsync(Guid procedureId, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.Manager, TenebitRoles.ProcedureManager);
        if (access.IsFailure) return Result<IReadOnlyList<ProcedureAcceptanceStatusResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var procedure = await _procedures.GetAsync(organizationId, procedureId, cancellationToken);
        if (procedure is null) return Result<IReadOnlyList<ProcedureAcceptanceStatusResponse>>.Failure(Error.NotFound("Procedura nie istnieje."));

        var scope = await _managerScope.ResolveAsync(_currentUser, [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.ProcedureManager], cancellationToken);
        IReadOnlyList<Tenebit.Domain.Assignments.Assignment> assignments;
        IReadOnlyList<Tenebit.Domain.People.Person> people;
        if (scope is null)
        {
            assignments = await _assignments.ListAsync(organizationId, cancellationToken);
            people = await _people.ListAsync(organizationId, null, cancellationToken);
        }
        else
        {
            if (!await _assignments.HasProcedureAssignmentForPeopleAsync(organizationId, scope.PersonIds, procedureId, cancellationToken))
            {
                return Result<IReadOnlyList<ProcedureAcceptanceStatusResponse>>.Failure(Error.NotFound("Procedura nie istnieje."));
            }

            assignments = await _assignments.ListByPersonIdsAsync(organizationId, scope.PersonIds, cancellationToken);
            people = await _people.ListScopedAsync(organizationId, null, scope.PersonIds, cancellationToken);
        }

        var rows = assignments
            .SelectMany(assignment => assignment.ProcedureAcceptances
                .Where(acceptance => acceptance.ProcedureId == procedureId)
                .Select(acceptance => new ProcedureAcceptanceStatusResponse(
                    acceptance.PersonId,
                    people.FirstOrDefault(p => p.Id == acceptance.PersonId)?.FullName ?? "-",
                    acceptance.Status,
                    acceptance.SentAt,
                    acceptance.AcceptedAt,
                    assignment.ProtocolNumber,
                    acceptance.ConfirmedIp,
                    acceptance.VerifyIntegrity())))
            .OrderBy(row => row.Status)
            .ThenBy(row => row.PersonName)
            .ToList();

        return Result<IReadOnlyList<ProcedureAcceptanceStatusResponse>>.Success(rows);
    }

    public async Task<Result<IReadOnlyList<ProcedureResponse>>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.ProcedureViewers);
        if (access.IsFailure) return Result<IReadOnlyList<ProcedureResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var scope = await _managerScope.ResolveAsync(_currentUser, OrgWideReadRoles, cancellationToken);
        IReadOnlyList<Procedure> procedures;
        if (scope is null)
        {
            procedures = await _procedures.ListAsync(organizationId, search, cancellationToken);
        }
        else
        {
            var procedureIds = await _assignments.ListProcedureIdsByPersonIdsAsync(organizationId, scope.PersonIds, cancellationToken);
            procedures = FilterAndOrder(await _procedures.GetByIdsAsync(organizationId, procedureIds, cancellationToken), search).ToList();
        }

        var documents = await LoadDocumentMetadataAsync(organizationId, procedures, cancellationToken);
        return Result<IReadOnlyList<ProcedureResponse>>.Success(procedures.Select(procedure => Map(procedure, documents)).ToList());
    }

    public async Task<Result<PagedResult<ProcedureResponse>>> ListPagedAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.ProcedureViewers);
        if (access.IsFailure) return Result<PagedResult<ProcedureResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var scope = await _managerScope.ResolveAsync(_currentUser, OrgWideReadRoles, cancellationToken);
        if (scope is null)
        {
            var (items, total) = await _procedures.ListPagedAsync(organizationId, search, page, pageSize, cancellationToken);
            var documents = await LoadDocumentMetadataAsync(organizationId, items, cancellationToken);
            return Result<PagedResult<ProcedureResponse>>.Success(new PagedResult<ProcedureResponse>(items.Select(item => Map(item, documents)).ToList(), total, Math.Max(page, 1), Math.Clamp(pageSize, 1, 100)));
        }

        var procedureIds = await _assignments.ListProcedureIdsByPersonIdsAsync(organizationId, scope.PersonIds, cancellationToken);
        var procedures = FilterAndOrder(await _procedures.GetByIdsAsync(organizationId, procedureIds, cancellationToken), search).ToList();
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var itemsForPage = procedures.Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize).ToList();
        var pageDocuments = await LoadDocumentMetadataAsync(organizationId, itemsForPage, cancellationToken);
        return Result<PagedResult<ProcedureResponse>>.Success(new PagedResult<ProcedureResponse>(itemsForPage.Select(item => Map(item, pageDocuments)).ToList(), procedures.Count, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<ProcedureResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.ProcedureViewers);
        if (access.IsFailure) return Result<ProcedureResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var scope = await _managerScope.ResolveAsync(_currentUser, OrgWideReadRoles, cancellationToken);
        if (scope is not null && !await _assignments.HasProcedureAssignmentForPeopleAsync(organizationId, scope.PersonIds, id, cancellationToken))
        {
            return Result<ProcedureResponse>.Failure(Error.NotFound("Procedura nie istnieje."));
        }

        var procedure = await _procedures.GetAsync(organizationId, id, cancellationToken);
        if (procedure is null) return Result<ProcedureResponse>.Failure(Error.NotFound("Procedura nie istnieje."));
        return Result<ProcedureResponse>.Success(await MapWithDocumentsAsync(organizationId, procedure, cancellationToken));
    }

    public async Task<Result<ProcedureResponse>> CreateAsync(CreateProcedureRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, ProcedureEditors);
        if (access.IsFailure) return Result<ProcedureResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var procedure = new Procedure(organizationId, request.Title, request.Version, request.Owner, request.RequiresAcceptance);
            procedure.Update(request.Title, request.Version, request.Owner, request.AppliesTo, request.ReviewDate, request.RequiresAcceptance);
            _procedures.Add(procedure);
            _activity.Add(new ActivityLog(organizationId, "procedure.created", "procedure", procedure.Id, _currentUser.Subject, procedure.Title, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ProcedureResponse>.Success(Map(procedure, []));
        }
        catch (DomainException ex)
        {
            return Result<ProcedureResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<ProcedureResponse>> UpdateAsync(Guid id, UpdateProcedureRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, ProcedureEditors);
        if (access.IsFailure) return Result<ProcedureResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        try
        {
            return await _unitOfWork.ExecuteWithResourceLocksAsync(organizationId, "procedure", [id], async ct =>
            {
                var procedure = await _procedures.GetAsync(organizationId, id, ct);
                if (procedure is null) return Result<ProcedureResponse>.Failure(Error.NotFound("Procedura nie istnieje."));
                procedure.Update(request.Title, request.Version, request.Owner, request.AppliesTo, request.ReviewDate, request.RequiresAcceptance);
                _activity.Add(new ActivityLog(organizationId, "procedure.updated", "procedure", procedure.Id, _currentUser.Subject, procedure.Title, _clock.UtcNow));
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<ProcedureResponse>.Success(await MapWithDocumentsAsync(organizationId, procedure, ct));
            }, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<ProcedureResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<ProcedureResponse>> AttachDocumentAsync(Guid id, string fileName, string contentType, byte[] content, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, ProcedureEditors);
        if (access.IsFailure) return Result<ProcedureResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        try
        {
            var validated = ProcedureDocumentValidator.Validate(fileName, content);
            return await _unitOfWork.ExecuteWithResourceLocksAsync(organizationId, "procedure", [id], async ct =>
            {
                var procedure = await _procedures.GetAsync(organizationId, id, ct);
                if (procedure is null) return Result<ProcedureResponse>.Failure(Error.NotFound("Procedura nie istnieje."));

                // Never trust multipart Content-Type. The validator derives a canonical MIME type from verified bytes.
                var document = procedure.AttachDocument(validated.FileName, validated.ContentType, content, _currentUser.Subject, _clock.UtcNow);
                _procedures.AddDocument(document);
                _activity.Add(new ActivityLog(organizationId, "procedure.document_uploaded", "procedure", procedure.Id, _currentUser.Subject, validated.FileName, _clock.UtcNow));
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<ProcedureResponse>.Success(await MapWithDocumentsAsync(organizationId, procedure, ct));
            }, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<ProcedureResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<ProcedureDocument>> GetDocumentAsync(Guid procedureId, Guid documentId, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.Manager, TenebitRoles.Employee, TenebitRoles.AssetOperator, TenebitRoles.Auditor, TenebitRoles.ProcedureManager);
        if (access.IsFailure) return Result<ProcedureDocument>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        if (!_currentUser.HasAnyRole(OrgWideReadRoles))
        {
            if (_currentUser.HasAnyRole(TenebitRoles.Manager))
            {
                var scope = await _managerScope.ResolveAsync(_currentUser, OrgWideReadRoles, cancellationToken);
                if (scope is null || !await _assignments.HasProcedureAssignmentForPeopleAsync(organizationId, scope.PersonIds, procedureId, cancellationToken))
                {
                    return Result<ProcedureDocument>.Failure(Error.NotFound("Plik procedury nie istnieje."));
                }
            }
            else if (_currentUser.PersonId is not { } personId || !await _assignments.HasProcedureAssignmentAsync(organizationId, personId, procedureId, cancellationToken))
            {
                return Result<ProcedureDocument>.Failure(Error.NotFound("Plik procedury nie istnieje."));
            }
        }

        var document = await _procedures.GetDocumentAsync(organizationId, procedureId, documentId, cancellationToken);
        return document is null ? Result<ProcedureDocument>.Failure(Error.NotFound("Plik procedury nie istnieje.")) : Result<ProcedureDocument>.Success(document);
    }

    public async Task<Result<ProcedureResponse>> PublishAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, ProcedureEditors);
        if (access.IsFailure) return Result<ProcedureResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        try
        {
            return await _unitOfWork.ExecuteWithResourceLocksAsync(organizationId, "procedure", [id], async ct =>
            {
                var procedure = await _procedures.GetAsync(organizationId, id, ct);
                if (procedure is null) return Result<ProcedureResponse>.Failure(Error.NotFound("Procedura nie istnieje."));
                var hasDocuments = await _procedures.HasDocumentsAsync(organizationId, id, ct);
                procedure.Publish(_clock.UtcNow, hasDocuments);
                _activity.Add(new ActivityLog(organizationId, "procedure.published", "procedure", procedure.Id, _currentUser.Subject, procedure.Title, _clock.UtcNow));
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<ProcedureResponse>.Success(await MapWithDocumentsAsync(organizationId, procedure, ct));
            }, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<ProcedureResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<ProcedureResponse>> ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, ProcedureEditors);
        if (access.IsFailure) return Result<ProcedureResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        return await _unitOfWork.ExecuteWithResourceLocksAsync(organizationId, "procedure", [id], async ct =>
        {
            var procedure = await _procedures.GetAsync(organizationId, id, ct);
            if (procedure is null) return Result<ProcedureResponse>.Failure(Error.NotFound("Procedura nie istnieje."));
            procedure.Archive();
            _activity.Add(new ActivityLog(organizationId, "procedure.archived", "procedure", procedure.Id, _currentUser.Subject, procedure.Title, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<ProcedureResponse>.Success(await MapWithDocumentsAsync(organizationId, procedure, ct));
        }, cancellationToken);
    }

    public async Task<Result<ProcedureResponse>> RemoveDocumentAsync(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, ProcedureEditors);
        if (access.IsFailure) return Result<ProcedureResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        try
        {
            return await _unitOfWork.ExecuteWithResourceLocksAsync(organizationId, "procedure", [id], async ct =>
            {
                var procedure = await _procedures.GetAsync(organizationId, id, ct);
                if (procedure is null) return Result<ProcedureResponse>.Failure(Error.NotFound("Procedura nie istnieje."));
                procedure.EnsureDocumentsEditable();

                var document = await _procedures.GetDocumentMetadataAsync(organizationId, id, documentId, ct);
                if (document is null) return Result<ProcedureResponse>.Failure(Error.NotFound("Plik procedury nie istnieje."));
                if (!await _procedures.DeleteDocumentAsync(organizationId, id, documentId, ct))
                {
                    return Result<ProcedureResponse>.Failure(Error.NotFound("Plik procedury nie istnieje."));
                }

                _activity.Add(new ActivityLog(organizationId, "procedure.document_removed", "procedure", procedure.Id, _currentUser.Subject, document.FileName, _clock.UtcNow));
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<ProcedureResponse>.Success(await MapWithDocumentsAsync(organizationId, procedure, ct));
            }, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<ProcedureResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    private async Task<IReadOnlyList<ProcedureDocumentMetadata>> LoadDocumentMetadataAsync(Guid organizationId, IEnumerable<Procedure> procedures, CancellationToken cancellationToken)
    {
        var ids = procedures.Select(x => x.Id).Distinct().ToArray();
        return await _procedures.ListDocumentMetadataByProcedureIdsAsync(organizationId, ids, cancellationToken);
    }

    private async Task<ProcedureResponse> MapWithDocumentsAsync(Guid organizationId, Procedure procedure, CancellationToken cancellationToken)
    {
        var documents = await _procedures.ListDocumentMetadataByProcedureIdsAsync(organizationId, [procedure.Id], cancellationToken);
        return Map(procedure, documents);
    }

    private static IEnumerable<Procedure> FilterAndOrder(IEnumerable<Procedure> procedures, string? search)
    {
        var query = procedures;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var phrase = search.Trim();
            query = query.Where(x => x.Title.Contains(phrase, StringComparison.OrdinalIgnoreCase)
                || x.Owner.Contains(phrase, StringComparison.OrdinalIgnoreCase)
                || x.Version.Contains(phrase, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderBy(x => x.Title);
    }

    private static ProcedureResponse Map(Procedure procedure, IReadOnlyList<ProcedureDocumentMetadata> documents)
    {
        var documentResponses = documents
            .Where(x => x.ProcedureId == procedure.Id)
            .OrderByDescending(x => x.UploadedAt)
            .Select(x => new ProcedureDocumentResponse(x.Id, x.FileName, x.ContentType, x.SizeBytes, x.UploadedAt, x.UploadedBy))
            .ToList();
        return new ProcedureResponse(procedure.Id, procedure.Title, procedure.Version, procedure.Owner, procedure.Status, procedure.AppliesTo, procedure.ReviewDate, procedure.RequiresAcceptance, documentResponses, procedure.CreatedAt, procedure.PublishedAt);
    }
}
