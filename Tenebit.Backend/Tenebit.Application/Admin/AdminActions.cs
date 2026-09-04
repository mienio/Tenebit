namespace Tenebit.Application.Admin;

/// <summary>
/// Action verbs written to the admin audit trail. Centralised because they are matched by value in
/// several places - the blast-radius counter excludes the non-moderation ones, and the review queue is
/// derived entirely from <see cref="OrganizationReviewed"/> entries - so a typo in a string literal
/// would silently break either the safety cap or the queue.
/// </summary>
public static class AdminActions
{
    public const string SignedIn = "admin.signed_in";
    public const string SignInFailed = "admin.sign_in_failed";
    public const string OrganizationSuspended = "organization.suspended";
    public const string OrganizationRestored = "organization.restored";

    /// <summary>Bookkeeping, not moderation: records that a name was checked against the terms of service.</summary>
    public const string OrganizationReviewed = "organization.reviewed";

    public const string UserBlocked = "user.blocked";
    public const string UserUnblocked = "user.unblocked";
    public const string UserForcedSignOut = "user.forced_sign_out";

    public const string PromoCodeCreated = "promo_code.created";
    public const string PromoCodeActivated = "promo_code.activated";
    public const string PromoCodeDeactivated = "promo_code.deactivated";
    public const string PromoCodeDeleted = "promo_code.deleted";
}
