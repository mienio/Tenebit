using Tenebit.Domain.Affiliates;
using Tenebit.Domain.Common;

namespace Tenebit.Tests;

public class AffiliateDomainTests
{
    private static DateTimeOffset Now => new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Registration_is_auto_active_with_no_manual_approval_step()
    {
        var affiliate = new Affiliate("damian@example.com", "hash", "Damian", "Kowalski", "PL", Now);
        Assert.Equal(AffiliateStatus.Active, affiliate.Status);
    }

    [Fact]
    public void Approve_still_works_for_a_manually_reactivated_account()
    {
        var affiliate = new Affiliate("damian@example.com", "hash", "Damian", "Kowalski", "PL", Now);
        affiliate.Approve(Now);
        Assert.Equal(AffiliateStatus.Active, affiliate.Status);
        Assert.NotNull(affiliate.ApprovedAt);
    }

    [Fact]
    public void Cannot_approve_a_blocked_affiliate_without_explicit_reactivation()
    {
        var affiliate = new Affiliate("damian@example.com", "hash", "Damian", "Kowalski", null, Now);
        affiliate.Block("Spam links", Now);

        Assert.Throws<DomainException>(() => affiliate.Approve(Now));
    }

    [Fact]
    public void Block_rotates_security_stamp_so_outstanding_tokens_are_invalidated()
    {
        var affiliate = new Affiliate("damian@example.com", "hash", "Damian", "Kowalski", null, Now);
        var stampBefore = affiliate.SecurityStamp;
        affiliate.Block("fraud", Now);
        Assert.NotEqual(stampBefore, affiliate.SecurityStamp);
    }

    [Fact]
    public void Reactivate_only_allowed_from_blocked()
    {
        var affiliate = new Affiliate("damian@example.com", "hash", "Damian", "Kowalski", null, Now);
        Assert.Throws<DomainException>(() => affiliate.Reactivate(Now));
    }

    [Theory]
    [InlineData("@damian.k")]
    [InlineData("@Damian_Kowalski99")]
    public void Valid_revolut_tags_are_accepted(string tag)
    {
        var affiliate = new Affiliate("damian@example.com", "hash", "Damian", "Kowalski", null, Now);
        affiliate.SetRevolutTag(tag, Now);
        Assert.Equal(tag, affiliate.RevolutTag);
    }

    [Theory]
    [InlineData("damian.k")] // missing leading @
    [InlineData("@a")] // too short
    [InlineData("@has space")]
    public void Invalid_revolut_tags_are_rejected(string tag)
    {
        var affiliate = new Affiliate("damian@example.com", "hash", "Damian", "Kowalski", null, Now);
        Assert.Throws<DomainException>(() => affiliate.SetRevolutTag(tag, Now));
    }

    [Fact]
    public void Revolut_tag_can_be_left_blank_at_registration()
    {
        var affiliate = new Affiliate("damian@example.com", "hash", "Damian", "Kowalski", null, Now);
        affiliate.SetRevolutTag(null, Now);
        Assert.Null(affiliate.RevolutTag);
    }

    [Theory]
    [InlineData("DAMIAN20")]
    [InlineData("A-B-C-D")] // 7 chars, exactly at the minimum
    public void Valid_code_formats_are_normalized_and_accepted(string code)
    {
        Assert.Equal(code.ToUpperInvariant(), AffiliateCode.Normalize(code));
    }

    [Theory]
    [InlineData("ab")] // too short
    [InlineData("DAMIAN")] // 6 chars, one under the 7-char minimum
    [InlineData("HAS SPACE")]
    [InlineData("<script>")]
    [InlineData("ADMIN2026")] // reserved word substring
    public void Invalid_or_reserved_codes_are_rejected(string code)
    {
        Assert.Throws<DomainException>(() => AffiliateCode.Normalize(code));
    }

    [Fact]
    public void Commission_is_computed_from_net_amount_when_configured_as_net_basis()
    {
        var conversion = AffiliateConversion.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "txn_1",
            AffiliateConversionEventType.InitialSale, Now, grossAmount: 11.95m, netAmount: 10.94m,
            currency: "EUR", commissionBase: AffiliateCommissionBase.Net, commissionPercent: 20m,
            isWithinCommissionWindow: true, requiresReview: false, reviewReason: null);

