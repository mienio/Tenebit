using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantCompositeForeignKeysP0 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_teams_OrganizationId_Id",
                schema: "tenebit",
                table: "teams",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_people_OrganizationId_Id",
                schema: "tenebit",
                table: "people",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_asset_inspections_OrganizationId_Id",
                schema: "tenebit",
                table: "asset_inspections",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_teams_OrganizationId_ManagerId",
                schema: "tenebit",
                table: "teams",
                columns: new[] { "OrganizationId", "ManagerId" });

            migrationBuilder.CreateIndex(
                name: "IX_service_tickets_OrganizationId_AssetInspectionId",
                schema: "tenebit",
                table: "service_tickets",
                columns: new[] { "OrganizationId", "AssetInspectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_people_OrganizationId_ManagerId",
                schema: "tenebit",
                table: "people",
                columns: new[] { "OrganizationId", "ManagerId" });

            migrationBuilder.CreateIndex(
                name: "IX_people_OrganizationId_TeamId",
                schema: "tenebit",
                table: "people",
                columns: new[] { "OrganizationId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_offboarding_cases_OrganizationId_ProcessOwnerId",
                schema: "tenebit",
                table: "offboarding_cases",
                columns: new[] { "OrganizationId", "ProcessOwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_job_profiles_OrganizationId_DefaultManagerId",
                schema: "tenebit",
                table: "job_profiles",
                columns: new[] { "OrganizationId", "DefaultManagerId" });

            migrationBuilder.CreateIndex(
                name: "IX_assets_OrganizationId_AssignedPersonId",
                schema: "tenebit",
                table: "assets",
                columns: new[] { "OrganizationId", "AssignedPersonId" });

            migrationBuilder.CreateIndex(
                name: "IX_assets_OrganizationId_TeamId",
                schema: "tenebit",
                table: "assets",
                columns: new[] { "OrganizationId", "TeamId" });

            migrationBuilder.AddForeignKey(
                name: "FK_assets_people_OrganizationId_AssignedPersonId",
                schema: "tenebit",
                table: "assets",
                columns: new[] { "OrganizationId", "AssignedPersonId" },
                principalSchema: "tenebit",
                principalTable: "people",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_assets_teams_OrganizationId_TeamId",
                schema: "tenebit",
                table: "assets",
                columns: new[] { "OrganizationId", "TeamId" },
                principalSchema: "tenebit",
                principalTable: "teams",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_job_profiles_people_OrganizationId_DefaultManagerId",
                schema: "tenebit",
                table: "job_profiles",
                columns: new[] { "OrganizationId", "DefaultManagerId" },
                principalSchema: "tenebit",
                principalTable: "people",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_offboarding_cases_people_OrganizationId_ProcessOwnerId",
                schema: "tenebit",
                table: "offboarding_cases",
                columns: new[] { "OrganizationId", "ProcessOwnerId" },
                principalSchema: "tenebit",
                principalTable: "people",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_people_people_OrganizationId_ManagerId",
                schema: "tenebit",
                table: "people",
                columns: new[] { "OrganizationId", "ManagerId" },
                principalSchema: "tenebit",
                principalTable: "people",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_people_teams_OrganizationId_TeamId",
                schema: "tenebit",
                table: "people",
                columns: new[] { "OrganizationId", "TeamId" },
                principalSchema: "tenebit",
                principalTable: "teams",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_service_tickets_asset_inspections_OrganizationId_AssetInspe~",
                schema: "tenebit",
                table: "service_tickets",
                columns: new[] { "OrganizationId", "AssetInspectionId" },
                principalSchema: "tenebit",
                principalTable: "asset_inspections",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_teams_people_OrganizationId_ManagerId",
                schema: "tenebit",
                table: "teams",
                columns: new[] { "OrganizationId", "ManagerId" },
                principalSchema: "tenebit",
                principalTable: "people",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_assets_people_OrganizationId_AssignedPersonId",
                schema: "tenebit",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_assets_teams_OrganizationId_TeamId",
                schema: "tenebit",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_job_profiles_people_OrganizationId_DefaultManagerId",
                schema: "tenebit",
                table: "job_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_offboarding_cases_people_OrganizationId_ProcessOwnerId",
                schema: "tenebit",
                table: "offboarding_cases");

            migrationBuilder.DropForeignKey(
                name: "FK_people_people_OrganizationId_ManagerId",
                schema: "tenebit",
                table: "people");

            migrationBuilder.DropForeignKey(
                name: "FK_people_teams_OrganizationId_TeamId",
                schema: "tenebit",
                table: "people");

            migrationBuilder.DropForeignKey(
                name: "FK_service_tickets_asset_inspections_OrganizationId_AssetInspe~",
                schema: "tenebit",
                table: "service_tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_teams_people_OrganizationId_ManagerId",
                schema: "tenebit",
                table: "teams");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_teams_OrganizationId_Id",
                schema: "tenebit",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "IX_teams_OrganizationId_ManagerId",
                schema: "tenebit",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "IX_service_tickets_OrganizationId_AssetInspectionId",
                schema: "tenebit",
                table: "service_tickets");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_people_OrganizationId_Id",
                schema: "tenebit",
                table: "people");

            migrationBuilder.DropIndex(
                name: "IX_people_OrganizationId_ManagerId",
                schema: "tenebit",
                table: "people");

            migrationBuilder.DropIndex(
                name: "IX_people_OrganizationId_TeamId",
                schema: "tenebit",
                table: "people");

            migrationBuilder.DropIndex(
                name: "IX_offboarding_cases_OrganizationId_ProcessOwnerId",
                schema: "tenebit",
                table: "offboarding_cases");

            migrationBuilder.DropIndex(
                name: "IX_job_profiles_OrganizationId_DefaultManagerId",
                schema: "tenebit",
                table: "job_profiles");

            migrationBuilder.DropIndex(
                name: "IX_assets_OrganizationId_AssignedPersonId",
                schema: "tenebit",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "IX_assets_OrganizationId_TeamId",
                schema: "tenebit",
                table: "assets");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_asset_inspections_OrganizationId_Id",
                schema: "tenebit",
                table: "asset_inspections");
        }
    }
}
