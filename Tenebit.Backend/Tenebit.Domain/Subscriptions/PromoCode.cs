using Tenebit.Domain.Common;

namespace Tenebit.Domain.Subscriptions;

public enum PromoDiscountType
{
    Percentage,
    FixedAmount
}

/// <summary>How long a promo code's discount keeps applying across a subscription's renewals - mirrors
/// Paddle's own Discount recurrence model (<c>recur</c> + <c>maximum_recurring_intervals</c>) 1:1, see
/// <see cref="Tenebit.Application.Abstractions.PromoCodeDiscount"/> and
/// Infrastructure.Services.PaddlePaymentGateway.EnsureDiscountAsync.</summary>
public enum PromoDurationType
{
    /// <summary>Applies once, to the first charge only (Paddle <c>recur: false</c>) - the only behavior
    /// that existed before duration was configurable.</summary>
    Once,

    /// <summary>Applies for a fixed number of billing cycles (Paddle <c>recur: true</c> +
    /// <c>maximum_recurring_intervals</c>).</summary>
    Repeating,

    /// <summary>Applies for the lifetime of the subscription (Paddle <c>recur: true</c> with no
    /// <c>maximum_recurring_intervals</c>).</summary>
    Forever
}

public sealed class PromoCode
{
    private PromoCode() { }

    public PromoCode(string code, string planKey, PromoDiscountType discountType, decimal discountValue, int? maxRedemptions, DateTimeOffset? expiresAt, PromoDurationType durationType, int? durationInMonths, string? description, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("Kod promocyjny nie może być pusty.");
        var plan = SubscriptionPlan.FromKey(planKey);
        if (plan is null || plan.Key == SubscriptionPlan.Free.Key)
            throw new DomainException("Nieprawidłowy plan dla kodu promocyjnego.");
        if (discountType == PromoDiscountType.Percentage && (discountValue <= 0 || discountValue > 100))
            throw new DomainException("Zniżka procentowa musi być w zakresie 1-100.");
        if (discountType == PromoDiscountType.FixedAmount && discountValue <= 0)
            throw new DomainException("Zniżka kwotowa musi być większa od zera.");
        if (maxRedemptions is <= 0)
            throw new DomainException("Limit użyć musi być większy od zera.");
        if (durationType == PromoDurationType.Repeating && durationInMonths is not (> 0))
            throw new DomainException("Liczba miesięcy zniżki musi być większa od zera.");
        if (durationType != PromoDurationType.Repeating && durationInMonths is not null)
            throw new DomainException("Liczba miesięcy dotyczy tylko zniżki na określoną liczbę miesięcy.");

        Id = Guid.NewGuid();
        Code = code.Trim().ToUpperInvariant();
        PlanKey = plan.Key;
        DiscountType = discountType;
        DiscountValue = discountValue;
        MaxRedemptions = maxRedemptions;
        TimesRedeemed = 0;
        ExpiresAt = expiresAt;
        DurationType = durationType;
        DurationInMonths = durationInMonths;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsActive = true;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string PlanKey { get; private set; } = string.Empty;
    public PromoDiscountType DiscountType { get; private set; }
    public decimal DiscountValue { get; private set; }
    public int? MaxRedemptions { get; private set; }
    public int TimesRedeemed { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public PromoDurationType DurationType { get; private set; }
    public int? DurationInMonths { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsUsable(DateTimeOffset now) =>
        IsActive
        && (ExpiresAt is null || ExpiresAt > now)
        && (MaxRedemptions is null || TimesRedeemed < MaxRedemptions);

    public void Redeem() => TimesRedeemed++;

    public void SetActive(bool active) => IsActive = active;

    public decimal ApplyTo(decimal price) => ComputeDiscountedPrice(price, DiscountType, DiscountValue);

    /// <summary>Shared with the affiliate-code discount path (SubscriptionService) - a percentage/fixed
    /// discount is computed the same way regardless of whether it came from an admin-managed PromoCode
    /// entity or an affiliate code's configured default discount.</summary>
    public static decimal ComputeDiscountedPrice(decimal price, PromoDiscountType discountType, decimal discountValue)
    {
        var discounted = discountType == PromoDiscountType.Percentage
            ? price - price * (discountValue / 100m)
            : price - discountValue;
        return Math.Max(0m, Math.Round(discounted, 2));
    }
}
