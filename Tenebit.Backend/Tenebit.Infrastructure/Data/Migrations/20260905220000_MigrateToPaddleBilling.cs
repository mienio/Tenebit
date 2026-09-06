using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

/// <summary>
/// Cuts billing over from Stripe (a payment processor - Tenebit stayed the legal seller and never got a
/// real invoice from Stripe itself) to Paddle (a Merchant of Record - Paddle is the seller of record,
/// handles VAT/sales tax worldwide and issues the actual invoice). See paddle_plan.md at the repo root
/// for the full rationale and rollout plan.
///
/// Verified against a restored copy of the real database (2026-09-06): 3 rows in `subscriptions` already
/// carry a Stripe id, 2 of them a live (Stripe *test-mode*, confirmed via the backend container's
/// Stripe:SecretKey prefix - sk_test_...) subscription with a pending scheduled downgrade. A bare rename
/// would leave those rows holding a Stripe-shaped id under a "Paddle*" column name - meaningless to
/// Paddle's API, so every subsequent portal/reconcile/cancel call for those three organizations would
/// 404. This migration resets any such row to a clean Free-plan slate instead of carrying the orphaned id
/// forward; re-upgrading through the new Paddle checkout afterward is a normal in-app action.
///
/// StripeScheduleId is dropped outright (not reset-then-dropped like the others, just dropped): Paddle
/// tracks a pending plan change directly on the subscription itself (scheduled_change), unlike Stripe's
/// separate subscription-schedule object, so there is no id shape left to keep even transiently.
///
/// Written by hand as SQL to match the convention used by the surrounding migrations.
/// </summary>
[DbContext(typeof(TenebitDbContext))]
[Migration("20260905220000_MigrateToPaddleBilling")]
public partial class MigrateToPaddleBilling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Orphan any existing Stripe-linked subscription before the rename below would otherwise make
            -- it look like a valid Paddle one. Whatever plan/pending-downgrade state that org had under
            -- Stripe is gone the moment its provider ids stop resolving to anything real, so this is not
            -- optional cleanup - it is the only way "PlanKey" stays truthful once nothing backs it.
            UPDATE tenebit.subscriptions
            SET "PlanKey" = 'free',
                "Status" = 'Active',
                "PendingPlanKey" = NULL,
                "PendingPlanEffectiveAt" = NULL,
                "CancelledAt" = NULL,
                "StripeCustomerId" = NULL,
                "StripeSubscriptionId" = NULL,
                "StripeScheduleId" = NULL,
                "UpdatedAt" = now()
            WHERE "StripeCustomerId" IS NOT NULL AND "StripeCustomerId" != ''
               OR "StripeSubscriptionId" IS NOT NULL AND "StripeSubscriptionId" != '';

            ALTER TABLE tenebit.subscriptions RENAME COLUMN "StripeCustomerId" TO "PaddleCustomerId";
            ALTER TABLE tenebit.subscriptions RENAME COLUMN "StripeSubscriptionId" TO "PaddleSubscriptionId";
            ALTER INDEX tenebit."IX_subscriptions_StripeCustomerId" RENAME TO "IX_subscriptions_PaddleCustomerId";
            ALTER TABLE tenebit.subscriptions DROP COLUMN IF EXISTS "StripeScheduleId";

            ALTER TABLE tenebit.processed_stripe_events RENAME TO processed_paddle_events;
            ALTER INDEX tenebit."IX_processed_stripe_events_EventId" RENAME TO "IX_processed_paddle_events_EventId";
            ALTER TABLE tenebit.processed_paddle_events RENAME CONSTRAINT "PK_processed_stripe_events" TO "PK_processed_paddle_events";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenebit.subscriptions RENAME COLUMN "PaddleCustomerId" TO "StripeCustomerId";
            ALTER TABLE tenebit.subscriptions RENAME COLUMN "PaddleSubscriptionId" TO "StripeSubscriptionId";
            ALTER INDEX tenebit."IX_subscriptions_PaddleCustomerId" RENAME TO "IX_subscriptions_StripeCustomerId";
            ALTER TABLE tenebit.subscriptions ADD COLUMN "StripeScheduleId" character varying(80);

            ALTER TABLE tenebit.processed_paddle_events RENAME TO processed_stripe_events;
            ALTER INDEX tenebit."IX_processed_paddle_events_EventId" RENAME TO "IX_processed_stripe_events_EventId";
            ALTER TABLE tenebit.processed_stripe_events RENAME CONSTRAINT "PK_processed_paddle_events" TO "PK_processed_stripe_events";
            """);
        // Deliberately not restoring the pre-Up plan/pending-downgrade/Stripe-id state cleared above -
        // that data was already reset to Free during Up() because it pointed at ids Paddle can never
        // resolve; Down() only reverts the schema shape, not a business decision that already happened.
    }
}
