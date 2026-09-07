using System.ComponentModel.DataAnnotations;
using Tenebit.Api.Auth;
using Tenebit.Api.Http;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Affiliates;
using Tenebit.Application.Common;

namespace Tenebit.Api.Endpoints;

[ValidatedRequest]
public sealed record AffiliateRegisterRequest(
    [property: Required, EmailAddress, StringLength(240)] string Email,
    [property: Required, StringLength(100, MinimumLength = 8)] string Password,
    [property: Required, StringLength(120)] string FirstName,
    [property: Required, StringLength(120)] string LastName,
    [property: StringLength(2, MinimumLength = 2)] string? CountryCode,
    [property: StringLength(34)] string? RevolutTag,
    [property: Required] bool AcceptTerms);

[ValidatedRequest]
public sealed record AffiliateLoginRequest(
    [property: Required, EmailAddress, StringLength(240)] string Email,
    [property: Required] string Password);

[ValidatedRequest]
public sealed record AffiliatePasswordResetRequestRequest([property: Required, EmailAddress, StringLength(240)] string Email);

[ValidatedRequest]
public sealed record AffiliatePasswordResetConfirmRequest(
    [property: Required, EmailAddress, StringLength(240)] string Email,
    [property: Required, StringLength(6, MinimumLength = 6)] string Code,
    [property: Required, StringLength(100, MinimumLength = 8)] string NewPassword);

[ValidatedRequest]
public sealed record AffiliateVerifyEmailRequest(
    [property: Required, EmailAddress, StringLength(240)] string Email,
    [property: Required, StringLength(6, MinimumLength = 6)] string Code);

[ValidatedRequest]
public sealed record AffiliateUpdateProfileRequest(
    [property: Required, StringLength(120)] string FirstName,
    [property: Required, StringLength(120)] string LastName,
    [property: StringLength(40)] string? PhoneNumber,
    [property: StringLength(2, MinimumLength = 2)] string? CountryCode,
    [property: StringLength(200)] string? CompanyName,
    [property: StringLength(40)] string? TaxId,
    [property: StringLength(34)] string? RevolutTag);

[ValidatedRequest]
public sealed record AffiliateCreateCodeRequest(string? Code, [property: StringLength(2, MinimumLength = 2)] string? CountryCode);

[ValidatedRequest]
public sealed record AffiliateSetCodeActiveRequest(bool Active);

[ValidatedRequest]
public sealed record AffiliateCreateMessageThreadRequest(
    [property: Required, StringLength(200, MinimumLength = 3)] string Subject,
    bool IsComplaint,
    [property: Required, StringLength(5000, MinimumLength = 1)] string Body);

[ValidatedRequest]
public sealed record AffiliateReplyMessageRequest([property: Required, StringLength(5000, MinimumLength = 1)] string Body);

/// <summary>
/// Fully isolated from /api and /api/admin: separate route group, separate JWT scope
/// (token_scope=affiliate), no organization_id, no PlatformAdmin claim. TenebitEndpoints explicitly
/// rejects this scope on every tenant route, and this group requires the "Affiliate" policy on every
/// route but the anonymous auth ones - the three identity kinds are mutually exclusive by
/// construction, exactly like AdminEndpoints already documents for the tenant/platform-admin pair.
/// </summary>
public static class PartnerEndpoints
{
    public static RouteGroupBuilder MapPartnerEndpoints(this IEndpointRouteBuilder app)
    {
        var partner = app.MapGroup("/api/partner").WithTags("Partner");
        partner.RequireAuthorization("Affiliate");
        partner.AddEndpointFilter<ValidationEndpointFilter>();

        MapAuth(partner);
        MapProfile(partner);
        MapCodes(partner);
        MapDashboard(partner);
        MapMessages(partner);
        return partner;
    }

