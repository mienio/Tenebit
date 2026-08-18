using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Abstractions;

public interface IPaymentGateway
{
    bool IsConfigured { get; }
    Task<string> CreateCustomerAsync(string email, Guid organizationId, CancellationToken cancellationToken);
    Task<string> CreateCheckoutSessionAsync(string customerId, Guid organizationId, string successUrl, string cancelUrl, CancellationToken cancellationToken);
    Task<string> CreateBillingPortalSessionAsync(string customerId, string returnUrl, CancellationToken cancellationToken);
    PaymentWebhookEvent? ParseWebhookEvent(string payload, string signatureHeader);
    Task<PaymentSubscriptionState?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken);
}

public sealed class PaymentWebhookValidationException : Exception
{
    public PaymentWebhookValidationException(string message, Exception? innerException = null) : base(message, innerException) { }
}

public sealed class PaymentGatewayException : Exception
{
    public PaymentGatewayException(string message, Exception? innerException = null) : base(message, innerException) { }
}

public sealed record PaymentWebhookEvent(
    string EventId, string EventType, string CustomerId, string? SubscriptionId, string PlanKey,
    SubscriptionStatus Status, DateTimeOffset EventCreatedAt, DateTimeOffset CurrentPeriodStart,
    DateTimeOffset CurrentPeriodEnd, Guid? OrganizationId);

public sealed record PaymentSubscriptionState(
    string CustomerId, string SubscriptionId, string PlanKey, SubscriptionStatus Status,
    DateTimeOffset CurrentPeriodStart, DateTimeOffset CurrentPeriodEnd, Guid? OrganizationId);
