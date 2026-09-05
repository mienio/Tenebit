using System.ComponentModel.DataAnnotations;
using Tenebit.Api.Auth;
using Tenebit.Api.Http;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Admin;
using Tenebit.Application.Common;
using Tenebit.Application.Identity;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Api.Endpoints;

[ValidatedRequest]
public sealed record AdminLoginRequest(
    [property: Required, EmailAddress, StringLength(240)] string Email,
    [property: Required] string Password,
    [property: Required, StringLength(11, MinimumLength = 6)] string TotpCode);

/// <summary>Every moderation request carries a fresh TOTP code - see the step-up note below.</summary>
[ValidatedRequest]
public sealed record AdminSuspendRequest(
    [property: Required, StringLength(500, MinimumLength = 3)] string Reason,
    [property: Required, StringLength(11, MinimumLength = 6)] string TotpCode);

[ValidatedRequest]
public sealed record AdminUserActionRequest(
    [property: StringLength(500)] string? Reason,
    [property: Required, StringLength(11, MinimumLength = 6)] string TotpCode);

[ValidatedRequest]
public sealed record AdminCreatePromoCodeRequest(
    [property: Required, StringLength(40)] string PlanKey,
    [property: Required] PromoDiscountType DiscountType,
    [property: Range(0.01, 100000)] decimal DiscountValue,
    [property: Range(1, 200)] int Quantity,
    string? Code,
    [property: Range(1, int.MaxValue)] int? MaxRedemptions,
    DateTimeOffset? ExpiresAt);

[ValidatedRequest]
public sealed record AdminSetPromoCodeActiveRequest(bool Active);

