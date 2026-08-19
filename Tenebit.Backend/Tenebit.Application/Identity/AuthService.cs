using Microsoft.Extensions.Logging;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Alerts;
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
    private readonly IAlertRuleRepository _rules;
    private readonly IActivityLogRepository _activity;
    private readonly IExternalLoginRepository _externalLogins;
    private readonly IPasswordResetTokenRepository _passwordResetTokens;
    private readonly IEmailVerificationTokenRepository _emailVerificationTokens;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IDeviceTrustTokenRepository _deviceTrustTokens;
    private readonly ITwoFactorRecoveryCodeRepository _recoveryCodes;
    private readonly IEmailSender _emailSender;
    private readonly IEmailOutboxWriter? _emailOutbox;
    private readonly IAppLinkBuilder _appLinkBuilder;
    private readonly IQrCodeGenerator _qrCodeGenerator;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuthService> _logger;
    private readonly IUserSecurityStateCache? _securityStateCache;
    private readonly IEmailAvailability? _emailAvailability;

    public AuthService(
        IOrganizationRepository organizations,
        IOrganizationUserRepository users,
        IAssetCategoryRepository categories,
        IPersonRelationTypeRepository relationTypes,
        IAlertRuleRepository rules,
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
        ILogger<AuthService> logger,
        IEmailOutboxWriter? emailOutbox = null,
        IUserSecurityStateCache? securityStateCache = null,
        IEmailAvailability? emailAvailability = null)
    {
        _organizations = organizations;
        _users = users;
        _categories = categories;
        _relationTypes = relationTypes;
        _rules = rules;
        _activity = activity;
        _externalLogins = externalLogins;
        _passwordResetTokens = passwordResetTokens;
        _emailVerificationTokens = emailVerificationTokens;
        _refreshTokens = refreshTokens;
        _deviceTrustTokens = deviceTrustTokens;
        _recoveryCodes = recoveryCodes;
        _emailSender = emailSender;
        _emailOutbox = emailOutbox;
        _appLinkBuilder = appLinkBuilder;
        _qrCodeGenerator = qrCodeGenerator;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _securityStateCache = securityStateCache;
        _emailAvailability = emailAvailability;
    }

    // No SMTP means the verification code can never reach the user, so the verification gate would
    // permanently lock every new account out. Auto-verify instead of blocking login; once a real
    // Email:Enabled=true SMTP config is deployed, registration requires the delivered one-time code.
    private bool AutoVerifyEmailOnRegister => _emailAvailability is not null && !_emailAvailability.Enabled;

    public bool RequiresEmailVerificationOnRegister => !AutoVerifyEmailOnRegister;

    public async Task<Result<AuthUserResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!request.AcceptTerms)
        {
            return Result<AuthUserResponse>.Failure(Error.Validation("Akceptacja regulaminu i polityki prywatności jest wymagana."));
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return Result<AuthUserResponse>.Failure(Error.Validation("Hasło musi mieć co najmniej 8 znaków."));
        }

        var existingUser = await _users.FindByEmailAsync(request.Email, cancellationToken);
        if (existingUser is not null)
        {
            // Registration must not disclose account existence. The endpoint returns the same 202 shape.
            if (!existingUser.IsEmailVerified && existingUser.IsActive)
            {
                if (AutoVerifyEmailOnRegister)
                {
                    existingUser.MarkEmailVerified();
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    await SendVerificationEmailBestEffortAsync(existingUser, cancellationToken);
                }
            }

            var existingOrganization = await _organizations.GetAsync(existingUser.OrganizationId, cancellationToken);
            if (existingOrganization is null)
                return Result<AuthUserResponse>.Failure(Error.Validation("Nie można dokończyć rejestracji."));
            return Result<AuthUserResponse>.Success(Map(existingUser, existingOrganization));
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
            if (AutoVerifyEmailOnRegister) user.MarkEmailVerified();
            _users.Add(user);

            foreach (var category in StarterAssetCategories.Create(organization.Id))
            {
                _categories.Add(category);
            }

            foreach (var relationType in StarterPersonRelationTypes.Create(organization.Id, language))
            {
                _relationTypes.Add(relationType);
            }

            foreach (var rule in StarterAlertRules.Create(organization.Id, _clock.UtcNow, user.Email))
            {
                _rules.Add(rule);
            }

            _activity.Add(new ActivityLog(organization.Id, "organization.registered", "organization", organization.Id, user.Email, organization.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (!AutoVerifyEmailOnRegister)
            {
                await SendVerificationEmailBestEffortAsync(user, cancellationToken);
            }

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
        var passwordHash = user?.PasswordHash ?? PasswordHasher.DummyHash;
        var passwordValid = PasswordHasher.Verify(request.Password, passwordHash);
        if (user is null || !user.IsActive || !user.IsEmailVerified || !passwordValid)
        {
            return Result<LoginOutcome>.Failure(Error.Validation("Nieprawidłowy e-mail lub hasło."));
        }

        if (PasswordHasher.NeedsRehash(user.PasswordHash))
        {
            user.SetPasswordHash(PasswordHasher.Hash(request.Password));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
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
        var user = await _users.GetByIdAsync(organizationUserId, cancellationToken);
        if (user is null || !user.IsActive || !user.IsEmailVerified || !user.IsTwoFactorEnabled)
            throw new InvalidOperationException("Zaufane urządzenie wymaga aktywnego, zweryfikowanego konta z 2FA.");

        var rawToken = TokenHasher.NewRawToken();
        var token = new DeviceTrustToken(organizationUserId, TokenHasher.Hash(rawToken), _clock.UtcNow.AddDays(30));
        _deviceTrustTokens.Add(token);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    public async Task<Result<AuthUserResponse>> CompleteTwoFactorLoginAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive || !user.IsEmailVerified || !user.IsTwoFactorEnabled || string.IsNullOrEmpty(user.TotpSecret))
        {
            return Result<AuthUserResponse>.Failure(Error.Validation("Nieprawidłowy kod uwierzytelniający."));
        }

        var isValidTotp = await TryConsumeTotpCodeAsync(user, code, cancellationToken);
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

        if (!await TryConsumeTotpCodeAsync(user, code, cancellationToken))
        {
            return Result<TwoFactorEnableResponse>.Failure(Error.Validation("Nieprawidłowy kod. Sprawdź godzinę na urządzeniu i spróbuj ponownie."));
        }

        var organization = await _organizations.GetAsync(user.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return Result<TwoFactorEnableResponse>.Failure(Error.Validation("Nie znaleziono organizacji powiązanej z kontem."));
        }

        user.EnableTwoFactor();
        user.RotateSecurityStamp();
        _securityStateCache?.Remove(user.Id);
        var rawCodes = await ReplaceRecoveryCodesAsync(userId, cancellationToken);

        // A 2FA policy change is a security-boundary change. Revoke every existing refresh/trust
        // credential and let the endpoint create exactly one replacement session for the browser that
        // successfully confirmed the TOTP code.
        await _refreshTokens.RevokeAllForUserAsync(userId, cancellationToken);
        await _deviceTrustTokens.RevokeAllForUserAsync(userId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<TwoFactorEnableResponse>.Success(new TwoFactorEnableResponse(rawCodes, Map(user, organization)));
    }

    public async Task<Result<AuthUserResponse>> DisableTwoFactorAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsTwoFactorEnabled || string.IsNullOrEmpty(user.TotpSecret))
        {
            return Result<AuthUserResponse>.Failure(Error.Validation("Dwuskładnikowe uwierzytelnianie nie jest włączone."));
        }

        if (!await TryConsumeTotpCodeAsync(user, code, cancellationToken))
        {
            return Result<AuthUserResponse>.Failure(Error.Validation("Nieprawidłowy kod uwierzytelniający."));
        }

        var organization = await _organizations.GetAsync(user.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return Result<AuthUserResponse>.Failure(Error.Validation("Nie znaleziono organizacji powiązanej z kontem."));
        }

        user.DisableTwoFactor();
        user.RotateSecurityStamp();
        _securityStateCache?.Remove(user.Id);
        var existingCodes = await _recoveryCodes.ListAsync(userId, cancellationToken);
        _recoveryCodes.RemoveAll(existingCodes);

        // Trust and refresh credentials issued under the previous 2FA state must never survive the
        // transition. The caller receives a newly issued current-browser session after this commit.
        await _refreshTokens.RevokeAllForUserAsync(userId, cancellationToken);
        await _deviceTrustTokens.RevokeAllForUserAsync(userId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AuthUserResponse>.Success(Map(user, organization));
    }

    public async Task<Result<TwoFactorRecoveryCodesResponse>> RegenerateRecoveryCodesAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsTwoFactorEnabled || string.IsNullOrEmpty(user.TotpSecret))
        {
            return Result<TwoFactorRecoveryCodesResponse>.Failure(Error.Validation("Dwuskładnikowe uwierzytelnianie nie jest włączone."));
        }

        if (!await TryConsumeTotpCodeAsync(user, code, cancellationToken))
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

    private async Task<bool> TryConsumeTotpCodeAsync(OrganizationUser user, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(user.TotpSecret) || !TotpService.TryValidateCode(user.TotpSecret, code, out var counter)) return false;
        if (!await _users.TryConsumeTotpCounterAsync(user.Id, counter, cancellationToken)) return false;
        // Keep the tracked entity in sync with the atomic database update.
        if (!user.LastUsedTotpCounter.HasValue || user.LastUsedTotpCounter.Value < counter) user.RecordTotpCounter(counter);
        return true;
    }

    private async Task<bool> TryConsumeRecoveryCodeAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();
        if (normalized.Length == 0) return false;

        var hash = TokenHasher.Hash(normalized);
        return await _recoveryCodes.TryConsumeAsync(userId, hash, _clock.UtcNow, cancellationToken);
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

    // Generic message for every OAuth rejection that stems from account state (inactive account,
    // unverified provider email) - the callback must not tell an attacker which reason applied
    // (audyt AUD3-002: "Zwracaj generyczne oauth_rejected, bez ujawniania statusu konta").
    private const string OAuthRejectedMessage = "Logowanie nie powiodło się.";

    public async Task<Result<LoginOutcome>> ExternalLoginAsync(ExternalUserInfo info, string? deviceTrustToken, CancellationToken cancellationToken)
    {
        var linkedUser = await _externalLogins.FindLinkedUserAsync(info.Provider, info.ProviderUserId, cancellationToken);
        if (linkedUser is not null)
        {
            if (!linkedUser.IsActive || !linkedUser.IsEmailVerified)
            {
                return Result<LoginOutcome>.Failure(Error.Validation(OAuthRejectedMessage));
            }

            return await BuildLoginOutcomeAsync(linkedUser, deviceTrustToken, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(info.Email))
        {
            return Result<LoginOutcome>.Failure(Error.Validation("Dostawca logowania nie udostępnił adresu e-mail. Wyraź zgodę na udostępnienie e-maila i spróbuj ponownie."));
        }

        var existingUser = await _users.FindByEmailAsync(info.Email, cancellationToken);
        if (existingUser is not null)
        {
            if (!existingUser.IsActive)
            {
                return Result<LoginOutcome>.Failure(Error.Validation(OAuthRejectedMessage));
            }

            if (!info.EmailVerified || !existingUser.IsEmailVerified)
            {
                // Never merge a verified provider identity into an unverified local account based on
                // e-mail equality alone. That account may have been preregistered by someone who does
                // not control the mailbox. The mailbox owner must first complete Tenebit's verification/
                // recovery flow, or link the provider explicitly from an authenticated session.
                return Result<LoginOutcome>.Failure(Error.Validation(OAuthRejectedMessage));
            }

            _externalLogins.Add(new ExternalLogin(existingUser.Id, info.Provider, info.ProviderUserId));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return await BuildLoginOutcomeAsync(existingUser, deviceTrustToken, cancellationToken);
        }

        if (!info.EmailVerified)
        {
            return Result<LoginOutcome>.Failure(Error.Validation(OAuthRejectedMessage));
        }

        try
        {
            var displayName = string.IsNullOrWhiteSpace(info.DisplayName) ? info.Email.Split('@')[0] : info.DisplayName;
            var organization = new Organization($"{displayName} - organizacja", "PL", "pl", "PLN", "Europe/Warsaw");
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

            foreach (var rule in StarterAlertRules.Create(organization.Id, _clock.UtcNow, user.Email))
            {
                _rules.Add(rule);
            }

            _activity.Add(new ActivityLog(organization.Id, "organization.registered_via_oauth", "organization", organization.Id, user.Email, organization.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<LoginOutcome>.Success(new LoginOutcome(false, null, Map(user, organization)));
        }
        catch (DomainException ex)
        {
            return Result<LoginOutcome>.Failure(Error.Validation(ex.Message));
        }
    }

    // Shared by every OAuth account resolution branch so a linked/matched-email account never skips
    // the same second-factor gate password login enforces (audyt AUD3-003).
    private async Task<Result<LoginOutcome>> BuildLoginOutcomeAsync(OrganizationUser user, string? deviceTrustToken, CancellationToken cancellationToken)
    {
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
            return Result<LoginOutcome>.Failure(Error.Validation("Nie znaleziono organizacji powiązanej z kontem."));
        }

        return Result<LoginOutcome>.Success(new LoginOutcome(false, null, Map(user, organization)));
    }

    public async Task RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.FindByEmailAsync(request.Email, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return;
        }

        var code = TokenHasher.NewOneTimeCode();
        var tokenHash = TokenHasher.HashOneTimeCode(user.Email, code);
        var now = _clock.UtcNow;
        var organization = await _organizations.GetAsync(user.OrganizationId, cancellationToken);
        var link = _appLinkBuilder.BuildPasswordResetLink(user.Email, code);
        var (subject, html) = EmailTemplates.PasswordReset(organization?.Language, code, link);

        if (_emailOutbox is not null)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _passwordResetTokens.RevokeUnusedForUserAsync(user.Id, now, ct);
                _passwordResetTokens.Add(new PasswordResetToken(user.Id, tokenHash, now.AddMinutes(15)));
                await _emailOutbox.EnqueueAsync(
                    user.OrganizationId,
                    user.Email,
                    subject,
                    html,
                    "password-reset",
                    $"password-reset:{user.Id:N}:{tokenHash}",
                    ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);
            return;
        }

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _passwordResetTokens.RevokeUnusedForUserAsync(user.Id, now, ct);
            _passwordResetTokens.Add(new PasswordResetToken(user.Id, tokenHash, now.AddMinutes(15)));
            await _unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        try
        {
            await _emailSender.SendAsync(user.Email, subject, html, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się wysłać e-maila resetującego hasło dla użytkownika {UserId}", user.Id);
        }
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return Result.Failure(Error.Validation("Hasło musi mieć co najmniej 8 znaków."));
        }

        var code = TokenHasher.NormalizeOneTimeCode(request.Code);
        if (code.Length != TokenHasher.OneTimeCodeLength)
        {
            return Result.Failure(Error.Validation("Kod resetujący jest nieprawidłowy lub wygasł."));
        }

        var tokenHash = TokenHasher.HashOneTimeCode(request.Email, code);
        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = _clock.UtcNow;
            var userId = await _passwordResetTokens.TryConsumeAsync(tokenHash, now, ct);
            if (!userId.HasValue)
            {
                return Result.Failure(Error.Validation("Kod resetujący jest nieprawidłowy lub wygasł."));
            }

            var user = await _users.GetByIdAsync(userId.Value, ct);
            if (user is null || !string.Equals(user.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure(Error.Validation("Kod resetujący jest nieprawidłowy lub wygasł."));
            }

            user.SetPasswordHash(PasswordHasher.Hash(request.NewPassword));
            user.MarkEmailVerified();
            user.RotateSecurityStamp();
            _securityStateCache?.Remove(user.Id);
            await _refreshTokens.RevokeAllForUserAsync(user.Id, ct);
            await _deviceTrustTokens.RevokeAllForUserAsync(user.Id, ct);
            await _passwordResetTokens.RevokeUnusedForUserAsync(user.Id, now, ct);
            await _emailVerificationTokens.RevokeUnusedForUserAsync(user.Id, now, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }, cancellationToken);
    }

    public async Task<Result> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return Result.Failure(Error.Validation("Hasło musi mieć co najmniej 8 znaków."));
        }

        var code = TokenHasher.NormalizeOneTimeCode(request.Code);
        if (code.Length != TokenHasher.OneTimeCodeLength)
        {
            return Result.Failure(Error.Validation("Kod weryfikacyjny jest nieprawidłowy lub wygasł."));
        }

        var tokenHash = TokenHasher.HashOneTimeCode(request.Email, code);
        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = _clock.UtcNow;
            var userId = await _emailVerificationTokens.TryConsumeAsync(tokenHash, now, ct);
            if (!userId.HasValue)
            {
                return Result.Failure(Error.Validation("Kod weryfikacyjny jest nieprawidłowy lub wygasł."));
            }

            var user = await _users.GetByIdAsync(userId.Value, ct);
            if (user is null || !string.Equals(user.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure(Error.Validation("Kod weryfikacyjny jest nieprawidłowy lub wygasł."));
            }

            user.SetPasswordHash(PasswordHasher.Hash(request.NewPassword));
            user.MarkEmailVerified();
            user.RotateSecurityStamp();
            _securityStateCache?.Remove(user.Id);
            await _refreshTokens.RevokeAllForUserAsync(user.Id, ct);
            await _deviceTrustTokens.RevokeAllForUserAsync(user.Id, ct);
            await _passwordResetTokens.RevokeUnusedForUserAsync(user.Id, now, ct);
            await _emailVerificationTokens.RevokeUnusedForUserAsync(user.Id, now, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }, cancellationToken);
    }

    public async Task ResendVerificationEmailAsync(string email, CancellationToken cancellationToken)
    {
        var user = await _users.FindByEmailAsync(email, cancellationToken);
        if (user is null || user.IsEmailVerified || !user.IsActive) return;
        await SendVerificationEmailBestEffortAsync(user, cancellationToken);
    }

    private async Task SendVerificationEmailBestEffortAsync(OrganizationUser user, CancellationToken cancellationToken)
    {
        var code = TokenHasher.NewOneTimeCode();
        var tokenHash = TokenHasher.HashOneTimeCode(user.Email, code);
        var now = _clock.UtcNow;
        try
        {
            var organization = await _organizations.GetAsync(user.OrganizationId, cancellationToken);
            var link = _appLinkBuilder.BuildEmailVerificationLink(user.Email, code);
            var (subject, html) = EmailTemplates.EmailVerification(organization?.Language, code, link);

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _emailVerificationTokens.RevokeUnusedForUserAsync(user.Id, now, ct);
                _emailVerificationTokens.Add(new EmailVerificationToken(user.Id, tokenHash, now.AddMinutes(30)));
                if (_emailOutbox is not null)
                {
                    await _emailOutbox.EnqueueAsync(
                        user.OrganizationId,
                        user.Email,
                        subject,
                        html,
                        "email-verification",
                        $"email-verification:{user.Id:N}:{tokenHash}",
                        ct);
                }
                await _unitOfWork.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);

            if (_emailOutbox is null)
            {
                await _emailSender.SendAsync(user.Email, subject, html, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się przygotować lub zakolejkować e-maila weryfikacyjnego dla użytkownika {UserId}", user.Id);
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
        var user = await _users.GetByIdAsync(organizationUserId, cancellationToken);
        if (user is null || !user.IsActive || !user.IsEmailVerified)
            throw new InvalidOperationException("Pełna sesja wymaga aktywnego, zweryfikowanego konta.");

        var rawToken = TokenHasher.NewRawToken();
        var token = new RefreshToken(organizationUserId, TokenHasher.Hash(rawToken), _clock.UtcNow.AddDays(30));
        _refreshTokens.Add(token);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    public async Task<Result<RefreshResult>> RefreshAsync(string rawToken, CancellationToken cancellationToken)
    {
        var tokenHash = TokenHasher.Hash(rawToken);
        var observed = await _refreshTokens.FindAsync(tokenHash, cancellationToken);
        if (observed is null)
        {
            return Result<RefreshResult>.Failure(Error.Unauthorized("Sesja wygasła. Zaloguj się ponownie."));
        }

        var observedUser = await _users.GetByIdAsync(observed.OrganizationUserId, cancellationToken);
        if (observedUser is null)
        {
            return Result<RefreshResult>.Failure(Error.Unauthorized("Sesja wygasła. Zaloguj się ponownie."));
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = _clock.UtcNow;
            var token = await _refreshTokens.FindAsync(tokenHash, ct);
            var user = await _users.GetByIdAsync(observed.OrganizationUserId, ct);
            if (token is null || user is null || !user.IsActive || !user.IsEmailVerified || token.ExpiresAt <= now)
            {
                return Result<RefreshResult>.Failure(Error.Unauthorized("Sesja wygasła. Zaloguj się ponownie."));
            }

            if (token.RevokedAt is not null)
            {
                // A rotated token being presented again is a replay signal, not just an expired session.
                // Revoke every descendant/sibling in the family and invalidate outstanding access JWTs.
                if (token.ReplacedByTokenId is not null || token.RevocationReason == "rotated")
                {
                    SecurityTelemetry.RefreshReuse();
                    await _refreshTokens.RevokeFamilyAsync(token.FamilyId, now, "refresh_reuse_detected", ct);
                    await _deviceTrustTokens.RevokeAllForUserAsync(user.Id, ct);
                    user.RotateSecurityStamp();
                    _securityStateCache?.Remove(user.Id);
                    await _unitOfWork.SaveChangesAsync(ct);
                }
                return Result<RefreshResult>.Failure(Error.Unauthorized("Sesja wygasła. Zaloguj się ponownie."));
            }

            var organization = await _organizations.GetAsync(user.OrganizationId, ct);
            if (organization is null)
            {
                return Result<RefreshResult>.Failure(Error.Unauthorized("Sesja wygasła. Zaloguj się ponownie."));
            }

            var newRawToken = TokenHasher.NewRawToken();
            var successor = new RefreshToken(user.Id, TokenHasher.Hash(newRawToken), now.AddDays(30), token.FamilyId, token.Id);
            _refreshTokens.Add(successor);
            // Insert successor inside the same transaction first so the self-FK from ReplacedByTokenId is valid.
            await _unitOfWork.SaveChangesAsync(ct);
            if (!await _refreshTokens.TryMarkRotatedAsync(token.Id, successor.Id, now, ct))
            {
                throw new Tenebit.Domain.Common.ConcurrencyException("Refresh token został zużyty równolegle.");
            }

            return Result<RefreshResult>.Success(new RefreshResult(Map(user, organization), newRawToken));
        }, cancellationToken);
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
        new(user.Id, organization.Id, organization.Name, user.Email, user.DisplayName, user.Roles.Select(x => x.Role).ToArray(), user.IsEmailVerified, user.IsTwoFactorEnabled, user.SecurityStamp, user.PersonId);
}
