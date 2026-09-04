using System.Reflection;

namespace Tenebit.Tests;

/// <summary>
/// Audyt P2 #12 - testy pilnujące kierunku zależności między warstwami Clean Architecture.
/// Sprawdzają nazwy assembly referencjonowanych przez każdy projekt (nie tylko .csproj
/// ProjectReference, które łatwo dodać przypadkiem) - złamanie dowolnej z tych reguł oznacza,
/// że ktoś dodał referencję łamiącą kierunek zależności Domain -> Application -> Infrastructure -> Api.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(Tenebit.Domain.Assets.Asset).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Tenebit.Application.Assets.AssetService).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Tenebit.Infrastructure.Data.TenebitDbContext).Assembly;

    private static IEnumerable<string> ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(x => x.Name!);

    [Fact]
    public void Domain_does_not_reference_other_layers()
    {
        var referenced = ReferencedAssemblyNames(DomainAssembly).ToList();

        Assert.DoesNotContain("Tenebit.Application", referenced);
        Assert.DoesNotContain("Tenebit.Infrastructure", referenced);
        Assert.DoesNotContain("Tenebit.Api", referenced);
    }

    [Fact]
    public void Domain_does_not_reference_aspnetcore_or_efcore()
    {
        var referenced = ReferencedAssemblyNames(DomainAssembly).ToList();

        Assert.DoesNotContain(referenced, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_does_not_reference_infrastructure_or_api()
    {
        var referenced = ReferencedAssemblyNames(ApplicationAssembly).ToList();

        Assert.DoesNotContain("Tenebit.Infrastructure", referenced);
        Assert.DoesNotContain("Tenebit.Api", referenced);
    }

    [Fact]
    public void Infrastructure_does_not_reference_api()
    {
        var referenced = ReferencedAssemblyNames(InfrastructureAssembly).ToList();

        Assert.DoesNotContain("Tenebit.Api", referenced);
    }

    /// <summary>Repozytoria, które z natury pracują ponad organizacjami: panel platformowy oraz tożsamość,
    /// gdzie wiersz odnajduje się po haszu tokenu albo adresie e-mail, a organizacja wynika dopiero ze
    /// znalezionego wiersza.</summary>
    private static readonly HashSet<string> GloballyScopedRepositories =
    [
        "IAdminRepository",
        "IOrganizationRepository",
        "IOrganizationUserRepository",
        "IRefreshTokenRepository",
        "IPasswordResetTokenRepository",
        "IEmailVerificationTokenRepository",
        "ITwoFactorChallengeRepository",
        "ITwoFactorRecoveryCodeRepository",
        "IDeviceTrustTokenRepository",
        "IExternalLoginRepository",
        "IOAuthTransactionRepository",
        "IProcessedStripeEventRepository",
        "IPromoCodeRepository"
    ];

    /// <summary>Pojedyncze metody bez organizationId w repozytoriach tenantowych - wyszukiwanie po haszu
    /// publicznego tokenu (organizacja wynika ze znalezionego wiersza), czyszczenie retencyjne w tle
    /// oraz uzgadnianie subskrypcji po identyfikatorze klienta Stripe.
    ///
    /// Kod z etykiety QR dołącza do tej listy z tego samego powodu co hasze tokenów: skanujący ma tylko
    /// dziesięć znaków ze skanera, organizacja wynika dopiero ze znalezionego wiersza. Zawężenie robią
    /// wywołujący - AssetService.ResolveScanCodeAsync porównuje organizację znalezionego aktywa
    /// z organizacją sesji, a ścieżki publiczne i tak ujawniają wyłącznie nazwę firmy.</summary>
    private static readonly HashSet<string> GloballyScopedMethods =
    [
        "IActivityLogRepository.DeleteOlderThanAsync",
        "IAssetRepository.FindByScanCodeAsync",
        "IAssetRepository.ScanCodeExistsAsync",
        "IAssetAuditParticipantRepository.FindByTokenHashAsync",
        "IAssignmentRepository.FindByPublicTokenHashAsync",
        "IOffboardingCaseRepository.FindByPublicTokenHashAsync",
        "ISubscriptionRepository.GetByStripeCustomerAsync",
        "ISubscriptionRepository.ListWithStripeSubscriptionAsync",
        "ISubscriptionRepository.ListPendingStripeLinkAsync"
    ];

    /// <summary>
    /// Zawężanie po organizacji jest podstawową ochroną między firmami - filtr EF jest tylko drugą warstwą
    /// i nie działa w przepływach bez tenanta. Nic dotąd nie pilnowało, żeby nowa metoda repozytorium w ogóle
    /// przyjmowała organizationId, więc dopisanie zapytania bez zawężenia przechodziło przez CI.
    /// </summary>
    [Fact]
    public void Repository_methods_are_scoped_by_organization_unless_explicitly_global()
    {
        var offenders = new List<string>();

        var repositories = ApplicationAssembly.GetTypes()
            .Where(type => type.IsInterface && type.Name.EndsWith("Repository", StringComparison.Ordinal));

        foreach (var repository in repositories)
        {
            if (GloballyScopedRepositories.Contains(repository.Name)) continue;

            foreach (var method in repository.GetMethods())
            {
                // Tylko operacje sięgające do bazy. Add/Remove/Update dostają całą encję, która sama niesie
                // OrganizationId, więc dodatkowy parametr byłby tam szumem.
                if (!typeof(Task).IsAssignableFrom(method.ReturnType)) continue;
                if (GloballyScopedMethods.Contains($"{repository.Name}.{method.Name}")) continue;
                if (method.GetParameters().Any(p => p.Name == "organizationId")) continue;

                offenders.Add($"{repository.Name}.{method.Name}");
            }
        }

        Assert.True(offenders.Count == 0,
            "Metoda repozytorium musi przyjmować organizationId albo trafić na jawną listę wyjątków: " +
            string.Join("; ", offenders));
    }
}
