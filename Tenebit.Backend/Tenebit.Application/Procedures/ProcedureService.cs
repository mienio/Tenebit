using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.Procedures;

namespace Tenebit.Application.Procedures;

public sealed class ProcedureService
{
    private readonly IProcedureRepository _procedures;
    private readonly IAssignmentRepository _assignments;
    private readonly IPersonRepository _people;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ProcedureService(IProcedureRepository procedures, IAssignmentRepository assignments, IPersonRepository people, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork)
    {
        _procedures = procedures;
        _assignments = assignments;
        _people = people;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<ProcedureAcceptanceStatusResponse>>> GetAcceptanceStatusAsync(Guid procedureId, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.Manager, TenebitRoles.ProcedureManager);
        if (access.IsFailure) return Result<IReadOnlyList<ProcedureAcceptanceStatusResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var procedure = await _procedures.GetAsync(organizationId, procedureId, cancellationToken);
        if (procedure is null) return Result<IReadOnlyList<ProcedureAcceptanceStatusResponse>>.Failure(Error.NotFound("Procedura nie istnieje."));

        var assignments = await _assignments.ListAsync(organizationId, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);

        var rows = assignments
            .SelectMany(assignment => assignment.ProcedureAcceptances
                .Where(acceptance => acceptance.ProcedureId == procedureId)
                .Select(acceptance => new ProcedureAcceptanceStatusResponse(
                    acceptance.PersonId,
                    people.FirstOrDefault(p => p.Id == acceptance.PersonId)?.FullName ?? "—",
                    acceptance.Status,
                    acceptance.SentAt,
                    acceptance.AcceptedAt,
                    assignment.ProtocolNumber)))
            .OrderBy(row => row.Status)
            .ThenBy(row => row.PersonName)
            .ToList();

        return Result<IReadOnlyList<ProcedureAcceptanceStatusResponse>>.Success(rows);
    }

