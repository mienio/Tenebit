using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Tenebit.Infrastructure.Data;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(TenebitDbContext))]
    [Migration("20260814153000_AddAssignmentIntegrityVersion")]
    public partial class AddAssignmentIntegrityVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntegrityVersion",
                schema: "tenebit",
                table: "assignments",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntegrityVersion",
                schema: "tenebit",
                table: "assignments");
        }
    }
}