    private static void MapAuth(RouteGroupBuilder partner)
    {
        partner.MapPost("/register", async (AffiliateRegisterRequest request, AffiliateAuthService service, CancellationToken cancellationToken) =>
            {
                var result = await service.RegisterAsync(
                    request.Email, request.Password, request.FirstName, request.LastName, request.CountryCode,
                    request.RevolutTag, request.AcceptTerms, cancellationToken);
                if (result.IsFailure) return result.ToHttpResultIfFailure()!;
                return Results.Accepted(value: new { requiresEmailVerification = true });
            })
            .AllowAnonymous()
            .RequireRateLimiting("affiliate-register");

        partner.MapPost("/login", async (
                AffiliateLoginRequest request, HttpContext http, HttpResponse response, IWebHostEnvironment env,
                AffiliateAuthService service, TokenIssuer tokens, CancellationToken cancellationToken) =>
            {
                var result = await service.LoginAsync(request.Email, request.Password, cancellationToken);
                if (result.IsFailure) return result.ToHttpResult();

                var affiliate = result.Value!.Affiliate;
                var refreshToken = await service.IssueRefreshTokenAsync(affiliate.Id, cancellationToken);
                AffiliateRefreshTokenCookie.Append(response, refreshToken, env.IsDevelopment());

                // Access token carries the security stamp at issue time; Program.cs re-checks it live
                // on every request, so a password change/block invalidates this token immediately, not
                // just at its 15-minute natural expiry.
                var securityStamp = Guid.NewGuid();
                var token = tokens.IssueAffiliate(affiliate.Id, affiliate.Email, securityStamp, service.AccessTokenMinutesValue);
                return Results.Ok(new { token, expiresInMinutes = service.AccessTokenMinutesValue, affiliate });
            })
            .AllowAnonymous()
            .RequireRateLimiting("affiliate-login");

        partner.MapPost("/refresh", async (
                HttpRequest request, HttpResponse response, IWebHostEnvironment env,
                AffiliateAuthService service, TokenIssuer tokens, CancellationToken cancellationToken) =>
            {
                var rawToken = request.Cookies[AffiliateRefreshTokenCookie.CookieName];
                if (string.IsNullOrEmpty(rawToken))
                {
                    return Results.Json(new ErrorResponse("Brak aktywnej sesji.", "UNAUTHORIZED"), statusCode: 401);
                }

                var result = await service.RefreshAsync(rawToken, cancellationToken);
                if (result.IsFailure)
                {
                    AffiliateRefreshTokenCookie.Delete(response, env.IsDevelopment());
                    return result.ToHttpResult();
                }

                AffiliateRefreshTokenCookie.Append(response, result.Value!.RawRefreshToken, env.IsDevelopment());
                var affiliate = result.Value!.Affiliate;
                var token = tokens.IssueAffiliate(affiliate.Id, affiliate.Email, Guid.NewGuid(), service.AccessTokenMinutesValue);
                return Results.Ok(new { token, expiresInMinutes = service.AccessTokenMinutesValue, affiliate });
            })
            .AllowAnonymous()
            .RequireRateLimiting("affiliate-recovery");

        partner.MapPost("/logout", async (HttpRequest request, HttpResponse response, IWebHostEnvironment env, AffiliateAuthService service, CancellationToken cancellationToken) =>
            {
                var rawToken = request.Cookies[AffiliateRefreshTokenCookie.CookieName];
                if (!string.IsNullOrEmpty(rawToken)) await service.RevokeRefreshTokenAsync(rawToken, cancellationToken);
                AffiliateRefreshTokenCookie.Delete(response, env.IsDevelopment());
                return Results.NoContent();
            })
            .AllowAnonymous();

        partner.MapPost("/password-reset/request", async (AffiliatePasswordResetRequestRequest request, AffiliateAuthService service, CancellationToken cancellationToken) =>
            {
                await service.RequestPasswordResetAsync(request.Email, cancellationToken);
                // Always 200 - never disclose whether the e-mail belongs to an account (spec §12.3).
                return Results.Ok(new { message = ResultExtensions.Localize("Jeśli konto istnieje, wysłaliśmy kod resetujący.") });
            })
            .AllowAnonymous()
            .RequireRateLimiting("affiliate-recovery");

        partner.MapPost("/password-reset/confirm", async (AffiliatePasswordResetConfirmRequest request, AffiliateAuthService service, CancellationToken cancellationToken) =>
                (await service.ResetPasswordAsync(request.Email, request.Code, request.NewPassword, cancellationToken)).ToNoContentResult())
            .AllowAnonymous()
            .RequireRateLimiting("affiliate-recovery");

        partner.MapPost("/verify-email", async (AffiliateVerifyEmailRequest request, AffiliateAuthService service, CancellationToken cancellationToken) =>
                (await service.VerifyEmailAsync(request.Email, request.Code, cancellationToken)).ToNoContentResult())
            .AllowAnonymous()
            .RequireRateLimiting("affiliate-recovery");
    }

