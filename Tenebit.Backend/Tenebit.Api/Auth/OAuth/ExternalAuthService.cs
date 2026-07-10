using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Tenebit.Application.Identity;

namespace Tenebit.Api.Auth.OAuth;

public static class OAuthProviders
{
    public const string Google = "google";
    public const string Microsoft = "microsoft";
    public const string Facebook = "facebook";
    public const string Apple = "apple";

    public static readonly string[] All = [Google, Microsoft, Facebook, Apple];
}

public sealed class ExternalAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OAuthOptions _options;
    private readonly IConfiguration _configuration;

    public ExternalAuthService(IHttpClientFactory httpClientFactory, IOptions<OAuthOptions> options, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _configuration = configuration;
    }

    public bool IsKnownProvider(string provider) => OAuthProviders.All.Contains(provider);

    public bool IsEnabled(string provider) => provider switch
    {
        OAuthProviders.Google => _options.Google.IsEnabled,
        OAuthProviders.Microsoft => _options.Microsoft.IsEnabled,
        OAuthProviders.Facebook => _options.Facebook.IsEnabled,
        OAuthProviders.Apple => _options.Apple.IsEnabled,
        _ => false
    };

    public IReadOnlyList<string> EnabledProviders() => OAuthProviders.All.Where(IsEnabled).ToArray();

    public string BuildAuthorizationUrl(string provider, string state, string codeChallenge)
    {
        var redirectUri = RedirectUriFor(provider);
        return provider switch
        {
            OAuthProviders.Google => $"https://accounts.google.com/o/oauth2/v2/auth?client_id={Uri.EscapeDataString(_options.Google.ClientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={Uri.EscapeDataString("openid email profile")}&state={state}&code_challenge={codeChallenge}&code_challenge_method=S256&prompt=select_account",
            OAuthProviders.Microsoft => $"https://login.microsoftonline.com/{_options.Microsoft.TenantId}/oauth2/v2.0/authorize?client_id={Uri.EscapeDataString(_options.Microsoft.ClientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={Uri.EscapeDataString("openid email profile")}&state={state}&code_challenge={codeChallenge}&code_challenge_method=S256",
            OAuthProviders.Facebook => $"https://www.facebook.com/v19.0/dialog/oauth?client_id={Uri.EscapeDataString(_options.Facebook.ClientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={Uri.EscapeDataString("email public_profile")}&state={state}&code_challenge={codeChallenge}&code_challenge_method=S256",
            OAuthProviders.Apple => $"https://appleid.apple.com/auth/authorize?client_id={Uri.EscapeDataString(_options.Apple.ClientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={Uri.EscapeDataString("name email")}&response_mode=form_post&state={state}&code_challenge={codeChallenge}&code_challenge_method=S256",
            _ => throw new InvalidOperationException($"Nieznany dostawca logowania: {provider}")
        };
    }

    public async Task<ExternalUserInfo?> ExchangeAndFetchProfileAsync(string provider, string code, string codeVerifier, CancellationToken cancellationToken)
    {
        var redirectUri = RedirectUriFor(provider);
        var client = _httpClientFactory.CreateClient(nameof(ExternalAuthService));

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        };

        string tokenEndpoint;
        switch (provider)
        {
            case OAuthProviders.Google:
                tokenEndpoint = "https://oauth2.googleapis.com/token";
                form["client_id"] = _options.Google.ClientId;
                form["client_secret"] = _options.Google.ClientSecret;
                break;
            case OAuthProviders.Microsoft:
                tokenEndpoint = $"https://login.microsoftonline.com/{_options.Microsoft.TenantId}/oauth2/v2.0/token";
                form["client_id"] = _options.Microsoft.ClientId;
                form["client_secret"] = _options.Microsoft.ClientSecret;
                break;
            case OAuthProviders.Facebook:
                tokenEndpoint = "https://graph.facebook.com/v19.0/oauth/access_token";
                form["client_id"] = _options.Facebook.ClientId;
                form["client_secret"] = _options.Facebook.ClientSecret;
                break;
            case OAuthProviders.Apple:
                tokenEndpoint = "https://appleid.apple.com/auth/token";
                form["client_id"] = _options.Apple.ClientId;
                form["client_secret"] = AppleClientSecretBuilder.Build(_options.Apple);
                break;
            default:
                throw new InvalidOperationException($"Nieznany dostawca logowania: {provider}");
        }

        using var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(form), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (provider == OAuthProviders.Facebook)
        {
            if (!root.TryGetProperty("access_token", out var accessTokenElement)) return null;
            return await FetchFacebookProfileAsync(client, accessTokenElement.GetString()!, cancellationToken);
        }

        if (!root.TryGetProperty("id_token", out var idTokenElement)) return null;
        return ParseIdToken(provider, idTokenElement.GetString()!);
    }

    private static async Task<ExternalUserInfo?> FetchFacebookProfileAsync(HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        var url = $"https://graph.facebook.com/me?fields=id,name,email&access_token={Uri.EscapeDataString(accessToken)}";
        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        var id = root.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(id)) return null;

        var email = root.TryGetProperty("email", out var emailElement) ? emailElement.GetString() : null;
        var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;

        return new ExternalUserInfo(OAuthProviders.Facebook, id, email, email is not null, name);
    }

    private static ExternalUserInfo? ParseIdToken(string provider, string idToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(idToken);
        var sub = token.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        if (string.IsNullOrWhiteSpace(sub)) return null;

        var email = token.Claims.FirstOrDefault(c => c.Type == "email")?.Value
            ?? token.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value;
        var emailVerifiedClaim = token.Claims.FirstOrDefault(c => c.Type == "email_verified")?.Value;
        var emailVerified = provider == OAuthProviders.Apple || string.Equals(emailVerifiedClaim, "true", StringComparison.OrdinalIgnoreCase);
        var name = token.Claims.FirstOrDefault(c => c.Type == "name")?.Value;

        return new ExternalUserInfo(provider, sub, email, emailVerified, name);
    }

    private string RedirectUriFor(string provider)
    {
        var publicUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{publicUrl}/api/auth/external/{provider}/callback";
    }
}
