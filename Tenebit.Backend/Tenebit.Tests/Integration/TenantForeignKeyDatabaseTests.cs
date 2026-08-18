using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tenebit.Domain.Assets;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Tests.Integration;

/// <summary>PostgreSQL proof for AUD3-013: migration constraints exist in the real database and reject
/// a cross-tenant reference even when application/repository validation is completely bypassed.</summary>
public sealed class TenantForeignKeyDatabaseTests : IClassFixture<TenebitApiFactory>
{
    private readonly TenebitApiFactory _factory;

    public TenantForeignKeyDatabaseTests(TenebitApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Migration_installs_all_tenant_foreign_key_constraints()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
        await db.Database.OpenConnectionAsync();

        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT conname
            FROM pg_constraint
            WHERE contype = 'f'
              AND connamespace = 'tenebit'::regnamespace
              AND conname LIKE 'FK_tenant_%';
            """;

        var actual = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) actual.Add(reader.GetString(0));

        var expected = new[]
        {
            "FK_tenant_assets_category",
            "FK_tenant_inspections_asset",
            "FK_tenant_inspections_assignment",
            "FK_tenant_inspections_offboarding_item",
            "FK_tenant_service_tickets_asset",
            "FK_tenant_locations_parent",
            "FK_tenant_procedure_documents_procedure",
            "FK_tenant_jobprofile_categories_owner",
            "FK_tenant_jobprofile_categories_category",
            "FK_tenant_jobprofile_procedures_owner",
            "FK_tenant_jobprofile_procedures_procedure",
            "FK_tenant_license_seats_owner",
            "FK_tenant_license_seats_person",
            "FK_tenant_assignments_person",
            "FK_tenant_assignment_assets_owner",
            "FK_tenant_assignment_assets_asset",
            "FK_tenant_procedure_acceptances_owner",
            "FK_tenant_procedure_acceptances_procedure",
            "FK_tenant_procedure_acceptances_person",
            "FK_tenant_audit_participants_person",
            "FK_tenant_audit_items_asset",
            "FK_tenant_audit_items_expected_person",
            "FK_tenant_reservations_requester",
            "FK_tenant_reservations_assignment",
            "FK_tenant_reservation_items_category",
            "FK_tenant_reservation_items_asset",
            "FK_tenant_reservation_items_original_asset",
            "FK_tenant_reservation_items_kit",
            "FK_tenant_kit_items_owner",
            "FK_tenant_kit_items_category",
            "FK_tenant_offboarding_cases_person",
            "FK_tenant_offboarding_items_asset",
            "FK_tenant_offboarding_items_assignment",
            "FK_tenant_offboarding_items_license",
            "FK_tenant_evidence_offboarding_item",
            "FK_tenant_evidence_audit_item",
            "FK_tenant_dashboard_layout_user"
        };

        foreach (var name in expected) Assert.Contains(name, actual);
    }

    [Fact]
    public async Task Database_rejects_asset_pointing_to_category_from_another_tenant()
    {
        var (organizationA, _, _) = await _factory.SeedTenantAsync("FkA", "owner");
        var (organizationB, _, _) = await _factory.SeedTenantAsync("FkB", "owner");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
        var categoryB = new AssetCategory(organizationB.Id, $"Category {Guid.NewGuid():N}", AssetCategoryType.Physical, null);
        db.AssetCategories.Add(categoryB);
        await db.SaveChangesAsync();

        // Deliberately bypass Application services: the DB itself must reject this relation.
        var invalid = new Asset(organizationA.Id, categoryB.Id, "Cross tenant asset", $"X-{Guid.NewGuid():N}"[..16]);
        db.Assets.Add(invalid);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
