using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicTokenHashIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_offboarding_cases_PublicTokenHash",
                schema: "tenebit",
                table: "offboarding_cases",
                column: "PublicTokenHash",
                unique: true,
                filter: "\"PublicTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_PublicTokenHash",
                schema: "tenebit",
                table: "assignments",
                column: "PublicTokenHash",
                unique: true,
                filter: "\"PublicTokenHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_offboarding_cases_PublicTokenHash",
                schema: "tenebit",
                table: "offboarding_cases");

            migrationBuilder.DropIndex(
                name: "IX_assignments_PublicTokenHash",
                schema: "tenebit",
                table: "assignments");
        }
    }
}
