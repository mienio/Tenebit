using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Tenebit.Api.Auth;
using Tenebit.Api.Auth.OAuth;
using Tenebit.Api.Http;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Identity;

namespace Tenebit.Api.Endpoints;

public static class ExternalAuthEndpoints
{
    public static void MapExternalAuthEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/auth/external/providers", (ExternalAuthService service) =>
                Results.Ok(new { providers = service.EnabledProviders() }))
            .AllowAnonymous()
            .WithTags("Auth");

        api.MapGet("/auth/external/links", async (ICurrentUser currentUser, AuthService authService, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.Subject, out var userId))
                {
                    return Results.Json(new ErrorResponse("Nieprawidłowa sesja.", "UNAUTHORIZED"), statusCode: 401);
                }

                var result = await authService.ListLinkedProvidersAsync(userId, cancellationToken);
                return Results.Ok(new { providers = result.Value });
            })
            .WithTags("Auth");

        api.MapPost("/auth/external/{provider}/unlink", async (string provider, ICurrentUser currentUser, AuthService authService, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.Subject, out var userId))
                {
                    return Results.Json(new ErrorResponse("Nieprawidłowa sesja.", "UNAUTHORIZED"), statusCode: 401);
                }

                var result = await authService.UnlinkProviderAsync(userId, provider, cancellationToken);
                return result.IsFailure ? result.ToNoContentResult() : Results.Ok(new { message = "Konto zostało odłączone." });
            })
            .WithTags("Auth");

        api.MapGet("/auth/external/{provider}/start", (string provider, string? returnUrl, ExternalAuthService service, OAuthStateStore stateStore) =>
                StartExternalLogin(provider, returnUrl, service, stateStore))
            .AllowAnonymous()
            .WithTags("Auth");

        api.MapGet("/auth/external/{provider}/callback", (string provider, string? code, string? state, string? error, ExternalAuthService service, OAuthStateStore stateStore, AuthService authService, TokenIssuer tokens, HttpResponse response, IWebHostEnvironment env, IConfiguration configuration, CancellationToken cancellationToken) =>
                HandleCallbackAsync(provider, code, state, error, service, stateStore, authService, tokens, response, env, configuration, cancellationToken))
            .AllowAnonymous()
            .WithTags("Auth");

        api.MapPost("/auth/external/{provider}/callback", async (HttpRequest request, HttpResponse response, string provider, ExternalAuthService service, OAuthStateStore stateStore, AuthService authService, TokenIssuer tokens, IWebHostEnvironment env, IConfiguration configuration, CancellationToken cancellationToken) =>
            {
                var form = await request.ReadFormAsync(cancellationToken);
                var code = form["code"].FirstOrDefault();
                var state = form["state"].FirstOrDefault();
                var error = form["error"].FirstOrDefault();
                return await HandleCallbackAsync(provider, code, state, error, service, stateStore, authService, tokens, response, env, configuration, cancellationToken);
            })
            .AllowAnonymous()
            .WithTags("Auth");
    }

    private static IResult StartExternalLogin(string provider, string? returnUrl, ExternalAuthService service, OAuthStateStore stateStore)
    {
        if (!service.IsKnownProvider(provider) || !service.IsEnabled(provider))
        {
            return Results.NotFound(new ErrorResponse("Nieznany lub niekonfigurowany dostawca logowania.", "PROVIDER_NOT_AVAILABLE"));
        }

        var safeReturnPath = IsSafeReturnPath(returnUrl) ? returnUrl! : "/dashboard";
        var verifier = PkceHelper.NewCodeVerifier();
        var challenge = PkceHelper.ChallengeFor(verifier);
        var state = stateStore.Create(provider, verifier, safeReturnPath);

        return Results.Redirect(service.BuildAuthorizationUrl(provider, state, challenge));
    }

    private static async Task<IResult> HandleCallbackAsync(string provider, string? code, string? state, string? error, ExternalAuthService service, OAuthStateStore stateStore, AuthService authService, TokenIssuer tokens, HttpResponse response, IWebHostEnvironment env, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var publicUrl = (configuration["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');

        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return Results.Redirect($"{publicUrl}/auth/callback#error=oauth_failed");
        }

        var entry = stateStore.Consume(state);
        if (entry is null || entry.Provider != provider)
        {
            return Results.Redirect($"{publicUrl}/auth/callback#error=oauth_expired");
        }

        var profile = await service.ExchangeAndFetchProfileAsync(provider, code, entry.CodeVerifier, cancellationToken);
        if (profile is null)
        {
            return Results.Redirect($"{publicUrl}/auth/callback#error=oauth_failed");
        }

        var result = await authService.ExternalLoginAsync(profile, cancellationToken);
        if (result.IsFailure)
        {
            return Results.Redirect($"{publicUrl}/auth/callback#error=oauth_rejected&message={Uri.EscapeDataString(result.Error!.Message)}");
        }

        var refreshToken = await authService.IssueRefreshTokenAsync(result.Value!.Id, cancellationToken);
        RefreshTokenCookie.Append(response, refreshToken, env.IsDevelopment());

        var token = tokens.Issue(result.Value!);
        return Results.Redirect($"{publicUrl}/auth/callback#token={Uri.EscapeDataString(token)}&returnUrl={Uri.EscapeDataString(entry.ReturnPath)}");
    }

    private static bool IsSafeReturnPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && path.StartsWith('/') && !path.StartsWith("//", StringComparison.Ordinal);
}
