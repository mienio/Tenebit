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
    private readonly IOrganizationRepository _organizations;
    private readonly IActivityLogRepository _activity;
    private readonly IPasswordResetTokenRepository _passwordResetTokens;
    private readonly IEmailSender _emailSender;
    private readonly IAppLinkBuilder _appLinkBuilder;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserAccessService> _logger;

    public UserAccessService(
        IOrganizationUserRepository users,
        IOrganizationRepository organizations,
        IActivityLogRepository activity,
        IPasswordResetTokenRepository passwordResetTokens,
        IEmailSender emailSender,
        IAppLinkBuilder appLinkBuilder,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork,
        ILogger<UserAccessService> logger)
    {
        _users = users;
        _organizations = organizations;
        _activity = activity;
        _passwordResetTokens = passwordResetTokens;
        _emailSender = emailSender;
        _appLinkBuilder = appLinkBuilder;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _logger = logger;
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
            if (await _users.EmailExistsAsync(organizationId, request.Email, null, cancellationToken)) return Result<OrganizationUserResponse>.Failure(Error.Conflict("Użytkownik z tym adresem e-mail już istnieje."));
            var user = new OrganizationUser(organizationId, request.Email, request.DisplayName, request.IsActive);
            user.Update(request.Email, request.DisplayName, request.IsActive, request.Roles);
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
            var organizationId = _currentUser.OrganizationId;
            var user = await _users.GetAsync(organizationId, id, cancellationToken);
            if (user is null) return Result<OrganizationUserResponse>.Failure(Error.NotFound("Użytkownik nie istnieje."));
            var roleValidation = ValidateRoles(request.Roles);
            if (roleValidation.IsFailure) return Result<OrganizationUserResponse>.Failure(roleValidation.Error!);
            if (await _users.EmailExistsAsync(organizationId, request.Email, id, cancellationToken)) return Result<OrganizationUserResponse>.Failure(Error.Conflict("Użytkownik z tym adresem e-mail już istnieje."));
            user.Update(request.Email, request.DisplayName, request.IsActive, request.Roles);
            _activity.Add(new ActivityLog(organizationId, "user.updated", "organization_user", user.Id, _currentUser.Subject, user.Email, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<OrganizationUserResponse>.Success(Map(user));
        }
        catch (DomainException ex) { return Result<OrganizationUserResponse>.Failure(Error.Validation(ex.Message)); }
    }

    private async Task SendInviteEmailBestEffortAsync(OrganizationUser user, CancellationToken cancellationToken)
    {
        try
        {
            var rawToken = TokenHasher.NewRawToken();
            var token = new PasswordResetToken(user.Id, TokenHasher.Hash(rawToken), _clock.UtcNow.AddHours(24));
            _passwordResetTokens.Add(token);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var link = _appLinkBuilder.BuildPasswordResetLink(rawToken);
            var organization = await _organizations.GetAsync(user.OrganizationId, cancellationToken);
            var (subject, html) = EmailTemplates.OrganizationInvitation(organization?.Language, link);
            await _emailSender.SendAsync(user.Email, subject, html, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się wysłać e-maila z zaproszeniem do {Email}", user.Email);
        }
    }

    private static Result ValidateRoles(IReadOnlyList<string> roles)
    {
        var known = TenebitRoles.All.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalid = roles.FirstOrDefault(role => !known.Contains(role));
        return invalid is null ? Result.Success() : Result.Failure(Error.Validation($"Nieznana rola: {invalid}."));
    }

    private static OrganizationUserResponse Map(OrganizationUser user) => new(user.Id, user.Email, user.DisplayName, user.IsActive, user.Roles.Select(x => x.Role).ToArray(), user.CreatedAt);
}
