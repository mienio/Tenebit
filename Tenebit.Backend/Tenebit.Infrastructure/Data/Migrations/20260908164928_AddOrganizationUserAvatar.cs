using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationUserAvatar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarContentType",
                schema: "tenebit",
                table: "organization_users",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "AvatarImage",
                schema: "tenebit",
                table: "organization_users",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AvatarVersion",
                schema: "tenebit",
                table: "organization_users",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarContentType",
                schema: "tenebit",
                table: "organization_users");

            migrationBuilder.DropColumn(
                name: "AvatarImage",
                schema: "tenebit",
                table: "organization_users");

            migrationBuilder.DropColumn(
                name: "AvatarVersion",
                schema: "tenebit",
                table: "organization_users");
        }
    }
}
