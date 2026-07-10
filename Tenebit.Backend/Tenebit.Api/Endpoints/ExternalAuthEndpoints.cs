using Tenebit.Api.Auth;
using Tenebit.Api.Auth.OAuth;
using Tenebit.Api.Http;
using Tenebit.Application.Identity;

namespace Tenebit.Api.Endpoints;

public static class ExternalAuthEndpoints
{
    public static void MapExternalAuthEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/auth/external/providers", (ExternalAuthService service) =>
                Results.Ok(new { providers = service.EnabledProviders() }))
            .AllowAnonymous()
            .WithTags("Auth")
            .WithOpenApi();

        api.MapGet("/auth/external/{provider}/start", (string provider, string? returnUrl, ExternalAuthService service, OAuthStateStore stateStore) =>
                StartExternalLogin(provider, returnUrl, service, stateStore))
            .AllowAnonymous()
            .WithTags("Auth")
            .WithOpenApi();

        api.MapGet("/auth/external/{provider}/callback", (string provider, string? code, string? state, string? error, ExternalAuthService service, OAuthStateStore stateStore, AuthService authService, TokenIssuer tokens, IConfiguration configuration, CancellationToken cancellationToken) =>
                HandleCallbackAsync(provider, code, state, error, service, stateStore, authService, tokens, configuration, cancellationToken))
            .AllowAnonymous()
            .WithTags("Auth")
            .WithOpenApi();

        api.MapPost("/auth/external/{provider}/callback", async (HttpRequest request, string provider, ExternalAuthService service, OAuthStateStore stateStore, AuthService authService, TokenIssuer tokens, IConfiguration configuration, CancellationToken cancellationToken) =>
            {
                var form = await request.ReadFormAsync(cancellationToken);
                var code = form["code"].FirstOrDefault();
                var state = form["state"].FirstOrDefault();
                var error = form["error"].FirstOrDefault();
                return await HandleCallbackAsync(provider, code, state, error, service, stateStore, authService, tokens, configuration, cancellationToken);
            })
            .AllowAnonymous()
            .WithTags("Auth")
            .WithOpenApi();
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

    private static async Task<IResult> HandleCallbackAsync(string provider, string? code, string? state, string? error, ExternalAuthService service, OAuthStateStore stateStore, AuthService authService, TokenIssuer tokens, IConfiguration configuration, CancellationToken cancellationToken)
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
            return Results.Redirect($"{publicUrl}/auth/callback#error={Uri.EscapeDataString(result.Error!.Code)}");
        }

        var token = tokens.Issue(result.Value!);
        return Results.Redirect($"{publicUrl}/auth/callback#token={Uri.EscapeDataString(token)}&returnUrl={Uri.EscapeDataString(entry.ReturnPath)}");
    }

    private static bool IsSafeReturnPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && path.StartsWith('/') && !path.StartsWith("//", StringComparison.Ordinal);
}
