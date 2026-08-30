using System.Security.Claims;
using Tenebit.Application.Abstractions;

namespace Tenebit.Api.Auth;

public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    /// <summary>
    /// Guid.Empty wyłącza globalny filtr tenanta w TenebitDbContext, więc nie wolno go zwrócić "przy okazji".
    /// Zwracamy go wyłącznie tam, gdzie brak tenanta jest zamierzony: żądanie nieuwierzytelnione (endpointy
    /// publiczne, webhooki, zadania w tle bez HttpContext) oraz token platform-admina, który z definicji pracuje
    /// ponad organizacjami przez IgnoreQueryFilters w AdminRepository.
    ///
    /// Żądanie uwierzytelnione tokenem tenanta bez czytelnego organization_id to stan niemożliwy - JwtBearer
    /// odrzuca taki token w OnTokenValidated. Gdyby kiedyś powstała ścieżka, która to omija, poprzednia wersja
    /// cicho zwracała Guid.Empty i odsłaniała dane wszystkich firm. Teraz taki request kończy się błędem.
    /// </summary>
    public Guid OrganizationId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return Guid.Empty;
            if (user.HasClaim(PlatformAdminClaims.ScopeClaimType, PlatformAdminClaims.ScopeValue)) return Guid.Empty;

            var value = user.FindFirstValue("organization_id");
            if (Guid.TryParse(value, out var organizationId) && organizationId != Guid.Empty)
            {
                return organizationId;
            }

            throw new InvalidOperationException(
                "Żądanie uwierzytelnione bez czytelnego claimu organization_id - odmowa dostępu do danych zamiast pominięcia filtra tenanta.");
        }
    }
}
