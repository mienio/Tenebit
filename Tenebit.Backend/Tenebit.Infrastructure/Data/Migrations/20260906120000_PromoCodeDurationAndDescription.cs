using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

/// <summary>
/// Lets a promo code's discount keep applying across renewals (not just the first charge) and gives admins
/// a free-text description to show the customer once a code is applied - see
/// <see cref="Tenebit.Domain.Subscriptions.PromoCode"/>.
///
/// Written by hand as SQL to match the convention used by the surrounding migrations.
/// </summary>
[DbContext(typeof(TenebitDbContext))]
[Migration("20260906120000_PromoCodeDurationAndDescription")]
public partial class PromoCodeDurationAndDescription : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenebit.promo_codes ADD COLUMN "DurationType" character varying(20) NOT NULL DEFAULT 'Once';
            ALTER TABLE tenebit.promo_codes ADD COLUMN "DurationInMonths" integer;
            ALTER TABLE tenebit.promo_codes ADD COLUMN "Description" character varying(500);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenebit.promo_codes DROP COLUMN IF EXISTS "Description";
            ALTER TABLE tenebit.promo_codes DROP COLUMN IF EXISTS "DurationInMonths";
            ALTER TABLE tenebit.promo_codes DROP COLUMN IF EXISTS "DurationType";
            """);
    }
}
