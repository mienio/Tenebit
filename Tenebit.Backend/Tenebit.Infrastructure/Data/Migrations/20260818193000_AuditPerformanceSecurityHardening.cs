using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

[DbContext(typeof(TenebitDbContext))]
[Migration("20260818193000_AuditPerformanceSecurityHardening")]
public partial class AuditPerformanceSecurityHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "LastUsedTotpCounter",
            schema: "tenebit",
            table: "organization_users",
            type: "bigint",
            nullable: true);

        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_assets_Name_trgm\" ON tenebit.assets USING gin (lower(\"Name\") gin_trgm_ops);",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_assets_AssetTag_trgm\" ON tenebit.assets USING gin (lower(\"AssetTag\") gin_trgm_ops);",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_assets_SerialNumber_trgm\" ON tenebit.assets USING gin (lower(\"SerialNumber\") gin_trgm_ops) WHERE \"SerialNumber\" IS NOT NULL;",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_procedures_Title_trgm\" ON tenebit.procedures USING gin (lower(\"Title\") gin_trgm_ops);",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_procedures_Owner_trgm\" ON tenebit.procedures USING gin (lower(\"Owner\") gin_trgm_ops);",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_procedures_Version_trgm\" ON tenebit.procedures USING gin (lower(\"Version\") gin_trgm_ops);",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_people_FirstName_trgm\" ON tenebit.people USING gin (lower(\"FirstName\") gin_trgm_ops);",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_people_LastName_trgm\" ON tenebit.people USING gin (lower(\"LastName\") gin_trgm_ops);",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_people_Email_trgm\" ON tenebit.people USING gin (lower(\"Email\") gin_trgm_ops);",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_people_FullName_trgm\" ON tenebit.people USING gin (lower(\"FirstName\" || ' ' || \"LastName\") gin_trgm_ops);",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_assignments_ProtocolNumber_trgm\" ON tenebit.assignments USING gin (lower(\"ProtocolNumber\") gin_trgm_ops);",
            suppressTransaction: true);

        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_activity_logs_Action_trgm\" ON tenebit.activity_logs USING gin (\"Action\" gin_trgm_ops);",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_activity_logs_Details_trgm\" ON tenebit.activity_logs USING gin (\"Details\" gin_trgm_ops) WHERE \"Details\" IS NOT NULL;",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_activity_logs_recent_entity_actor_action\" ON tenebit.activity_logs (\"OrganizationId\", \"EntityType\", \"EntityId\", \"ActorSubject\", \"Action\", \"CreatedAt\" DESC);",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_asset_evidence_retention_due\" ON tenebit.asset_evidence (\"OrganizationId\", \"UploadedAt\") WHERE \"LegalHold\" = FALSE AND \"RedactedAt\" IS NULL;",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_assets_warranty_due\" ON tenebit.assets (\"OrganizationId\", \"WarrantyUntil\") WHERE \"WarrantyUntil\" IS NOT NULL;",
            suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS tenebit.\"IX_assets_Name_trgm\";", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS tenebit.\"IX_assets_AssetTag_trgm\";", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS tenebit.\"IX_assets_SerialNumber_trgm\";", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS tenebit.\"IX_procedures_Title_trgm\";", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS tenebit.\"IX_procedures_Owner_trgm\";", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS tenebit.\"IX_procedures_Version_trgm\";", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS tenebit.\"IX_people_FirstName_trgm\";", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS tenebit.\"IX_people_LastName_trgm\";", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS tenebit.\"IX_people_Email_trgm\";", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS tenebit.\"IX_people_FullName_trgm\";", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS tenebit.\"IX_assignments_ProtocolNumber_trgm\";", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS tenebit.\"IX_activity_logs_Action_trgm\";", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS tenebit.\"IX_activity_logs_Details_trgm\";", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS tenebit.\"IX_activity_logs_recent_entity_actor_action\";", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS tenebit.\"IX_asset_evidence_retention_due\";", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS tenebit.\"IX_assets_warranty_due\";", suppressTransaction: true);

        migrationBuilder.DropColumn(
            name: "LastUsedTotpCounter",
            schema: "tenebit",
            table: "organization_users");
    }
}
