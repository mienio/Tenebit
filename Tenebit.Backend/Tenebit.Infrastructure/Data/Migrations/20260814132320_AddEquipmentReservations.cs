using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "equipment_reservations",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PickupLocation = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    RejectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    DecisionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_reservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "equipment_reservation_items",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedQuantity = table.Column<int>(type: "integer", nullable: false),
                    KitDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubstitutionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_reservation_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_equipment_reservation_items_equipment_reservations_Reservat~",
                        column: x => x.ReservationId,
                        principalSchema: "tenebit",
                        principalTable: "equipment_reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_equipment_reservation_items_OrganizationId_AssetId",
                schema: "tenebit",
                table: "equipment_reservation_items",
                columns: new[] { "OrganizationId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_equipment_reservation_items_OrganizationId_ReservationId",
                schema: "tenebit",
                table: "equipment_reservation_items",
                columns: new[] { "OrganizationId", "ReservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_equipment_reservation_items_ReservationId",
                schema: "tenebit",
                table: "equipment_reservation_items",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_equipment_reservations_OrganizationId_RequesterPersonId",
                schema: "tenebit",
                table: "equipment_reservations",
                columns: new[] { "OrganizationId", "RequesterPersonId" });

            migrationBuilder.CreateIndex(
                name: "IX_equipment_reservations_OrganizationId_Status_StartAt_EndAt",
                schema: "tenebit",
                table: "equipment_reservations",
                columns: new[] { "OrganizationId", "Status", "StartAt", "EndAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipment_reservation_items",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "equipment_reservations",
                schema: "tenebit");
        }
    }
}