// Fully isolated from /api: separate route group, separate JWT scope (token_scope=platform_admin),
// no organization_id, no OrganizationUser row. TenebitEndpoints explicitly rejects this scope on every
// tenant route, and this group requires it on every route but /login - the two are mutually exclusive
// by construction, not just by convention.
public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").WithTags("Admin");
        admin.RequireAuthorization("PlatformAdmin");

        // Network fence applied to the whole group including /login, so a disallowed address cannot even
        // reach the password check.
        admin.AddEndpointFilter(async (context, next) =>
        {
            var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            if (!AdminAccountOptions.IsIpAllowed(configuration, context.HttpContext.Connection.RemoteIpAddress))
            {
                return Results.NotFound();
            }

            return await next(context);
        });

        MapAuth(admin);
        MapReads(admin);
        MapModeration(admin);
        MapPromoCodes(admin);
        return admin;
    }

    private static void MapAuth(RouteGroupBuilder admin)
    {
        admin.MapPost("/login", async (
                AdminLoginRequest request,
                HttpContext http,
                IAuthenticationAbuseLimiter abuseLimiter,
                AdminLoginGuard guard,
                AdminAlertSender alerts,
                TokenIssuer tokens,
                IAdminRepository adminRepository,
                IUnitOfWork unitOfWork,
                IClock clock,
                IConfiguration configuration,
                CancellationToken cancellationToken) =>
            {
                if (!AdminAccountOptions.IsConfigured(configuration))
                {
                    return Results.Json(new ErrorResponse("Panel administracyjny nie jest skonfigurowany.", "ADMIN_NOT_CONFIGURED"), statusCode: 503);
                }

                var ip = http.Connection.RemoteIpAddress;
                if (guard.IsLockedOut(out var retryAfter))
                {
                    return Results.Json(
                        new ErrorResponse($"Panel zablokowany po nieudanych próbach. Spróbuj za {(int)retryAfter.TotalMinutes + 1} min.", "ADMIN_LOCKED"),
                        statusCode: 429);
                }

                if (!await abuseLimiter.TryAcquireAsync("admin-login", "platform-admin", ip?.ToString(), 5, TimeSpan.FromMinutes(15), cancellationToken))
                {
                    return Results.Json(new ErrorResponse("Zbyt wiele prób logowania. Spróbuj ponownie później.", "RATE_LIMITED"), statusCode: 429);
                }

                var configuredEmail = AdminAccountOptions.Email(configuration)!;
                var submittedEmail = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
                var passwordOk = PasswordHasher.Verify(request.Password ?? string.Empty, AdminAccountOptions.PasswordHash(configuration));
                var totpOk = TotpService.ValidateCode(AdminAccountOptions.TotpSecret(configuration)!, request.TotpCode ?? string.Empty);

                if (submittedEmail != configuredEmail || !passwordOk || !totpOk)
                {
                    var failures = guard.RecordFailure();
                    var lockedOut = guard.IsLockedOut(out _);
                    await alerts.SignInFailedAsync(ip, failures, lockedOut, cancellationToken);
                    await WriteAuditAsync(adminRepository, unitOfWork, clock, AdminActions.SignInFailed, null, null, null, ip?.ToString(), cancellationToken);
                    return Results.Json(new ErrorResponse("Nieprawidłowe dane logowania.", "INVALID_CREDENTIALS"), statusCode: 401);
                }

                guard.RecordSuccess();
                await alerts.SignInSucceededAsync(ip, http.Request.Headers.UserAgent.ToString(), cancellationToken);
                await WriteAuditAsync(adminRepository, unitOfWork, clock, AdminActions.SignedIn, null, null, null, ip?.ToString(), cancellationToken);

                var minutes = AdminAccountOptions.TokenMinutes(configuration);
                return Results.Ok(new { token = tokens.IssueAdmin(configuredEmail, minutes), expiresInMinutes = minutes });
            })
            .AllowAnonymous()
            .RequireRateLimiting("admin-login");
    }

    private static void MapReads(RouteGroupBuilder admin)
    {
        admin.MapGet("/dashboard", async (string? from, string? to, AdminOverviewService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetDashboardAsync(ParseDay(from), ParseDay(to), cancellationToken)));

        admin.MapGet("/organizations", async (AdminOverviewService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListOrganizationsAsync(cancellationToken)));

        admin.MapGet("/organizations/{id:guid}", async (Guid id, string? from, string? to, AdminOverviewService service, CancellationToken cancellationToken) =>
        {
            var detail = await service.GetOrganizationDetailAsync(id, ParseDay(from), ParseDay(to), cancellationToken);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });

        admin.MapGet("/organizations/{id:guid}/payments", async (Guid id, AdminOverviewService service, CancellationToken cancellationToken) =>
        {
            var payments = await service.GetOrganizationPaymentsAsync(id, cancellationToken);
            return payments is null ? Results.NotFound() : Results.Ok(payments);
        });

        admin.MapGet("/users", async (string? search, int? page, int? pageSize, AdminOverviewService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListUsersAsync(search, page ?? 1, pageSize ?? 50, cancellationToken)));

        admin.MapGet("/logins", async (string? search, bool? succeeded, Guid? organizationId, int? page, int? pageSize, AdminOverviewService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListLoginsAsync(search, succeeded, organizationId, page ?? 1, pageSize ?? 50, cancellationToken)));

        admin.MapGet("/audit", async (int? limit, AdminOverviewService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAdminAuditAsync(limit ?? 200, cancellationToken)));
    }

    private static void MapModeration(RouteGroupBuilder admin)
    {
        admin.MapPost("/organizations/{id:guid}/suspend", async (
                Guid id, AdminSuspendRequest request, HttpContext http, AdminModerationService moderation,
                AdminAlertSender alerts, IConfiguration configuration, CancellationToken cancellationToken) =>
            {
                if (!RequireStepUp(configuration, request.TotpCode, out var denied)) return denied;
                var result = await moderation.SuspendOrganizationAsync(id, request.Reason ?? string.Empty, http.Connection.RemoteIpAddress?.ToString(), cancellationToken);
                if (result.IsSuccess) await alerts.ModerationActionAsync("zawieszenie organizacji", id.ToString(), http.Connection.RemoteIpAddress, cancellationToken);
                return result.ToNoContentResult();
            });

        admin.MapPost("/organizations/{id:guid}/restore", async (
                Guid id, AdminUserActionRequest request, HttpContext http, AdminModerationService moderation,
                AdminAlertSender alerts, IConfiguration configuration, CancellationToken cancellationToken) =>
            {
                if (!RequireStepUp(configuration, request.TotpCode, out var denied)) return denied;
                var result = await moderation.RestoreOrganizationAsync(id, http.Connection.RemoteIpAddress?.ToString(), cancellationToken);
                if (result.IsSuccess) await alerts.ModerationActionAsync("przywrócenie organizacji", id.ToString(), http.Connection.RemoteIpAddress, cancellationToken);
                return result.ToNoContentResult();
            });

        // No step-up code and no alert: this records that a name was checked, it changes nobody's access.
        admin.MapPost("/organizations/{id:guid}/review", async (
                Guid id, HttpContext http, AdminModerationService moderation, CancellationToken cancellationToken) =>
            (await moderation.MarkReviewedAsync(id, http.Connection.RemoteIpAddress?.ToString(), cancellationToken)).ToNoContentResult());

        admin.MapPost("/users/{id:guid}/block", async (
                Guid id, AdminUserActionRequest request, HttpContext http, AdminModerationService moderation,
                AdminAlertSender alerts, IConfiguration configuration, CancellationToken cancellationToken) =>
            {
                if (!RequireStepUp(configuration, request.TotpCode, out var denied)) return denied;
                var result = await moderation.SetUserActiveAsync(id, false, request.Reason, http.Connection.RemoteIpAddress?.ToString(), cancellationToken);
                if (result.IsSuccess) await alerts.ModerationActionAsync("blokada konta", id.ToString(), http.Connection.RemoteIpAddress, cancellationToken);
                return result.ToNoContentResult();
            });

        admin.MapPost("/users/{id:guid}/unblock", async (
                Guid id, AdminUserActionRequest request, HttpContext http, AdminModerationService moderation,
                AdminAlertSender alerts, IConfiguration configuration, CancellationToken cancellationToken) =>
            {
                if (!RequireStepUp(configuration, request.TotpCode, out var denied)) return denied;
                var result = await moderation.SetUserActiveAsync(id, true, request.Reason, http.Connection.RemoteIpAddress?.ToString(), cancellationToken);
                if (result.IsSuccess) await alerts.ModerationActionAsync("odblokowanie konta", id.ToString(), http.Connection.RemoteIpAddress, cancellationToken);
                return result.ToNoContentResult();
            });

        admin.MapPost("/users/{id:guid}/force-signout", async (
                Guid id, AdminUserActionRequest request, HttpContext http, AdminModerationService moderation,
                AdminAlertSender alerts, IConfiguration configuration, CancellationToken cancellationToken) =>
            {
                if (!RequireStepUp(configuration, request.TotpCode, out var denied)) return denied;
                var result = await moderation.ForceSignOutAsync(id, http.Connection.RemoteIpAddress?.ToString(), cancellationToken);
                if (result.IsSuccess) await alerts.ModerationActionAsync("wymuszone wylogowanie", id.ToString(), http.Connection.RemoteIpAddress, cancellationToken);
                return result.ToNoContentResult();
            });
    }

    // No step-up code: promo codes are marketing configuration, not a path to anyone's account or data.
    private static void MapPromoCodes(RouteGroupBuilder admin)
    {
        admin.MapGet("/promo-codes", async (PromoCodeAdminService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cancellationToken)));

        admin.MapPost("/promo-codes", async (
                AdminCreatePromoCodeRequest request, HttpContext http, PromoCodeAdminService service, CancellationToken cancellationToken) =>
            (await service.CreateAsync(
                request.PlanKey, request.DiscountType, request.DiscountValue, request.Quantity,
                request.Code, request.MaxRedemptions, request.ExpiresAt,
                http.Connection.RemoteIpAddress?.ToString(), cancellationToken)).ToHttpResult());

        admin.MapPost("/promo-codes/{id:guid}/active", async (
                Guid id, AdminSetPromoCodeActiveRequest request, HttpContext http, PromoCodeAdminService service, CancellationToken cancellationToken) =>
            (await service.SetActiveAsync(id, request.Active, http.Connection.RemoteIpAddress?.ToString(), cancellationToken)).ToNoContentResult());

        admin.MapDelete("/promo-codes/{id:guid}", async (
                Guid id, HttpContext http, PromoCodeAdminService service, CancellationToken cancellationToken) =>
            (await service.DeleteAsync(id, http.Connection.RemoteIpAddress?.ToString(), cancellationToken)).ToNoContentResult());
    }

    /// <summary>
    /// Step-up authentication (NIST 800-63B AAL2 pattern): holding a valid session token is not enough to
    /// change anyone's access. Each moderation call must carry a current TOTP code, so an attacker who
    /// stole a token - or walked up to an unlocked browser - still cannot act without the authenticator
    /// device itself. Combined with the per-hour action cap this keeps the blast radius small.
    /// </summary>
    private static bool RequireStepUp(IConfiguration configuration, string? totpCode, out IResult denied)
    {
        var secret = AdminAccountOptions.TotpSecret(configuration);
        if (!string.IsNullOrWhiteSpace(secret) && TotpService.ValidateCode(secret, totpCode ?? string.Empty))
        {
            denied = Results.Empty;
            return true;
        }

        denied = Results.Json(new ErrorResponse("Wymagany aktualny kod 2FA dla tej operacji.", "STEP_UP_REQUIRED"), statusCode: 403);
        return false;
    }

    /// <summary>Parses an ISO date from the query string; anything unparsable falls back to the default range.</summary>
    private static DateOnly? ParseDay(string? value) =>
        DateOnly.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var day) ? day : null;

    private static async Task WriteAuditAsync(
        IAdminRepository adminRepository, IUnitOfWork unitOfWork, IClock clock,
        string action, string? targetType, Guid? targetId, string? label, string? ip, CancellationToken cancellationToken)
    {
        adminRepository.AddAdminAudit(new Domain.Identity.AdminAuditLog(action, targetType, targetId, label, null, ip, clock.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
