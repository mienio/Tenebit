using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

/// <summary>
/// Removes the drawn-signature columns added earlier the same day.
///
/// The handover protocol is a plain report now: the acceptance record already carries who confirmed,
/// when, and the hash of what they confirmed, and that is what the document needs to state. Keeping a
/// signature image would mean keeping a biometric-adjacent artefact of every employee for no benefit
/// the acceptance record does not already provide.
///
/// Dropping rather than leaving the columns dormant is safe here: they were never successfully written
/// to. The request validator inferred a 2048-character URL limit from the "SignatureDataUrl" property
/// name and rejected every real drawing, so the count of non-null values was zero in production before
/// this ran.
///
/// Written by hand as SQL to match the convention used by the surrounding migrations.
/// </summary>
[DbContext(typeof(TenebitDbContext))]
[Migration("20260826210000_DropAssignmentSignature")]
public partial class DropAssignmentSignature : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenebit.assignments
                DROP COLUMN IF EXISTS "SignatureImage",
                DROP COLUMN IF EXISTS "SignatureSha256",
                DROP COLUMN IF EXISTS "SignerName";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenebit.assignments
                ADD COLUMN IF NOT EXISTS "SignatureImage" bytea,
                ADD COLUMN IF NOT EXISTS "SignatureSha256" character varying(64),
                ADD COLUMN IF NOT EXISTS "SignerName" character varying(240);
            """);
    }
}
