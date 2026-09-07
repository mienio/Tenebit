using Microsoft.Extensions.Logging;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Application.Identity;
using Tenebit.Domain.Affiliates;
using Tenebit.Domain.Common;

namespace Tenebit.Application.Affiliates;

public sealed record AffiliateProfileResponse(
    Guid Id, string Email, string FirstName, string LastName, string Status, string? CountryCode,
    string? PhoneNumber, string? CompanyName, string? TaxId, string? RevolutTag, bool IsEmailVerified,
    DateTimeOffset? AcceptedTermsAt, DateTimeOffset CreatedAt);

public sealed record AffiliateLoginOutcome(AffiliateProfileResponse Affiliate, Guid SecurityStamp);
public sealed record AffiliateRefreshResult(AffiliateProfileResponse Affiliate, string RawRefreshToken, Guid SecurityStamp);

/// <summary>
/// Registration/login/session lifecycle for the affiliate identity - deliberately simpler than
/// <see cref="Tenebit.Application.Identity.AuthService"/> (no 2FA, no device trust, no OAuth: spec §4.3
/// explains why those don't pay for themselves yet at this account's risk/scale). Everything else -
/// one-time-code email verification/reset, refresh rotation with reuse detection, non-disclosure of
/// account existence - mirrors AuthService's tenant flow 1:1 against fully separate tables.
/// </summary>
public sealed class AffiliateAuthService
{
    private const int AccessTokenMinutes = 15;
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan OneTimeCodeLifetime = TimeSpan.FromMinutes(15);

    private readonly IAffiliateRepository _affiliates;
    private readonly IAffiliateRefreshTokenRepository _refreshTokens;
    private readonly IAffiliatePasswordResetTokenRepository _passwordResetTokens;
    private readonly IAffiliateEmailVerificationTokenRepository _emailVerificationTokens;
    private readonly IAffiliateProgramSettingsRepository _settings;
    private readonly IAffiliateSecurityStateCache? _securityStateCache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IEmailSender _emailSender;
    private readonly IAppLinkBuilder _appLinkBuilder;
    private readonly ILogger<AffiliateAuthService> _logger;

    public AffiliateAuthService(
        IAffiliateRepository affiliates, IAffiliateRefreshTokenRepository refreshTokens,
        IAffiliatePasswordResetTokenRepository passwordResetTokens, IAffiliateEmailVerificationTokenRepository emailVerificationTokens,
        IAffiliateProgramSettingsRepository settings, IUnitOfWork unitOfWork, IClock clock,
        IEmailSender emailSender, IAppLinkBuilder appLinkBuilder, ILogger<AffiliateAuthService> logger,
        IAffiliateSecurityStateCache? securityStateCache = null)
    {
        _affiliates = affiliates;
        _refreshTokens = refreshTokens;
        _passwordResetTokens = passwordResetTokens;
        _emailVerificationTokens = emailVerificationTokens;
        _settings = settings;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _emailSender = emailSender;
        _appLinkBuilder = appLinkBuilder;
        _logger = logger;
        _securityStateCache = securityStateCache;
    }

    public async Task<Result> RegisterAsync(
        string email, string password, string firstName, string lastName, string? countryCode,
        string? revolutTag, bool acceptTerms, CancellationToken cancellationToken)
    {
        if (!acceptTerms)
            return Result.Failure(Error.Validation("Akceptacja regulaminu programu partnerskiego jest wymagana."));
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            return Result.Failure(Error.Validation("Hasło musi mieć co najmniej 8 znaków."));
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
            return Result.Failure(Error.Validation("Poprawny e-mail jest wymagany."));

        var existing = await _affiliates.FindByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            // Same non-disclosure principle as the tenant flow: never reveal whether an account
            // already exists. If it does and just needs verifying, quietly resend the code.
            if (!existing.IsEmailVerified)
            {
                await SendVerificationEmailBestEffortAsync(existing, cancellationToken);
            }
            return Result.Success();
        }

