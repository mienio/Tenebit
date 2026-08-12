using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.Licenses;

namespace Tenebit.Application.Licenses;

public sealed class LicenseService
{
    private readonly ILicenseRepository _licenses;
    private readonly IPersonRepository _people;
    private readonly IRolePermissionRepository _rolePermissions;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public LicenseService(ILicenseRepository licenses, IPersonRepository people, IRolePermissionRepository rolePermissions, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork)
    {
        _licenses = licenses;
        _people = people;
        _rolePermissions = rolePermissions;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<LicenseResponse>>> ListAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.LicenseViewers);
        if (access.IsFailure) return Result<IReadOnlyList<LicenseResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var licenses = await _licenses.ListAsync(organizationId, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var canViewKey = await CanViewLicenseKeysAsync(cancellationToken);
        return Result<IReadOnlyList<LicenseResponse>>.Success(licenses.Select(license => Map(license, people, canViewKey)).ToList());
    }

    public async Task<Result<LicenseResponse>> CreateAsync(CreateLicenseRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.LicenseManager);
        if (access.IsFailure) return Result<LicenseResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var license = new License(organizationId, request.Name, request.Vendor, request.LicenseKey, request.SeatsTotal, request.ExpiresAt, request.Notes);
            _licenses.Add(license);
            _activity.Add(new ActivityLog(organizationId, "license.created", "license", license.Id, _currentUser.Subject, license.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var people = await _people.ListAsync(organizationId, null, cancellationToken);
            return Result<LicenseResponse>.Success(Map(license, people, await CanViewLicenseKeysAsync(cancellationToken)));
        }
        catch (DomainException ex) { return Result<LicenseResponse>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result<LicenseResponse>> UpdateAsync(Guid id, UpdateLicenseRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.LicenseManager);
        if (access.IsFailure) return Result<LicenseResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var license = await _licenses.GetAsync(organizationId, id, cancellationToken);
            if (license is null) return Result<LicenseResponse>.Failure(Error.NotFound("Licencja nie istnieje."));
            license.Update(request.Name, request.Vendor, request.LicenseKey, request.SeatsTotal, request.ExpiresAt, request.Notes);
            _activity.Add(new ActivityLog(organizationId, "license.updated", "license", license.Id, _currentUser.Subject, license.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var people = await _people.ListAsync(organizationId, null, cancellationToken);
            return Result<LicenseResponse>.Success(Map(license, people, await CanViewLicenseKeysAsync(cancellationToken)));
        }
        catch (DomainException ex) { return Result<LicenseResponse>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.LicenseManager);
        if (access.IsFailure) return access;
        var organizationId = _currentUser.OrganizationId;
        var license = await _licenses.GetAsync(organizationId, id, cancellationToken);
        if (license is null) return Result.Failure(Error.NotFound("Licencja nie istnieje."));
        _licenses.Remove(license);
        _activity.Add(new ActivityLog(organizationId, "license.deleted", "license", license.Id, _currentUser.Subject, license.Name, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<LicenseResponse>> AssignSeatAsync(Guid id, AssignLicenseSeatRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.LicenseManager);
        if (access.IsFailure) return Result<LicenseResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var license = await _licenses.GetAsync(organizationId, id, cancellationToken);
            if (license is null) return Result<LicenseResponse>.Failure(Error.NotFound("Licencja nie istnieje."));
            var person = await _people.GetAsync(organizationId, request.PersonId, cancellationToken);
            if (person is null) return Result<LicenseResponse>.Failure(Error.Validation("Wybrana osoba nie istnieje."));
            license.AssignSeat(request.PersonId, _clock.UtcNow);
            _activity.Add(new ActivityLog(organizationId, "license.seat_assigned", "license", license.Id, _currentUser.Subject, $"{license.Name} → {person.FullName}", _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var people = await _people.ListAsync(organizationId, null, cancellationToken);
            return Result<LicenseResponse>.Success(Map(license, people, await CanViewLicenseKeysAsync(cancellationToken)));
        }
        catch (DomainException ex) { return Result<LicenseResponse>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result<LicenseResponse>> UnassignSeatAsync(Guid id, Guid personId, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.LicenseManager);
        if (access.IsFailure) return Result<LicenseResponse>.Failure(access.Error!);
        var organizationId = _currentUser.OrganizationId;
        var license = await _licenses.GetAsync(organizationId, id, cancellationToken);
        if (license is null) return Result<LicenseResponse>.Failure(Error.NotFound("Licencja nie istnieje."));
        license.UnassignSeat(personId);
        _activity.Add(new ActivityLog(organizationId, "license.seat_unassigned", "license", license.Id, _currentUser.Subject, license.Name, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        return Result<LicenseResponse>.Success(Map(license, people, await CanViewLicenseKeysAsync(cancellationToken)));
    }

    private async Task<bool> CanViewLicenseKeysAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.HasAnyRole(TenebitRoles.Owner)) return true;

        var overrides = await _rolePermissions.ListAsync(_currentUser.OrganizationId, cancellationToken);
        var defaults = RolePermissionKeys.DefaultAllowedRoles[RolePermissionKeys.ViewLicenseKeys];

        foreach (var role in _currentUser.Roles)
        {
            var overrideRow = overrides.FirstOrDefault(x => string.Equals(x.RoleKey, role, StringComparison.OrdinalIgnoreCase) && x.PermissionKey == RolePermissionKeys.ViewLicenseKeys);
            var allowed = overrideRow?.Allowed ?? defaults.Contains(role, StringComparer.OrdinalIgnoreCase);
            if (allowed) return true;
        }

        return false;
    }

    private static LicenseResponse Map(License license, IReadOnlyList<Domain.People.Person> people, bool canViewKey) => new(
        license.Id,
        license.Name,
        license.Vendor,
        canViewKey ? license.LicenseKey : null,
        license.LicenseKey is not null,
        canViewKey,
        license.SeatsTotal,
        license.Seats.Count,
        license.ExpiresAt,
        license.Notes,
        license.Seats.Select(seat => new LicenseSeatResponse(seat.PersonId, people.FirstOrDefault(p => p.Id == seat.PersonId)?.FullName ?? "—", seat.AssignedAt)).ToList());
}
