using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

[DbContext(typeof(TenebitDbContext))]
[Migration("20260818204500_AddActivityLogRetentionIndex")]
public partial class AddActivityLogRetentionIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_activity_logs_CreatedAt\" ON tenebit.activity_logs (\"CreatedAt\");",
            suppressTransaction: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            "DROP INDEX CONCURRENTLY IF EXISTS tenebit.\"IX_activity_logs_CreatedAt\";",
            suppressTransaction: true);
}
