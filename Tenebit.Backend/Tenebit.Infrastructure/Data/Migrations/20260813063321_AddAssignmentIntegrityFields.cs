using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentIntegrityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfirmationHash",
                schema: "tenebit",
                table: "procedure_acceptances",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedIp",
                schema: "tenebit",
                table: "procedure_acceptances",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptanceHash",
                schema: "tenebit",
                table: "assignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedIp",
                schema: "tenebit",
                table: "assignments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmationHash",
                schema: "tenebit",
                table: "procedure_acceptances");

            migrationBuilder.DropColumn(
                name: "ConfirmedIp",
                schema: "tenebit",
                table: "procedure_acceptances");

            migrationBuilder.DropColumn(
                name: "AcceptanceHash",
                schema: "tenebit",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "AcceptedIp",
                schema: "tenebit",
                table: "assignments");
        }
    }
}
