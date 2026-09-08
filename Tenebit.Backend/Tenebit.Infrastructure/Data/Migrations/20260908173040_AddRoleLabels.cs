using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "role_labels",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_labels", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_role_labels_OrganizationId_RoleKey",
                schema: "tenebit",
                table: "role_labels",
                columns: new[] { "OrganizationId", "RoleKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_labels",
                schema: "tenebit");
        }
    }
}
