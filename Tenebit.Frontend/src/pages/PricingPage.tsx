import { Zap } from 'lucide-react';
import { useEffect, useState } from 'react';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { PricingCards, type PlanDef } from '../components/PricingCards';
import { useAsyncData } from '../hooks/useAsyncData';
import { useI18n } from '../i18n/I18nProvider';

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

      <PricingCards
        renderCta={(plan) => {
          const isCurrent = currentPlanKey === plan.key;
          const showCta = !isCurrent && plan.key !== 'free';
          if (isCurrent) {
            return (
              <Button variant="secondary" className="pricing-cta" disabled>
                {t('pricing.currentPlan')}
              </Button>
            );
          }
          if (showCta) {
            return (
              <Button
                onClick={() => setSelectedPlan(plan)}
                disabled={upgrading || subscription.isLoading}
                icon={<Zap size={18} />}
                className="pricing-cta"
              >
                {upgrading ? t('pricing.processing') : t('pricing.upgrade', { plan: plan.name })}
              </Button>
            );
          }
          return null;
        }}
      />

      <ConfirmDialog
        open={selectedPlan !== null}
        variant="positive"
        title={t('pricing.confirmUpgradeTitle')}
        description={selectedPlan ? t('pricing.confirmUpgrade', { plan: selectedPlan.name, price: String(selectedPlan.price) }) : ''}
        confirmLabel={selectedPlan ? t('pricing.upgrade', { plan: selectedPlan.name }) : ''}
        onConfirm={confirmUpgrade}
        onClose={() => setSelectedPlan(null)}
      >
        {selectedPlan && (
          <p className="pricing-confirm-detail">
            {t('pricing.confirmUpgradeDetail', { limit: selectedPlan.limitLabel })}
          </p>
        )}
      </ConfirmDialog>
    </div>
  );
}
