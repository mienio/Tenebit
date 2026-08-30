using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

/// <summary>
/// Replaces the pair of identifiers in an asset's QR code with a ten-character random code.
///
/// The point is physical, not architectural: a URL carrying two GUIDs produces a 57x57 QR code, which on
/// a 63.5 mm label leaves roughly 0.39 mm per module - under the size a phone camera reads on the first
/// attempt. The short code takes the same label to 33x33 and 0.52 mm. Ten characters is the ceiling that
/// is free: eight and ten produce an identically sized code, so the extra two characters cost nothing
/// and carry ten more bits.
///
/// Existing rows are backfilled here rather than lazily. Lazily would mean a nullable column, a null
/// check on every path that prints or resolves a label, and a GET that writes - all to avoid one pass
/// over a table that has to be walked exactly once in the product's life.
///
/// The pass is row by row on purpose. The set-based version reads better but is a trap: the random
/// expression does not reference the row, so the planner is free to evaluate it once and hand every
/// asset the same code, which the unique index below would then reject. The inner loop redraws on
/// collision; with fifty bits it will essentially never iterate, but a unique index is not a place to
/// assume.
///
/// Written by hand as SQL to match the convention used by the surrounding migrations.
/// </summary>
[DbContext(typeof(TenebitDbContext))]
[Migration("20260829140000_AssetScanCode")]
public partial class AssetScanCode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenebit.assets ADD COLUMN IF NOT EXISTS "ScanCode" character varying(16);

            DO $$
            DECLARE
                alphabet CONSTANT text := '0123456789ABCDEFGHJKMNPQRSTVWXYZ';
                target uuid;
                candidate text;
            BEGIN
                LOOP
                    SELECT "Id" INTO target FROM tenebit.assets WHERE "ScanCode" IS NULL LIMIT 1;
                    EXIT WHEN target IS NULL;

                    LOOP
                        SELECT string_agg(substr(alphabet, 1 + floor(random() * 32)::int, 1), '')
                          INTO candidate
                          FROM generate_series(1, 10);
                        EXIT WHEN NOT EXISTS (SELECT 1 FROM tenebit.assets WHERE "ScanCode" = candidate);
                    END LOOP;

                    UPDATE tenebit.assets SET "ScanCode" = candidate WHERE "Id" = target;
                END LOOP;
            END $$;

            ALTER TABLE tenebit.assets ALTER COLUMN "ScanCode" SET NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_assets_ScanCode" ON tenebit.assets ("ScanCode");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS tenebit."IX_assets_ScanCode";
            ALTER TABLE tenebit.assets DROP COLUMN IF EXISTS "ScanCode";
            """);
    }
}
