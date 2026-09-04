using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

/// <summary>
/// A downgrade must not take effect until the current billing period ends (the org keeps the higher
/// plan's entitlements until then), applied on Stripe's side via a subscription schedule
/// (docs.stripe.com/billing/subscriptions/subscription-schedules#changing-subscriptions). These three
/// columns are the local mirror of that pending state; see OrganizationSubscription.ScheduleDowngrade.
///
/// Written by hand as SQL to match the convention used by the surrounding migrations.
/// </summary>
[DbContext(typeof(TenebitDbContext))]
[Migration("20260904220000_ScheduledPlanDowngrade")]
public partial class ScheduledPlanDowngrade : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenebit.subscriptions
                ADD COLUMN "PendingPlanKey" character varying(40),
                ADD COLUMN "PendingPlanEffectiveAt" timestamp with time zone,
                ADD COLUMN "StripeScheduleId" character varying(80);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenebit.subscriptions
                DROP COLUMN IF EXISTS "PendingPlanKey",
                DROP COLUMN IF EXISTS "PendingPlanEffectiveAt",
                DROP COLUMN IF EXISTS "StripeScheduleId";
            """);
    }
}
