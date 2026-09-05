using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Abstractions;

public interface IPaymentGateway
{
    bool IsConfigured { get; }
    Task<string> CreateCustomerAsync(string email, Guid organizationId, string idempotencyKey, CancellationToken cancellationToken);
    Task<string> CreateCheckoutSessionAsync(string customerId, Guid organizationId, string planKey, string successUrl, string cancelUrl, string idempotencyKey, CancellationToken cancellationToken, PromoCodeDiscount? discount = null);
    bool IsPlanConfigured(string planKey);
    Task<string> CreateBillingPortalSessionAsync(string customerId, string returnUrl, CancellationToken cancellationToken);
    PaymentWebhookEvent? ParseWebhookEvent(string payload, string signatureHeader);
    Task<PaymentSubscriptionState?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken);
    Task<PaymentSubscriptionState?> FindSubscriptionByCustomerAsync(string customerId, CancellationToken cancellationToken);
    Task<PaymentSubscriptionState> ChangeSubscriptionPlanAsync(string subscriptionId, string newPlanKey, string idempotencyKey, CancellationToken cancellationToken, PromoCodeDiscount? discount = null);

    /// <summary>Schedules a plan switch to take effect at the end of the subscription's current billing
    /// period, via a Stripe subscription schedule - the subscription stays on its current price/plan
    /// until then. Pass <paramref name="existingScheduleId"/> to retarget an already-scheduled change
    /// instead of creating a second schedule (Stripe allows only one per subscription).</summary>
    Task<PaymentScheduleState> ScheduleDowngradeAsync(string subscriptionId, string? existingScheduleId, string newPlanKey, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Cancels a pending scheduled plan change, leaving the subscription on its current plan
    /// indefinitely (release, not cancel - the underlying subscription itself is untouched).</summary>
    Task ReleaseScheduleAsync(string scheduleId, CancellationToken cancellationToken);

    /// <summary>Lists a customer's Stripe invoices, newest first - the actual payment record (amount
    /// charged, currency, status, hosted/PDF copy) behind a subscription. Stripe is the only place this is
    /// stored; Tenebit's own database never mirrors it (see AdminOverviewService.GetOrganizationPaymentsAsync).</summary>
    Task<IReadOnlyList<PaymentInvoice>> ListInvoicesAsync(string customerId, CancellationToken cancellationToken);
}

public sealed class PaymentWebhookValidationException : Exception
{
    public PaymentWebhookValidationException(string message, Exception? innerException = null) : base(message, innerException) { }
}

public sealed class PaymentGatewayException : Exception
{
    /// <summary>The upstream HTTP status Stripe returned, when the exception came from a non-2xx Stripe
    /// response - lets callers distinguish an expected business outcome (402: card declined) from an
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
    DateTimeOffset CurrentPeriodEnd, Guid? OrganizationId);

public sealed record PaymentSubscriptionState(
    string CustomerId, string SubscriptionId, string PlanKey, SubscriptionStatus Status,
    DateTimeOffset CurrentPeriodStart, DateTimeOffset CurrentPeriodEnd, Guid? OrganizationId);

public sealed record PromoCodeDiscount(PromoDiscountType Type, decimal Value);

public sealed record PaymentScheduleState(string ScheduleId, string PendingPlanKey, DateTimeOffset EffectiveAt);

/// <summary>A single Stripe invoice - amounts in major currency units (already converted from Stripe's
/// minor-unit cents), Currency as an ISO 4217 code (e.g. "EUR").</summary>
public sealed record PaymentInvoice(
    string Id, string? Number, decimal AmountPaid, decimal AmountDue, string Currency, string Status,
    DateTimeOffset Created, string? HostedInvoiceUrl, string? InvoicePdfUrl);
