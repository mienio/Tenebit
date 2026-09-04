using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

/// <summary>
/// Marketing promo codes, scoped to one paid plan each - the admin panel's "Kody promocyjne" tab.
/// Platform-wide (no OrganizationId), matching how <see cref="Tenebit.Domain.Subscriptions.SubscriptionPlan"/>
/// itself is a static catalog rather than per-tenant data.
///
/// Written by hand as SQL to match the convention used by the surrounding migrations.
/// </summary>
[DbContext(typeof(TenebitDbContext))]
[Migration("20260830090000_PromoCodes")]
public partial class PromoCodes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE tenebit.promo_codes (
                "Id" uuid NOT NULL,
                "Code" character varying(40) NOT NULL,
                "PlanKey" character varying(40) NOT NULL,
                "DiscountType" character varying(20) NOT NULL,
                "DiscountValue" numeric(10,2) NOT NULL,
                "MaxRedemptions" integer,
                "TimesRedeemed" integer NOT NULL DEFAULT 0,
                "ExpiresAt" timestamp with time zone,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "CreatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_promo_codes" PRIMARY KEY ("Id")
            );

            CREATE UNIQUE INDEX "IX_promo_codes_Code" ON tenebit.promo_codes ("Code");
            CREATE INDEX "IX_promo_codes_PlanKey" ON tenebit.promo_codes ("PlanKey");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS tenebit.promo_codes;
            """);
    }
}
