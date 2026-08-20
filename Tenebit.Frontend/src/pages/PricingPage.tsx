import { Check, Zap } from 'lucide-react';
import { useEffect, useState } from 'react';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { useAsyncData } from '../hooks/useAsyncData';
import { useI18n } from '../i18n/I18nProvider';

interface PlanDef {
  key: string;
  name: string;
  price: number;
  limitLabel: string;
  featureCount: number;
  badge?: 'free' | 'recommended';
}

const PLANS: PlanDef[] = [
  { key: 'free', name: 'Free', price: 0, limitLabel: '10', featureCount: 1, badge: 'free' },
  { key: 'starter', name: 'Starter', price: 12, limitLabel: '100', featureCount: 1 },
  { key: 'growth', name: 'Growth', price: 29, limitLabel: '300', featureCount: 1, badge: 'recommended' },
  { key: 'business', name: 'Business', price: 59, limitLabel: '1000', featureCount: 1 },
  { key: 'enterprise', name: 'Scale', price: 99, limitLabel: '1000+', featureCount: 1 },
];

export function PricingPage() {
  const { t } = useI18n();
  const subscription = useAsyncData(api.subscription, []);
  const [upgrading, setUpgrading] = useState(false);
  const [selectedPlan, setSelectedPlan] = useState<PlanDef | null>(null);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const currentPlanKey = subscription.data?.planKey.toLowerCase() ?? null;

  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(() => setMessage(null), message.type === 'success' ? 3500 : 6500);
    return () => window.clearTimeout(timeout);
  }, [message]);

  async function confirmUpgrade() {
    if (!selectedPlan) return;
    const plan = selectedPlan;
    setSelectedPlan(null);
    setUpgrading(true);
    try {
      const checkoutUrl = await api.createCheckoutSession(plan.key, '/dashboard?checkout=success', '/pricing?checkout=cancelled');
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
        {PLANS.map((plan) => {
          const isCurrent = currentPlanKey === plan.key;
          const showCta = !isCurrent && plan.key !== 'free';
          return (
            <Card key={plan.key} className={`pricing-card${plan.badge === 'recommended' ? ' pricing-card--featured' : ''}`}>
              {plan.badge === 'recommended' && (
                <span className="pricing-card__badge pricing-card__badge--recommended">{t('pricing.badge.recommended')}</span>
              )}
              {plan.badge === 'free' && (
                <span className="pricing-card__badge pricing-card__badge--free">{t('pricing.badge.free')}</span>
              )}

              <div>
                <h3>{plan.name}</h3>
                <div style={{ marginTop: '8px' }}>
                  <span className="pricing-price">
                    {plan.price === 0 ? '0 €' : `${plan.price} €`}
                    <small>{t('landing.perMonth')}</small>
                  </span>
                </div>
                <p style={{ marginTop: '12px', color: 'var(--muted)' }}>
                  {t(`pricing.${plan.key}.desc`)}
                </p>
              </div>

              <ul className="pricing-features">
                {Array.from({ length: plan.featureCount }, (_, i) => i + 1).map((n) => (
                  <li key={n}>
                    <Check size={20} />
                    <span>{t(`pricing.${plan.key}.f${n}`)}</span>
                  </li>
                ))}
              </ul>

              {isCurrent ? (
                <Button variant="secondary" className="pricing-cta" disabled>
                  {t('pricing.currentPlan')}
                </Button>
              ) : showCta ? (
                <Button
                  onClick={() => setSelectedPlan(plan)}
                  disabled={upgrading || subscription.isLoading}
                  icon={<Zap size={18} />}
                  className="pricing-cta"
                >
                  {upgrading ? t('pricing.processing') : t('pricing.upgrade', { plan: plan.name })}
                </Button>
              ) : null}
            </Card>
          );
        })}
      </div>

      <ConfirmDialog
        open={selectedPlan !== null}
        variant="positive"
        title={t('pricing.confirmUpgradeTitle')}
        description={selectedPlan ? t('pricing.confirmUpgrade', { plan: selectedPlan.name, price: String(selectedPlan.price) }) : ''}
        confirmLabel={selectedPlan ? t('pricing.upgrade', { plan: selectedPlan.name }) : ''}
        onConfirm={confirmUpgrade}
        onClose={() => setSelectedPlan(null)}
      />
    </div>
  );
}
