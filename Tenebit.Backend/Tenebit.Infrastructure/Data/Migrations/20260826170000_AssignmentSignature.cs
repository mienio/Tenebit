using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

/// <summary>
/// Storage for the signature an employee draws on the public acceptance page, so the handover protocol
/// PDF can show who confirmed the receipt and not just when.
///
/// The image itself lives on the assignment rather than in asset_evidence: evidence rows are per asset,
/// while one signature covers the whole protocol. Its checksum feeds the acceptance hash from integrity
/// version 4 onwards, which is why the column is stored next to the hash it seals.
///
/// Written by hand as SQL to match the convention used by the surrounding migrations.
/// </summary>
[DbContext(typeof(TenebitDbContext))]
[Migration("20260826170000_AssignmentSignature")]
public partial class AssignmentSignature : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenebit.assignments
                ADD COLUMN IF NOT EXISTS "SignatureImage" bytea,
                ADD COLUMN IF NOT EXISTS "SignatureSha256" character varying(64),
                ADD COLUMN IF NOT EXISTS "SignerName" character varying(240);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenebit.assignments
                DROP COLUMN IF EXISTS "SignatureImage",
                DROP COLUMN IF EXISTS "SignatureSha256",
                DROP COLUMN IF EXISTS "SignerName";
            """);
    }
}