    private static void MapProfile(RouteGroupBuilder partner)
    {
        partner.MapGet("/me", async (HttpContext http, AffiliateAuthService service, CancellationToken cancellationToken) =>
            (await service.GetProfileAsync(http.GetAffiliateId(), cancellationToken)).ToHttpResult());

        partner.MapPatch("/me", async (AffiliateUpdateProfileRequest request, HttpContext http, AffiliateAuthService service, CancellationToken cancellationToken) =>
            (await service.UpdateProfileAsync(
                http.GetAffiliateId(), request.FirstName, request.LastName, request.PhoneNumber, request.CountryCode,
                request.CompanyName, request.TaxId, request.RevolutTag, cancellationToken)).ToHttpResult());
    }

    private static void MapCodes(RouteGroupBuilder partner)
    {
        partner.MapGet("/codes", async (HttpContext http, AffiliateCodeService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(http.GetAffiliateId(), cancellationToken)));

        // Pre-check only for UX - the POST below re-validates everything server-side regardless
        // (spec §5.2: never trust the availability check alone, another request can land in between).
        partner.MapGet("/codes/availability", async (string code, AffiliateCodeService service, CancellationToken cancellationToken) =>
                Results.Ok(new { available = await service.IsAvailableAsync(code, cancellationToken) }))
            .RequireRateLimiting("affiliate-code-check");

        partner.MapPost("/codes", async (AffiliateCreateCodeRequest request, HttpContext http, AffiliateCodeService service, CancellationToken cancellationToken) =>
            (await service.CreateAsync(http.GetAffiliateId(), request.Code, request.CountryCode, cancellationToken)).ToHttpResult());

        partner.MapPatch("/codes/{id:guid}", async (Guid id, AffiliateSetCodeActiveRequest request, HttpContext http, AffiliateCodeService service, CancellationToken cancellationToken) =>
            (await service.SetActiveAsync(http.GetAffiliateId(), id, request.Active, cancellationToken)).ToNoContentResult());
    }

    private static void MapDashboard(RouteGroupBuilder partner)
    {
        partner.MapGet("/dashboard", async (HttpContext http, AffiliateDashboardService service, CancellationToken cancellationToken) =>
            (await service.GetDashboardAsync(http.GetAffiliateId(), cancellationToken)).ToHttpResult());

        partner.MapGet("/conversions", async (int? page, int? pageSize, HttpContext http, AffiliateDashboardService service, CancellationToken cancellationToken) =>
        {
            var affiliateId = http.GetAffiliateId();
            var effectivePage = page ?? 1;
            var effectivePageSize = Math.Clamp(pageSize ?? 50, 1, 200);
            var items = await service.GetConversionsAsync(affiliateId, effectivePage, effectivePageSize, cancellationToken);
            var total = await service.CountConversionsAsync(affiliateId, cancellationToken);
            return Results.Ok(new PagedResult<AffiliateConversionSummaryDto>(items, total, effectivePage, effectivePageSize));
        });

        partner.MapGet("/payouts", async (HttpContext http, AffiliateDashboardService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetPayoutsAsync(http.GetAffiliateId(), cancellationToken)));
    }

    private static void MapMessages(RouteGroupBuilder partner)
    {
        partner.MapGet("/messages", async (HttpContext http, AffiliateMessageService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListForAffiliateAsync(http.GetAffiliateId(), cancellationToken)));

        partner.MapGet("/messages/{id:guid}", async (Guid id, HttpContext http, AffiliateMessageService service, CancellationToken cancellationToken) =>
            (await service.GetThreadForAffiliateAsync(http.GetAffiliateId(), id, cancellationToken)).ToHttpResult());

        partner.MapPost("/messages", async (AffiliateCreateMessageThreadRequest request, HttpContext http, AffiliateMessageService service, AffiliateAlertSender alerts, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateThreadAsync(http.GetAffiliateId(), request.Subject, request.IsComplaint, request.Body, cancellationToken);
            if (result.IsSuccess) await alerts.NewPartnerMessageAsync(request.Subject, request.IsComplaint, cancellationToken);
            return result.ToHttpResult();
        });

        partner.MapPost("/messages/{threadId:guid}/reply", async (Guid threadId, AffiliateReplyMessageRequest request, HttpContext http, AffiliateMessageService service, AffiliateAlertSender alerts, CancellationToken cancellationToken) =>
        {
            var result = await service.ReplyAsAffiliateAsync(http.GetAffiliateId(), threadId, request.Body, cancellationToken);
            if (result.IsSuccess) await alerts.NewPartnerMessageAsync("Odpowiedź w wątku", false, cancellationToken);
            return result.ToNoContentResult();
        });
    }
}