        try
        {
            var now = _clock.UtcNow;
            var settings = await _settings.GetAsync(cancellationToken);
            var affiliate = new Affiliate(email, PasswordHasher.Hash(password), firstName, lastName, countryCode, now);
            affiliate.AcceptTerms(settings.TermsVersion, now);
            if (!string.IsNullOrWhiteSpace(revolutTag))
            {
                affiliate.SetRevolutTag(revolutTag, now);
            }

            _affiliates.Add(affiliate);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await SendVerificationEmailBestEffortAsync(affiliate, cancellationToken);
            return Result.Success();
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<AffiliateProfileResponse>> GetProfileAsync(Guid affiliateId, CancellationToken cancellationToken)
    {
        var affiliate = await _affiliates.GetByIdAsync(affiliateId, cancellationToken);
        return affiliate is null
            ? Result<AffiliateProfileResponse>.Failure(Error.NotFound("Konto partnerskie nie istnieje."))
            : Result<AffiliateProfileResponse>.Success(Map(affiliate));
    }

    public async Task<Result<AffiliateProfileResponse>> UpdateProfileAsync(
        Guid affiliateId, string firstName, string lastName, string? phoneNumber, string? countryCode,
        string? companyName, string? taxId, string? revolutTag, CancellationToken cancellationToken)
    {
        var affiliate = await _affiliates.GetByIdAsync(affiliateId, cancellationToken);
        if (affiliate is null) return Result<AffiliateProfileResponse>.Failure(Error.NotFound("Konto partnerskie nie istnieje."));

        try
        {
            var now = _clock.UtcNow;
            affiliate.UpdateContactDetails(firstName, lastName, phoneNumber, countryCode, now);
            affiliate.UpdateCompanyDetails(companyName, taxId, now);
            affiliate.SetRevolutTag(revolutTag, now);
        }
        catch (DomainException ex)
        {
            return Result<AffiliateProfileResponse>.Failure(Error.Validation(ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AffiliateProfileResponse>.Success(Map(affiliate));
    }

    public async Task<Result<AffiliateLoginOutcome>> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var affiliate = await _affiliates.FindByEmailAsync(email, cancellationToken);
        // Constant-shape failure whether the account is missing or the password is wrong - and always
        // hash something so the response time does not itself disclose account existence.
        var passwordOk = affiliate is not null && PasswordHasher.Verify(password, affiliate.PasswordHash);
        if (affiliate is null || !passwordOk)
        {
            return Result<AffiliateLoginOutcome>.Failure(Error.Unauthorized("Nieprawidłowy e-mail lub hasło."));
        }

        if (affiliate.Status == AffiliateStatus.Blocked)
        {
            return Result<AffiliateLoginOutcome>.Failure(Error.Forbidden("To konto partnerskie zostało zablokowane."));
        }

        if (!affiliate.IsEmailVerified)
        {
            return Result<AffiliateLoginOutcome>.Failure(Error.Validation("Potwierdź adres e-mail przed zalogowaniem."));
        }

        return Result<AffiliateLoginOutcome>.Success(new AffiliateLoginOutcome(Map(affiliate), affiliate.SecurityStamp));
    }

    public async Task<string> IssueRefreshTokenAsync(Guid affiliateId, CancellationToken cancellationToken)
    {
        var rawToken = TokenHasher.NewRawToken();
        var token = new AffiliateRefreshToken(affiliateId, TokenHasher.Hash(rawToken), _clock.UtcNow.Add(RefreshTokenLifetime));
        _refreshTokens.Add(token);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    public int AccessTokenMinutesValue => AccessTokenMinutes;

    public async Task<Result<AffiliateRefreshResult>> RefreshAsync(string rawToken, CancellationToken cancellationToken)
    {
        var tokenHash = TokenHasher.Hash(rawToken);
        var observed = await _refreshTokens.FindAsync(tokenHash, cancellationToken);
        if (observed is null)
        {
            return Result<AffiliateRefreshResult>.Failure(Error.Unauthorized("Sesja wygasła. Zaloguj się ponownie."));
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = _clock.UtcNow;
            var token = await _refreshTokens.FindAsync(tokenHash, ct);
            var affiliate = token is null ? null : await _affiliates.GetByIdAsync(token.AffiliateId, ct);
            if (token is null || affiliate is null || affiliate.Status == AffiliateStatus.Blocked || token.ExpiresAt <= now)
            {
                return Result<AffiliateRefreshResult>.Failure(Error.Unauthorized("Sesja wygasła. Zaloguj się ponownie."));
            }

            if (token.RevokedAt is not null)
            {
                // A rotated token presented again is a replay signal - revoke the whole family and
                // force a fresh login, same as the tenant refresh flow.
                if (token.ReplacedByTokenId is not null || token.RevocationReason == "rotated")
                {
                    await _refreshTokens.RevokeFamilyAsync(token.FamilyId, now, "refresh_reuse_detected", ct);
                    affiliate.RotateSecurityStamp();
                    _securityStateCache?.Remove(affiliate.Id);
                    await _unitOfWork.SaveChangesAsync(ct);
                }
                return Result<AffiliateRefreshResult>.Failure(Error.Unauthorized("Sesja wygasła. Zaloguj się ponownie."));
            }

            var newRawToken = TokenHasher.NewRawToken();
            var successor = new AffiliateRefreshToken(affiliate.Id, TokenHasher.Hash(newRawToken), now.Add(RefreshTokenLifetime), token.FamilyId, token.Id);
            _refreshTokens.Add(successor);
            await _unitOfWork.SaveChangesAsync(ct);
            if (!await _refreshTokens.TryMarkRotatedAsync(token.Id, successor.Id, now, ct))
            {
                throw new ConcurrencyException("Refresh token został zużyty równolegle.");
            }

            return Result<AffiliateRefreshResult>.Success(new AffiliateRefreshResult(Map(affiliate), newRawToken, affiliate.SecurityStamp));
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

    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        var affiliate = await _affiliates.FindByEmailAsync(email, cancellationToken);
        if (affiliate is null || affiliate.Status == AffiliateStatus.Blocked) return;

        var code = TokenHasher.NewOneTimeCode();
        var tokenHash = TokenHasher.HashOneTimeCode(affiliate.Email, code);
        var now = _clock.UtcNow;
        var link = _appLinkBuilder.BuildAppUrl($"/partner/reset-password?email={Uri.EscapeDataString(affiliate.Email)}&code={code}");
        var (subject, html) = EmailTemplates.PasswordReset("pl", code, link);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _passwordResetTokens.RevokeUnusedForAffiliateAsync(affiliate.Id, now, ct);
            _passwordResetTokens.Add(new AffiliatePasswordResetToken(affiliate.Id, tokenHash, now.Add(OneTimeCodeLifetime)));
            await _unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        try
        {
            await _emailSender.SendAsync(affiliate.Email, subject, html, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się wysłać e-maila resetującego hasło dla afilianta {AffiliateId}", affiliate.Id);
        }
    }

    public async Task<Result> ResetPasswordAsync(string email, string code, string newPassword, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            return Result.Failure(Error.Validation("Hasło musi mieć co najmniej 8 znaków."));

        var normalizedCode = TokenHasher.NormalizeOneTimeCode(code);
        if (normalizedCode.Length != TokenHasher.OneTimeCodeLength)
            return Result.Failure(Error.Validation("Kod resetujący jest nieprawidłowy lub wygasł."));

        var tokenHash = TokenHasher.HashOneTimeCode(email, normalizedCode);
        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = _clock.UtcNow;
            var affiliateId = await _passwordResetTokens.TryConsumeAsync(tokenHash, now, ct);
            if (!affiliateId.HasValue)
                return Result.Failure(Error.Validation("Kod resetujący jest nieprawidłowy lub wygasł."));

            var affiliate = await _affiliates.GetByIdAsync(affiliateId.Value, ct);
            if (affiliate is null || !string.Equals(affiliate.Email, email.Trim(), StringComparison.OrdinalIgnoreCase))
                return Result.Failure(Error.Validation("Kod resetujący jest nieprawidłowy lub wygasł."));

            affiliate.SetPasswordHash(PasswordHasher.Hash(newPassword));
            affiliate.MarkEmailVerified();
            _securityStateCache?.Remove(affiliate.Id);
            await _refreshTokens.RevokeAllForAffiliateAsync(affiliate.Id, ct);
            await _passwordResetTokens.RevokeUnusedForAffiliateAsync(affiliate.Id, now, ct);
            await _emailVerificationTokens.RevokeUnusedForAffiliateAsync(affiliate.Id, now, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }, cancellationToken);
    }

    public async Task<Result> VerifyEmailAsync(string email, string code, CancellationToken cancellationToken)
    {
        var normalizedCode = TokenHasher.NormalizeOneTimeCode(code);
        if (normalizedCode.Length != TokenHasher.OneTimeCodeLength)
            return Result.Failure(Error.Validation("Kod weryfikacyjny jest nieprawidłowy lub wygasł."));

        var tokenHash = TokenHasher.HashOneTimeCode(email, normalizedCode);
        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = _clock.UtcNow;
            var affiliateId = await _emailVerificationTokens.TryConsumeAsync(tokenHash, now, ct);
            if (!affiliateId.HasValue)
                return Result.Failure(Error.Validation("Kod weryfikacyjny jest nieprawidłowy lub wygasł."));

            var affiliate = await _affiliates.GetByIdAsync(affiliateId.Value, ct);
            if (affiliate is null || !string.Equals(affiliate.Email, email.Trim(), StringComparison.OrdinalIgnoreCase))
                return Result.Failure(Error.Validation("Kod weryfikacyjny jest nieprawidłowy lub wygasł."));

            affiliate.MarkEmailVerified();
            await _emailVerificationTokens.RevokeUnusedForAffiliateAsync(affiliate.Id, now, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }, cancellationToken);
    }

    private async Task SendVerificationEmailBestEffortAsync(Affiliate affiliate, CancellationToken cancellationToken)
    {
        var code = TokenHasher.NewOneTimeCode();
        var tokenHash = TokenHasher.HashOneTimeCode(affiliate.Email, code);
        var now = _clock.UtcNow;
        var link = _appLinkBuilder.BuildAppUrl($"/partner/verify-email?email={Uri.EscapeDataString(affiliate.Email)}&code={code}");
        var (subject, html) = EmailTemplates.EmailVerification("pl", code, link);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _emailVerificationTokens.RevokeUnusedForAffiliateAsync(affiliate.Id, now, ct);
            _emailVerificationTokens.Add(new AffiliateEmailVerificationToken(affiliate.Id, tokenHash, now.Add(OneTimeCodeLifetime)));
            await _unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        try
        {
            await _emailSender.SendAsync(affiliate.Email, subject, html, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się wysłać e-maila weryfikacyjnego dla afilianta {AffiliateId}", affiliate.Id);
        }
    }

    public static AffiliateProfileResponse Map(Affiliate affiliate) => new(
        affiliate.Id, affiliate.Email, affiliate.FirstName, affiliate.LastName, affiliate.Status.ToString(),
        affiliate.CountryCode, affiliate.PhoneNumber, affiliate.CompanyName, affiliate.TaxId, affiliate.RevolutTag,
        affiliate.IsEmailVerified, affiliate.AcceptedTermsAt, affiliate.CreatedAt);
}
