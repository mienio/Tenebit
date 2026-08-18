using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

[DbContext(typeof(TenebitDbContext))]
[Migration("20260817184500_AddOrganizationUserSecurityStamp")]
public partial class AddOrganizationUserSecurityStamp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "SecurityStamp",
            schema: "tenebit",
            table: "organization_users",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty);

        // The stamp is a version marker, not a secret. Using each user's existing unique Id gives every
        // migrated account a distinct non-empty initial version without requiring a PostgreSQL extension.
        migrationBuilder.Sql(
            """
            UPDATE tenebit.organization_users
            SET "SecurityStamp" = "Id"
            WHERE "SecurityStamp" = '00000000-0000-0000-0000-000000000000';
            """);

        // New rows always receive a random stamp in the domain constructor. Remove the temporary
        // database default so a future write path that forgets the invariant fails visibly.
        migrationBuilder.AlterColumn<Guid>(
            name: "SecurityStamp",
            schema: "tenebit",
            table: "organization_users",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldDefaultValue: Guid.Empty);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SecurityStamp",
            schema: "tenebit",
            table: "organization_users");
    }
}
