import { Check, Zap } from 'lucide-react';
import { useEffect, useState } from 'react';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { useI18n } from '../i18n/I18nProvider';

export function PricingPage() {
  const { t } = useI18n();
  const [upgrading, setUpgrading] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(() => setMessage(null), message.type === 'success' ? 3500 : 6500);
    return () => window.clearTimeout(timeout);
  }, [message]);

  async function confirmUpgrade() {
    setConfirmOpen(false);
    setUpgrading(true);
    try {
      const returnBase = window.location.origin;
      const checkoutUrl = await api.createCheckoutSession(`${returnBase}/dashboard?checkout=success`, `${returnBase}/pricing?checkout=cancelled`);
      window.location.assign(checkoutUrl);
    } catch (error) {
      setMessage({ type: 'error', text: t('pricing.upgradeError', { error: String(error) }) });
      setUpgrading(false);
    }
  }

  return (
    <div className="pageStack">
      {message && (
        <div className="toastStack" aria-live="polite">
          <div className={`toast toast--${message.type}`}>{message.text}</div>
        </div>
      )}

      <div className="pricing-hero">
        <h1>{t('pricing.title')}</h1>
        <p>{t('pricing.lead')}</p>
      </div>

      <div className="pricing-cards">
        <Card className="pricing-card">
          <div>
            <h3>Free</h3>
            <div style={{ marginTop: '8px' }}>
              <span className="pricing-price">
                $0
                <small>{t('landing.perMonth')}</small>
              </span>
            </div>
            <p style={{ marginTop: '12px', color: 'var(--muted)' }}>
              {t('pricing.freeDesc')}
            </p>
          </div>

          <ul className="pricing-features">
            <li><Check size={20} /><span>{t('pricing.free.f1')}</span></li>
            <li><Check size={20} /><span>{t('pricing.free.f2')}</span></li>
            <li><Check size={20} /><span>{t('pricing.free.f3')}</span></li>
            <li><Check size={20} /><span>{t('pricing.free.f4')}</span></li>
            <li><Check size={20} /><span>{t('pricing.free.f5')}</span></li>
            <li><Check size={20} /><span>{t('pricing.free.f6')}</span></li>
          </ul>

          <Button variant="secondary" className="pricing-cta" disabled>
            {t('pricing.currentPlan')}
          </Button>
        </Card>

        <Card className="pricing-card pricing-card--featured">
          <div>
            <h3>Pro</h3>
            <div style={{ marginTop: '8px' }}>
              <span className="pricing-price">
                $10
                <small>{t('landing.perMonth')}</small>
              </span>
            </div>
            <p style={{ marginTop: '12px', color: 'var(--muted)' }}>
              {t('pricing.proDesc')}
            </p>
          </div>

          <ul className="pricing-features">
            <li><Check size={20} /><span><strong>{t('pricing.pro.f1')}</strong></span></li>
            <li><Check size={20} /><span><strong>{t('pricing.pro.f2')}</strong></span></li>
            <li><Check size={20} /><span>{t('pricing.pro.f3')}</span></li>
            <li><Check size={20} /><span>{t('pricing.pro.f4')}</span></li>
            <li><Check size={20} /><span>{t('pricing.pro.f5')}</span></li>
            <li><Check size={20} /><span>{t('pricing.pro.f6')}</span></li>
            <li><Check size={20} /><span>{t('pricing.pro.f7')}</span></li>
            <li><Check size={20} /><span>{t('pricing.pro.f8')}</span></li>
            <li><Check size={20} /><span>{t('pricing.pro.f9')}</span></li>
          </ul>

          <Button
            onClick={() => setConfirmOpen(true)}
            disabled={upgrading}
            icon={<Zap size={18} />}
            className="pricing-cta"
          >
            {upgrading ? t('pricing.processing') : t('pricing.upgrade')}
          </Button>
        </Card>
      </div>

      <ConfirmDialog
        open={confirmOpen}
        title={t('pricing.confirmUpgradeTitle')}
        description={t('pricing.confirmUpgrade')}
        confirmLabel={t('pricing.upgrade')}
        onConfirm={confirmUpgrade}
        onClose={() => setConfirmOpen(false)}
      />

      <div style={{ marginTop: '60px', textAlign: 'center', maxWidth: '720px', margin: '60px auto 0' }}>
        <h2 style={{ fontSize: '28px', marginBottom: '16px' }}>{t('pricing.questionsTitle')}</h2>
        <p style={{ color: 'var(--muted)', lineHeight: '1.6', marginBottom: '24px' }}>
          {t('pricing.questionsDesc')}
        </p>
        <a href="mailto:kontakt@tenebit.app"><Button variant="secondary">{t('pricing.contactTeam')}</Button></a>
      </div>
    </div>
  );
}
