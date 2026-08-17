using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantCompositeForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_asset_audit_items_asset_audit_campaigns_CampaignId",
                schema: "tenebit",
                table: "asset_audit_items");

            migrationBuilder.DropForeignKey(
                name: "FK_asset_audit_items_asset_audit_participants_ParticipantId",
                schema: "tenebit",
                table: "asset_audit_items");

            migrationBuilder.DropForeignKey(
                name: "FK_asset_audit_participants_asset_audit_campaigns_CampaignId",
                schema: "tenebit",
                table: "asset_audit_participants");

            migrationBuilder.DropForeignKey(
                name: "FK_asset_evidence_assets_AssetId",
                schema: "tenebit",
                table: "asset_evidence");

            migrationBuilder.DropForeignKey(
                name: "FK_asset_evidence_assignments_AssignmentId",
                schema: "tenebit",
                table: "asset_evidence");

            migrationBuilder.DropForeignKey(
                name: "FK_equipment_reservation_items_equipment_reservations_Reservat~",
                schema: "tenebit",
                table: "equipment_reservation_items");

            migrationBuilder.DropForeignKey(
                name: "FK_offboarding_items_offboarding_cases_OffboardingCaseId",
                schema: "tenebit",
                table: "offboarding_items");

            migrationBuilder.DropIndex(
                name: "IX_offboarding_items_OffboardingCaseId",
                schema: "tenebit",
                table: "offboarding_items");

            migrationBuilder.DropIndex(
                name: "IX_equipment_reservation_items_ReservationId",
                schema: "tenebit",
                table: "equipment_reservation_items");

            migrationBuilder.DropIndex(
                name: "IX_asset_evidence_AssetId",
                schema: "tenebit",
                table: "asset_evidence");

            migrationBuilder.DropIndex(
                name: "IX_asset_evidence_AssignmentId",
                schema: "tenebit",
                table: "asset_evidence");

            migrationBuilder.DropIndex(
                name: "IX_asset_audit_participants_CampaignId",
                schema: "tenebit",
                table: "asset_audit_participants");

            migrationBuilder.DropIndex(
                name: "IX_asset_audit_items_CampaignId",
                schema: "tenebit",
                table: "asset_audit_items");

            migrationBuilder.DropIndex(
                name: "IX_asset_audit_items_ParticipantId",
                schema: "tenebit",
                table: "asset_audit_items");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_offboarding_cases_OrganizationId_Id",
                schema: "tenebit",
                table: "offboarding_cases",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_equipment_reservations_OrganizationId_Id",
                schema: "tenebit",
                table: "equipment_reservations",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_assignments_OrganizationId_Id",
                schema: "tenebit",
                table: "assignments",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_assets_OrganizationId_Id",
                schema: "tenebit",
                table: "assets",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_asset_audit_participants_OrganizationId_Id",
                schema: "tenebit",
                table: "asset_audit_participants",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_asset_audit_campaigns_OrganizationId_Id",
                schema: "tenebit",
                table: "asset_audit_campaigns",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_asset_evidence_OrganizationId_AssignmentId",
                schema: "tenebit",
                table: "asset_evidence",
                columns: new[] { "OrganizationId", "AssignmentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_asset_audit_items_asset_audit_campaigns_OrganizationId_Camp~",
                schema: "tenebit",
                table: "asset_audit_items",
                columns: new[] { "OrganizationId", "CampaignId" },
                principalSchema: "tenebit",
                principalTable: "asset_audit_campaigns",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_asset_audit_items_asset_audit_participants_OrganizationId_P~",
                schema: "tenebit",
                table: "asset_audit_items",
                columns: new[] { "OrganizationId", "ParticipantId" },
                principalSchema: "tenebit",
                principalTable: "asset_audit_participants",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_asset_audit_participants_asset_audit_campaigns_Organization~",
                schema: "tenebit",
                table: "asset_audit_participants",
                columns: new[] { "OrganizationId", "CampaignId" },
                principalSchema: "tenebit",
                principalTable: "asset_audit_campaigns",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_asset_evidence_assets_OrganizationId_AssetId",
                schema: "tenebit",
                table: "asset_evidence",
                columns: new[] { "OrganizationId", "AssetId" },
                principalSchema: "tenebit",
                principalTable: "assets",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_asset_evidence_assignments_OrganizationId_AssignmentId",
                schema: "tenebit",
                table: "asset_evidence",
                columns: new[] { "OrganizationId", "AssignmentId" },
                principalSchema: "tenebit",
                principalTable: "assignments",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_equipment_reservation_items_equipment_reservations_Organiza~",
                schema: "tenebit",
                table: "equipment_reservation_items",
                columns: new[] { "OrganizationId", "ReservationId" },
                principalSchema: "tenebit",
                principalTable: "equipment_reservations",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_offboarding_items_offboarding_cases_OrganizationId_Offboard~",
                schema: "tenebit",
                table: "offboarding_items",
                columns: new[] { "OrganizationId", "OffboardingCaseId" },
                principalSchema: "tenebit",
                principalTable: "offboarding_cases",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_asset_audit_items_asset_audit_campaigns_OrganizationId_Camp~",
                schema: "tenebit",
                table: "asset_audit_items");

            migrationBuilder.DropForeignKey(
                name: "FK_asset_audit_items_asset_audit_participants_OrganizationId_P~",
                schema: "tenebit",
                table: "asset_audit_items");

            migrationBuilder.DropForeignKey(
                name: "FK_asset_audit_participants_asset_audit_campaigns_Organization~",
                schema: "tenebit",
                table: "asset_audit_participants");

            migrationBuilder.DropForeignKey(
                name: "FK_asset_evidence_assets_OrganizationId_AssetId",
                schema: "tenebit",
                table: "asset_evidence");

            migrationBuilder.DropForeignKey(
                name: "FK_asset_evidence_assignments_OrganizationId_AssignmentId",
                schema: "tenebit",
                table: "asset_evidence");

            migrationBuilder.DropForeignKey(
                name: "FK_equipment_reservation_items_equipment_reservations_Organiza~",
                schema: "tenebit",
                table: "equipment_reservation_items");

            migrationBuilder.DropForeignKey(
                name: "FK_offboarding_items_offboarding_cases_OrganizationId_Offboard~",
                schema: "tenebit",
                table: "offboarding_items");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_offboarding_cases_OrganizationId_Id",
                schema: "tenebit",
                table: "offboarding_cases");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_equipment_reservations_OrganizationId_Id",
                schema: "tenebit",
                table: "equipment_reservations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_assignments_OrganizationId_Id",
                schema: "tenebit",
                table: "assignments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_assets_OrganizationId_Id",
                schema: "tenebit",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "IX_asset_evidence_OrganizationId_AssignmentId",
                schema: "tenebit",
                table: "asset_evidence");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_asset_audit_participants_OrganizationId_Id",
                schema: "tenebit",
                table: "asset_audit_participants");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_asset_audit_campaigns_OrganizationId_Id",
                schema: "tenebit",
                table: "asset_audit_campaigns");

            migrationBuilder.CreateIndex(
                name: "IX_offboarding_items_OffboardingCaseId",
                schema: "tenebit",
                table: "offboarding_items",
                column: "OffboardingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_equipment_reservation_items_ReservationId",
                schema: "tenebit",
                table: "equipment_reservation_items",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_evidence_AssetId",
                schema: "tenebit",
                table: "asset_evidence",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_evidence_AssignmentId",
                schema: "tenebit",
                table: "asset_evidence",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_audit_participants_CampaignId",
                schema: "tenebit",
                table: "asset_audit_participants",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_audit_items_CampaignId",
                schema: "tenebit",
                table: "asset_audit_items",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_audit_items_ParticipantId",
                schema: "tenebit",
                table: "asset_audit_items",
                column: "ParticipantId");

            migrationBuilder.AddForeignKey(
                name: "FK_asset_audit_items_asset_audit_campaigns_CampaignId",
                schema: "tenebit",
                table: "asset_audit_items",
                column: "CampaignId",
                principalSchema: "tenebit",
                principalTable: "asset_audit_campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_asset_audit_items_asset_audit_participants_ParticipantId",
                schema: "tenebit",
                table: "asset_audit_items",
                column: "ParticipantId",
                principalSchema: "tenebit",
                principalTable: "asset_audit_participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_asset_audit_participants_asset_audit_campaigns_CampaignId",
                schema: "tenebit",
                table: "asset_audit_participants",
                column: "CampaignId",
                principalSchema: "tenebit",
                principalTable: "asset_audit_campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_asset_evidence_assets_AssetId",
                schema: "tenebit",
                table: "asset_evidence",
                column: "AssetId",
                principalSchema: "tenebit",
                principalTable: "assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_asset_evidence_assignments_AssignmentId",
                schema: "tenebit",
                table: "asset_evidence",
                column: "AssignmentId",
                principalSchema: "tenebit",
                principalTable: "assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_equipment_reservation_items_equipment_reservations_Reservat~",
                schema: "tenebit",
                table: "equipment_reservation_items",
                column: "ReservationId",
                principalSchema: "tenebit",
                principalTable: "equipment_reservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_offboarding_items_offboarding_cases_OffboardingCaseId",
                schema: "tenebit",
                table: "offboarding_items",
                column: "OffboardingCaseId",
                principalSchema: "tenebit",
                principalTable: "offboarding_cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
