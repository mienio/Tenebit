using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Tenebit.Api.Auth;
using Tenebit.Application.Identity;
using Tenebit.Domain.Identity;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.People;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Tests.Integration;

/// <summary>Uruchamia prawdziwy Tenebit.Api (WebApplicationFactory) na realnym PostgreSQL zamiast
/// InMemoryRepositories — audyt AUD-040: fake repository nie wykryje złej konfiguracji auth/authz middleware,
/// brakującego query filtra ani wadliwego FK. Baza docelowa: zmienna środowiskowa TENEBIT_TEST_DB_CONNECTION,
/// domyślnie lokalny dev Postgres (ten sam serwer co appsettings.json, osobna baza "tenebit_test").</summary>
public sealed class TenebitApiFactory : WebApplicationFactory<Program>
{
    public static string ConnectionString { get; } = Environment.GetEnvironmentVariable("TENEBIT_TEST_DB_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=tenebit_test;Username=postgres;Password=postgres";

    // Program.cs czyta ConnectionStrings:TenebitDb i rejestruje UseNpgsql(...) SYNCHRONICZNIE w trakcie
    // wykonywania top-level statements (przed app.Build()) — WebApplicationFactory.ConfigureAppConfiguration
    // dokłada źródło konfiguracji za późno, żeby to przechwycić. Zmienne środowiskowe są czytane od razu
    // przez WebApplication.CreateBuilder(args), więc trzeba je ustawić zanim host zostanie w ogóle zbudowany
    // (stąd konstruktor statyczny — działa raz, przed pierwszym użyciem fabryki w tym procesie testowym).
    static TenebitApiFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__TenebitDb", ConnectionString);
        Environment.SetEnvironmentVariable("Database__AutoCreate", "true");
        Environment.SetEnvironmentVariable("Seed__Enabled", "false");
        Environment.SetEnvironmentVariable("Auth__SigningKey", "integration-test-signing-key-not-for-prod-32c");
        Environment.SetEnvironmentVariable("Alerts__Enabled", "false");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    /// <summary>Zakłada organizację + użytkownika bezpośrednio przez DbContext (bez przechodzenia przez
    /// rejestrację HTTP) i wystawia dla niego prawdziwy JWT tym samym TokenIssuerem co produkcyjny login —
    /// test więc realnie sprawdza middleware auth/authz, nie tylko warstwę Application.</summary>
    public async Task<(Organization Organization, OrganizationUser User, string Token)> SeedTenantAsync(string namePrefix, params string[] roles)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();

        var organization = new Organization($"{namePrefix} {Guid.NewGuid():N}", "PL", "pl", "PLN", "Europe/Warsaw");
        var user = new OrganizationUser(organization.Id, $"{Guid.NewGuid():N}@example.test", $"{namePrefix} owner", true);
        user.Update(user.Email, user.DisplayName, true, roles.Length > 0 ? roles : ["owner"]);
        user.MarkEmailVerified();

        db.Organizations.Add(organization);
        db.OrganizationUsers.Add(user);
        await db.SaveChangesAsync();

        var tokenIssuer = scope.ServiceProvider.GetRequiredService<TokenIssuer>();
        var authUser = new AuthUserResponse(user.Id, organization.Id, organization.Name, user.Email, user.DisplayName, roles.Length > 0 ? roles : ["owner"], true, false, user.SecurityStamp);
        var token = tokenIssuer.Issue(authUser);

        return (organization, user, token);
    }

    public async Task<(Organization Organization, OrganizationUser User, Person Person, string Token)> SeedTenantWithPersonAsync(string namePrefix, params string[] roles)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();

        var organization = new Organization($"{namePrefix} {Guid.NewGuid():N}", "PL", "pl", "PLN", "Europe/Warsaw");
        var email = $"{Guid.NewGuid():N}@example.test";
        var person = new Person(organization.Id, namePrefix, "User", email);
        var user = new OrganizationUser(organization.Id, email, $"{namePrefix} user", true);
        user.Update(user.Email, user.DisplayName, true, roles.Length > 0 ? roles : ["employee"]);
        user.LinkPerson(person.Id);
        user.MarkEmailVerified();

        db.Organizations.Add(organization);
        db.People.Add(person);
        db.OrganizationUsers.Add(user);
        await db.SaveChangesAsync();

        var tokenIssuer = scope.ServiceProvider.GetRequiredService<TokenIssuer>();
        var authUser = new AuthUserResponse(user.Id, organization.Id, organization.Name, user.Email, user.DisplayName, roles.Length > 0 ? roles : ["employee"], true, false, user.SecurityStamp, person.Id);
        return (organization, user, person, tokenIssuer.Issue(authUser));
    }

    public HttpClient CreateAuthenticatedClient(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
