import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { api } from '../api/endpoints';
import {
  ensurePaddleReady,
  openPaddleCheckout,
  paddleLocaleFor,
  readTransactionId,
  stripTransactionId
} from '../api/paddleClient';
import { BrandMark } from '../components/BrandMark';
import { PublicFooter } from '../components/PublicFooter';
import { ErrorState, LoadingState } from '../components/StateViews';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';
import { useI18n } from '../i18n/I18nProvider';

/**
 * Public landing spot for Paddle's Default payment link (Paddle dashboard - Checkout settings). Paddle
 * appends `?_ptxn=<transaction id>` to it whenever it sends a shopper here to pay: the "Pay" button on an
 * invoice or dunning mail, a payment-method update after a failed charge, or a transaction created by
 * hand in the dashboard.
 *
 * It has to stay outside the auth guard. The recipient of such a mail is frequently not logged in - often
 * not even the person who owns the account - and bouncing them through /login would swallow the `_ptxn`
 * query string on the way, leaving Paddle.js with nothing to open. Nothing about the organization is
 * rendered here; the transaction id in the URL is all this page ever knows.
 */
export function CheckoutPage() {
  const { t, language } = useI18n();
  const location = useLocation();
  const navigate = useNavigate();
  const transactionId = useMemo(() => readTransactionId(location.search), [location.search]);
  const [error, setError] = useState<string | null>(null);
  // checkout.closed also arrives right after a completed payment. Without this the success redirect
  // would be immediately overwritten by the close redirect and the buyer would land on the landing page
  // wondering whether the payment went through.
  const completed = useRef(false);

  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        const config = await api.paddleConfig();
        if (cancelled) return;
        if (!config.clientToken) throw new Error('Paddle client token is not configured.');

        // Has to happen before Initialize: Paddle.js picks `_ptxn` out of the address bar itself and would
        // open a second, unlocalized overlay next to the one opened below.
        if (transactionId) window.history.replaceState(null, '', stripTransactionId(window.location.href));

        const locale = paddleLocaleFor(language);
        const paddle = await ensurePaddleReady(
          config.clientToken,
          config.environment,
          {
            onCompleted: () => {
              completed.current = true;
              navigate('/checkout/success', { replace: true });
            },
            onClosed: () => {
              if (!completed.current) navigate('/', { replace: true });
            }
          },
          locale
        );
        if (cancelled) return;
        // No transaction means this was a bare visit - Paddle.js is loaded and initialized, which is what
        // the default payment link has to provide, but there is nothing to open.
        if (transactionId) openPaddleCheckout(paddle, { transactionId, settings: { locale } });
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : String(err));
      }
    })();

    return () => { cancelled = true; };
  }, [transactionId, language, navigate]);

  // A bare visit used to redirect to /pricing, which sits behind the auth guard - so this page, which
  // Paddle requires to be a public page carrying Paddle.js, bounced an anonymous visitor into a login
  // wall. It now stands on its own and points at the public price list instead.
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
        {error ? <ErrorState message={t('checkout.error')} />
          : transactionId ? <LoadingState title={t('checkout.loadingTitle')} description={t('checkout.loadingLead')} />
            : (
              <div className="authIntro">
                <h1>{t('checkout.idleTitle')}</h1>
                <p>{t('checkout.idleLead')}</p>
                <Link to="/plans" className="button button--primary">{t('landing.navPricing')}</Link>
              </div>
            )}
      </section>
      <PublicFooter compact />
    </main>
  );
}
