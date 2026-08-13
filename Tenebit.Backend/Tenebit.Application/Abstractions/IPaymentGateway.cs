using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Abstractions;

/// <summary>
/// Stripe Checkout/Billing integration. Implemented in Infrastructure against the Stripe REST API.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>True once the Stripe secret key and Pro price id are configured (see appsettings "Stripe" section).</summary>
    bool IsConfigured { get; }

    Task<string> CreateCustomerAsync(string email, Guid organizationId, CancellationToken cancellationToken);

    /// <summary>Creates a Stripe Checkout session for the Pro plan and returns the hosted checkout URL.</summary>
    Task<string> CreateCheckoutSessionAsync(string customerId, Guid organizationId, string successUrl, string cancelUrl, CancellationToken cancellationToken);

    /// <summary>Creates a Stripe Billing Portal session (manage payment method, invoices, cancel) and returns its URL.</summary>
    Task<string> CreateBillingPortalSessionAsync(string customerId, string returnUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies the Stripe-Signature header and parses the payload. Throws if the signature is missing/invalid.
    /// Returns null for event types we don't act on.
    /// </summary>
    PaymentWebhookEvent? ParseWebhookEvent(string payload, string signatureHeader);
}

public sealed record PaymentWebhookEvent(
    string EventType,
    string CustomerId,
    string? SubscriptionId,
    string PlanKey,
    SubscriptionStatus Status,
    DateTimeOffset CurrentPeriodStart,
    DateTimeOffset CurrentPeriodEnd,
    Guid? OrganizationId);
