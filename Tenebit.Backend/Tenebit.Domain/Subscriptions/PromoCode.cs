using Tenebit.Domain.Common;

namespace Tenebit.Domain.Subscriptions;

public enum PromoDiscountType
{
    Percentage,
    FixedAmount
}

public sealed class PromoCode
{
    private PromoCode() { }

    public PromoCode(string code, string planKey, PromoDiscountType discountType, decimal discountValue, int? maxRedemptions, DateTimeOffset? expiresAt, DateTimeOffset now)
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

        Id = Guid.NewGuid();
        Code = code.Trim().ToUpperInvariant();
        PlanKey = plan.Key;
        DiscountType = discountType;
        DiscountValue = discountValue;
        MaxRedemptions = maxRedemptions;
        TimesRedeemed = 0;
        ExpiresAt = expiresAt;
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
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsUsable(DateTimeOffset now) =>
        IsActive
        && (ExpiresAt is null || ExpiresAt > now)
        && (MaxRedemptions is null || TimesRedeemed < MaxRedemptions);

    public void Redeem() => TimesRedeemed++;

    public void SetActive(bool active) => IsActive = active;

    public decimal ApplyTo(decimal price)
    {
        var discounted = DiscountType == PromoDiscountType.Percentage
            ? price - price * (DiscountValue / 100m)
            : price - DiscountValue;
        return Math.Max(0m, Math.Round(discounted, 2));
    }
}
