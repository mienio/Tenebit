using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

[DbContext(typeof(TenebitDbContext))]
[Migration("20260818004500_SecurityStateAndNormalizedLocations")]
public partial class SecurityStateAndNormalizedLocations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(name: "FamilyId", schema: "tenebit", table: "refresh_tokens", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "ParentTokenId", schema: "tenebit", table: "refresh_tokens", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "ReplacedByTokenId", schema: "tenebit", table: "refresh_tokens", type: "uuid", nullable: true);
        migrationBuilder.Sql("UPDATE tenebit.refresh_tokens SET \"FamilyId\" = \"Id\" WHERE \"FamilyId\" IS NULL;");
        migrationBuilder.AlterColumn<Guid>(name: "FamilyId", schema: "tenebit", table: "refresh_tokens", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
        migrationBuilder.CreateIndex(name: "IX_refresh_tokens_FamilyId", schema: "tenebit", table: "refresh_tokens", column: "FamilyId");
        migrationBuilder.CreateIndex(name: "IX_refresh_tokens_ParentTokenId", schema: "tenebit", table: "refresh_tokens", column: "ParentTokenId");
        migrationBuilder.CreateIndex(name: "IX_refresh_tokens_ReplacedByTokenId", schema: "tenebit", table: "refresh_tokens", column: "ReplacedByTokenId");
        migrationBuilder.AddForeignKey(name: "FK_refresh_tokens_refresh_tokens_ParentTokenId", schema: "tenebit", table: "refresh_tokens", column: "ParentTokenId", principalSchema: "tenebit", principalTable: "refresh_tokens", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_refresh_tokens_refresh_tokens_ReplacedByTokenId", schema: "tenebit", table: "refresh_tokens", column: "ReplacedByTokenId", principalSchema: "tenebit", principalTable: "refresh_tokens", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

        migrationBuilder.AlterColumn<string>(name: "LicenseKey", schema: "tenebit", table: "licenses", type: "text", nullable: true, oldClrType: typeof(string), oldType: "character varying(600)", oldMaxLength: 600, oldNullable: true);
        migrationBuilder.AlterColumn<string>(name: "Value", schema: "tenebit", table: "asset_field_values", type: "text", nullable: false, oldClrType: typeof(string), oldType: "character varying(2000)", oldMaxLength: 2000);

        migrationBuilder.CreateTable(
            name: "background_job_runs",
            schema: "tenebit",
            columns: table => new
            {
                JobName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_background_job_runs", x => x.JobName));

        migrationBuilder.CreateTable(
            name: "oauth_transactions",
            schema: "tenebit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                StateHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CodeVerifier = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                ReturnPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                CorrelationHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Nonce = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_oauth_transactions", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_oauth_transactions_StateHash", schema: "tenebit", table: "oauth_transactions", column: "StateHash", unique: true);
        migrationBuilder.CreateIndex(name: "IX_oauth_transactions_ExpiresAt", schema: "tenebit", table: "oauth_transactions", column: "ExpiresAt");

        migrationBuilder.CreateTable(
            name: "two_factor_challenges",
            schema: "tenebit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TicketHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_two_factor_challenges", x => x.Id);
                table.ForeignKey(name: "FK_two_factor_challenges_organization_users_OrganizationUserId", column: x => x.OrganizationUserId, principalSchema: "tenebit", principalTable: "organization_users", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex(name: "IX_two_factor_challenges_TicketHash", schema: "tenebit", table: "two_factor_challenges", column: "TicketHash", unique: true);
        migrationBuilder.CreateIndex(name: "IX_two_factor_challenges_ExpiresAt", schema: "tenebit", table: "two_factor_challenges", column: "ExpiresAt");
        migrationBuilder.CreateIndex(name: "IX_two_factor_challenges_OrganizationUserId", schema: "tenebit", table: "two_factor_challenges", column: "OrganizationUserId");

        migrationBuilder.AddColumn<string>(name: "NormalizedName", schema: "tenebit", table: "asset_locations", type: "character varying(120)", maxLength: 120, nullable: true);
        migrationBuilder.Sql("UPDATE tenebit.asset_locations SET \"NormalizedName\" = upper(trim(\"Name\"));");
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM tenebit.asset_locations
                    GROUP BY "OrganizationId", "ParentId", "NormalizedName"
                    HAVING count(*) > 1
                ) THEN
                    RAISE EXCEPTION 'AUD3-019 preflight: duplicate sibling location names must be resolved before migration.';
                END IF;
            END $$;
            """);
        migrationBuilder.AlterColumn<string>(name: "NormalizedName", schema: "tenebit", table: "asset_locations", type: "character varying(120)", maxLength: 120, nullable: false, oldClrType: typeof(string), oldType: "character varying(120)", oldMaxLength: 120, oldNullable: true);
        migrationBuilder.CreateIndex(name: "UX_asset_locations_sibling_name", schema: "tenebit", table: "asset_locations", columns: new[] { "OrganizationId", "ParentId", "NormalizedName" }, unique: true, filter: "\"ParentId\" IS NOT NULL");
        migrationBuilder.CreateIndex(name: "UX_asset_locations_root_name", schema: "tenebit", table: "asset_locations", columns: new[] { "OrganizationId", "NormalizedName" }, unique: true, filter: "\"ParentId\" IS NULL");

        migrationBuilder.AddColumn<Guid>(name: "LocationId", schema: "tenebit", table: "assets", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "LocationId", schema: "tenebit", table: "people", type: "uuid", nullable: true);
        migrationBuilder.Sql("UPDATE tenebit.assets SET \"Location\" = NULL WHERE \"Location\" IS NOT NULL AND btrim(\"Location\") = ''; ");
        migrationBuilder.Sql("UPDATE tenebit.people SET \"Location\" = NULL WHERE \"Location\" IS NOT NULL AND btrim(\"Location\") = ''; ");

        migrationBuilder.Sql(
            """
            WITH RECURSIVE location_paths AS (
                SELECT l."Id", l."OrganizationId", l."ParentId", l."Name"::text AS full_path
                FROM tenebit.asset_locations l
                WHERE l."ParentId" IS NULL
                UNION ALL
                SELECT c."Id", c."OrganizationId", c."ParentId", (p.full_path || ' / ' || c."Name")::text
                FROM tenebit.asset_locations c
                JOIN location_paths p ON p."Id" = c."ParentId" AND p."OrganizationId" = c."OrganizationId"
            )
            UPDATE tenebit.assets a
            SET "LocationId" = p."Id", "Location" = p.full_path
            FROM location_paths p
            WHERE a."OrganizationId" = p."OrganizationId"
              AND a."Location" IS NOT NULL
              AND lower(trim(a."Location")) = lower(p.full_path);

            WITH RECURSIVE location_paths AS (
                SELECT l."Id", l."OrganizationId", l."ParentId", l."Name"::text AS full_path
                FROM tenebit.asset_locations l
                WHERE l."ParentId" IS NULL
                UNION ALL
                SELECT c."Id", c."OrganizationId", c."ParentId", (p.full_path || ' / ' || c."Name")::text
                FROM tenebit.asset_locations c
                JOIN location_paths p ON p."Id" = c."ParentId" AND p."OrganizationId" = c."OrganizationId"
            )
            UPDATE tenebit.people pe
            SET "LocationId" = p."Id", "Location" = p.full_path
            FROM location_paths p
            WHERE pe."OrganizationId" = p."OrganizationId"
              AND pe."Location" IS NOT NULL
              AND lower(trim(pe."Location")) = lower(p.full_path);
            """);

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM tenebit.assets WHERE "Location" IS NOT NULL AND "LocationId" IS NULL) THEN
                    RAISE EXCEPTION 'AUD3-019 preflight: at least one asset has a legacy location path that does not resolve to asset_locations.';
                END IF;
                IF EXISTS (SELECT 1 FROM tenebit.people WHERE "Location" IS NOT NULL AND "LocationId" IS NULL) THEN
                    RAISE EXCEPTION 'AUD3-019 preflight: at least one person has a legacy location path that does not resolve to asset_locations.';
                END IF;
            END $$;
            """);

        migrationBuilder.CreateIndex(name: "IX_assets_OrganizationId_LocationId", schema: "tenebit", table: "assets", columns: new[] { "OrganizationId", "LocationId" });
        migrationBuilder.CreateIndex(name: "IX_people_OrganizationId_LocationId", schema: "tenebit", table: "people", columns: new[] { "OrganizationId", "LocationId" });
        migrationBuilder.AddForeignKey(name: "FK_assets_asset_locations_OrganizationId_LocationId", schema: "tenebit", table: "assets", columns: new[] { "OrganizationId", "LocationId" }, principalSchema: "tenebit", principalTable: "asset_locations", principalColumns: new[] { "OrganizationId", "Id" }, onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_people_asset_locations_OrganizationId_LocationId", schema: "tenebit", table: "people", columns: new[] { "OrganizationId", "LocationId" }, principalSchema: "tenebit", principalTable: "asset_locations", principalColumns: new[] { "OrganizationId", "Id" }, onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_assets_asset_locations_OrganizationId_LocationId", schema: "tenebit", table: "assets");
        migrationBuilder.DropForeignKey(name: "FK_people_asset_locations_OrganizationId_LocationId", schema: "tenebit", table: "people");
        migrationBuilder.DropIndex(name: "IX_assets_OrganizationId_LocationId", schema: "tenebit", table: "assets");
        migrationBuilder.DropIndex(name: "IX_people_OrganizationId_LocationId", schema: "tenebit", table: "people");
        migrationBuilder.DropColumn(name: "LocationId", schema: "tenebit", table: "assets");
        migrationBuilder.DropColumn(name: "LocationId", schema: "tenebit", table: "people");
        migrationBuilder.DropIndex(name: "UX_asset_locations_sibling_name", schema: "tenebit", table: "asset_locations");
        migrationBuilder.DropIndex(name: "UX_asset_locations_root_name", schema: "tenebit", table: "asset_locations");
        migrationBuilder.DropColumn(name: "NormalizedName", schema: "tenebit", table: "asset_locations");

        migrationBuilder.DropTable(name: "two_factor_challenges", schema: "tenebit");
        migrationBuilder.DropTable(name: "oauth_transactions", schema: "tenebit");

        migrationBuilder.DropTable(name: "background_job_runs", schema: "tenebit");
        migrationBuilder.AlterColumn<string>(name: "LicenseKey", schema: "tenebit", table: "licenses", type: "character varying(600)", maxLength: 600, nullable: true, oldClrType: typeof(string), oldType: "text", oldNullable: true);
        migrationBuilder.AlterColumn<string>(name: "Value", schema: "tenebit", table: "asset_field_values", type: "character varying(2000)", maxLength: 2000, nullable: false, oldClrType: typeof(string), oldType: "text");

        migrationBuilder.DropForeignKey(name: "FK_refresh_tokens_refresh_tokens_ParentTokenId", schema: "tenebit", table: "refresh_tokens");
        migrationBuilder.DropForeignKey(name: "FK_refresh_tokens_refresh_tokens_ReplacedByTokenId", schema: "tenebit", table: "refresh_tokens");
        migrationBuilder.DropIndex(name: "IX_refresh_tokens_FamilyId", schema: "tenebit", table: "refresh_tokens");
        migrationBuilder.DropIndex(name: "IX_refresh_tokens_ParentTokenId", schema: "tenebit", table: "refresh_tokens");
        migrationBuilder.DropIndex(name: "IX_refresh_tokens_ReplacedByTokenId", schema: "tenebit", table: "refresh_tokens");
        migrationBuilder.DropColumn(name: "FamilyId", schema: "tenebit", table: "refresh_tokens");
        migrationBuilder.DropColumn(name: "ParentTokenId", schema: "tenebit", table: "refresh_tokens");
        migrationBuilder.DropColumn(name: "ReplacedByTokenId", schema: "tenebit", table: "refresh_tokens");
    }
}
