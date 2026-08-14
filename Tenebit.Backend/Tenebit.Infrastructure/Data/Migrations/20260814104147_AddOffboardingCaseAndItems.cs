using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOffboardingCaseAndItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "offboarding_cases",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EmploymentEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReturnDueDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DefaultReturnLocation = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ProcessOwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    BlockNewReservations = table.Column<bool>(type: "boolean", nullable: false),
                    CancelFutureReservations = table.Column<bool>(type: "boolean", nullable: false),
                    AutoReleaseLicenses = table.Column<bool>(type: "boolean", nullable: false),
                    PersonDeactivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScheduledActionsCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublicTokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PublicTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublicTokenRevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FinalProtocolNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offboarding_cases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "offboarding_items",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OffboardingCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Label = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EmployeeResponse = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    EmployeeComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AutomationMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AutomationLastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AutomationError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReceivedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    InspectionCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InspectionCompletedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offboarding_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_offboarding_items_offboarding_cases_OffboardingCaseId",
                        column: x => x.OffboardingCaseId,
                        principalSchema: "tenebit",
                        principalTable: "offboarding_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_offboarding_cases_OrganizationId_PersonId_Open",
                schema: "tenebit",
                table: "offboarding_cases",
                columns: new[] { "OrganizationId", "PersonId" },
                unique: true,
                filter: "\"Status\" NOT IN ('Completed', 'Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_offboarding_items_OffboardingCaseId",
                schema: "tenebit",
                table: "offboarding_items",
                column: "OffboardingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_offboarding_items_OrganizationId_OffboardingCaseId",
                schema: "tenebit",
                table: "offboarding_items",
                columns: new[] { "OrganizationId", "OffboardingCaseId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "offboarding_items",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "offboarding_cases",
                schema: "tenebit");
        }
    }
}
