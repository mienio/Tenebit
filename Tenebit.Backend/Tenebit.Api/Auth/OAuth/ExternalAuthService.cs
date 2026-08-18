using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
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

    public string BuildAuthorizationUrl(string provider, string state, string codeChallenge, string nonce)
    {
        var redirectUri = RedirectUriFor(provider);
        return provider switch
        {
            OAuthProviders.Google => $"https://accounts.google.com/o/oauth2/v2/auth?client_id={Uri.EscapeDataString(_options.Google.ClientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={Uri.EscapeDataString("openid email profile")}&state={state}&nonce={nonce}&code_challenge={codeChallenge}&code_challenge_method=S256&prompt=select_account",
            OAuthProviders.Microsoft => $"https://login.microsoftonline.com/{_options.Microsoft.TenantId}/oauth2/v2.0/authorize?client_id={Uri.EscapeDataString(_options.Microsoft.ClientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={Uri.EscapeDataString("openid email profile")}&state={state}&nonce={nonce}&code_challenge={codeChallenge}&code_challenge_method=S256",
            OAuthProviders.Facebook => $"https://www.facebook.com/v19.0/dialog/oauth?client_id={Uri.EscapeDataString(_options.Facebook.ClientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={Uri.EscapeDataString("email public_profile")}&state={state}&code_challenge={codeChallenge}&code_challenge_method=S256",
            OAuthProviders.Apple => $"https://appleid.apple.com/auth/authorize?client_id={Uri.EscapeDataString(_options.Apple.ClientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={Uri.EscapeDataString("name email")}&response_mode=form_post&state={state}&nonce={nonce}&code_challenge={codeChallenge}&code_challenge_method=S256",
            _ => throw new InvalidOperationException($"Nieznany dostawca logowania: {provider}")
        };
    }

    public async Task<ExternalUserInfo?> ExchangeAndFetchProfileAsync(string provider, string code, string codeVerifier, string expectedNonce, CancellationToken cancellationToken)
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
        return await ValidateAndParseIdTokenAsync(provider, idTokenElement.GetString()!, expectedNonce, cancellationToken);
    }

    private static async Task<ExternalUserInfo?> FetchFacebookProfileAsync(HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.facebook.com/me?fields=id,name,email");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        var id = root.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(id)) return null;

        var email = root.TryGetProperty("email", out var emailElement) ? emailElement.GetString() : null;
        var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;

        // The basic profile response does not carry a trustworthy e-mail verification signal. It may
        // identify a previously linked provider account, but it must not auto-link/create by e-mail.
        return new ExternalUserInfo(OAuthProviders.Facebook, id, email, false, name);
    }

    // AUD-006: id_token providerów OIDC musi być kryptograficznie zweryfikowany (podpis przez JWKS, issuer,
    // audience, lifetime) — samo ReadJwtToken tylko dekoduje payload bez weryfikacji, więc dowolny nadawca
    // mógłby podrobić email/sub. ConfigurationManager cache'uje JWKS/metadata providera (odświeża wg jego TTL).
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> OidcConfigCache = new();

    private static ConfigurationManager<OpenIdConnectConfiguration> GetConfigManager(string metadataAddress) =>
        OidcConfigCache.GetOrAdd(metadataAddress, address =>
            new ConfigurationManager<OpenIdConnectConfiguration>(address, new OpenIdConnectConfigurationRetriever()));

    private (string MetadataAddress, string Audience, IssuerValidator? IssuerValidator) OidcSettingsFor(string provider) => provider switch
    {
        OAuthProviders.Google => ("https://accounts.google.com/.well-known/openid-configuration", _options.Google.ClientId, null),
        OAuthProviders.Microsoft => MicrosoftOidcSettings(),
        OAuthProviders.Apple => ("https://appleid.apple.com/.well-known/openid-configuration", _options.Apple.ClientId, null),
        _ => throw new InvalidOperationException($"Dostawca {provider} nie obsługuje walidacji id_token OIDC.")
    };

    private (string MetadataAddress, string Audience, IssuerValidator? IssuerValidator) MicrosoftOidcSettings()
    {
        var tenant = _options.Microsoft.TenantId.Trim();
        var metadata = $"https://login.microsoftonline.com/{tenant}/v2.0/.well-known/openid-configuration";

        // Only explicitly multi-tenant authorities accept the issuer template. For a concrete tenant
        // GUID or verified domain, normal validation against metadata's exact issuer is used. This
        // prevents a single-tenant configuration from accepting a valid token from another tenant.
        var isMultiTenant = string.Equals(tenant, "common", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(tenant, "organizations", StringComparison.OrdinalIgnoreCase);
        return (metadata, _options.Microsoft.ClientId, isMultiTenant ? ValidateMicrosoftMultiTenantIssuer : null);
    }

    private static string ValidateMicrosoftMultiTenantIssuer(string issuer, SecurityToken token, TokenValidationParameters parameters)
    {
        if (!Regex.IsMatch(issuer, @"^https://login\.microsoftonline\.com/[0-9a-fA-F-]{36}/v2\.0$"))
        {
            throw new SecurityTokenInvalidIssuerException("Nieprawidłowy issuer tokenu Microsoft.") { InvalidIssuer = issuer };
        }

        return issuer;
    }

    private async Task<ExternalUserInfo?> ValidateAndParseIdTokenAsync(string provider, string idToken, string expectedNonce, CancellationToken cancellationToken)
    {
        var (metadataAddress, audience, issuerValidator) = OidcSettingsFor(provider);

        OpenIdConnectConfiguration config;
        try
        {
            config = await GetConfigManager(metadataAddress).GetConfigurationAsync(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ValidateIssuer = issuerValidator is null,
            ValidIssuer = issuerValidator is null ? config.Issuer : null,
            IssuerValidator = issuerValidator,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true
        };

        ClaimsPrincipal principal;
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            principal = handler.ValidateToken(idToken, parameters, out _);
        }
        catch (Exception)
        {
            return null;
        }

        var sub = principal.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(sub)) return null;

        // Ties this id_token to the specific authorization request we started — without it, an id_token
        // obtained through another flow (or replayed) would still pass signature/issuer/audience checks.
        var nonce = principal.FindFirst("nonce")?.Value;
        if (!string.Equals(nonce, expectedNonce, StringComparison.Ordinal)) return null;

        var email = principal.FindFirst("email")?.Value ?? principal.FindFirst("preferred_username")?.Value;
        var emailVerifiedClaim = principal.FindFirst("email_verified")?.Value;
        var emailVerified = string.Equals(emailVerifiedClaim, "true", StringComparison.OrdinalIgnoreCase);
        var name = principal.FindFirst("name")?.Value;

        return new ExternalUserInfo(provider, sub, email, emailVerified, name);
    }

    private string RedirectUriFor(string provider)
    {
        var publicUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{publicUrl}/api/auth/external/{provider}/callback";
    }
}
