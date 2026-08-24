using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Tenebit.Api.Auth;
using Tenebit.Api.Http;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Alerts;
using Tenebit.Application.Assets;
using Tenebit.Application.Assignments;
using Tenebit.Application.Audit;
using Tenebit.Application.Common;
using Tenebit.Domain.Alerts;
using Tenebit.Application.Audits;
using Tenebit.Domain.Audits;
using Tenebit.Application.Dashboard;
using Tenebit.Application.Evidence;
using Tenebit.Application.Identity;
using Tenebit.Application.JobProfiles;
using Tenebit.Application.Licenses;
using Tenebit.Application.Offboarding;
using Tenebit.Application.Onboarding;
using Tenebit.Application.Organizations;
using Tenebit.Application.People;
using Tenebit.Application.Procedures;
using Tenebit.Application.Reservations;
using Tenebit.Application.Settings;
using Tenebit.Application.Subscriptions;
using Tenebit.Application.Workspace;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.Reservations;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/register", async (RegisterRequest request, HttpContext http, AuthService service, IAuthenticationAbuseLimiter abuseLimiter, CancellationToken cancellationToken) =>
            {
                if (!await abuseLimiter.TryAcquireAsync("register", request.Email, http.Connection.RemoteIpAddress?.ToString(), 5, TimeSpan.FromMinutes(15), cancellationToken))
                    return Results.Json(new ErrorResponse("Zbyt wiele prób. Spróbuj ponownie później.", "RATE_LIMITED"), statusCode: 429);
                var result = await service.RegisterAsync(request, cancellationToken);
                if (result.IsFailure) return result.ToHttpResult();

                // Registration proves knowledge of a password, not mailbox ownership. Do not issue any
                // workspace JWT/refresh cookie until the one-time verification code has been consumed.
                return Results.Accepted(value: new { requiresEmailVerification = service.RequiresEmailVerificationOnRegister });
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth-register")
            .WithTags("Auth");

        api.MapPost("/auth/login", async (LoginRequest request, HttpContext http, AuthService service, IAuthenticationAbuseLimiter abuseLimiter, TokenIssuer tokens, TwoFactorChallengeStore challenges, HttpResponse response, IWebHostEnvironment env, CancellationToken cancellationToken) =>
            {
                if (!await abuseLimiter.TryAcquireAsync("login", request.Email, http.Connection.RemoteIpAddress?.ToString(), 10, TimeSpan.FromMinutes(5), cancellationToken))
                    return Results.Json(new ErrorResponse("Zbyt wiele prób logowania. Spróbuj ponownie później.", "RATE_LIMITED"), statusCode: 429);
                var deviceTrustToken = http.Request.Cookies[DeviceTrustCookie.CookieName];
                var result = await service.LoginAsync(request, deviceTrustToken, http.ToAuthRequestContext(), cancellationToken);
                if (result.IsFailure) return result.ToHttpResult();

                if (result.Value!.RequiresTwoFactor)
                {
                    var challengeToken = await challenges.CreateAsync(result.Value!.PendingUserId!.Value, cancellationToken);
                    return Results.Ok(new { requiresTwoFactor = true, challengeToken });
                }

                var user = result.Value!.User!;
                var refreshToken = await service.IssueRefreshTokenAsync(user.Id, cancellationToken);
                RefreshTokenCookie.Append(response, refreshToken, env.IsDevelopment());
                return Results.Ok(new { token = tokens.Issue(user), user });
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth-login")
            .WithTags("Auth");

        api.MapPost("/auth/login/2fa", async (TwoFactorLoginRequest request, HttpContext http, AuthService service, TokenIssuer tokens, TwoFactorChallengeStore challenges, HttpResponse response, IWebHostEnvironment env, CancellationToken cancellationToken) =>
            {
                var userId = await challenges.ConsumeAsync(request.ChallengeToken, cancellationToken);
                if (userId is null)
                {
                    return Results.Json(new ErrorResponse("Sesja logowania wygasła. Zaloguj się ponownie.", "CHALLENGE_EXPIRED"), statusCode: 401);
                }

                var result = await service.CompleteTwoFactorLoginAsync(userId.Value, request.Code, http.ToAuthRequestContext(), cancellationToken);
                if (result.IsFailure) return result.ToHttpResult();

                var refreshToken = await service.IssueRefreshTokenAsync(result.Value!.Id, cancellationToken);
                RefreshTokenCookie.Append(response, refreshToken, env.IsDevelopment());

                if (request.RememberDevice)
                {
                    var deviceTrustToken = await service.IssueDeviceTrustTokenAsync(result.Value!.Id, cancellationToken);
                    DeviceTrustCookie.Append(response, deviceTrustToken, env.IsDevelopment());
                }

                return Results.Ok(new { token = tokens.Issue(result.Value!), user = result.Value });
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth-login")
            .WithTags("Auth");

        api.MapPost("/auth/refresh", async (HttpRequest request, HttpResponse response, AuthService service, TokenIssuer tokens, IWebHostEnvironment env, CancellationToken cancellationToken) =>
            {
                var rawToken = request.Cookies[RefreshTokenCookie.CookieName];
                if (string.IsNullOrEmpty(rawToken))
                {
                    return Results.Json(new ErrorResponse("Brak aktywnej sesji.", "UNAUTHORIZED"), statusCode: 401);
                }

                var result = await service.RefreshAsync(rawToken, cancellationToken);
                if (result.IsFailure)
                {
                    RefreshTokenCookie.Delete(response, env.IsDevelopment());
                    return result.ToHttpResult();
                }

                RefreshTokenCookie.Append(response, result.Value!.RefreshToken, env.IsDevelopment());
                return Results.Ok(new { token = tokens.Issue(result.Value!.User), user = result.Value!.User });
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth-refresh")
            .WithTags("Auth");

        api.MapPost("/auth/logout", async (HttpRequest request, HttpResponse response, AuthService service, IWebHostEnvironment env, CancellationToken cancellationToken) =>
            {
                var rawToken = request.Cookies[RefreshTokenCookie.CookieName];
                if (!string.IsNullOrEmpty(rawToken))
                {
                    await service.RevokeRefreshTokenAsync(rawToken, cancellationToken);
                }

                RefreshTokenCookie.Delete(response, env.IsDevelopment());
                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithTags("Auth");

        api.MapPost("/auth/password/forgot", async (ForgotPasswordRequest request, HttpContext http, AuthService service, IAuthenticationAbuseLimiter abuseLimiter, CancellationToken cancellationToken) =>
            {
                if (await abuseLimiter.TryAcquireAsync("password-forgot", request.Email, http.Connection.RemoteIpAddress?.ToString(), 5, TimeSpan.FromMinutes(15), cancellationToken))
                    await service.RequestPasswordResetAsync(request, cancellationToken);
                return Results.Ok(new { message = "Jeśli podany adres e-mail istnieje w systemie, wysłaliśmy kod do resetu hasła." });
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth-recovery")
            .WithTags("Auth");

        api.MapPost("/auth/password/reset", async (ResetPasswordRequest request, HttpContext http, AuthService service, IAuthenticationAbuseLimiter abuseLimiter, CancellationToken cancellationToken) =>
            {
                if (!await abuseLimiter.TryAcquireAsync("password-reset-code", request.Email, http.Connection.RemoteIpAddress?.ToString(), 10, TimeSpan.FromMinutes(15), cancellationToken))
                    return Results.Json(new ErrorResponse(ErrorMessageTranslator.Translate("Zbyt wiele prób. Poproś o nowy kod lub spróbuj ponownie później.", RequestLanguageAccessor.CurrentLanguage), "RATE_LIMITED"), statusCode: 429);
                var result = await service.ResetPasswordAsync(request, cancellationToken);
                return result.IsFailure ? result.ToNoContentResult() : Results.Ok(new { message = "Hasło zostało zmienione." });
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth-recovery")
            .WithTags("Auth");

        api.MapPost("/auth/verify-email", async (VerifyEmailRequest request, HttpContext http, AuthService service, IAuthenticationAbuseLimiter abuseLimiter, CancellationToken cancellationToken) =>
            {
                if (!await abuseLimiter.TryAcquireAsync("email-verification-code", request.Email, http.Connection.RemoteIpAddress?.ToString(), 10, TimeSpan.FromMinutes(15), cancellationToken))
                    return Results.Json(new ErrorResponse(ErrorMessageTranslator.Translate("Zbyt wiele prób. Poproś o nowy kod lub spróbuj ponownie później.", RequestLanguageAccessor.CurrentLanguage), "RATE_LIMITED"), statusCode: 429);
                var result = await service.VerifyEmailAsync(request, cancellationToken);
                return result.IsFailure ? result.ToNoContentResult() : Results.Ok(new { message = "E-mail został potwierdzony." });
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth-recovery")
            .WithTags("Auth");

        api.MapPost("/auth/resend-verification", async (ResendVerificationRequest request, HttpContext http, AuthService service, IAuthenticationAbuseLimiter abuseLimiter, CancellationToken cancellationToken) =>
            {
                if (await abuseLimiter.TryAcquireAsync("verification-resend", request.Email, http.Connection.RemoteIpAddress?.ToString(), 5, TimeSpan.FromMinutes(15), cancellationToken))
                    await service.ResendVerificationEmailAsync(request.Email, cancellationToken);
                return Results.Ok(new { message = "Jeśli konto oczekuje na potwierdzenie, wysłaliśmy nowy kod." });
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth-recovery")
            .WithTags("Auth");

        api.MapPut("/auth/display-name", async (UpdateDisplayNameRequest request, ICurrentUser currentUser, AuthService service, TokenIssuer tokens, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.Subject, out var userId))
                {
                    return Results.Json(new ErrorResponse("Nieprawidłowa sesja.", "UNAUTHORIZED"), statusCode: 401);
                }

                var result = await service.UpdateDisplayNameAsync(userId, request.DisplayName, cancellationToken);
                if (result.IsFailure) return result.ToHttpResult();

                var user = result.Value!;
                return Results.Ok(new { token = tokens.Issue(user), user });
            })
            .WithTags("Auth");

        api.MapPost("/auth/2fa/setup", async (ICurrentUser currentUser, AuthService service, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.Subject, out var userId))
                {
                    return Results.Json(new ErrorResponse("Nieprawidłowa sesja.", "UNAUTHORIZED"), statusCode: 401);
                }

                var result = await service.SetupTwoFactorAsync(userId, cancellationToken);
                return result.ToHttpResult();
            })
            .RequireRateLimiting("auth-recovery")
            .WithTags("Auth");

        api.MapPost("/auth/2fa/enable", async (TwoFactorCodeRequest request, ICurrentUser currentUser, AuthService service, TokenIssuer tokens, HttpResponse response, IWebHostEnvironment env, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.Subject, out var userId))
                {
                    return Results.Json(new ErrorResponse("Nieprawidłowa sesja.", "UNAUTHORIZED"), statusCode: 401);
                }

                var result = await service.EnableTwoFactorAsync(userId, request.Code, cancellationToken);
                if (result.IsFailure) return result.ToHttpResult();

                var user = result.Value!.User;
                var refreshToken = await service.IssueRefreshTokenAsync(user.Id, cancellationToken);
                RefreshTokenCookie.Append(response, refreshToken, env.IsDevelopment());
                DeviceTrustCookie.Delete(response, env.IsDevelopment());
                return Results.Ok(new { recoveryCodes = result.Value.RecoveryCodes, token = tokens.Issue(user), user });
            })
            .RequireRateLimiting("auth-recovery")
            .WithTags("Auth");

        api.MapPost("/auth/2fa/disable", async (TwoFactorCodeRequest request, ICurrentUser currentUser, AuthService service, TokenIssuer tokens, HttpResponse response, IWebHostEnvironment env, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.Subject, out var userId))
                {
                    return Results.Json(new ErrorResponse("Nieprawidłowa sesja.", "UNAUTHORIZED"), statusCode: 401);
                }

                var result = await service.DisableTwoFactorAsync(userId, request.Code, cancellationToken);
                if (result.IsFailure) return result.ToNoContentResult();

                var user = result.Value!;
                var refreshToken = await service.IssueRefreshTokenAsync(user.Id, cancellationToken);
                RefreshTokenCookie.Append(response, refreshToken, env.IsDevelopment());
                DeviceTrustCookie.Delete(response, env.IsDevelopment());
                return Results.Ok(new { message = "Dwuskładnikowe uwierzytelnianie zostało wyłączone.", token = tokens.Issue(user), user });
            })
            .RequireRateLimiting("auth-recovery")
            .WithTags("Auth");

        api.MapPost("/auth/2fa/recovery-codes/regenerate", async (TwoFactorCodeRequest request, ICurrentUser currentUser, AuthService service, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.Subject, out var userId))
                {
                    return Results.Json(new ErrorResponse("Nieprawidłowa sesja.", "UNAUTHORIZED"), statusCode: 401);
                }

                var result = await service.RegenerateRecoveryCodesAsync(userId, request.Code, cancellationToken);
                return result.ToHttpResult();
            })
            .RequireRateLimiting("auth-recovery")
            .WithTags("Auth");

        api.MapGet("/auth/2fa/recovery-codes/status", async (ICurrentUser currentUser, AuthService service, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.Subject, out var userId))
                {
                    return Results.Json(new ErrorResponse("Nieprawidłowa sesja.", "UNAUTHORIZED"), statusCode: 401);
                }

                var result = await service.GetRecoveryCodesRemainingAsync(userId, cancellationToken);
                return result.ToHttpResult();
            })
            .RequireRateLimiting("auth-recovery")
            .WithTags("Auth");

        return api;
    }
}
