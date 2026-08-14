using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonEmploymentLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeactivatedAt",
                schema: "tenebit",
                table: "people",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmploymentEndsAt",
                schema: "tenebit",
                table: "people",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmploymentStatus",
                schema: "tenebit",
                table: "people",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                schema: "tenebit",
                table: "people",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE tenebit.people SET \"EmploymentStatus\" = CASE WHEN \"IsActive\" THEN 'Active' ELSE 'Inactive' END;");

            migrationBuilder.AlterColumn<string>(
                name: "EmploymentStatus",
                schema: "tenebit",
                table: "people",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_people_OrganizationId_EmploymentEndsAt",
                schema: "tenebit",
                table: "people",
                columns: new[] { "OrganizationId", "EmploymentEndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_people_OrganizationId_EmploymentStatus",
                schema: "tenebit",
                table: "people",
                columns: new[] { "OrganizationId", "EmploymentStatus" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_people_employment_status_active",
                schema: "tenebit",
                table: "people",
                sql: "(\"EmploymentStatus\" IN ('Active', 'Offboarding') AND \"IsActive\") OR (\"EmploymentStatus\" = 'Inactive' AND NOT \"IsActive\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_people_OrganizationId_EmploymentEndsAt",
                schema: "tenebit",
                table: "people");

            migrationBuilder.DropIndex(
                name: "IX_people_OrganizationId_EmploymentStatus",
                schema: "tenebit",
                table: "people");

            migrationBuilder.DropCheckConstraint(
                name: "CK_people_employment_status_active",
                schema: "tenebit",
                table: "people");

            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                schema: "tenebit",
                table: "people");

            migrationBuilder.DropColumn(
                name: "EmploymentEndsAt",
                schema: "tenebit",
                table: "people");

            migrationBuilder.DropColumn(
                name: "EmploymentStatus",
                schema: "tenebit",
                table: "people");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                schema: "tenebit",
                table: "people");
        }
    }
}
