import { Tag, Zap } from 'lucide-react';
import { useEffect, useState } from 'react';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { Field, TextInput } from '../components/FormFields';
import { PricingCards, type PlanDef } from '../components/PricingCards';
import { useAsyncData } from '../hooks/useAsyncData';
import { useI18n } from '../i18n/I18nProvider';
import type { PromoCodeValidation } from '../types/domain';

export function PricingPage() {
  const { t } = useI18n();
  const subscription = useAsyncData(api.subscription, []);
  const [upgrading, setUpgrading] = useState(false);
  const [selectedPlan, setSelectedPlan] = useState<PlanDef | null>(null);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [promoInput, setPromoInput] = useState('');
  const [promoStatus, setPromoStatus] = useState<'idle' | 'checking' | 'applied' | 'error'>('idle');
  const [promoError, setPromoError] = useState<string | null>(null);
  const [appliedPromo, setAppliedPromo] = useState<PromoCodeValidation | null>(null);
  const currentPlanKey = subscription.data?.planKey.toLowerCase() ?? null;

  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(() => setMessage(null), message.type === 'success' ? 3500 : 6500);
    return () => window.clearTimeout(timeout);
  }, [message]);

  function openCheckout(plan: PlanDef) {
    setPromoInput('');
    setPromoStatus('idle');
    setPromoError(null);
    setAppliedPromo(null);
    setSelectedPlan(plan);
  }

  function closeCheckout() {
    setSelectedPlan(null);
  }

  async function applyPromoCode() {
    if (!selectedPlan || !promoInput.trim()) return;
    setPromoStatus('checking');
    setPromoError(null);
    try {
      const validation = await api.validatePromoCode(selectedPlan.key, promoInput.trim());
      setAppliedPromo(validation);
      setPromoStatus('applied');
    } catch (error) {
      setAppliedPromo(null);
      setPromoStatus('error');
      setPromoError(error instanceof Error ? error.message : String(error));
    }
  }

  function removePromoCode() {
    setAppliedPromo(null);
    setPromoInput('');
    setPromoStatus('idle');
    setPromoError(null);
  }

  async function confirmUpgrade() {
    if (!selectedPlan) return;
    const plan = selectedPlan;
    const promoCode = appliedPromo?.code;
    setSelectedPlan(null);
    setUpgrading(true);
    try {
      const checkoutUrl = await api.createCheckoutSession(plan.key, '/dashboard?checkout=success', '/pricing?checkout=cancelled', promoCode);
      window.location.assign(checkoutUrl);
    } catch (error) {
      setMessage({ type: 'error', text: t('pricing.upgradeError', { error: String(error) }) });
      setUpgrading(false);
    }
  }

  const totalPrice = appliedPromo ? appliedPromo.discountedPrice : selectedPlan?.price ?? 0;

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
                onClick={() => openCheckout(plan)}
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
        description={selectedPlan ? t('pricing.confirmUpgrade', { plan: selectedPlan.name, price: totalPrice.toFixed(2) }) : ''}
        confirmLabel={selectedPlan ? t('pricing.upgrade', { plan: selectedPlan.name }) : ''}
        onConfirm={confirmUpgrade}
        onClose={closeCheckout}
      >
        {selectedPlan && (
          <>
            <p className="pricing-confirm-detail">
              {t('pricing.confirmUpgradeDetail', { limit: selectedPlan.limitLabel })}
            </p>

            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 14, marginTop: 14 }}>
              <span>{t('pricing.checkout.subtotal')}</span>
              <span>{selectedPlan.price.toFixed(2)} €{t('landing.perMonth')}</span>
            </div>
            {appliedPromo && (
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 14, marginTop: 6, color: 'var(--success, #047857)' }}>
                <span>{t('pricing.checkout.discount')} ({appliedPromo.code})</span>
                <span>-{(appliedPromo.originalPrice - appliedPromo.discountedPrice).toFixed(2)} €</span>
              </div>
            )}
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 16, fontWeight: 700, marginTop: 8, paddingTop: 8, borderTop: '1px solid var(--border)' }}>
              <span>{t('pricing.checkout.total')}</span>
              <span>{totalPrice.toFixed(2)} €{t('landing.perMonth')}</span>
            </div>

            <div style={{ marginTop: 16 }}>
              <Field label={t('pricing.checkout.promoLabel')}>
                <div style={{ display: 'flex', gap: 8 }}>
                  <TextInput
                    value={promoInput}
                    onChange={e => setPromoInput(e.target.value.toUpperCase())}
                    placeholder={t('pricing.checkout.promoPlaceholder')}
                    disabled={promoStatus === 'applied'}
                  />
                  {promoStatus === 'applied' ? (
                    <Button type="button" variant="secondary" onClick={removePromoCode}>{t('pricing.checkout.promoRemove')}</Button>
                  ) : (
                    <Button
                      type="button"
                      variant="secondary"
                      icon={<Tag size={16} />}
                      onClick={applyPromoCode}
                      disabled={!promoInput.trim() || promoStatus === 'checking'}
                    >
                      {promoStatus === 'checking' ? t('pricing.checkout.promoChecking') : t('pricing.checkout.promoApply')}
                    </Button>
                  )}
                </div>
              </Field>
              {promoStatus === 'error' && promoError && <p className="formMessage formMessage--error" style={{ marginTop: 8 }}>{promoError}</p>}
              {promoStatus === 'applied' && <p className="formMessage formMessage--success" style={{ marginTop: 8 }}>{t('pricing.checkout.promoApplied', { code: appliedPromo!.code })}</p>}
            </div>
          </>
        )}
      </ConfirmDialog>
    </div>
  );
}
