using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Common;
using Tenebit.Domain.Identity;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Admin;

/// <summary>
/// Admin-side management of marketing promo codes. Platform-wide, not tenant-scoped - a code belongs to
/// a paid plan (<see cref="SubscriptionPlan"/>), not to an organization.
/// </summary>
public sealed class PromoCodeAdminService
{
    private const int MaxQuantity = 200;
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789"; // no 0/O/1/I - avoids misread codes

    private readonly IPromoCodeRepository _promoCodes;
    private readonly IAdminRepository _admin;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public PromoCodeAdminService(IPromoCodeRepository promoCodes, IAdminRepository admin, IUnitOfWork unitOfWork, IClock clock)
    {
        _promoCodes = promoCodes;
        _admin = admin;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PromoCodeResponse>> ListAsync(CancellationToken cancellationToken) =>
        (await _promoCodes.ListAsync(cancellationToken)).Select(ToResponse).ToList();

    public async Task<Result<IReadOnlyList<PromoCodeResponse>>> CreateAsync(
        string planKey, PromoDiscountType discountType, decimal discountValue, int quantity,
        string? code, int? maxRedemptions, DateTimeOffset? expiresAt, string? actorIp, CancellationToken cancellationToken)
    {
        if (quantity is < 1 or > MaxQuantity)
            return Result<IReadOnlyList<PromoCodeResponse>>.Failure(Error.Validation($"Liczba kodów musi być w zakresie 1-{MaxQuantity}."));

        var created = new List<PromoCode>();
        try
        {
            if (quantity == 1 && !string.IsNullOrWhiteSpace(code))
            {
                var explicitCode = code.Trim().ToUpperInvariant();
                if (await _promoCodes.GetByCodeAsync(explicitCode, cancellationToken) is not null)
                    return Result<IReadOnlyList<PromoCodeResponse>>.Failure(Error.Conflict("Taki kod już istnieje."));
                created.Add(new PromoCode(explicitCode, planKey, discountType, discountValue, maxRedemptions, expiresAt, _clock.UtcNow));
            }
            else
            {
                for (var i = 0; i < quantity; i++)
                {
                    var generated = await GenerateUniqueCodeAsync(code, cancellationToken);
                    created.Add(new PromoCode(generated, planKey, discountType, discountValue, maxRedemptions, expiresAt, _clock.UtcNow));
                }
            }
        }
        catch (DomainException ex)
        {
            return Result<IReadOnlyList<PromoCodeResponse>>.Failure(Error.Validation(ex.Message));
        }

        foreach (var promo in created) _promoCodes.Add(promo);

        _admin.AddAdminAudit(new AdminAuditLog(
            AdminActions.PromoCodeCreated, "promo_code", null, $"{planKey} × {created.Count}", null, actorIp, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<IReadOnlyList<PromoCodeResponse>>.Success(created.Select(ToResponse).ToList());
    }

    public async Task<Result> SetActiveAsync(Guid id, bool active, string? actorIp, CancellationToken cancellationToken)
    {
        var promo = await _promoCodes.GetByIdAsync(id, cancellationToken);
        if (promo is null) return Result.Failure(Error.NotFound("Kod nie istnieje."));

        promo.SetActive(active);
        _admin.AddAdminAudit(new AdminAuditLog(
            active ? AdminActions.PromoCodeActivated : AdminActions.PromoCodeDeactivated, "promo_code", id, promo.Code, null, actorIp, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, string? actorIp, CancellationToken cancellationToken)
    {
        var promo = await _promoCodes.GetByIdAsync(id, cancellationToken);
        if (promo is null) return Result.Failure(Error.NotFound("Kod nie istnieje."));

        _promoCodes.Remove(promo);
        _admin.AddAdminAudit(new AdminAuditLog(AdminActions.PromoCodeDeleted, "promo_code", id, promo.Code, null, actorIp, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<string> GenerateUniqueCodeAsync(string? prefix, CancellationToken cancellationToken)
    {
        var cleanPrefix = string.IsNullOrWhiteSpace(prefix)
            ? string.Empty
            : Regex.Replace(prefix.Trim().ToUpperInvariant(), "[^A-Z0-9]", "");

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var suffix = string.Create(6, CodeAlphabet, (span, alphabet) =>
            {
                for (var i = 0; i < span.Length; i++) span[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            });
            var candidate = cleanPrefix.Length == 0 ? suffix : $"{cleanPrefix}-{suffix}";
            if (await _promoCodes.GetByCodeAsync(candidate, cancellationToken) is null) return candidate;
        }

        throw new DomainException("Nie udało się wygenerować unikalnego kodu, spróbuj ponownie.");
    }

    private static PromoCodeResponse ToResponse(PromoCode promo) => new(
        promo.Id, promo.Code, promo.PlanKey, promo.DiscountType.ToString(), promo.DiscountValue,
        promo.MaxRedemptions, promo.TimesRedeemed, promo.ExpiresAt, promo.IsActive, promo.CreatedAt);
}

public sealed record PromoCodeResponse(
    Guid Id, string Code, string PlanKey, string DiscountType, decimal DiscountValue,
    int? MaxRedemptions, int TimesRedeemed, DateTimeOffset? ExpiresAt, bool IsActive, DateTimeOffset CreatedAt);
