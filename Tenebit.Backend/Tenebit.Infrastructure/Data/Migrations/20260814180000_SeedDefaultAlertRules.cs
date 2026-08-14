using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Data.Migrations;

/// <summary>
/// Backfill domyślnych reguł alertów dla wszystkich istniejących organizacji. Trzy typy, które przed
/// wprowadzeniem konfigurowalnych alertów działały bezwarunkowo (gwarancja, termin zwrotu wydania, brak
/// potwierdzenia wydania), są włączone — żeby wdrożenie nie uciszyło istniejących powiadomień. Nowe kategorie
/// startują wyłączone. Nowe organizacje otrzymują te same reguły przez StarterAlertRules (hook w AuthService).
/// </summary>
[DbContext(typeof(TenebitDbContext))]
[Migration("20260814180000_SeedDefaultAlertRules")]
public sealed class SeedDefaultAlertRules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO tenebit.alert_rules ("Id", "OrganizationId", "Type", "IsEnabled", "ThresholdDays", "DeliveryMode", "RecipientMode", "CustomEmails", "CooldownDays", "CreatedAt", "UpdatedAt", "UpdatedBy")
            SELECT gen_random_uuid(), o."Id", d."Type", d."IsEnabled", d."ThresholdDays", 'Immediate', 'OwnersAndAdmins', NULL, 1, now(), now(), 'system'
            FROM tenebit.organizations o
            CROSS JOIN (VALUES
                ('AssetWarrantyExpiring', true, ARRAY[30,7]::integer[]),
                ('AssignmentReturnDue', true, ARRAY[0]::integer[]),
                ('AssignmentNotConfirmed', true, ARRAY[0]::integer[]),
                ('LicenseExpiring', false, ARRAY[30,7]::integer[]),
                ('ProcedureReviewDue', false, ARRAY[30,7]::integer[]),
                ('OffboardingReturnDue', false, ARRAY[7]::integer[]),
                ('AssetAuditNoResponse', false, ARRAY[7]::integer[]),
                ('ReservationAwaitingApproval', false, ARRAY[1]::integer[]),
                ('ReservationPickupUpcoming', false, ARRAY[1]::integer[]),
                ('ReservationOverdue', false, ARRAY[0]::integer[])
            ) AS d("Type", "IsEnabled", "ThresholdDays")
            WHERE NOT EXISTS (
                SELECT 1 FROM tenebit.alert_rules r
                WHERE r."OrganizationId" = o."Id" AND r."Type" = d."Type"
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM tenebit.alert_rules
            WHERE "UpdatedBy" = 'system'
              AND "DeliveryMode" = 'Immediate'
              AND "RecipientMode" = 'OwnersAndAdmins'
              AND "Type" IN ('AssetWarrantyExpiring','AssignmentReturnDue','AssignmentNotConfirmed','LicenseExpiring','ProcedureReviewDue','OffboardingReturnDue','AssetAuditNoResponse','ReservationAwaitingApproval','ReservationPickupUpcoming','ReservationOverdue');
            """);
    }
}
