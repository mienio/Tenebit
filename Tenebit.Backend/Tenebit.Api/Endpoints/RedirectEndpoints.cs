namespace Tenebit.Api.Endpoints;

/// <summary>
/// The <c>GET /r/{code}</c> click-through redirect (and the click-tracking it drove) was removed:
/// attribution now comes exclusively from a customer typing an affiliate's code into the checkout
/// "promo code" box (see SubscriptionService.ResolveCodeAsync) - product decision, not every affiliate
/// code needs a link. <see cref="AttributionCookieName"/> stays only so SubscriptionEndpoints can still
/// read an already-issued <c>tnb_aff</c> cookie from a browser that clicked a link before this change;
/// no code anywhere sets this cookie any more, so it has no new effect and can be deleted once those
/// cookies (30-day lifetime, see the old click-recording code) have all expired.
/// </summary>
public static class RedirectEndpoints
{
    public const string AttributionCookieName = "tnb_aff";
}
