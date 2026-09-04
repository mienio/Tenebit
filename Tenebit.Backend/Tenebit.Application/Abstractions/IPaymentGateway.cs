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
    Task<PaymentSubscriptionState> ChangeSubscriptionPlanAsync(string subscriptionId, string newPlanKey, string idempotencyKey, CancellationToken cancellationToken);
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
