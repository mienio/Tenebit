using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Abstractions;

public interface IPaymentGateway
{
    bool IsConfigured { get; }
    bool IsPlanConfigured(string planKey, BillingInterval interval);
    Task<string> CreateCustomerAsync(string email, Guid organizationId, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Resolves what the frontend needs to open a Paddle.js checkout for a brand-new subscription.
    /// Unlike Stripe Checkout Sessions there is no server-generated hosted redirect URL in Paddle Billing -
    /// Paddle.js runs client-side and opens the checkout overlay itself from these parameters.
    /// <paramref name="affiliateCode"/> is resolved server-side from the <c>tnb_aff</c> attribution
    /// cookie (spec §13.1) - the frontend never chooses or sees the code itself, only echoes back
    /// whatever <see cref="PaddleCheckoutParams.AffiliateCode"/> comes back here as Paddle.js
    /// <c>customData</c>.</summary>
    Task<PaddleCheckoutParams> GetCheckoutParamsAsync(string customerId, string planKey, BillingInterval interval, CancellationToken cancellationToken, PromoCodeDiscount? discount = null, string? affiliateCode = null);

    /// <summary>Creates a one-time authenticated link into Paddle's customer portal (payment method,
    /// invoices, cancellation) - optionally scoped to a single subscription. Not cacheable; generate a new
    /// one per request, same as Stripe's billing portal session.</summary>
    Task<string> CreateCustomerPortalSessionAsync(string customerId, string? subscriptionId, CancellationToken cancellationToken);

    PaymentWebhookEvent? ParseWebhookEvent(string payload, string signatureHeader);
    Task<PaymentSubscriptionState?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken);
    Task<PaymentSubscriptionState?> FindSubscriptionByCustomerAsync(string customerId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the subscription's whole item list with the single configured price for
    /// (<paramref name="newPlanKey"/>, <paramref name="newInterval"/>).
    ///
    /// Paddle has no notion of a *scheduled* plan change (its <c>scheduled_change</c> only covers
    /// cancel/pause/resume), so every mode here applies the new price to the subscription object
    /// immediately and only differs in how it is billed - see <see cref="PlanChangeBilling"/>. Deferring a
    /// downgrade is therefore Tenebit's own job (OrganizationSubscription.ScheduleDowngrade +
    /// SubscriptionReconciliationService.ApplyDuePlanChangesAsync), never Paddle's.
    /// </summary>
    Task<PlanChangeResult> ChangeSubscriptionPlanAsync(string subscriptionId, string newPlanKey, BillingInterval newInterval, PlanChangeBilling billing, string idempotencyKey, CancellationToken cancellationToken, PromoCodeDiscount? discount = null);

    /// <summary>Previews - without applying anything - the exact amount Paddle would charge right now for an
    /// immediate, prorated switch, so the confirmation dialog can show a real number instead of the new
    /// plan's flat list price (wrong for a mid-cycle switch). Deferred downgrades are never previewed
    /// through Paddle: nothing is charged today and the effective date is our own period end.</summary>
    Task<PlanChangePreview> PreviewPlanChangeAsync(string subscriptionId, string newPlanKey, BillingInterval newInterval, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies the subscription carries exactly one line item - the configured price for
    /// (<paramref name="planKey"/>, <paramref name="interval"/>) - and rewrites the item list when it does
    /// not, without billing anything.
    ///
    /// A subscription that accumulates a second copy of its own plan renews at a multiple of its list price
    /// and hands back a multiple of the proration credit on the next switch - the "double credit / negative
    /// amount due" failure from the 18-19.09.2026 payment test report. This is the belt-and-braces check
    /// that neither can survive a plan change or a reconciliation cycle.
    ///
    /// Scope, precisely: this covers the subscription's own <c>items</c> and nothing else. Deferred
    /// proration already accrued against the *upcoming transaction* is not an item and cannot be rewritten
    /// away here - see <see cref="GetRenewalAuditAsync"/>, which is what surfaces those.
    /// </summary>
    Task<SubscriptionItemRepair> EnsureCanonicalItemsAsync(string subscriptionId, string planKey, BillingInterval interval, CancellationToken cancellationToken);

    /// <summary>
    /// Reads what Paddle will actually bill at the next renewal, so a renewal inflated beyond the plan's
    /// list price is noticed before the customer pays it rather than afterwards.
    ///
    /// A healthy renewal is a single line: the plan, at its price. Extra lines are deferred proration -
    /// charges Paddle accrued but postponed, which is exactly what <c>full_next_billing_period</c> produced
    /// while the deferred-downgrade path still went through Paddle (the 0,01 EUR and 2,76 EUR lines the
    /// 20.09.2026 verification found still queued on the sandbox account, with accrual windows that match
    /// the pre-fix plan changes to the second). No current code path can create one - the switch either
    /// bills immediately or does not bill at all - but residue from before the fix stays on the account
    /// until it is billed, and Paddle offers no API to drop a charge from a future transaction. Detecting
    /// and reporting it is therefore the whole remedy available in code; anything further is a credit
    /// issued by hand.
    /// </summary>
    Task<SubscriptionRenewalAudit?> GetRenewalAuditAsync(string subscriptionId, CancellationToken cancellationToken);

    /// <summary>Compares every configured Paddle Price against the plan catalogue's own amount. A price
    /// edited (or mistyped) in the Paddle dashboard silently overrides what the pricing page promises - the
    /// Growth Annual case from the test report, where Paddle charged 36,85 EUR/year less than advertised.</summary>
    Task<IReadOnlyList<PlanPriceMismatch>> ListPlanPriceMismatchesAsync(CancellationToken cancellationToken);

    /// <summary>Pushes the organization's own invoice details (company name, VAT ID, address) onto the
    /// Paddle customer, creating or updating Paddle's Address and Business objects for it, and returns
    /// their ids so a checkout can be opened against them.
    ///
    /// Paddle is the Merchant of Record and issues the invoice itself, so this is the only way a buyer's
    /// VAT ID ever reaches the document. Attaching it up front also means a company does not retype its
    /// NIP inside the Paddle overlay - and that what the invoice preview shows before payment is exactly
    /// what Paddle will print.</summary>
    Task<PaddleBillingEntities> SyncCustomerBillingAsync(string customerId, BillingProfile profile, CancellationToken cancellationToken);

    /// <summary>Lists a customer's Paddle transactions, newest first - the actual payment record (amount
    /// charged, currency, status, invoice PDF link) behind a subscription. Paddle is the only place this is
    /// stored; Tenebit's own database never mirrors it (see AdminOverviewService.GetOrganizationPaymentsAsync).</summary>
    /// <param name="limit">How many of the newest transactions to read. Each one costs a second Paddle
    /// call for its invoice PDF link, so a screen that only shows a recent history asks for that much.</param>
    Task<IReadOnlyList<PaymentInvoice>> ListInvoicesAsync(string customerId, CancellationToken cancellationToken, int limit = 100);
}

/// <summary>When a plan switch takes effect for the customer: right now (an upgrade - prorated and charged
/// immediately) or at the end of the current billing period (a downgrade - nothing charged today, the org
/// keeps the entitlements it already paid for until then). The deferred case is tracked by Tenebit itself
/// (Paddle cannot schedule a plan change); see <see cref="IPaymentGateway.ChangeSubscriptionPlanAsync"/>.</summary>
public enum PlanChangeTiming
{
    Immediately,
    NextBillingPeriod
}

/// <summary>How Paddle should bill an item swap that, either way, applies to the subscription object at
/// once.</summary>
public enum PlanChangeBilling
{
    /// <summary>Charge the new plan now, minus a credit for the unused remainder of the current one, and
    /// roll the billing anchor to today. Used for every upgrade, and for a deferred change whose billing
    /// interval also changes (a new cycle has to start somewhere).</summary>
    ProrateImmediately,

    /// <summary>Swap the price with no charge, no credit and no change to the billing anchor - the next
    /// renewal simply bills the new price. Used to land a deferred downgrade on its effective date.</summary>
    SwitchWithoutCharge
}

public sealed class PaymentWebhookValidationException : Exception
{
    public PaymentWebhookValidationException(string message, Exception? innerException = null) : base(message, innerException) { }
}

public sealed class PaymentGatewayException : Exception
{
    /// <summary>The upstream HTTP status Paddle returned, when the exception came from a non-2xx Paddle
    /// response - lets callers distinguish an expected business outcome (payment declined) from an
    /// unexpected transport/config failure without parsing the message string.</summary>
    public int? StatusCode { get; }

    public PaymentGatewayException(string message, Exception? innerException = null) : base(message, innerException) { }

    public PaymentGatewayException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}

public sealed record PaymentWebhookEvent(
    string EventId, string EventType, string CustomerId, string? SubscriptionId, string PlanKey,
    SubscriptionStatus Status, DateTimeOffset EventCreatedAt, DateTimeOffset CurrentPeriodStart,
    DateTimeOffset CurrentPeriodEnd, Guid? OrganizationId,
    // Populated only when EventType == "transaction.completed" (spec §13.3) - a shape wholly unrelated
    // to subscription entitlement sync (Status/PlanKey/CurrentPeriod* above are meaningless for it and
    // left at their defaults). Kept on this one record rather than a second type so the whole webhook
    // still goes through a single ParseWebhookEvent/signature-verification path; callers must branch on
    // EventType before touching the subscription-only fields.
    string? TransactionId = null, string? AffiliateCode = null, decimal GrossAmount = 0m, decimal NetAmount = 0m,
    string? Currency = null, bool IsRenewal = false,
    // Populated only when EventType == "adjustment.created" (spec §7.2/§13.4 - a refund/chargeback
    // compensating an earlier transaction.completed). TransactionId above is the adjustment's own id
    // (used as the compensation AffiliateConversion's idempotency key); this is the id of the original
    // transaction it refunds, used to look up which AffiliateConversion to compensate.
    string? OriginalTransactionId = null,
    // Meaningless for the transaction.completed/adjustment.created shapes above, same as PlanKey/Status/
    // CurrentPeriod* - defaults to Monthly there, never read. Appended last (rather than next to PlanKey)
    // so every existing positional constructor call above keeps binding by position unchanged.
    BillingInterval BillingInterval = BillingInterval.Monthly);

public sealed record PaymentSubscriptionState(
    string CustomerId, string SubscriptionId, string PlanKey, SubscriptionStatus Status,
    DateTimeOffset CurrentPeriodStart, DateTimeOffset CurrentPeriodEnd, Guid? OrganizationId,
    BillingInterval BillingInterval = BillingInterval.Monthly);

/// <summary>What Paddle.js needs to open a checkout overlay for a new subscription - no secrets, safe to
/// return to the frontend (the same trust level as a Stripe Checkout Session's client_secret used to be,
/// but here it's just the plan's Price ID plus the customer/discount to prefill).</summary>
public sealed record PaddleCheckoutParams(string PriceId, string CustomerId, string? DiscountId, string? AffiliateCode = null, string? AddressId = null, string? BusinessId = null);

/// <summary>What a plan switch would actually do right now: either the exact amount Paddle would charge
/// immediately (an upgrade), or - when EffectiveAt is set - the date the new price takes effect for free
/// with nothing charged today (a downgrade). <paramref name="AmountDue"/> is Paddle's raw figure and may be
/// negative when the proration credit exceeds the new plan's price; callers must not bill a negative amount
/// (see SubscriptionService.PreviewPlanChangeAsync, which reports the excess as account credit instead).</summary>
public sealed record PlanChangePreview(decimal AmountDue, string Currency, DateTimeOffset? EffectiveAt);

/// <summary>Outcome of <see cref="IPaymentGateway.EnsureCanonicalItemsAsync"/>: how many line items the
/// subscription carried, whether they had to be rewritten, and whether the rewrite actually converged.</summary>
public sealed record SubscriptionItemRepair(int ItemsBefore, bool Repaired, bool Canonical, string? Detail = null)
{
    public static readonly SubscriptionItemRepair AlreadyCanonical = new(1, false, true);
}

/// <summary>What Paddle will charge at the next renewal. <paramref name="DeferredCharges"/> holds every
/// line beyond the plan's own - each one an amount accrued earlier and postponed to this invoice.</summary>
public sealed record SubscriptionRenewalAudit(
    decimal Total,
    string Currency,
    DateTimeOffset? BillingPeriodStart,
    IReadOnlyList<DeferredChargeLine> DeferredCharges);

/// <summary>One postponed proration charge riding along on the next renewal, and the window it accrued
/// over - the window is what identifies which earlier plan change produced it.</summary>
public sealed record DeferredChargeLine(decimal Amount, DateTimeOffset? AccruedFrom, DateTimeOffset? AccruedTo);

/// <summary>A configured Paddle Price that does not match the plan catalogue. Both sides carry their own
/// currency: the interesting mismatch is often exactly that the two differ, which a single shared currency
/// field cannot express without reading as though the catalogue asked for Paddle's.</summary>
public sealed record PlanPriceMismatch(
    string PlanKey,
    BillingInterval Interval,
    string PriceId,
    string Reason,
    decimal Expected,
    string ExpectedCurrency,
    decimal Actual,
    string ActualCurrency);

/// <summary>The outcome of an applied plan switch - the updated subscription plus what was actually
/// charged (0 for a downgrade, or when the proration credit fully covered the new plan - a real, correct
/// outcome and not a sign that nothing happened).</summary>
public sealed record PlanChangeResult(PaymentSubscriptionState Subscription, decimal AmountCharged, string Currency);

/// <summary>The buyer's own details as they should appear on the invoice Paddle issues.
/// <paramref name="TaxId"/> is already normalized by the domain (no separators, EU country prefix where
/// one applies - see Organization.NormalizeTaxId).</summary>
public sealed record BillingProfile(
    string CompanyName, string? TaxId, string? AddressLine1, string? AddressLine2, string? City,
    string? PostalCode, string CountryCode);

/// <summary>The Paddle objects carrying those details. Either id can be null when Paddle had nothing to
/// attach them to (no address data yet, or no VAT ID - Paddle rejects a Business without one).</summary>
public sealed record PaddleBillingEntities(string? AddressId, string? BusinessId)
{
    public static readonly PaddleBillingEntities None = new(null, null);
}

/// <summary>A single Paddle transaction - amounts in major currency units (already converted from Paddle's
/// minor-unit string amounts), Currency as an ISO 4217 code (e.g. "EUR").</summary>
public sealed record PaymentInvoice(
    string Id, string? Number, decimal AmountPaid, decimal AmountDue, string Currency, string Status,
    DateTimeOffset Created, string? HostedInvoiceUrl, string? InvoicePdfUrl);
