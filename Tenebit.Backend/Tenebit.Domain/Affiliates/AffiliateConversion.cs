using Tenebit.Domain.Common;

namespace Tenebit.Domain.Affiliates;

public enum AffiliateConversionEventType
{
    InitialSale,
    Renewal
}

/// <summary>Which amount a conversion's commission was computed from - frozen per row (see
/// <see cref="AffiliateConversion.CommissionBase"/>) so a later change to
/// <see cref="AffiliateProgramSettings.CommissionBase"/> never silently rewrites already-recorded
/// commissions.</summary>
public enum AffiliateCommissionBase
{
    Gross,
    Net
}

/// <summary>
/// The sale event that actually owes an affiliate money - the one row type this program never lets
/// anyone create by hand through an API: only the Paddle webhook handler is allowed to construct one,
/// exactly like <see cref="Tenebit.Domain.Subscriptions.ProcessedPaddleEvent"/> guards against
/// double-processing the same webhook delivery via <see cref="PaddleTransactionId"/>'s unique index.
///
/// <see cref="CommissionPercent"/> and <see cref="CommissionAmount"/> are frozen at creation time -
/// never a live read of <see cref="Affiliate.CommissionPercentOverride"/> - so raising or lowering an
/// affiliate's rate tomorrow can never rewrite what they were actually owed for a sale made today.
/// </summary>
public sealed class AffiliateConversion
{
    private AffiliateConversion() { }

    private AffiliateConversion(
        Guid affiliateId, Guid affiliateCodeId, Guid? organizationId, Guid? organizationSubscriptionId,
        string paddleTransactionId, AffiliateConversionEventType eventType, DateTimeOffset occurredAt,
        decimal grossAmount, decimal netAmount, string currency, AffiliateCommissionBase commissionBase,
        decimal commissionPercent, bool isWithinCommissionWindow, bool requiresReview, string? reviewReason)
    {
        if (string.IsNullOrWhiteSpace(paddleTransactionId))
            throw new DomainException("Identyfikator transakcji Paddle jest wymagany.");
        if (grossAmount < 0 || netAmount < 0)
            throw new DomainException("Kwoty transakcji nie mogą być ujemne - użyj CreateRefundCompensation dla zwrotów.");
        if (commissionPercent is < 0 or > 100)
            throw new DomainException("Prowizja musi być w zakresie 0-100%.");

        Id = Guid.NewGuid();
        AffiliateId = affiliateId;
        AffiliateCodeId = affiliateCodeId;
        OrganizationId = organizationId;
        OrganizationSubscriptionId = organizationSubscriptionId;
        PaddleTransactionId = paddleTransactionId;
        EventType = eventType;
        OccurredAt = occurredAt;
        GrossAmount = grossAmount;
        NetAmount = netAmount;
        Currency = currency;
        CommissionBase = commissionBase;
        CommissionPercent = commissionPercent;
        var basisAmount = commissionBase == AffiliateCommissionBase.Net ? netAmount : grossAmount;
        CommissionAmount = Math.Round(basisAmount * (commissionPercent / 100m), 2);
        IsWithinCommissionWindow = isWithinCommissionWindow;
        RequiresReview = requiresReview;
        ReviewReason = string.IsNullOrWhiteSpace(reviewReason) ? null : reviewReason.Trim();
        RecordedAt = occurredAt;
    }

    /// <summary>Only entry point - deliberately private constructor, so a conversion can only ever
    /// come from this factory (called exclusively by the Paddle webhook handler).</summary>
    public static AffiliateConversion Create(
        Guid affiliateId, Guid affiliateCodeId, Guid? organizationId, Guid? organizationSubscriptionId,
        string paddleTransactionId, AffiliateConversionEventType eventType, DateTimeOffset occurredAt,
        decimal grossAmount, decimal netAmount, string currency, AffiliateCommissionBase commissionBase,
        decimal commissionPercent, bool isWithinCommissionWindow, bool requiresReview, string? reviewReason) =>
        new(affiliateId, affiliateCodeId, organizationId, organizationSubscriptionId, paddleTransactionId,
            eventType, occurredAt, grossAmount, netAmount, currency, commissionBase, commissionPercent,
            isWithinCommissionWindow, requiresReview, reviewReason);

