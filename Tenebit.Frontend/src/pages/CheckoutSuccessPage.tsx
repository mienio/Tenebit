import { ArrowRight, CheckCircle2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/Button';
import { BrandMark } from '../components/BrandMark';
import { PublicFooter } from '../components/PublicFooter';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';
import { useI18n } from '../i18n/I18nProvider';

/**
 * Where /checkout sends a buyer after Paddle reports `checkout.completed`. Deliberately says the plan is
 * *being* activated rather than that it is active: the entitlement is written by the Paddle webhooks
 * (subscription.created/updated, see SubscriptionService.HandleWebhookAsync), which are the only source
 * of truth for what an organization has paid for. A browser event is a claim made by the shopper's own
 * tab - trusting it here would let anyone grant themselves a plan by opening this URL.
 */
export function CheckoutSuccessPage() {
  const { t } = useI18n();
  const navigate = useNavigate();

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
        <div className="stateBox">
          <CheckCircle2 size={30} />
          <h2>{t('checkout.successTitle')}</h2>
          <p>{t('checkout.successLead')}</p>
          <Button onClick={() => navigate('/')} icon={<ArrowRight size={16} />}>
            {t('checkout.successCta')}
          </Button>
        </div>
      </section>
      <PublicFooter compact />
    </main>
  );
}
