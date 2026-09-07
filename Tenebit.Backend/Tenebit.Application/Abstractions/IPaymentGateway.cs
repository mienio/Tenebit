using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Abstractions;

public interface IPaymentGateway
{
    bool IsConfigured { get; }
    bool IsPlanConfigured(string planKey);
    Task<string> CreateCustomerAsync(string email, Guid organizationId, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Resolves what the frontend needs to open a Paddle.js checkout for a brand-new subscription.
    /// Unlike Stripe Checkout Sessions there is no server-generated hosted redirect URL in Paddle Billing -
    /// Paddle.js runs client-side and opens the checkout overlay itself from these parameters.
    /// <paramref name="affiliateCode"/> is resolved server-side from the <c>tnb_aff</c> attribution
    /// cookie (spec §13.1) - the frontend never chooses or sees the code itself, only echoes back
    /// whatever <see cref="PaddleCheckoutParams.AffiliateCode"/> comes back here as Paddle.js
    /// <c>customData</c>.</summary>
    Task<PaddleCheckoutParams> GetCheckoutParamsAsync(string customerId, string planKey, CancellationToken cancellationToken, PromoCodeDiscount? discount = null, string? affiliateCode = null);

    /// <summary>Creates a one-time authenticated link into Paddle's customer portal (payment method,
    /// invoices, cancellation) - optionally scoped to a single subscription. Not cacheable; generate a new
    /// one per request, same as Stripe's billing portal session.</summary>
    Task<string> CreateCustomerPortalSessionAsync(string customerId, string? subscriptionId, CancellationToken cancellationToken);

    PaymentWebhookEvent? ParseWebhookEvent(string payload, string signatureHeader);
    Task<PaymentSubscriptionState?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken);
    Task<PaymentSubscriptionState?> FindSubscriptionByCustomerAsync(string customerId, CancellationToken cancellationToken);

    /// <summary>
    /// Switches an existing live subscription directly to a different configured plan via Paddle's own
    /// subscription-item price swap - both upgrade (<see cref="PlanChangeTiming.Immediately"/>, prorated and
    /// charged now) and downgrade (<see cref="PlanChangeTiming.NextBillingPeriod"/>, deferred - Paddle keeps
    /// the subscription on its current plan and tracks the pending switch on the subscription itself, no
    /// separate schedule object needed, unlike Stripe subscription schedules).
    /// </summary>
    Task<PlanChangeResult> ChangeSubscriptionPlanAsync(string subscriptionId, string newPlanKey, PlanChangeTiming timing, string idempotencyKey, CancellationToken cancellationToken, PromoCodeDiscount? discount = null);

    /// <summary>Previews - without applying anything - what ChangeSubscriptionPlanAsync would actually do
    /// for the same switch: the exact amount charged now for an immediate upgrade, or the deferred effective
    /// date for a downgrade - so the confirmation dialog can show a real number/date before the customer
    /// commits, instead of the new plan's flat list price (wrong for a mid-cycle switch).</summary>
    Task<PlanChangePreview> PreviewPlanChangeAsync(string subscriptionId, string newPlanKey, PlanChangeTiming timing, CancellationToken cancellationToken);

    /// <summary>Cancels a pending scheduled plan change (set via <see cref="ChangeSubscriptionPlanAsync"/>
    /// with <see cref="PlanChangeTiming.NextBillingPeriod"/>), leaving the subscription on its current plan
    /// indefinitely.</summary>
    Task CancelScheduledChangeAsync(string subscriptionId, CancellationToken cancellationToken);

    /// <summary>Lists a customer's Paddle transactions, newest first - the actual payment record (amount
    /// charged, currency, status, invoice PDF link) behind a subscription. Paddle is the only place this is
    /// stored; Tenebit's own database never mirrors it (see AdminOverviewService.GetOrganizationPaymentsAsync).</summary>
    Task<IReadOnlyList<PaymentInvoice>> ListInvoicesAsync(string customerId, CancellationToken cancellationToken);
}

/// <summary>When to apply a plan switch: right now (upgrade - prorated and charged immediately) or at the
/// end of the current billing period (downgrade - nothing charged today, the org keeps its current plan's
/// entitlements until then).</summary>
public enum PlanChangeTiming
{
    Immediately,
    NextBillingPeriod
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
    string? OriginalTransactionId = null);

public sealed record PaymentSubscriptionState(
    string CustomerId, string SubscriptionId, string PlanKey, SubscriptionStatus Status,
    DateTimeOffset CurrentPeriodStart, DateTimeOffset CurrentPeriodEnd, Guid? OrganizationId);

public sealed record PromoCodeDiscount(PromoDiscountType Type, decimal Value, PromoDurationType DurationType, int? DurationInMonths);

/// <summary>What Paddle.js needs to open a checkout overlay for a new subscription - no secrets, safe to
/// return to the frontend (the same trust level as a Stripe Checkout Session's client_secret used to be,
/// but here it's just the plan's Price ID plus the customer/discount to prefill).</summary>
public sealed record PaddleCheckoutParams(string PriceId, string CustomerId, string? DiscountId, string? AffiliateCode = null);

/// <summary>What a plan switch would actually do right now: either the exact amount Paddle would charge
/// immediately (an upgrade), or - when EffectiveAt is set - the date the new price takes effect for free
/// with nothing charged today (a downgrade).</summary>
public sealed record PlanChangePreview(decimal AmountDue, string Currency, DateTimeOffset? EffectiveAt);

/// <summary>The outcome of an applied plan switch - the updated subscription plus what was actually
/// charged (0 for a downgrade, or when the proration credit fully covered the new plan - a real, correct
/// outcome and not a sign that nothing happened).</summary>
public sealed record PlanChangeResult(PaymentSubscriptionState Subscription, decimal AmountCharged, string Currency, DateTimeOffset? PendingEffectiveAt = null);

/// <summary>A single Paddle transaction - amounts in major currency units (already converted from Paddle's
/// minor-unit string amounts), Currency as an ISO 4217 code (e.g. "EUR").</summary>
public sealed record PaymentInvoice(
    string Id, string? Number, decimal AmountPaid, decimal AmountDue, string Currency, string Status,
    DateTimeOffset Created, string? HostedInvoiceUrl, string? InvoicePdfUrl);
