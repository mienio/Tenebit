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
}
