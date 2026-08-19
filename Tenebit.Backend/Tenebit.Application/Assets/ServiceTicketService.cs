using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;

namespace Tenebit.Application.Assets;

public sealed class ServiceTicketService
{
    private readonly IServiceTicketRepository _tickets;
    private readonly IAssetRepository _assets;
    private readonly IAssetInspectionRepository _inspections;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AssetAuthorizationService _assetAuthorization;

    public ServiceTicketService(IServiceTicketRepository tickets, IAssetRepository assets, IAssetInspectionRepository inspections, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, AssetAuthorizationService assetAuthorization)
    {
        _tickets = tickets;
        _assets = assets;
        _inspections = inspections;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _assetAuthorization = assetAuthorization;
    }

    public async Task<Result<IReadOnlyList<ServiceTicketResponse>>> ListByAssetAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<IReadOnlyList<ServiceTicketResponse>>.Failure(access.Error!);

        var assetAccess = await _assetAuthorization.EnsureCanViewAsync(assetId, cancellationToken);
        if (assetAccess.IsFailure) return Result<IReadOnlyList<ServiceTicketResponse>>.Failure(assetAccess.Error!);
        var tickets = await _tickets.ListByAssetAsync(_currentUser.OrganizationId, assetId, cancellationToken);
        return Result<IReadOnlyList<ServiceTicketResponse>>.Success(tickets.Select(Map).ToList());
    }

    public async Task<Result<ServiceTicketListResponse>> ListPagedAsync(ServiceTicketStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<ServiceTicketListResponse>.Failure(access.Error!);

        var scope = await _assetAuthorization.ResolveListScopeAsync(cancellationToken);
        var (items, total) = scope is null
            ? await _tickets.ListPagedAsync(_currentUser.OrganizationId, status, page, pageSize, cancellationToken)
            : await _tickets.ListPagedScopedAsync(_currentUser.OrganizationId, status, page, pageSize, scope.PersonIds, scope.TeamIds, cancellationToken);
        return Result<ServiceTicketListResponse>.Success(new ServiceTicketListResponse(items.Select(Map).ToList(), total));
    }

    public async Task<Result<ServiceTicketResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<ServiceTicketResponse>.Failure(access.Error!);

        var ticket = await _tickets.GetAsync(_currentUser.OrganizationId, id, cancellationToken);
        if (ticket is null) return Result<ServiceTicketResponse>.Failure(Error.NotFound("Zgłoszenie serwisowe nie istnieje."));
        var assetAccess = await _assetAuthorization.EnsureCanViewAsync(ticket.AssetId, cancellationToken);
        if (assetAccess.IsFailure) return Result<ServiceTicketResponse>.Failure(Error.NotFound("Zgłoszenie serwisowe nie istnieje."));
        return Result<ServiceTicketResponse>.Success(Map(ticket));
    }

    public async Task<Result<ServiceTicketResponse>> OpenAsync(OpenServiceTicketRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Technician);
        if (access.IsFailure) return Result<ServiceTicketResponse>.Failure(access.Error!);

        try
        {
            var organizationId = _currentUser.OrganizationId;
            var asset = await _assets.GetAsync(organizationId, request.AssetId, cancellationToken);
            if (asset is null) return Result<ServiceTicketResponse>.Failure(Error.NotFound("Aktywo nie istnieje."));

            if (request.AssetInspectionId.HasValue)
            {
                var inspection = await _inspections.GetAsync(organizationId, request.AssetInspectionId.Value, cancellationToken);
                if (inspection is null)
                    return Result<ServiceTicketResponse>.Failure(Error.Validation("Wybrana inspekcja aktywa nie istnieje."));
                if (inspection.AssetId != request.AssetId)
                    return Result<ServiceTicketResponse>.Failure(Error.Validation("Wybrana inspekcja dotyczy innego aktywa."));
            }

            var ticket = new ServiceTicket(organizationId, request.AssetId, request.Vendor, request.Description, request.AssetInspectionId);
            ticket.UpdateDetails(request.Vendor, request.Description, request.EstimatedCost, request.Currency, request.SlaDueAt);

            asset.ChangeStatus(AssetStatus.InService);

            _tickets.Add(ticket);
            _activity.Add(new ActivityLog(organizationId, "asset.service_ticket_opened", "asset", asset.Id, _currentUser.Subject, asset.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ServiceTicketResponse>.Success(Map(ticket));
        }
        catch (DomainException ex)
        {
            return Result<ServiceTicketResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<ServiceTicketResponse>> UpdateAsync(Guid id, UpdateServiceTicketRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Technician);
        if (access.IsFailure) return Result<ServiceTicketResponse>.Failure(access.Error!);

        try
        {
            var organizationId = _currentUser.OrganizationId;
            var ticket = await _tickets.GetAsync(organizationId, id, cancellationToken);
            if (ticket is null) return Result<ServiceTicketResponse>.Failure(Error.NotFound("Zgłoszenie serwisowe nie istnieje."));

            ticket.UpdateDetails(request.Vendor, request.Description, request.EstimatedCost, request.Currency, request.SlaDueAt);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ServiceTicketResponse>.Success(Map(ticket));
        }
        catch (DomainException ex)
        {
            return Result<ServiceTicketResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<ServiceTicketResponse>> CompleteAsync(Guid id, CompleteServiceTicketRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Technician);
        if (access.IsFailure) return Result<ServiceTicketResponse>.Failure(access.Error!);

        try
        {
            var organizationId = _currentUser.OrganizationId;
            var ticket = await _tickets.GetAsync(organizationId, id, cancellationToken);
            if (ticket is null) return Result<ServiceTicketResponse>.Failure(Error.NotFound("Zgłoszenie serwisowe nie istnieje."));

            var asset = await _assets.GetAsync(organizationId, ticket.AssetId, cancellationToken);
            if (asset is null) return Result<ServiceTicketResponse>.Failure(Error.NotFound("Aktywo nie istnieje."));

            if (request.ResultStatus is not (AssetStatus.InStock or AssetStatus.Damaged or AssetStatus.Retired or AssetStatus.Disposed))
            {
                return Result<ServiceTicketResponse>.Failure(Error.Validation("Nieprawidłowy status docelowy aktywa po zamknięciu zgłoszenia serwisowego."));
            }

            ticket.Complete(request.ActualCost, request.Resolution);
            asset.ChangeStatus(request.ResultStatus);

            _activity.Add(new ActivityLog(organizationId, "asset.service_ticket_completed", "asset", asset.Id, _currentUser.Subject, asset.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ServiceTicketResponse>.Success(Map(ticket));
        }
        catch (DomainException ex)
        {
            return Result<ServiceTicketResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<ServiceTicketResponse>> CancelAsync(Guid id, CancelServiceTicketRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Technician);
        if (access.IsFailure) return Result<ServiceTicketResponse>.Failure(access.Error!);

        try
        {
            var organizationId = _currentUser.OrganizationId;
            var ticket = await _tickets.GetAsync(organizationId, id, cancellationToken);
            if (ticket is null) return Result<ServiceTicketResponse>.Failure(Error.NotFound("Zgłoszenie serwisowe nie istnieje."));

            ticket.Cancel(request.Resolution);
            _activity.Add(new ActivityLog(organizationId, "asset.service_ticket_cancelled", "asset", ticket.AssetId, _currentUser.Subject, null, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ServiceTicketResponse>.Success(Map(ticket));
        }
        catch (DomainException ex)
        {
            return Result<ServiceTicketResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    private static ServiceTicketResponse Map(ServiceTicket ticket) => new(
        ticket.Id,
        ticket.AssetId,
        ticket.AssetInspectionId,
        ticket.Vendor,
        ticket.Description,
        ticket.EstimatedCost,
        ticket.ActualCost,
        ticket.Currency,
        ticket.OpenedAt,
        ticket.SlaDueAt,
        ticket.ClosedAt,
        ticket.Status,
        ticket.Resolution);
}
