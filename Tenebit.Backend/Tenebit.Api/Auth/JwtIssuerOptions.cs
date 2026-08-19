namespace Tenebit.Api.Auth;

/// <summary>Wspólne źródło issuer/audience dla wystawiania i walidacji JWT (audyt P1.1) - bez tego serwer
/// akceptował token podpisany właściwym kluczem niezależnie od tego, dla jakiego API/odbiorcy został wydany.</summary>
public static class JwtIssuerOptions
{
    public static string GetIssuer(IConfiguration configuration) => configuration["Auth:Issuer"] ?? "tenebit-api";

    public static string GetAudience(IConfiguration configuration) => configuration["Auth:Audience"] ?? "tenebit-clients";

    // Krótki TTL ogranicza okno, w którym odwołana rola/deaktywacja usera pozostaje ważna w już wydanym
    // tokenie - RefreshAsync (AuthService) i tak przy każdym odświeżeniu wczytuje aktualny stan z DB
    // (audyt P1-AUTH-002), więc skrócenie TTL bezpośrednio skraca czas propagacji zmiany.
    public static int GetAccessTokenMinutes(IConfiguration configuration) =>
        int.TryParse(configuration["Auth:AccessTokenMinutes"], out var minutes) && minutes > 0 ? minutes : 10;
}
