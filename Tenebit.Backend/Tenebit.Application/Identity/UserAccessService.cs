using Microsoft.Extensions.Logging;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.Identity;

namespace Tenebit.Application.Identity;

public sealed class UserAccessService
{
    private readonly IOrganizationUserRepository _users;
    private readonly IPersonRepository _people;
    private readonly IOrganizationRepository _organizations;
    private readonly IActivityLogRepository _activity;
    private readonly IPasswordResetTokenRepository _passwordResetTokens;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IDeviceTrustTokenRepository _deviceTrustTokens;
    private readonly IEmailSender _emailSender;
    private readonly IEmailOutboxWriter? _emailOutbox;
    private readonly IAppLinkBuilder _appLinkBuilder;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserAccessService> _logger;
    private readonly IUserSecurityStateCache? _securityStateCache;

    public UserAccessService(
        IOrganizationUserRepository users,
        IPersonRepository people,
        IOrganizationRepository organizations,
        IActivityLogRepository activity,
        IPasswordResetTokenRepository passwordResetTokens,
        IRefreshTokenRepository refreshTokens,
        IDeviceTrustTokenRepository deviceTrustTokens,
        IEmailSender emailSender,
        IAppLinkBuilder appLinkBuilder,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork,
        ILogger<UserAccessService> logger,
        IEmailOutboxWriter? emailOutbox = null,
        IUserSecurityStateCache? securityStateCache = null)
    {
        _users = users;
        _people = people;
        _organizations = organizations;
        _activity = activity;
        _passwordResetTokens = passwordResetTokens;
        _refreshTokens = refreshTokens;
        _deviceTrustTokens = deviceTrustTokens;
        _emailSender = emailSender;
        _emailOutbox = emailOutbox;
        _appLinkBuilder = appLinkBuilder;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _securityStateCache = securityStateCache;
    }

    public IReadOnlyList<RoleResponse> Roles() => TenebitRoles.All.Select(x => new RoleResponse(x.Key, x.Label, x.Description)).ToArray();

    public async Task<Result<IReadOnlyList<OrganizationUserResponse>>> ListAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<IReadOnlyList<OrganizationUserResponse>>.Failure(access.Error!);

