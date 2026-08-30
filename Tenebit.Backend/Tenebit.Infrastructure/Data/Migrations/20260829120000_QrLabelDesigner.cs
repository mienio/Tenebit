using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

/// <summary>
/// Gives the QR label the rest of its content: the owner's name, a free line of text, and a mark.
///
/// The mark is stored as bytes rather than as another URL beside LogoUrl. The label is composed into a
/// single self-contained SVG so the browser can rasterise it to PNG/JPG through a canvas, and an image
/// loaded that way may not fetch anything external - a referenced logo would print as an empty box.
/// Holding the bytes also keeps the server from calling out to an address a tenant supplied.
///
/// Written by hand as SQL to match the convention used by the surrounding migrations.
/// </summary>
[DbContext(typeof(TenebitDbContext))]
[Migration("20260829120000_QrLabelDesigner")]
public partial class QrLabelDesigner : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenebit.organizations
                ADD COLUMN IF NOT EXISTS "QrLabelShowSerialNumber" boolean NOT NULL DEFAULT FALSE,
                ADD COLUMN IF NOT EXISTS "QrLabelShowOrganizationName" boolean NOT NULL DEFAULT FALSE,
                ADD COLUMN IF NOT EXISTS "QrLabelCustomText" character varying(60),
                ADD COLUMN IF NOT EXISTS "QrLabelLogo" character varying(20) NOT NULL DEFAULT 'None',
                ADD COLUMN IF NOT EXISTS "QrLabelLogoImage" bytea,
                ADD COLUMN IF NOT EXISTS "QrLabelLogoContentType" character varying(60),
                ADD COLUMN IF NOT EXISTS "QrLabelCodeSize" character varying(20) NOT NULL DEFAULT 'Medium',
                ADD COLUMN IF NOT EXISTS "QrLabelFormat" character varying(20) NOT NULL DEFAULT 'Medium63';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenebit.organizations
                DROP COLUMN IF EXISTS "QrLabelShowSerialNumber",
                DROP COLUMN IF EXISTS "QrLabelShowOrganizationName",
                DROP COLUMN IF EXISTS "QrLabelCustomText",
                DROP COLUMN IF EXISTS "QrLabelLogo",
                DROP COLUMN IF EXISTS "QrLabelLogoImage",
                DROP COLUMN IF EXISTS "QrLabelLogoContentType",
                DROP COLUMN IF EXISTS "QrLabelCodeSize",
                DROP COLUMN IF EXISTS "QrLabelFormat";
            """);
    }
}
