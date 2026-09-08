using Tenebit.Domain.Common;

namespace Tenebit.Domain.Affiliates;

/// <summary>
/// Global, platform-wide configuration for the affiliate program - a single row, not per-affiliate and
/// not per-organization (no existing "one global settings row" pattern exists elsewhere in the domain
/// to mirror, so this uses the simplest reasonable shape: one row at a well-known, fixed id). Every
/// field here is a business/legal decision the product owner left open in the source plan (§16) -
/// deliberately kept as admin-editable configuration instead of a code constant, so tuning the program
/// (raising commission, changing the payout day) never needs a deployment.
/// </summary>
public sealed class AffiliateProgramSettings
{
    /// <summary>Fixed, well-known row id - there is exactly one of these, ever.</summary>
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000001");

    private AffiliateProgramSettings() { }

    private AffiliateProgramSettings(bool markerOnly)
    {
        Id = SingletonId;
        DefaultCommissionPercent = 10m;
        CommissionBase = AffiliateCommissionBase.Net;
        DefaultCommissionWindowMonths = null; // lifetime, per spec recommendation §6.1/§16.3
        DefaultMaxCodesPerAffiliate = Affiliate.DefaultMaxActiveCodes;
        PayoutDayOfMonth = 20;
        PayoutGraceDays = 5;
        MinimumPayoutAmount = null;
        CodeGrantsCustomerDiscountByDefault = true;
        DefaultCustomerDiscountPercent = 20m;
        DefaultCustomerDiscountDurationMonths = 3;
        PublicLeaderboardEnabled = false;
        TermsVersion = "2026-09-06";
    }

    public static AffiliateProgramSettings CreateDefault() => new(true);

    public Guid Id { get; private set; }

    /// <summary>Default commission rate applied unless <see cref="Affiliate.CommissionPercentOverride"/> is set.</summary>
    public decimal DefaultCommissionPercent { get; private set; }

    /// <summary>Gross (customer-paid) or Net (after Paddle's own cut) - spec §6.1 recommends Net,
    /// since Paddle is the merchant of record and Tenebit only ever actually receives the net amount.</summary>
    public AffiliateCommissionBase CommissionBase { get; private set; }

    /// <summary>Null = commission applies for the customer's entire subscription lifetime (every
    /// renewal), matching <see cref="Tenebit.Domain.Subscriptions.PromoDurationType.Forever"/>'s
    /// convention elsewhere in the codebase. A number = only that many months of renewals count.</summary>
    public int? DefaultCommissionWindowMonths { get; private set; }

    public int DefaultMaxCodesPerAffiliate { get; private set; }

    /// <summary>Day of the month payouts are targeted for (spec: the 20th). Applies only to periods
    /// that have already closed - see <see cref="AffiliatePayoutPeriod"/>'s own cutoff rule.</summary>
    public int PayoutDayOfMonth { get; private set; }

    /// <summary>Maximum days past <see cref="PayoutDayOfMonth"/> the owner commits to actually paying
    /// by (spec: 5 days) - the number the terms of service quotes as the payment commitment.</summary>
    public int PayoutGraceDays { get; private set; }

    /// <summary>Null = no threshold; every non-zero balance is eligible each period.</summary>
    public decimal? MinimumPayoutAmount { get; private set; }

    /// <summary>Whether an affiliate code discounts the customer's price by default (independent
    /// toggle from earning commission - spec §5.3/§16.7). Per-country rules can still apply a discount
    /// even when this default is off. This is what makes a manually-typed affiliate code (not just a
    /// click-through link) actually count at checkout: SubscriptionService checks the "promo code" box
    /// against AffiliateCode whenever it doesn't match a PromoCode, and - when this is true - applies
    /// <see cref="DefaultCustomerDiscountPercent"/>/<see cref="DefaultCustomerDiscountDurationMonths"/>
    /// as a real Paddle discount, while still crediting the affiliate's commission. The customer is never
    /// told the code is an affiliate's; the discount just looks like any other promo code.</summary>
    public bool CodeGrantsCustomerDiscountByDefault { get; private set; }

