using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Affiliates;
using Tenebit.Domain.Common;

namespace Tenebit.Application.Affiliates;

public sealed record AffiliateCodeResponse(Guid Id, string Code, string? CountryCode, bool IsActive, int ClickCount, DateTimeOffset CreatedAt);

/// <summary>
/// Code creation/validation for the affiliate's own dashboard - enforces the active-code limit and
/// format/blacklist rules server-side (spec §5.1/§5.2: never trust the client, and never trust the
/// availability pre-check alone since a second request can land between GET and POST). Uniqueness
/// itself is a database unique index; a genuine race lands on the global 23505-to-409 handler in
/// Program.cs, same as PromoCodeAdminService.
/// </summary>
public sealed class AffiliateCodeService
{
    private readonly IAffiliateCodeRepository _codes;
    private readonly IAffiliateRepository _affiliates;
    private readonly IPromoCodeRepository _promoCodes;
    private readonly IAffiliateProgramSettingsRepository _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AffiliateCodeService(
        IAffiliateCodeRepository codes, IAffiliateRepository affiliates, IPromoCodeRepository promoCodes,
        IAffiliateProgramSettingsRepository settings, IUnitOfWork unitOfWork, IClock clock)
    {
        _codes = codes;
        _affiliates = affiliates;
        _promoCodes = promoCodes;
        _settings = settings;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<IReadOnlyList<AffiliateCodeResponse>> ListAsync(Guid affiliateId, CancellationToken cancellationToken) =>
        (await _codes.ListByAffiliateAsync(affiliateId, cancellationToken)).Select(ToResponse).ToList();

    /// <summary>Pre-check only, for UX - see the class remarks on why the POST re-validates
    /// everything regardless.</summary>
    public async Task<bool> IsAvailableAsync(string code, CancellationToken cancellationToken)
    {
        string normalized;
        try
        {
            normalized = AffiliateCode.Normalize(code);
        }
        catch (DomainException)
        {
            return false;
        }

        return await _codes.GetByCodeAsync(normalized, cancellationToken) is null
            && await _promoCodes.GetByCodeAsync(normalized, cancellationToken) is null;
    }

    public async Task<Result<AffiliateCodeResponse>> CreateAsync(Guid affiliateId, string? requestedCode, string? countryCode, CancellationToken cancellationToken)
    {
        var affiliate = await _affiliates.GetByIdAsync(affiliateId, cancellationToken);
        if (affiliate is null) return Result<AffiliateCodeResponse>.Failure(Error.NotFound("Konto partnerskie nie istnieje."));

        var settings = await _settings.GetAsync(cancellationToken);
        var activeCount = await _codes.CountActiveByAffiliateAsync(affiliateId, cancellationToken);
        var limit = affiliate.ResolveMaxActiveCodes(settings);
        if (activeCount >= limit)
        {
            return Result<AffiliateCodeResponse>.Failure(Error.Validation($"Osiągnięto limit {limit} aktywnych kodów. Dezaktywuj jeden, aby zwolnić miejsce."));
        }

        string code;
        try
        {
            code = string.IsNullOrWhiteSpace(requestedCode)
                ? await GenerateUniqueCodeAsync(affiliate.LastName, cancellationToken)
                : AffiliateCode.Normalize(requestedCode);
        }
        catch (DomainException ex)
        {
            return Result<AffiliateCodeResponse>.Failure(Error.Validation(ex.Message));
        }

        if (!string.IsNullOrWhiteSpace(requestedCode))
        {
            if (await _codes.GetByCodeAsync(code, cancellationToken) is not null || await _promoCodes.GetByCodeAsync(code, cancellationToken) is not null)
            {
                return Result<AffiliateCodeResponse>.Failure(Error.Conflict("Ten kod jest już zajęty."));
            }
        }

        try
        {
            var affiliateCode = new AffiliateCode(affiliateId, code, countryCode, _clock.UtcNow);
            _codes.Add(affiliateCode);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<AffiliateCodeResponse>.Success(ToResponse(affiliateCode));
        }
        catch (DomainException ex)
        {
            return Result<AffiliateCodeResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result> SetActiveAsync(Guid affiliateId, Guid codeId, bool active, CancellationToken cancellationToken)
    {
        var code = await _codes.GetByIdAsync(codeId, cancellationToken);
        if (code is null || code.AffiliateId != affiliateId)
        {
            return Result.Failure(Error.NotFound("Kod nie istnieje."));
        }

        if (active)
        {
            var affiliate = await _affiliates.GetByIdAsync(affiliateId, cancellationToken);
            var settings = await _settings.GetAsync(cancellationToken);
            var limit = affiliate!.ResolveMaxActiveCodes(settings);
            var activeCount = await _codes.CountActiveByAffiliateAsync(affiliateId, cancellationToken);
            if (!code.IsActive && activeCount >= limit)
            {
                return Result.Failure(Error.Validation($"Osiągnięto limit {limit} aktywnych kodów."));
            }
        }

        code.SetActive(active);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<string> GenerateUniqueCodeAsync(string lastName, CancellationToken cancellationToken)
    {
        var basePrefix = Regex.Replace(lastName.Trim().ToUpperInvariant(), "[^A-Z0-9]", "");
        if (basePrefix.Length == 0) basePrefix = "PARTNER";
        if (basePrefix.Length > 12) basePrefix = basePrefix[..12];

        const string alphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789"; // no 0/O/1/I - avoids misread codes
        // 5, not 4: guarantees {prefix}-{suffix} clears AffiliateCode's 7-character minimum even for a
        // 1-character basePrefix (a real last name can normalize to a single letter after stripping
        // non-alphanumerics) - shortest possible result is "X-ABCDE" (7 chars).
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var suffix = string.Create(5, alphabet, (span, chars) =>
            {
                for (var i = 0; i < span.Length; i++) span[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
            });
            var candidate = $"{basePrefix}-{suffix}";
            if (await _codes.GetByCodeAsync(candidate, cancellationToken) is null
                && await _promoCodes.GetByCodeAsync(candidate, cancellationToken) is null)
            {
                return candidate;
            }
        }

        throw new DomainException("Nie udało się wygenerować unikalnego kodu, spróbuj ponownie.");
    }

    private static AffiliateCodeResponse ToResponse(AffiliateCode code) => new(
        code.Id, code.Code, code.CountryCode, code.IsActive, code.ClickCount, code.CreatedAt);
}
