using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

[DbContext(typeof(TenebitDbContext))]
[Migration("20260817201500_AddOrganizationUserPersonLink")]
public partial class AddOrganizationUserPersonLink : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "PersonId",
            schema: "tenebit",
            table: "organization_users",
            type: "uuid",
            nullable: true);

        // Existing deployments historically identified Employee/Manager by e-mail. Backfill only an
        // exact same-tenant match. Both tables already enforce unique (OrganizationId, Email), so this
        // cannot choose between multiple people and never crosses the tenant boundary.
        migrationBuilder.Sql(
            """
            UPDATE tenebit.organization_users AS u
            SET "PersonId" = p."Id"
            FROM tenebit.people AS p
            WHERE p."OrganizationId" = u."OrganizationId"
              AND lower(p."Email") = lower(u."Email")
              AND u."PersonId" IS NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_organization_users_OrganizationId_PersonId",
            schema: "tenebit",
            table: "organization_users",
            columns: new[] { "OrganizationId", "PersonId" },
            unique: true,
            filter: "\"PersonId\" IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_organization_users_people_OrganizationId_PersonId",
            schema: "tenebit",
            table: "organization_users",
            columns: new[] { "OrganizationId", "PersonId" },
            principalSchema: "tenebit",
            principalTable: "people",
            principalColumns: new[] { "OrganizationId", "Id" },
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_organization_users_people_OrganizationId_PersonId",
            schema: "tenebit",
            table: "organization_users");

        migrationBuilder.DropIndex(
            name: "IX_organization_users_OrganizationId_PersonId",
            schema: "tenebit",
            table: "organization_users");

        migrationBuilder.DropColumn(
            name: "PersonId",
            schema: "tenebit",
            table: "organization_users");
    }
}