    /// <summary>Percentage off applied when <see cref="CodeGrantsCustomerDiscountByDefault"/> is true and
    /// no more specific <see cref="AffiliateCountryDiscountRule"/> applies. Null while the feature is off.</summary>
    public decimal? DefaultCustomerDiscountPercent { get; private set; }

    /// <summary>How many billing cycles <see cref="DefaultCustomerDiscountPercent"/> lasts (Paddle
    /// <c>PromoDurationType.Repeating</c>). Null = applies for the customer's whole subscription lifetime.</summary>
    public int? DefaultCustomerDiscountDurationMonths { get; private set; }

    /// <summary>Whether affiliates can see a ranking of other affiliates (spec §16.6 recommends
    /// keeping this off - each affiliate only ever sees their own numbers regardless).</summary>
    public bool PublicLeaderboardEnabled { get; private set; }

    /// <summary>Version string stamped on <see cref="Affiliate.AcceptedTermsVersion"/> at registration -
    /// bump this whenever the terms of service text changes materially.</summary>
    public string TermsVersion { get; private set; } = string.Empty;

    public void Update(
        decimal defaultCommissionPercent, AffiliateCommissionBase commissionBase, int? defaultCommissionWindowMonths,
        int defaultMaxCodesPerAffiliate, int payoutDayOfMonth, int payoutGraceDays, decimal? minimumPayoutAmount,
        bool codeGrantsCustomerDiscountByDefault, decimal? defaultCustomerDiscountPercent, int? defaultCustomerDiscountDurationMonths,
        bool publicLeaderboardEnabled)
    {
        if (defaultCommissionPercent is < 0 or > 100) throw new DomainException("Prowizja musi być w zakresie 0-100%.");
        if (defaultCommissionWindowMonths is <= 0) throw new DomainException("Okno prowizyjne w miesiącach musi być większe od zera.");
        if (defaultMaxCodesPerAffiliate <= 0) throw new DomainException("Domyślny limit kodów musi być większy od zera.");
        if (payoutDayOfMonth is < 1 or > 28) throw new DomainException("Dzień wypłaty musi być w zakresie 1-28 (żeby istniał w każdym miesiącu).");
        if (payoutGraceDays is < 0 or > 28) throw new DomainException("Liczba dni poślizgu musi być w zakresie 0-28.");
        if (minimumPayoutAmount is < 0) throw new DomainException("Minimalna kwota wypłaty nie może być ujemna.");
        if (codeGrantsCustomerDiscountByDefault && defaultCustomerDiscountPercent is not (> 0 and <= 100))
            throw new DomainException("Domyślna zniżka dla klienta musi być w zakresie 1-100%, jeśli kod ma dawać zniżkę.");
        if (defaultCustomerDiscountDurationMonths is <= 0)
            throw new DomainException("Liczba miesięcy domyślnej zniżki dla klienta musi być większa od zera, jeśli podana.");

        DefaultCommissionPercent = defaultCommissionPercent;
        CommissionBase = commissionBase;
        DefaultCommissionWindowMonths = defaultCommissionWindowMonths;
        DefaultMaxCodesPerAffiliate = defaultMaxCodesPerAffiliate;
        PayoutDayOfMonth = payoutDayOfMonth;
        PayoutGraceDays = payoutGraceDays;
        MinimumPayoutAmount = minimumPayoutAmount;
        CodeGrantsCustomerDiscountByDefault = codeGrantsCustomerDiscountByDefault;
        DefaultCustomerDiscountPercent = codeGrantsCustomerDiscountByDefault ? defaultCustomerDiscountPercent : null;
        DefaultCustomerDiscountDurationMonths = codeGrantsCustomerDiscountByDefault ? defaultCustomerDiscountDurationMonths : null;
        PublicLeaderboardEnabled = publicLeaderboardEnabled;
    }

    public void SetTermsVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) throw new DomainException("Wersja regulaminu jest wymagana.");
        TermsVersion = version.Trim();
    }
}
