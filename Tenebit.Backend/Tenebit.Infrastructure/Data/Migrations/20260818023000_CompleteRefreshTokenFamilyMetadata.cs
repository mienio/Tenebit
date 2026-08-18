using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

[DbContext(typeof(TenebitDbContext))]
[Migration("20260818023000_CompleteRefreshTokenFamilyMetadata")]
public partial class CompleteRefreshTokenFamilyMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "RevocationReason", schema: "tenebit", table: "refresh_tokens", type: "character varying(80)", maxLength: 80, nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "RevocationReason", schema: "tenebit", table: "refresh_tokens");
}
