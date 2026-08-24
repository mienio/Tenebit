using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

/// <summary>
/// Straight-line depreciation period per asset category, in months. Nullable with no default, so every
/// existing category starts as "not depreciated" and current book values stay equal to purchase price
/// until an organization opts in by setting a schedule.
/// Written by hand as SQL to match the convention used by the surrounding migrations.
/// </summary>
[DbContext(typeof(TenebitDbContext))]
[Migration("20260824160000_AssetCategoryDepreciation")]
public partial class AssetCategoryDepreciation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            ALTER TABLE tenebit.asset_categories
                ADD COLUMN IF NOT EXISTS "DepreciationMonths" integer NULL;
            """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            ALTER TABLE tenebit.asset_categories
                DROP COLUMN IF EXISTS "DepreciationMonths";
            """);
}