    public async Task<IReadOnlyList<ProcedureResponse>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        var procedures = await _procedures.ListAsync(_currentUser.OrganizationId, search, cancellationToken);
        return procedures.Select(Map).ToList();
    }

    public async Task<Result<ProcedureResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var procedure = await _procedures.GetAsync(_currentUser.OrganizationId, id, cancellationToken);
        return procedure is null ? Result<ProcedureResponse>.Failure(Error.NotFound("Procedura nie istnieje.")) : Result<ProcedureResponse>.Success(Map(procedure));
    }

    public async Task<Result<ProcedureResponse>> CreateAsync(CreateProcedureRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.Manager, TenebitRoles.ProcedureManager);
        if (access.IsFailure) return Result<ProcedureResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var procedure = new Procedure(organizationId, request.Title, request.Version, request.Owner, request.RequiresAcceptance);
            procedure.Update(request.Title, request.Version, request.Owner, request.AppliesTo, request.ReviewDate, request.RequiresAcceptance);
            _procedures.Add(procedure);
            _activity.Add(new ActivityLog(organizationId, "procedure.created", "procedure", procedure.Id, _currentUser.Subject, procedure.Title, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ProcedureResponse>.Success(Map(procedure));
        }
        catch (DomainException ex) { return Result<ProcedureResponse>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result<ProcedureResponse>> UpdateAsync(Guid id, UpdateProcedureRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.Manager, TenebitRoles.ProcedureManager);
        if (access.IsFailure) return Result<ProcedureResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var procedure = await _procedures.GetAsync(organizationId, id, cancellationToken);
            if (procedure is null) return Result<ProcedureResponse>.Failure(Error.NotFound("Procedura nie istnieje."));
            procedure.Update(request.Title, request.Version, request.Owner, request.AppliesTo, request.ReviewDate, request.RequiresAcceptance);
            _activity.Add(new ActivityLog(organizationId, "procedure.updated", "procedure", procedure.Id, _currentUser.Subject, procedure.Title, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ProcedureResponse>.Success(Map(procedure));
        }
        catch (DomainException ex) { return Result<ProcedureResponse>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result<ProcedureResponse>> AttachDocumentAsync(Guid id, string fileName, string contentType, byte[] content, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.ProcedureManager);
        if (access.IsFailure) return Result<ProcedureResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var procedure = await _procedures.GetAsync(organizationId, id, cancellationToken);
            if (procedure is null) return Result<ProcedureResponse>.Failure(Error.NotFound("Procedura nie istnieje."));
            var document = procedure.AttachDocument(fileName, contentType, content, _currentUser.Subject, _clock.UtcNow);
            _procedures.AddDocument(document);
            _activity.Add(new ActivityLog(organizationId, "procedure.document_uploaded", "procedure", procedure.Id, _currentUser.Subject, fileName, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ProcedureResponse>.Success(Map(procedure));
        }
        catch (DomainException ex) { return Result<ProcedureResponse>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result<ProcedureDocument>> GetDocumentAsync(Guid procedureId, Guid documentId, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.Manager, TenebitRoles.Employee, TenebitRoles.Auditor, TenebitRoles.ProcedureManager);
        if (access.IsFailure) return Result<ProcedureDocument>.Failure(access.Error!);
        var document = await _procedures.GetDocumentAsync(_currentUser.OrganizationId, procedureId, documentId, cancellationToken);
        return document is null ? Result<ProcedureDocument>.Failure(Error.NotFound("Plik procedury nie istnieje.")) : Result<ProcedureDocument>.Success(document);
    }

    public async Task<Result<ProcedureResponse>> PublishAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.Manager, TenebitRoles.ProcedureManager);
        if (access.IsFailure) return Result<ProcedureResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var procedure = await _procedures.GetAsync(organizationId, id, cancellationToken);
            if (procedure is null) return Result<ProcedureResponse>.Failure(Error.NotFound("Procedura nie istnieje."));
            procedure.Publish(_clock.UtcNow);
            _activity.Add(new ActivityLog(organizationId, "procedure.published", "procedure", procedure.Id, _currentUser.Subject, procedure.Title, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ProcedureResponse>.Success(Map(procedure));
        }
        catch (DomainException ex) { return Result<ProcedureResponse>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result<ProcedureResponse>> ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.Manager, TenebitRoles.ProcedureManager);
        if (access.IsFailure) return Result<ProcedureResponse>.Failure(access.Error!);
        var organizationId = _currentUser.OrganizationId;
        var procedure = await _procedures.GetAsync(organizationId, id, cancellationToken);
        if (procedure is null) return Result<ProcedureResponse>.Failure(Error.NotFound("Procedura nie istnieje."));
        procedure.Archive();
        _activity.Add(new ActivityLog(organizationId, "procedure.archived", "procedure", procedure.Id, _currentUser.Subject, procedure.Title, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<ProcedureResponse>.Success(Map(procedure));
    }

    public async Task<Result<ProcedureResponse>> RemoveDocumentAsync(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.ProcedureManager);
        if (access.IsFailure) return Result<ProcedureResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var procedure = await _procedures.GetAsync(organizationId, id, cancellationToken);
            if (procedure is null) return Result<ProcedureResponse>.Failure(Error.NotFound("Procedura nie istnieje."));
            var document = procedure.RemoveDocument(documentId);
            _procedures.RemoveDocument(document);
            _activity.Add(new ActivityLog(organizationId, "procedure.document_removed", "procedure", procedure.Id, _currentUser.Subject, document.FileName, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ProcedureResponse>.Success(Map(procedure));
        }
        catch (DomainException ex) { return Result<ProcedureResponse>.Failure(Error.Validation(ex.Message)); }
    }

    private static ProcedureResponse Map(Procedure procedure)
    {
        var documents = procedure.Documents
            .OrderByDescending(x => x.UploadedAt)
            .Select(x => new ProcedureDocumentResponse(x.Id, x.FileName, x.ContentType, x.SizeBytes, x.UploadedAt, x.UploadedBy))
            .ToList();
        return new ProcedureResponse(procedure.Id, procedure.Title, procedure.Version, procedure.Owner, procedure.Status, procedure.AppliesTo, procedure.ReviewDate, procedure.RequiresAcceptance, documents, procedure.CreatedAt, procedure.PublishedAt);
    }
}
