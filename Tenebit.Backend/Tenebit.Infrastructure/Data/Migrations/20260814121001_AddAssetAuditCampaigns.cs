using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetAuditCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asset_audit_campaigns",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DueDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_audit_campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "asset_audit_participants",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TokenRevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReminderAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_audit_participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_audit_participants_asset_audit_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "tenebit",
                        principalTable: "asset_audit_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_audit_items",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedLocation = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Response = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ResolutionNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_audit_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_audit_items_asset_audit_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "tenebit",
                        principalTable: "asset_audit_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_asset_audit_items_asset_audit_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalSchema: "tenebit",
                        principalTable: "asset_audit_participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_asset_audit_campaigns_OrganizationId_Status",
                schema: "tenebit",
                table: "asset_audit_campaigns",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_asset_audit_items_CampaignId",
                schema: "tenebit",
                table: "asset_audit_items",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_audit_items_OrganizationId_CampaignId",
                schema: "tenebit",
                table: "asset_audit_items",
                columns: new[] { "OrganizationId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_asset_audit_items_OrganizationId_ParticipantId",
                schema: "tenebit",
                table: "asset_audit_items",
                columns: new[] { "OrganizationId", "ParticipantId" });

            migrationBuilder.CreateIndex(
                name: "IX_asset_audit_items_ParticipantId",
                schema: "tenebit",
                table: "asset_audit_items",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_audit_participants_CampaignId",
                schema: "tenebit",
                table: "asset_audit_participants",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_audit_participants_OrganizationId_CampaignId_PersonId",
                schema: "tenebit",
                table: "asset_audit_participants",
                columns: new[] { "OrganizationId", "CampaignId", "PersonId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asset_audit_items",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "asset_audit_participants",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "asset_audit_campaigns",
                schema: "tenebit");
        }
    }
}