    /// <summary>A chargeback/refund does not undo a bank transfer that already happened - it creates a
    /// negative row that offsets the balance still owed, or (if the original was already paid) the next
    /// payout (spec §7.2). <paramref name="originalTransactionId"/> keeps its own value: Paddle sends a
    /// distinct notification id for the refund event, so this still satisfies the unique index.</summary>
    public static AffiliateConversion CreateRefundCompensation(
        AffiliateConversion original, string refundTransactionId, DateTimeOffset occurredAt, decimal refundedGrossAmount, decimal refundedNetAmount)
    {
        if (refundedGrossAmount <= 0 || refundedNetAmount <= 0)
            throw new DomainException("Kwota zwrotu musi być większa od zera.");

        var basisAmount = original.CommissionBase == AffiliateCommissionBase.Net ? refundedNetAmount : refundedGrossAmount;
        var compensationAmount = Math.Round(basisAmount * (original.CommissionPercent / 100m), 2);

        return new AffiliateConversion
        {
            Id = Guid.NewGuid(),
            AffiliateId = original.AffiliateId,
            AffiliateCodeId = original.AffiliateCodeId,
            OrganizationId = original.OrganizationId,
            OrganizationSubscriptionId = original.OrganizationSubscriptionId,
            PaddleTransactionId = refundTransactionId,
            EventType = original.EventType,
            OccurredAt = occurredAt,
            GrossAmount = -refundedGrossAmount,
            NetAmount = -refundedNetAmount,
            Currency = original.Currency,
            CommissionBase = original.CommissionBase,
            CommissionPercent = original.CommissionPercent,
            CommissionAmount = -compensationAmount,
            IsWithinCommissionWindow = original.IsWithinCommissionWindow,
            RequiresReview = false,
            ReviewReason = null,
            RecordedAt = occurredAt
        };
    }

    public Guid Id { get; private set; }
    public Guid AffiliateId { get; private set; }
    public Guid AffiliateCodeId { get; private set; }

    /// <summary>Only ever readable by admin-facing services - never surfaced to the affiliate's own
    /// DTOs (spec §6.3/§12.2: zero PII, and an organization id is enough to look one up).</summary>
    public Guid? OrganizationId { get; private set; }
    public Guid? OrganizationSubscriptionId { get; private set; }
    public string PaddleTransactionId { get; private set; } = string.Empty;
    public AffiliateConversionEventType EventType { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public AffiliateCommissionBase CommissionBase { get; private set; }
    public decimal CommissionPercent { get; private set; }
    public decimal CommissionAmount { get; private set; }
    public bool IsWithinCommissionWindow { get; private set; }

    /// <summary>Set when the buyer's own email/domain overlaps the affiliate's (self-referral
    /// heuristic, spec §6.4/§12.7) - held out of automatic payout aggregation until an admin looks at
    /// it explicitly.</summary>
    public bool RequiresReview { get; private set; }
    public string? ReviewReason { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    public Guid? AffiliatePayoutPeriodId { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    public void ClearReview(DateTimeOffset now)
    {
        if (!RequiresReview) throw new DomainException("Ta konwersja nie wymaga przeglądu.");
        RequiresReview = false;
        ReviewedAt = now;
    }

    /// <summary>Only a conversion that has cleared review can ever enter a payout period's total - a
    /// flagged row must never be silently included just because a month rolled over.</summary>
    public void AssignToPeriod(Guid payoutPeriodId)
    {
        if (RequiresReview) throw new DomainException("Konwersja wymaga przeglądu przed przypisaniem do okresu rozliczeniowego.");
        AffiliatePayoutPeriodId = payoutPeriodId;
    }
}
