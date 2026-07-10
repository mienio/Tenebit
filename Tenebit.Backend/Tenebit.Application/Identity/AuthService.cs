using Tenebit.Application.Abstractions;
using Tenebit.Application.Assets;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.Identity;
using Tenebit.Domain.Organizations;

namespace Tenebit.Application.Identity;

public sealed class AuthService
{
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationUserRepository _users;
    private readonly IAssetCategoryRepository _categories;
    private readonly IActivityLogRepository _activity;
    private readonly IExternalLoginRepository _externalLogins;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public AuthService(IOrganizationRepository organizations, IOrganizationUserRepository users, IAssetCategoryRepository categories, IActivityLogRepository activity, IExternalLoginRepository externalLogins, IClock clock, IUnitOfWork unitOfWork)
    {
        _organizations = organizations;
        _users = users;
        _categories = categories;
        _activity = activity;
        _externalLogins = externalLogins;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthUserResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return Result<AuthUserResponse>.Failure(Error.Validation("Hasło musi mieć co najmniej 8 znaków."));
        }

        if (await _users.FindByEmailAsync(request.Email, cancellationToken) is not null)
        {
            return Result<AuthUserResponse>.Failure(Error.Conflict("Użytkownik z tym adresem e-mail już istnieje."));
        }

        try
        {
            var currency = string.IsNullOrWhiteSpace(request.Currency) ? "PLN" : request.Currency.Trim().ToUpperInvariant();
            var language = string.IsNullOrWhiteSpace(request.Language) ? "pl" : request.Language.Trim().ToLowerInvariant();
            var organization = new Organization(request.OrganizationName, "PL", language, currency, "Europe/Warsaw");
            _organizations.Add(organization);

            var user = new OrganizationUser(organization.Id, request.Email, request.DisplayName, true);
            user.Update(request.Email, request.DisplayName, true, [TenebitRoles.Owner]);
            user.SetPasswordHash(PasswordHasher.Hash(request.Password));
            _users.Add(user);

            foreach (var category in StarterAssetCategories.Create(organization.Id))
            {
                _categories.Add(category);
            }

            _activity.Add(new ActivityLog(organization.Id, "organization.registered", "organization", organization.Id, user.Email, organization.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<AuthUserResponse>.Success(Map(user, organization));
        }
        catch (DomainException ex)
        {
            return Result<AuthUserResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<AuthUserResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.FindByEmailAsync(request.Email, cancellationToken);
        if (user is null || !user.IsActive || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Result<AuthUserResponse>.Failure(Error.Validation("Nieprawidłowy e-mail lub hasło."));
        }

        var organization = await _organizations.GetAsync(user.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return Result<AuthUserResponse>.Failure(Error.Validation("Nieprawidłowy e-mail lub hasło."));
        }

        return Result<AuthUserResponse>.Success(Map(user, organization));
    }

    public async Task<Result<AuthUserResponse>> ExternalLoginAsync(ExternalUserInfo info, CancellationToken cancellationToken)
    {
        var linkedUser = await _externalLogins.FindLinkedUserAsync(info.Provider, info.ProviderUserId, cancellationToken);
        if (linkedUser is not null)
        {
            var linkedOrganization = await _organizations.GetAsync(linkedUser.OrganizationId, cancellationToken);
            if (linkedOrganization is null)
            {
                return Result<AuthUserResponse>.Failure(Error.Validation("Nie znaleziono organizacji powiązanej z kontem."));
            }

            return Result<AuthUserResponse>.Success(Map(linkedUser, linkedOrganization));
        }

        if (string.IsNullOrWhiteSpace(info.Email))
        {
            return Result<AuthUserResponse>.Failure(Error.Validation("Dostawca logowania nie udostępnił adresu e-mail. Wyraź zgodę na udostępnienie e-maila i spróbuj ponownie."));
        }

        var existingUser = await _users.FindByEmailAsync(info.Email, cancellationToken);
        if (existingUser is not null)
        {
            if (!info.EmailVerified)
            {
                return Result<AuthUserResponse>.Failure(Error.Validation("E-mail z tego dostawcy nie jest zweryfikowany. Zaloguj się hasłem i połącz konto w ustawieniach."));
            }

            _externalLogins.Add(new ExternalLogin(existingUser.Id, info.Provider, info.ProviderUserId));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var existingOrganization = await _organizations.GetAsync(existingUser.OrganizationId, cancellationToken);
            if (existingOrganization is null)
            {
                return Result<AuthUserResponse>.Failure(Error.Validation("Nie znaleziono organizacji powiązanej z kontem."));
            }

            return Result<AuthUserResponse>.Success(Map(existingUser, existingOrganization));
        }

        try
        {
            var displayName = string.IsNullOrWhiteSpace(info.DisplayName) ? info.Email.Split('@')[0] : info.DisplayName;
            var organization = new Organization($"{displayName} — organizacja", "PL", "pl", "PLN", "Europe/Warsaw");
            _organizations.Add(organization);

            var user = new OrganizationUser(organization.Id, info.Email, displayName, true);
            user.Update(info.Email, displayName, true, [TenebitRoles.Owner]);
            _users.Add(user);

            _externalLogins.Add(new ExternalLogin(user.Id, info.Provider, info.ProviderUserId));

            foreach (var category in StarterAssetCategories.Create(organization.Id))
            {
                _categories.Add(category);
            }

            _activity.Add(new ActivityLog(organization.Id, "organization.registered_via_oauth", "organization", organization.Id, user.Email, organization.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<AuthUserResponse>.Success(Map(user, organization));
        }
        catch (DomainException ex)
        {
            return Result<AuthUserResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    private static AuthUserResponse Map(OrganizationUser user, Organization organization) =>
        new(user.Id, organization.Id, organization.Name, user.Email, user.DisplayName, user.Roles.Select(x => x.Role).ToArray());
}
