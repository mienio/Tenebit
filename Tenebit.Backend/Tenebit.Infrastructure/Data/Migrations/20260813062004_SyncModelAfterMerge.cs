using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelAfterMerge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackgroundColor",
                schema: "tenebit",
                table: "asset_status_settings",
                type: "character varying(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Color",
                schema: "tenebit",
                table: "asset_status_settings",
                type: "character varying(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "dashboard_layouts",
                schema: "tenebit",
                columns: table => new
                {
                    OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LayoutJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dashboard_layouts", x => x.OrganizationUserId);
                });

            migrationBuilder.CreateTable(
                name: "dashboard_snapshots",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalAssets = table.Column<int>(type: "integer", nullable: false),
                    AssetsWithoutOwner = table.Column<int>(type: "integer", nullable: false),
                    OpenAssignments = table.Column<int>(type: "integer", nullable: false),
                    VisibleAssetValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dashboard_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "licenses",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Vendor = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    LicenseKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    SeatsTotal = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_licenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "person_relation_types",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_relation_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    PermissionKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Allowed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "two_factor_recovery_codes",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_two_factor_recovery_codes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "license_seats",
                schema: "tenebit",
                columns: table => new
                {
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_seats", x => new { x.LicenseId, x.PersonId });
                    table.ForeignKey(
                        name: "FK_license_seats_licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalSchema: "tenebit",
                        principalTable: "licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dashboard_snapshots_OrganizationId_SnapshotDate",
                schema: "tenebit",
                table: "dashboard_snapshots",
                columns: new[] { "OrganizationId", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_person_relation_types_OrganizationId_Name",
                schema: "tenebit",
                table: "person_relation_types",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_OrganizationId_RoleKey_PermissionKey",
                schema: "tenebit",
                table: "role_permissions",
                columns: new[] { "OrganizationId", "RoleKey", "PermissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_two_factor_recovery_codes_OrganizationUserId",
                schema: "tenebit",
                table: "two_factor_recovery_codes",
                column: "OrganizationUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dashboard_layouts",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "dashboard_snapshots",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "license_seats",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "person_relation_types",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "two_factor_recovery_codes",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "licenses",
                schema: "tenebit");

            migrationBuilder.DropColumn(
                name: "BackgroundColor",
                schema: "tenebit",
                table: "asset_status_settings");

            migrationBuilder.DropColumn(
                name: "Color",
                schema: "tenebit",
                table: "asset_status_settings");
        }
    }
}
