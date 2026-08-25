using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;

namespace Tenebit.Application.Assets;

public sealed record MaintenanceScheduleResponse(
    Guid Id,
    Guid AssetId,
    string AssetName,
    string? AssetTag,
    string Name,
    int IntervalMonths,
    string NextDueOn,
    string? LastPerformedOn,
    string? LastPerformedBy,
    bool IsActive,
    int DaysRemaining,
    /// <summary>0-100 share of the current cycle already elapsed; the UI renders it as a bar.</summary>
    int CycleProgress);

[ValidatedRequest]
public sealed record SaveMaintenanceScheduleRequest(
    Guid AssetId,
    [property: System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(160, MinimumLength = 1)] string Name,
    [property: System.ComponentModel.DataAnnotations.Range(1, 120)] int IntervalMonths,
    DateOnly NextDueOn);

[ValidatedRequest]
public sealed record CompleteMaintenanceRequest(DateOnly? PerformedOn, [property: System.ComponentModel.DataAnnotations.StringLength(240)] string? PerformedBy);

/// <summary>
/// Recurring maintenance deadlines. Read access follows the asset module (anyone who may see assets may
/// see what is due on them); creating and completing follows the operator roles, because completing an
/// inspection is a record of fact, not a note.
/// </summary>
public sealed class MaintenanceService
{
    private readonly IMaintenanceScheduleRepository _schedules;
    private readonly IAssetRepository _assets;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public MaintenanceService(
        IMaintenanceScheduleRepository schedules,
        IAssetRepository assets,
        IActivityLogRepository activity,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _schedules = schedules;
        _assets = assets;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<MaintenanceScheduleResponse>>> ListAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<IReadOnlyList<MaintenanceScheduleResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var schedules = await _schedules.ListAsync(organizationId, cancellationToken);
        return Result<IReadOnlyList<MaintenanceScheduleResponse>>.Success(await MapAsync(organizationId, schedules, cancellationToken));
    }

    /// <summary>Everything due within <paramref name="withinDays"/>, plus anything already overdue.</summary>
    public async Task<Result<IReadOnlyList<MaintenanceScheduleResponse>>> ListDueAsync(int withinDays, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<IReadOnlyList<MaintenanceScheduleResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var schedules = await _schedules.ListDueAsync(organizationId, today.AddDays(Math.Clamp(withinDays, 1, 365)), cancellationToken);
        return Result<IReadOnlyList<MaintenanceScheduleResponse>>.Success(await MapAsync(organizationId, schedules, cancellationToken));
    }

    public async Task<Result<MaintenanceScheduleResponse>> CreateAsync(SaveMaintenanceScheduleRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Technician);
        if (access.IsFailure) return Result<MaintenanceScheduleResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var asset = await _assets.GetAsync(organizationId, request.AssetId, cancellationToken);
        if (asset is null) return Result<MaintenanceScheduleResponse>.Failure(Error.NotFound("Aktywo nie istnieje."));

        try
        {
            var schedule = new MaintenanceSchedule(organizationId, request.AssetId, request.Name, request.IntervalMonths, request.NextDueOn, _clock.UtcNow);
            _schedules.Add(schedule);
            _activity.Add(new ActivityLog(organizationId, "maintenance.created", "maintenance_schedule", schedule.Id, _currentUser.Subject, schedule.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<MaintenanceScheduleResponse>.Success((await MapAsync(organizationId, [schedule], cancellationToken))[0]);
        }
        catch (DomainException ex)
        {
            return Result<MaintenanceScheduleResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<MaintenanceScheduleResponse>> CompleteAsync(Guid id, CompleteMaintenanceRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Technician);
        if (access.IsFailure) return Result<MaintenanceScheduleResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var schedule = await _schedules.GetAsync(organizationId, id, cancellationToken);
        if (schedule is null) return Result<MaintenanceScheduleResponse>.Failure(Error.NotFound("Przegląd nie istnieje."));

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var performedOn = request.PerformedOn ?? today;
        if (performedOn > today) return Result<MaintenanceScheduleResponse>.Failure(Error.Validation("Data wykonania nie może być w przyszłości."));

        schedule.MarkPerformed(performedOn, request.PerformedBy ?? _currentUser.Subject);
        _activity.Add(new ActivityLog(organizationId, "maintenance.completed", "maintenance_schedule", schedule.Id, _currentUser.Subject, schedule.Name, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<MaintenanceScheduleResponse>.Success((await MapAsync(organizationId, [schedule], cancellationToken))[0]);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var schedule = await _schedules.GetAsync(organizationId, id, cancellationToken);
        if (schedule is null) return Result.Failure(Error.NotFound("Przegląd nie istnieje."));

        _schedules.Remove(schedule);
        _activity.Add(new ActivityLog(organizationId, "maintenance.deleted", "maintenance_schedule", schedule.Id, _currentUser.Subject, schedule.Name, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<IReadOnlyList<MaintenanceScheduleResponse>> MapAsync(
        Guid organizationId, IReadOnlyList<MaintenanceSchedule> schedules, CancellationToken cancellationToken)
    {
        if (schedules.Count == 0) return [];

        var assetIds = schedules.Select(x => x.AssetId).Distinct().ToArray();
        var assets = (await _assets.GetByIdsAsync(organizationId, assetIds, cancellationToken)).ToDictionary(x => x.Id);
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

        return schedules.Select(schedule =>
        {
            assets.TryGetValue(schedule.AssetId, out var asset);
            return new MaintenanceScheduleResponse(
                schedule.Id,
                schedule.AssetId,
                asset?.Name ?? "—",
                asset?.AssetTag,
                schedule.Name,
                schedule.IntervalMonths,
                schedule.NextDueOn.ToString("yyyy-MM-dd"),
                schedule.LastPerformedOn?.ToString("yyyy-MM-dd"),
                schedule.LastPerformedBy,
                schedule.IsActive,
                schedule.DaysRemaining(today),
                CycleProgress(schedule, today));
        }).ToArray();
    }

    /// <summary>
    /// How far through the current cycle we are, as 0-100. Measured backwards from the due date so it
    /// works even for a schedule that has never been performed yet (no start date to measure from).
    /// </summary>
    private static int CycleProgress(MaintenanceSchedule schedule, DateOnly today)
    {
        var cycleStart = schedule.NextDueOn.AddMonths(-schedule.IntervalMonths);
        var totalDays = schedule.NextDueOn.DayNumber - cycleStart.DayNumber;
        if (totalDays <= 0) return 100;

        var elapsed = today.DayNumber - cycleStart.DayNumber;
        return Math.Clamp((int)Math.Round(elapsed * 100.0 / totalDays), 0, 100);
    }
}
