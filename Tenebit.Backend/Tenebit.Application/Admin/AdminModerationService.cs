using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Common;
using Tenebit.Domain.Identity;

namespace Tenebit.Application.Admin;

/// <summary>
/// Every state-changing action the admin panel can perform. Three properties are deliberate and load-bearing:
///
/// 1. Nothing here deletes data. Suspending an organization or blocking a user is fully reversible, so a
///    hijacked admin session cannot destroy a customer's records - only inconvenience them until undone.
/// 2. Every action writes an <see cref="AdminAuditLog"/> row before it returns, including the actor's IP.
///    That table has no delete path, so it is the forensic record if the account is ever taken over.
/// 3. Actions are capped per rolling hour (blast radius). An attacker with a valid token and a stolen TOTP
///    still cannot mass-suspend the customer base; they get a handful of reversible actions and a trail.
/// </summary>
public sealed class AdminModerationService
{
    /// <summary>
    /// Deliberately low. Legitimate moderation is a handful of considered actions per session; a burst
    /// is far more likely to be an attacker than a genuine workflow, and hitting the cap is recoverable
    /// (it clears within the hour) whereas mass-suspending every tenant is not.
    /// </summary>
    public const int MaxActionsPerHour = 10;

    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IDeviceTrustTokenRepository _deviceTrustTokens;
    private readonly IAdminRepository _admin;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IUserSecurityStateCache? _securityStateCache;

    public AdminModerationService(
        IOrganizationRepository organizations,
        IOrganizationUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IDeviceTrustTokenRepository deviceTrustTokens,
        IAdminRepository admin,
        IUnitOfWork unitOfWork,
        IClock clock,
        IUserSecurityStateCache? securityStateCache = null)
    {
        _organizations = organizations;
        _users = users;
        _refreshTokens = refreshTokens;
        _deviceTrustTokens = deviceTrustTokens;
        _admin = admin;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _securityStateCache = securityStateCache;
    }

    public async Task<Result> SuspendOrganizationAsync(Guid organizationId, string reason, string? actorIp, CancellationToken cancellationToken)
    {
        var budget = await EnsureBudgetAsync(cancellationToken);
        if (budget.IsFailure) return budget;

        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        if (organization is null) return Result.Failure(Error.NotFound("Organizacja nie istnieje."));
        if (organization.IsSuspended) return Result.Failure(Error.Conflict("Organizacja jest już zawieszona."));

        try
        {
            organization.Suspend(reason, _clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }

        // Sign-in is blocked by the organization flag itself, but existing access tokens would stay valid
        // until they expire; rotating every member's security stamp cuts those sessions immediately.
        await RevokeOrganizationSessionsAsync(organizationId, cancellationToken);

        _admin.AddAdminAudit(new AdminAuditLog(AdminActions.OrganizationSuspended, "organization", organizationId, organization.Name, reason, actorIp, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RestoreOrganizationAsync(Guid organizationId, string? actorIp, CancellationToken cancellationToken)
    {
        var budget = await EnsureBudgetAsync(cancellationToken);
        if (budget.IsFailure) return budget;

        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        if (organization is null) return Result.Failure(Error.NotFound("Organizacja nie istnieje."));
        if (!organization.IsSuspended) return Result.Failure(Error.Conflict("Organizacja nie jest zawieszona."));

        organization.Restore();
        _admin.AddAdminAudit(new AdminAuditLog(AdminActions.OrganizationRestored, "organization", organizationId, organization.Name, null, actorIp, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SetUserActiveAsync(Guid userId, bool isActive, string? reason, string? actorIp, CancellationToken cancellationToken)
    {
        var budget = await EnsureBudgetAsync(cancellationToken);
        if (budget.IsFailure) return budget;

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return Result.Failure(Error.NotFound("Użytkownik nie istnieje."));
        if (user.IsActive == isActive) return Result.Failure(Error.Conflict(isActive ? "Konto jest już aktywne." : "Konto jest już zablokowane."));

        // Update() is the only way to flip IsActive on the entity; roles and identity fields are passed
        // through unchanged so this stays a pure activation toggle.
        user.Update(user.Email, user.DisplayName, isActive, user.Roles.Select(r => r.Role).ToArray());
        await RevokeUserSessionsAsync(user, cancellationToken);

        _admin.AddAdminAudit(new AdminAuditLog(
            isActive ? AdminActions.UserUnblocked : AdminActions.UserBlocked, "organization_user", userId, user.Email, reason, actorIp, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ForceSignOutAsync(Guid userId, string? actorIp, CancellationToken cancellationToken)
    {
        var budget = await EnsureBudgetAsync(cancellationToken);
        if (budget.IsFailure) return budget;

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return Result.Failure(Error.NotFound("Użytkownik nie istnieje."));

        await RevokeUserSessionsAsync(user, cancellationToken);
        _admin.AddAdminAudit(new AdminAuditLog(AdminActions.UserForcedSignOut, "organization_user", userId, user.Email, null, actorIp, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Records that an organization's name was checked against the terms of service - the point of the
    /// panel's review queue. Unlike the other actions here it changes nobody's access, so it needs no
    /// step-up code and does not consume the hourly moderation budget; it only appends to the audit trail.
    /// </summary>
    public async Task<Result> MarkReviewedAsync(Guid organizationId, string? actorIp, CancellationToken cancellationToken)
    {
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        if (organization is null) return Result.Failure(Error.NotFound("Organizacja nie istnieje."));

        _admin.AddAdminAudit(new AdminAuditLog(
            AdminActions.OrganizationReviewed, "organization", organizationId, organization.Name, null, actorIp, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task RevokeOrganizationSessionsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var members = await _users.ListAsync(organizationId, cancellationToken);
        foreach (var member in members)
        {
            await RevokeUserSessionsAsync(member, cancellationToken);
        }
    }

    private async Task RevokeUserSessionsAsync(OrganizationUser user, CancellationToken cancellationToken)
    {
        user.RotateSecurityStamp();
        _securityStateCache?.Remove(user.Id);
        await _refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
        await _deviceTrustTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
    }

    private async Task<Result> EnsureBudgetAsync(CancellationToken cancellationToken)
    {
        var used = await _admin.CountRecentModerationActionsAsync(_clock.UtcNow.AddHours(-1), cancellationToken);
        return used >= MaxActionsPerHour
            ? Result.Failure(Error.Validation(
                $"Przekroczono limit {MaxActionsPerHour} akcji moderacyjnych na godzinę. To zabezpieczenie przed masowym działaniem z przejętego konta — odczekaj i spróbuj ponownie."))
            : Result.Success();
    }
}