        // 10.94 * 20% = 2.188 -> rounded to 2.19, never 11.95 * 20% = 2.39 (the gross-basis figure).
        Assert.Equal(2.19m, conversion.CommissionAmount);
    }

    [Fact]
    public void Commission_is_computed_from_gross_amount_when_configured_as_gross_basis()
    {
        var conversion = AffiliateConversion.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "txn_2",
            AffiliateConversionEventType.InitialSale, Now, grossAmount: 100m, netAmount: 90m,
            currency: "EUR", commissionBase: AffiliateCommissionBase.Gross, commissionPercent: 15m,
            isWithinCommissionWindow: true, requiresReview: false, reviewReason: null);

        Assert.Equal(15.00m, conversion.CommissionAmount);
    }

    [Fact]
    public void Refund_compensation_is_negative_and_reuses_the_original_commission_rate()
    {
        var original = AffiliateConversion.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "txn_3",
            AffiliateConversionEventType.InitialSale, Now, grossAmount: 100m, netAmount: 90m,
            currency: "EUR", commissionBase: AffiliateCommissionBase.Net, commissionPercent: 20m,
            isWithinCommissionWindow: true, requiresReview: false, reviewReason: null);

        var refund = AffiliateConversion.CreateRefundCompensation(original, "txn_3_refund", Now.AddDays(1), refundedGrossAmount: 100m, refundedNetAmount: 90m);

        Assert.Equal(-18.00m, refund.CommissionAmount);
        Assert.Equal(-90m, refund.NetAmount);
        Assert.NotEqual(original.PaddleTransactionId, refund.PaddleTransactionId);
    }

    [Fact]
    public void A_conversion_flagged_for_review_cannot_be_assigned_to_a_payout_period()
    {
        var conversion = AffiliateConversion.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "txn_4",
            AffiliateConversionEventType.InitialSale, Now, grossAmount: 100m, netAmount: 90m,
            currency: "EUR", commissionBase: AffiliateCommissionBase.Net, commissionPercent: 20m,
            isWithinCommissionWindow: true, requiresReview: true, reviewReason: "self-referral");

        Assert.Throws<DomainException>(() => conversion.AssignToPeriod(Guid.NewGuid()));
    }

    // The exact rule the product owner spelled out: a sale on 2026-09-10 must land in the September
    // period (paid around 20 October), never be treated as already payable on 20 September.
    [Fact]
    public void A_sale_mid_month_belongs_to_that_calendar_month_period_not_a_days_based_cutoff()
    {
        var saleDate = new DateTimeOffset(2026, 9, 10, 8, 0, 0, TimeSpan.Zero);
        var (start, end) = AffiliatePayoutPeriod.ResolveMonthBounds(saleDate);

        Assert.Equal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), end);
    }

    [Fact]
    public void A_sale_on_the_first_of_the_month_still_belongs_to_that_month()
    {
        var saleDate = new DateTimeOffset(2026, 9, 1, 0, 0, 1, TimeSpan.Zero);
        var (start, _) = AffiliatePayoutPeriod.ResolveMonthBounds(saleDate);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), start);
    }

    [Fact]
    public void Period_cannot_close_before_its_calendar_month_has_actually_ended()
    {
        var period = AffiliatePayoutPeriod.OpenFor(Guid.NewGuid(), new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero), Now);
        var stillSeptember = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);

        Assert.Throws<DomainException>(() => period.Close(stillSeptember));
    }

    [Fact]
    public void Period_closes_once_its_calendar_month_has_ended()
    {
        var period = AffiliatePayoutPeriod.OpenFor(Guid.NewGuid(), new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero), Now);
        var afterMonthEnd = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

        period.Close(afterMonthEnd);
        Assert.Equal(PayoutPeriodStatus.AwaitingPayout, period.Status);
    }

    [Fact]
    public void Payout_requires_at_least_one_covered_period_and_a_positive_amount()
    {
        Assert.Throws<DomainException>(() => new AffiliatePayout(Guid.NewGuid(), 0m, "EUR", [Guid.NewGuid()], null, null, Now));
        Assert.Throws<DomainException>(() => new AffiliatePayout(Guid.NewGuid(), 10m, "EUR", [], null, null, Now));
    }

    [Fact]
    public void Message_body_over_the_limit_is_rejected()
    {
        var thread = new AffiliateMessageThread(Guid.NewGuid(), "Pytanie", AffiliateMessageThreadCategory.General, Now);
        var tooLong = new string('a', AffiliateMessage.MaxBodyLength + 1);
        Assert.Throws<DomainException>(() => new AffiliateMessage(thread.Id, AffiliateMessageSenderType.Affiliate, tooLong, Now));
    }
}