        var users = await _users.ListAsync(_currentUser.OrganizationId, cancellationToken);
        return Result<IReadOnlyList<OrganizationUserResponse>>.Success(users.Select(Map).ToList());
    }

    public async Task<Result<OrganizationUserResponse>> CreateAsync(SaveOrganizationUserRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<OrganizationUserResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var roleValidation = ValidateRoles(request.Roles);
            if (roleValidation.IsFailure) return Result<OrganizationUserResponse>.Failure(roleValidation.Error!);
            if (HasRole(request.Roles, TenebitRoles.Owner) && !_currentUser.HasAnyRole(TenebitRoles.Owner))
                return Result<OrganizationUserResponse>.Failure(Error.Forbidden("Tylko właściciel może nadać rolę Właściciela."));
            if (await _users.EmailExistsAsync(organizationId, request.Email, null, cancellationToken)) return Result<OrganizationUserResponse>.Failure(Error.Conflict("Użytkownik z tym adresem e-mail już istnieje."));

            var personLink = await ResolvePersonLinkAsync(organizationId, request.PersonId, null, request.Email, null, cancellationToken);
            if (personLink.Error is not null) return Result<OrganizationUserResponse>.Failure(personLink.Error);

            var user = new OrganizationUser(organizationId, request.Email, request.DisplayName, request.IsActive);
            user.Update(request.Email, request.DisplayName, request.IsActive, request.Roles);
            user.LinkPerson(personLink.PersonId);
            _users.Add(user);
            _activity.Add(new ActivityLog(organizationId, "user.created", "organization_user", user.Id, _currentUser.Subject, user.Email, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (request.IsActive)
            {
                await SendInviteEmailBestEffortAsync(user, cancellationToken);
            }

            return Result<OrganizationUserResponse>.Success(Map(user));
        }
        catch (DomainException ex) { return Result<OrganizationUserResponse>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result<OrganizationUserResponse>> UpdateAsync(Guid id, SaveOrganizationUserRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<OrganizationUserResponse>.Failure(access.Error!);

        try
        {
            // The last-owner check is a check-then-write invariant. Serialize only membership
            // changes for this organization instead of blocking every write in the tenant.
            return await _unitOfWork.ExecuteWithResourceLocksAsync(
                _currentUser.OrganizationId,
                "organization-users",
                [_currentUser.OrganizationId],
                ct => UpdateUnderOrganizationLockAsync(id, request, ct),
                cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OrganizationUserResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    private async Task<Result<OrganizationUserResponse>> UpdateUnderOrganizationLockAsync(Guid id, SaveOrganizationUserRequest request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        var user = await _users.GetAsync(organizationId, id, cancellationToken);
        if (user is null) return Result<OrganizationUserResponse>.Failure(Error.NotFound("Użytkownik nie istnieje."));

        var roleValidation = ValidateRoles(request.Roles);
        if (roleValidation.IsFailure) return Result<OrganizationUserResponse>.Failure(roleValidation.Error!);

        var actorHasOwner = _currentUser.HasAnyRole(TenebitRoles.Owner);
        var targetHadOwner = HasRole(user.Roles.Select(x => x.Role), TenebitRoles.Owner);
        var requestHasOwner = HasRole(request.Roles, TenebitRoles.Owner);

        if (!actorHasOwner && requestHasOwner && !targetHadOwner)
            return Result<OrganizationUserResponse>.Failure(Error.Forbidden("Tylko właściciel może nadać rolę Właściciela."));

        // An Admin must not be able to take over an Owner indirectly by changing the Owner's e-mail,
        // disabling the account or editing its roles. Owner accounts are an owner-only boundary.
        if (!actorHasOwner && targetHadOwner)
            return Result<OrganizationUserResponse>.Failure(Error.Forbidden("Tylko właściciel może modyfikować konto innego właściciela."));

        if (targetHadOwner && (!requestHasOwner || !request.IsActive))
        {
            var allUsers = await _users.ListAsync(organizationId, cancellationToken);
            var remainingActiveOwners = allUsers.Count(u => u.Id != user.Id && u.IsActive && HasRole(u.Roles.Select(x => x.Role), TenebitRoles.Owner));
            if (remainingActiveOwners == 0)
                return Result<OrganizationUserResponse>.Failure(Error.Validation("W firmie musi pozostać co najmniej jeden aktywny właściciel."));
        }

        var existingRoles = user.Roles.Select(x => x.Role).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requestedRoles = request.Roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var personLink = await ResolvePersonLinkAsync(organizationId, request.PersonId, user.PersonId, request.Email, id, cancellationToken);
        if (personLink.Error is not null) return Result<OrganizationUserResponse>.Failure(personLink.Error);

        var securityStateChanged = user.IsActive != request.IsActive ||
                                   !existingRoles.SetEquals(requestedRoles) ||
                                   !string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase) ||
                                   user.PersonId != personLink.PersonId;

        if (await _users.EmailExistsAsync(organizationId, request.Email, id, cancellationToken))
            return Result<OrganizationUserResponse>.Failure(Error.Conflict("Użytkownik z tym adresem e-mail już istnieje."));

        user.Update(request.Email, request.DisplayName, request.IsActive, request.Roles);
        user.LinkPerson(personLink.PersonId);
        _activity.Add(new ActivityLog(organizationId, "user.updated", "organization_user", user.Id, _currentUser.Subject, user.Email, _clock.UtcNow));

        if (securityStateChanged)
        {
            user.RotateSecurityStamp();
            _securityStateCache?.Remove(user.Id);
            await _refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
            await _deviceTrustTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<OrganizationUserResponse>.Success(Map(user));
    }

    private async Task<(Guid? PersonId, Error? Error)> ResolvePersonLinkAsync(
        Guid organizationId,
        Guid? requestedPersonId,
        Guid? existingPersonId,
        string email,
        Guid? excludingUserId,
        CancellationToken cancellationToken)
    {
        // Once a login is explicitly linked, omitting PersonId on an older client preserves that stable
        // identity. For an unlinked login we may safely auto-link an exact e-mail match because Person
        // e-mail is unique inside the organization. Authorization itself never falls back to e-mail.
        var personId = requestedPersonId ?? existingPersonId;
        if (!personId.HasValue)
        {
            var exactMatch = await _people.FindByEmailAsync(organizationId, email, cancellationToken);
            personId = exactMatch?.Id;
        }

        if (!personId.HasValue) return (null, null);

        var person = await _people.GetAsync(organizationId, personId.Value, cancellationToken);
        if (person is null)
            return (null, Error.Validation("Powiązany pracownik nie istnieje w tej firmie."));

        if (await _users.PersonLinkExistsAsync(organizationId, personId.Value, excludingUserId, cancellationToken))
            return (null, Error.Conflict("Ten pracownik jest już powiązany z innym loginem."));

        return (personId, null);
    }

    private async Task SendInviteEmailBestEffortAsync(OrganizationUser user, CancellationToken cancellationToken)
    {
        try
        {
            var code = TokenHasher.NewOneTimeCode();
            var tokenHash = TokenHasher.HashOneTimeCode(user.Email, code);
            var now = _clock.UtcNow;
            var link = _appLinkBuilder.BuildPasswordResetLink(user.Email, code);
            var organization = await _organizations.GetAsync(user.OrganizationId, cancellationToken);
            var (subject, html) = EmailTemplates.OrganizationInvitation(organization?.Language, code, link);

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _passwordResetTokens.RevokeUnusedForUserAsync(user.Id, now, ct);
                _passwordResetTokens.Add(new PasswordResetToken(user.Id, tokenHash, now.AddHours(24)));
                if (_emailOutbox is not null)
                {
                    await _emailOutbox.EnqueueAsync(
                        user.OrganizationId,
                        user.Email,
                        subject,
                        html,
                        "organization-invitation",
                        $"organization-invitation:{user.Id:N}:{tokenHash}",
                        ct);
                }
                await _unitOfWork.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);

            if (_emailOutbox is null)
                await _emailSender.SendAsync(user.Email, subject, html, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się przygotować lub zakolejkować e-maila z zaproszeniem dla użytkownika {UserId}", user.Id);
        }
    }

    private static bool HasRole(IEnumerable<string> roles, string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    private static Result ValidateRoles(IReadOnlyList<string> roles)
    {
        var known = TenebitRoles.All.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalid = roles.FirstOrDefault(role => !known.Contains(role));
        return invalid is null ? Result.Success() : Result.Failure(Error.Validation($"Nieznana rola: {invalid}."));
    }

    private static OrganizationUserResponse Map(OrganizationUser user) => new(user.Id, user.Email, user.DisplayName, user.IsActive, user.Roles.Select(x => x.Role).ToArray(), user.CreatedAt, user.PersonId);
}
