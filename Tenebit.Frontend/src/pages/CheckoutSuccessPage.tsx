import { ArrowRight, CheckCircle2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { BrandMark } from '../components/BrandMark';
import { PublicFooter } from '../components/PublicFooter';
import { LoadingState } from '../components/StateViews';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';
import { useAuth } from '../auth/AuthProvider';
import { useI18n } from '../i18n/I18nProvider';

/** Set by the pricing page just before it opens Paddle, read here after the overlay reports a completed
 * payment. sessionStorage rather than router state because getting here is a full page load - the buyer's
 * tab is deliberately reloaded so nothing anywhere in the app is left holding the pre-purchase plan. */
export const EXPECTED_PLAN_KEY = 'tenebit.checkout.expectedPlan';

/** Paddle's webhook normally lands within a second or two. This is the point at which we stop insisting
 * the buyer wait and tell them the truth instead: the money is taken, the plan will follow. */
const WAIT_TIMEOUT_MS = 30_000;
const POLL_INTERVAL_MS = 1_500;

/**
 * Where a completed checkout lands.
 *
 * It waits for the entitlement rather than announcing it. The plan is written by Paddle's webhooks
 * (subscription.created/updated, see SubscriptionService.HandleWebhookAsync), which are the only source of
 * truth for what an organization has paid for - a browser event is a claim made by the shopper's own tab,
 * and trusting it here would let anyone grant themselves a plan by opening this URL. So this page polls
 * its own backend until the plan it was told to expect actually shows up, and only then says it is active.
 *
 * That wait is the whole point. Before it existed the buyer was dropped on a page that said "activating"
 * while the app around it still showed the old plan, and the only way out was a manual refresh.
 */
export function CheckoutSuccessPage() {
  const { t } = useI18n();
  const auth = useAuth();
  const [expected] = useState<string | null>(() => {
    try { return sessionStorage.getItem(EXPECTED_PLAN_KEY); } catch { return null; }
  });
  const [activePlanName, setActivePlanName] = useState<string | null>(null);
  const [timedOut, setTimedOut] = useState(false);

  // Nothing to wait for when the buyer is not signed in (an invoice paid from a Paddle mail via /checkout,
  // often by someone who is not the account owner) or when no plan was recorded before the checkout.
  const waiting = Boolean(auth.isAuthenticated && expected) && activePlanName === null && !timedOut;

  useEffect(() => {
    if (!auth.isAuthenticated || !expected) return;
    let cancelled = false;
    const startedAt = Date.now();

    const poll = async () => {
      if (cancelled) return;
      try {
        const subscription = await api.subscription();
        if (cancelled) return;
        // Matching on the plan we expected, not merely on "not free": a buyer whose previous paid
        // subscription was cancelled already has a non-free plan key on the record, so "not free" would
        // report success the instant the page opened, before the webhook had changed anything.
        if (subscription.planKey.toLowerCase() === expected.toLowerCase() && subscription.status !== 'Cancelled') {
          try { sessionStorage.removeItem(EXPECTED_PLAN_KEY); } catch { /* private mode - nothing to clean */ }
          setActivePlanName(subscription.planName);
          return;
        }
      } catch {
        // A failed poll is not a failed payment. Keep trying until the timeout says otherwise.
      }
      if (cancelled) return;
      if (Date.now() - startedAt >= WAIT_TIMEOUT_MS) { setTimedOut(true); return; }
      window.setTimeout(poll, POLL_INTERVAL_MS);
    };

    void poll();
    return () => { cancelled = true; };
  }, [auth.isAuthenticated, expected]);

  // A hard navigation, not a router push. Subscription state is cached in several places that only load
  // once per app session (Layout's sidebar among them), so a client-side transition would carry the old
  // plan and asset limit straight into the app the buyer just paid to upgrade.
  const goToApp = () => { window.location.href = '/'; };

  return (
    <main className="authShell">
      <section className="authCard">
        <div className="authTop">
          <div className="brand">
            <div className="brand__mark"><BrandMark /></div>
            <div>
              <strong>Tenebit</strong>
              <small>{t('nav.tagline')}</small>
            </div>
          </div>
          <LanguageSwitcher />
        </div>

        {waiting ? (
          <LoadingState title={t('checkout.successTitle')} description={t('checkout.successLead')} />
        ) : (
          <div className="stateBox">
            <CheckCircle2 size={30} />
            <h2>{activePlanName ? t('checkout.activeTitle', { plan: activePlanName }) : t('checkout.successTitle')}</h2>
            <p>{activePlanName ? t('checkout.activeLead') : timedOut ? t('checkout.slowLead') : t('checkout.successLead')}</p>
            <Button onClick={goToApp} icon={<ArrowRight size={16} />}>
              {t('checkout.successCta')}
            </Button>
          </div>
        )}
      </section>
      <PublicFooter compact />
    </main>
  );
}
