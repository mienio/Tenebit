using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Infrastructure.Data;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

/// <summary>
/// Globalny filtr zapytań chroni odczyt, ten strażnik chroni zapis. Testy nie dotykają bazy - kontekst
/// celowo wskazuje port, na którym nic nie nasłuchuje, bo sprawdzamy wyłącznie decyzję podjętą zanim
/// EF wyśle cokolwiek do PostgreSQL.
/// </summary>
public sealed class TenantWriteGuardTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Insert_of_entity_stamped_with_another_organization_is_blocked()
    {
        await using var db = CreateContext(TenantA);
        db.Assets.Add(new Asset(TenantB, Guid.NewGuid(), "Laptop", "AST-1"));

        await Assert.ThrowsAsync<CrossTenantWriteException>(() => db.SaveChangesAsync());
    }

    /// <summary>
    /// Encje z kluczem alternatywnym (OrganizationId, Id) - aktywa, wydania, kampanie - odrzuca już sam
    /// EF: kolumna jest częścią klucza i nie da się jej zmodyfikować. Reszta encji tenantowych, jak
    /// dziennik aktywności, takiego klucza nie ma i to je pilnuje ten strażnik.
    /// </summary>
    [Fact]
    public async Task Rewriting_a_tracked_row_onto_the_current_tenant_is_blocked()
    {
        await using var db = CreateContext(TenantA);
        var foreignLog = new ActivityLog(TenantB, "asset.updated", "asset", Guid.NewGuid(), "intruder", null, DateTimeOffset.UtcNow);
        db.Attach(foreignLog);
        db.Entry(foreignLog).Property(nameof(ActivityLog.OrganizationId)).CurrentValue = TenantA;

        await Assert.ThrowsAsync<CrossTenantWriteException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Insert_for_the_current_tenant_passes_the_guard()
    {
        await using var db = CreateContext(TenantA);
        db.Assets.Add(new Asset(TenantA, Guid.NewGuid(), "Laptop", "AST-3"));

        var exception = await Record.ExceptionAsync(() => db.SaveChangesAsync());

        Assert.False(exception is CrossTenantWriteException);
    }

    [Fact]
    public async Task Flow_without_a_tenant_is_left_to_the_explicit_repository_filters()
    {
        await using var db = CreateContext(Guid.Empty);
        db.Assets.Add(new Asset(TenantB, Guid.NewGuid(), "Laptop", "AST-4"));

        var exception = await Record.ExceptionAsync(() => db.SaveChangesAsync());

        Assert.False(exception is CrossTenantWriteException);
    }

    private static TenebitDbContext CreateContext(Guid organizationId)
    {
        var options = new DbContextOptionsBuilder<TenebitDbContext>()
            .UseNpgsql("Host=localhost;Port=1;Database=guard_only;Username=guard_only;Password=guard_only;Timeout=1")
            .Options;
        return new TenebitDbContext(options, new FakeFieldEncryptor(), new FakeTenantContext(organizationId));
    }

    private sealed record FakeTenantContext(Guid OrganizationId) : ITenantContext;
}
