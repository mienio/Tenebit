using Microsoft.Extensions.Logging;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Assets;
using Tenebit.Application.Common;
using Tenebit.Application.People;
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
    private readonly IPersonRelationTypeRepository _relationTypes;
    private readonly IActivityLogRepository _activity;
    private readonly IExternalLoginRepository _externalLogins;
    private readonly IPasswordResetTokenRepository _passwordResetTokens;
    private readonly IEmailVerificationTokenRepository _emailVerificationTokens;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IDeviceTrustTokenRepository _deviceTrustTokens;
    private readonly ITwoFactorRecoveryCodeRepository _recoveryCodes;
    private readonly IEmailSender _emailSender;
    private readonly IAppLinkBuilder _appLinkBuilder;
    private readonly IQrCodeGenerator _qrCodeGenerator;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IOrganizationRepository organizations,
        IOrganizationUserRepository users,
        IAssetCategoryRepository categories,
        IPersonRelationTypeRepository relationTypes,
        IActivityLogRepository activity,
        IExternalLoginRepository externalLogins,
        IPasswordResetTokenRepository passwordResetTokens,
        IEmailVerificationTokenRepository emailVerificationTokens,
        IRefreshTokenRepository refreshTokens,
        IDeviceTrustTokenRepository deviceTrustTokens,
        ITwoFactorRecoveryCodeRepository recoveryCodes,
        IEmailSender emailSender,
        IAppLinkBuilder appLinkBuilder,
        IQrCodeGenerator qrCodeGenerator,
        IClock clock,
        IUnitOfWork unitOfWork,
        ILogger<AuthService> logger)
    {
        _organizations = organizations;
        _users = users;
        _categories = categories;
        _relationTypes = relationTypes;
        _activity = activity;
        _externalLogins = externalLogins;
        _passwordResetTokens = passwordResetTokens;
        _emailVerificationTokens = emailVerificationTokens;
        _refreshTokens = refreshTokens;
        _deviceTrustTokens = deviceTrustTokens;
        _recoveryCodes = recoveryCodes;
        _emailSender = emailSender;
        _appLinkBuilder = appLinkBuilder;
        _qrCodeGenerator = qrCodeGenerator;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _logger = logger;
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

            foreach (var relationType in StarterPersonRelationTypes.Create(organization.Id, language))
            {
                _relationTypes.Add(relationType);
            }

            _activity.Add(new ActivityLog(organization.Id, "organization.registered", "organization", organization.Id, user.Email, organization.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await SendVerificationEmailBestEffortAsync(user, cancellationToken);

            return Result<AuthUserResponse>.Success(Map(user, organization));
        }
        catch (DomainException ex)
        {
            return Result<AuthUserResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<LoginOutcome>> LoginAsync(LoginRequest request, string? deviceTrustToken, CancellationToken cancellationToken)
    {
        var user = await _users.FindByEmailAsync(request.Email, cancellationToken);
        if (user is null || !user.IsActive || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Result<LoginOutcome>.Failure(Error.Validation("Nieprawidłowy e-mail lub hasło."));
        }

        var trustedDevice = user.IsTwoFactorEnabled
            && !string.IsNullOrEmpty(deviceTrustToken)
            && await _deviceTrustTokens.FindValidAsync(user.Id, TokenHasher.Hash(deviceTrustToken), _clock.UtcNow, cancellationToken) is not null;

        if (user.IsTwoFactorEnabled && !trustedDevice)
        {
            return Result<LoginOutcome>.Success(new LoginOutcome(true, user.Id, null));
        }

        var organization = await _organizations.GetAsync(user.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return Result<LoginOutcome>.Failure(Error.Validation("Nieprawidłowy e-mail lub hasło."));
        }

        return Result<LoginOutcome>.Success(new LoginOutcome(false, null, Map(user, organization)));
    }

    public async Task<string> IssueDeviceTrustTokenAsync(Guid organizationUserId, CancellationToken cancellationToken)
    {
        var rawToken = TokenHasher.NewRawToken();
        var token = new DeviceTrustToken(organizationUserId, TokenHasher.Hash(rawToken), _clock.UtcNow.AddDays(30));
        _deviceTrustTokens.Add(token);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    public async Task<Result<AuthUserResponse>> CompleteTwoFactorLoginAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive || !user.IsTwoFactorEnabled || string.IsNullOrEmpty(user.TotpSecret))
        {
            return Result<AuthUserResponse>.Failure(Error.Validation("Nieprawidłowy kod uwierzytelniający."));
        }

        var isValidTotp = TotpService.ValidateCode(user.TotpSecret, code);
        if (!isValidTotp && !await TryConsumeRecoveryCodeAsync(userId, code, cancellationToken))
        {
            return Result<AuthUserResponse>.Failure(Error.Validation("Nieprawidłowy kod uwierzytelniający."));
        }

        var organization = await _organizations.GetAsync(user.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return Result<AuthUserResponse>.Failure(Error.Validation("Nie znaleziono organizacji powiązanej z kontem."));
        }

        return Result<AuthUserResponse>.Success(Map(user, organization));
    }

    public async Task<Result<TwoFactorSetupResponse>> SetupTwoFactorAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result<TwoFactorSetupResponse>.Failure(Error.NotFound("Nie znaleziono konta."));
        }

        var secret = TotpService.GenerateSecret();
        user.SetPendingTotpSecret(secret);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var otpAuthUri = TotpService.BuildOtpAuthUri(secret, user.Email);
        var qrSvg = _qrCodeGenerator.CreateTotpQrSvg(otpAuthUri);
        return Result<TwoFactorSetupResponse>.Success(new TwoFactorSetupResponse(secret, otpAuthUri, qrSvg));
    }

    public async Task<Result<TwoFactorEnableResponse>> EnableTwoFactorAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || string.IsNullOrEmpty(user.TotpSecret))
        {
            return Result<TwoFactorEnableResponse>.Failure(Error.Validation("Najpierw wygeneruj sekret 2FA."));
        }

        if (!TotpService.ValidateCode(user.TotpSecret, code))
        {
            return Result<TwoFactorEnableResponse>.Failure(Error.Validation("Nieprawidłowy kod. Sprawdź godzinę na urządzeniu i spróbuj ponownie."));
        }

        user.EnableTwoFactor();
        var rawCodes = await ReplaceRecoveryCodesAsync(userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<TwoFactorEnableResponse>.Success(new TwoFactorEnableResponse(rawCodes));
    }

    public async Task<Result> DisableTwoFactorAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsTwoFactorEnabled || string.IsNullOrEmpty(user.TotpSecret))
        {
            return Result.Failure(Error.Validation("Dwuskładnikowe uwierzytelnianie nie jest włączone."));
        }

        if (!TotpService.ValidateCode(user.TotpSecret, code))
        {
            return Result.Failure(Error.Validation("Nieprawidłowy kod uwierzytelniający."));
        }

        user.DisableTwoFactor();
        var existingCodes = await _recoveryCodes.ListAsync(userId, cancellationToken);
        _recoveryCodes.RemoveAll(existingCodes);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<TwoFactorRecoveryCodesResponse>> RegenerateRecoveryCodesAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsTwoFactorEnabled || string.IsNullOrEmpty(user.TotpSecret))
        {
            return Result<TwoFactorRecoveryCodesResponse>.Failure(Error.Validation("Dwuskładnikowe uwierzytelnianie nie jest włączone."));
        }

        if (!TotpService.ValidateCode(user.TotpSecret, code))
        {
            return Result<TwoFactorRecoveryCodesResponse>.Failure(Error.Validation("Nieprawidłowy kod uwierzytelniający."));
        }

        var rawCodes = await ReplaceRecoveryCodesAsync(userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<TwoFactorRecoveryCodesResponse>.Success(new TwoFactorRecoveryCodesResponse(rawCodes, rawCodes.Count));
    }

    public async Task<Result<int>> GetRecoveryCodesRemainingAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await _recoveryCodes.ListAsync(userId, cancellationToken);
        return Result<int>.Success(existing.Count(x => x.IsUnused));
    }

    private async Task<IReadOnlyList<string>> ReplaceRecoveryCodesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await _recoveryCodes.ListAsync(userId, cancellationToken);
        _recoveryCodes.RemoveAll(existing);

        var rawCodes = new List<string>();
        var entities = new List<Domain.Identity.TwoFactorRecoveryCode>();
        for (var i = 0; i < 10; i++)
        {
            var raw = GenerateRecoveryCode();
            rawCodes.Add(raw);
            entities.Add(new Domain.Identity.TwoFactorRecoveryCode(userId, TokenHasher.Hash(raw.Replace("-", "")), _clock.UtcNow));
        }

        _recoveryCodes.AddRange(entities);
        return rawCodes;
    }

    private async Task<bool> TryConsumeRecoveryCodeAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();
        if (normalized.Length == 0) return false;

        var hash = TokenHasher.Hash(normalized);
        var codes = await _recoveryCodes.ListAsync(userId, cancellationToken);
        var match = codes.FirstOrDefault(x => x.IsUnused && x.CodeHash == hash);
        if (match is null) return false;

        match.MarkUsed(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string GenerateRecoveryCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(10);
        var chars = new char[10];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        }

        return $"{new string(chars, 0, 5)}-{new string(chars, 5, 5)}";
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
            if (!existingUser.IsEmailVerified)
            {
                existingUser.MarkEmailVerified();
            }

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
            user.MarkEmailVerified();
            _users.Add(user);

            _externalLogins.Add(new ExternalLogin(user.Id, info.Provider, info.ProviderUserId));

            foreach (var category in StarterAssetCategories.Create(organization.Id))
            {
                _categories.Add(category);
            }

            foreach (var relationType in StarterPersonRelationTypes.Create(organization.Id, organization.Language))
            {
                _relationTypes.Add(relationType);
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

    public async Task RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.FindByEmailAsync(request.Email, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return;
        }

        var rawToken = TokenHasher.NewRawToken();
        var token = new PasswordResetToken(user.Id, TokenHasher.Hash(rawToken), _clock.UtcNow.AddHours(1));
        _passwordResetTokens.Add(token);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var organization = await _organizations.GetAsync(user.OrganizationId, cancellationToken);
            var link = _appLinkBuilder.BuildPasswordResetLink(rawToken);
            var (subject, html) = EmailTemplates.PasswordReset(organization?.Language, link);
            await _emailSender.SendAsync(user.Email, subject, html, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się wysłać e-maila resetującego hasło do {Email}", user.Email);
        }
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return Result.Failure(Error.Validation("Hasło musi mieć co najmniej 8 znaków."));
        }

        var tokenHash = TokenHasher.Hash(request.Token);
        var token = await _passwordResetTokens.FindValidAsync(tokenHash, _clock.UtcNow, cancellationToken);
        if (token is null)
        {
            return Result.Failure(Error.Validation("Link do resetu hasła jest nieprawidłowy lub wygasł."));
        }

        var user = await _users.GetByIdAsync(token.OrganizationUserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(Error.Validation("Nie znaleziono konta powiązanego z tym linkiem."));
        }

        user.SetPasswordHash(PasswordHasher.Hash(request.NewPassword));
        token.MarkUsed();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = TokenHasher.Hash(request.Token);
        var token = await _emailVerificationTokens.FindValidAsync(tokenHash, _clock.UtcNow, cancellationToken);
        if (token is null)
        {
            return Result.Failure(Error.Validation("Link weryfikacyjny jest nieprawidłowy lub wygasł."));
        }

        var user = await _users.GetByIdAsync(token.OrganizationUserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(Error.Validation("Nie znaleziono konta powiązanego z tym linkiem."));
        }

        if (!user.IsEmailVerified)
        {
            user.MarkEmailVerified();
        }

        token.MarkUsed();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task ResendVerificationEmailAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.IsEmailVerified)
        {
            return;
        }

        await SendVerificationEmailBestEffortAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task SendVerificationEmailBestEffortAsync(OrganizationUser user, CancellationToken cancellationToken)
    {
        try
        {
            var rawToken = TokenHasher.NewRawToken();
            var token = new EmailVerificationToken(user.Id, TokenHasher.Hash(rawToken), _clock.UtcNow.AddHours(48));
            _emailVerificationTokens.Add(token);

            var organization = await _organizations.GetAsync(user.OrganizationId, cancellationToken);
            var link = _appLinkBuilder.BuildEmailVerificationLink(rawToken);
            var (subject, html) = EmailTemplates.EmailVerification(organization?.Language, link);
            await _emailSender.SendAsync(user.Email, subject, html, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się wysłać e-maila weryfikacyjnego do {Email}", user.Email);
        }
    }

    public async Task<Result<IReadOnlyList<string>>> ListLinkedProvidersAsync(Guid userId, CancellationToken cancellationToken)
    {
        var providers = await _externalLogins.ListProvidersAsync(userId, cancellationToken);
        return Result<IReadOnlyList<string>>.Success(providers);
    }

    public async Task<Result> UnlinkProviderAsync(Guid userId, string provider, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("Nie znaleziono konta."));
        }

        var link = await _externalLogins.FindAsync(userId, provider, cancellationToken);
        if (link is null)
        {
            return Result.Failure(Error.NotFound("To konto nie jest połączone z tym dostawcą."));
        }

        var providers = await _externalLogins.ListProvidersAsync(userId, cancellationToken);
        var hasPassword = !string.IsNullOrEmpty(user.PasswordHash);
        if (!hasPassword && providers.Count <= 1)
        {
            return Result.Failure(Error.Validation("Nie możesz odłączyć jedynego sposobu logowania. Ustaw najpierw hasło."));
        }

        _externalLogins.Remove(link);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<string> IssueRefreshTokenAsync(Guid organizationUserId, CancellationToken cancellationToken)
    {
        var rawToken = TokenHasher.NewRawToken();
        var token = new RefreshToken(organizationUserId, TokenHasher.Hash(rawToken), _clock.UtcNow.AddDays(30));
        _refreshTokens.Add(token);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    public async Task<Result<RefreshResult>> RefreshAsync(string rawToken, CancellationToken cancellationToken)
    {
        var tokenHash = TokenHasher.Hash(rawToken);
        var token = await _refreshTokens.FindValidAsync(tokenHash, _clock.UtcNow, cancellationToken);
        if (token is null)
        {
            return Result<RefreshResult>.Failure(Error.Unauthorized("Sesja wygasła. Zaloguj się ponownie."));
        }

        var user = await _users.GetByIdAsync(token.OrganizationUserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result<RefreshResult>.Failure(Error.Unauthorized("Sesja wygasła. Zaloguj się ponownie."));
        }

        var organization = await _organizations.GetAsync(user.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return Result<RefreshResult>.Failure(Error.Unauthorized("Sesja wygasła. Zaloguj się ponownie."));
        }

        token.Revoke();
        var newRawToken = TokenHasher.NewRawToken();
        _refreshTokens.Add(new RefreshToken(user.Id, TokenHasher.Hash(newRawToken), _clock.UtcNow.AddDays(30)));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<RefreshResult>.Success(new RefreshResult(Map(user, organization), newRawToken));
    }

    public async Task RevokeRefreshTokenAsync(string rawToken, CancellationToken cancellationToken)
    {
        var tokenHash = TokenHasher.Hash(rawToken);
        var token = await _refreshTokens.FindValidAsync(tokenHash, _clock.UtcNow, cancellationToken);
        if (token is null) return;
        token.Revoke();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static AuthUserResponse Map(OrganizationUser user, Organization organization) =>
        new(user.Id, organization.Id, organization.Name, user.Email, user.DisplayName, user.Roles.Select(x => x.Role).ToArray(), user.IsEmailVerified, user.IsTwoFactorEnabled);
}
