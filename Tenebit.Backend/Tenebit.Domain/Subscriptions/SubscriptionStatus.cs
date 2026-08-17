namespace Tenebit.Domain.Subscriptions;

public enum SubscriptionStatus
{
    Active,
    Cancelled,
    PastDue,
    Expired,

    // An unrecognized Stripe status — never treated as entitled and never lets SyncFromStripe grant a
    // paid PlanKey (audyt AUD3-010: nieznany status nie może po cichu odblokować płatnego planu).
    Unknown
}
