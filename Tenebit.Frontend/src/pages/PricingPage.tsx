import { Tag, Zap } from 'lucide-react';
import { useEffect, useState } from 'react';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { TextInput } from '../components/FormFields';
import { PLANS, PricingCards, type PlanDef } from '../components/PricingCards';
import { useAsyncData } from '../hooks/useAsyncData';
import { useI18n } from '../i18n/I18nProvider';
import type { PromoCodeValidation } from '../types/domain';
import { formatDate } from '../utils/format';

export function PricingPage() {
  const { t } = useI18n();
  const subscription = useAsyncData(api.subscription, []);
  const [upgrading, setUpgrading] = useState(false);
  const [selectedPlan, setSelectedPlan] = useState<PlanDef | null>(null);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [promoOpen, setPromoOpen] = useState(false);
  const [promoInput, setPromoInput] = useState('');
  const [promoStatus, setPromoStatus] = useState<'idle' | 'checking' | 'applied' | 'error'>('idle');
  const [promoError, setPromoError] = useState<string | null>(null);
  const [appliedPromo, setAppliedPromo] = useState<PromoCodeValidation | null>(null);
  const [portalLoading, setPortalLoading] = useState(false);
  const [cancellingScheduled, setCancellingScheduled] = useState(false);
  const currentPlanKey = subscription.data?.planKey.toLowerCase() ?? null;
  // A live paid Stripe subscription already exists - switching plans must reuse it (Stripe proration)
  // instead of Checkout, which only ever creates a first subscription and refuses to create a second.
  const hasLivePaidSubscription = !!subscription.data && subscription.data.planKey !== 'free' && subscription.data.status !== 'Cancelled';
  const currentPlan = currentPlanKey ? PLANS.find(p => p.key === currentPlanKey) ?? null : null;
  // A plan change to a cheaper plan is scheduled for the end of the paid period, not charged now (see
  // SubscriptionService.ChangePlanAsync) - a promo code has nothing to discount there, so only offer it
  // for a real upgrade (equal-or-higher price), which bills immediately.
  const isDowngrade = hasLivePaidSubscription && !!selectedPlan && !!currentPlan && selectedPlan.price < currentPlan.price;

  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(() => setMessage(null), message.type === 'success' ? 3500 : 6500);
    return () => window.clearTimeout(timeout);
  }, [message]);

  function openCheckout(plan: PlanDef) {
    setPromoOpen(false);
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
    const isPlanChange = hasLivePaidSubscription;
    setSelectedPlan(null);
    setUpgrading(true);
    try {
      if (isPlanChange) {
        await api.changeSubscriptionPlan(plan.key, promoCode);
        await subscription.reload();
        setMessage({ type: 'success', text: t('pricing.changePlanSuccess', { plan: plan.name }) });
        setUpgrading(false);
      } else {
        const checkoutUrl = await api.createCheckoutSession(plan.key, '/dashboard?checkout=success', '/pricing?checkout=cancelled', promoCode);
        window.location.assign(checkoutUrl);
      }
    } catch (error) {
      setMessage({ type: 'error', text: t('pricing.upgradeError', { error: String(error) }) });
      setUpgrading(false);
    }
  }

  async function cancelScheduledChange() {
    setCancellingScheduled(true);
    try {
      await api.cancelScheduledPlanChange();
      await subscription.reload();
      setMessage({ type: 'success', text: t('pricing.scheduledChangeCancelled') });
    } catch (error) {
      setMessage({ type: 'error', text: t('pricing.upgradeError', { error: String(error) }) });
    } finally {
      setCancellingScheduled(false);
    }
  }

  async function openBillingPortal() {
    setPortalLoading(true);
    try {
      const portalUrl = await api.createBillingPortalSession('/pricing');
      window.location.assign(portalUrl);
    } catch (error) {
      setMessage({ type: 'error', text: t('pricing.upgradeError', { error: String(error) }) });
      setPortalLoading(false);
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

      {subscription.data?.pendingPlanName && subscription.data.pendingPlanEffectiveAt && (
        <div className="pricing-scheduledBanner">
          <span>{t('pricing.scheduledChange', { plan: subscription.data.pendingPlanName, date: formatDate(subscription.data.pendingPlanEffectiveAt) })}</span>
          <button type="button" className="pricing-promoToggle" onClick={cancelScheduledChange} disabled={cancellingScheduled}>
            {t('pricing.cancelScheduledChange')}
          </button>
        </div>
      )}

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
                {upgrading ? t('pricing.processing') : t(hasLivePaidSubscription ? 'pricing.changePlan' : 'pricing.upgrade', { plan: plan.name })}
              </Button>
            );
          }
          return null;
        }}
      />

      {hasLivePaidSubscription && (
        <div className="pricing-manageBilling">
          <Button variant="ghost" onClick={openBillingPortal} disabled={portalLoading}>
            {t('pricing.manageBilling')}
          </Button>
        </div>
      )}

      <ConfirmDialog
        open={selectedPlan !== null}
        variant="positive"
        title={t(hasLivePaidSubscription ? 'pricing.confirmChangePlanTitle' : 'pricing.confirmUpgradeTitle')}
        description={selectedPlan ? t(hasLivePaidSubscription ? 'pricing.confirmChangePlan' : 'pricing.confirmUpgrade', { plan: selectedPlan.name, price: totalPrice.toFixed(2) }) : ''}
        confirmLabel={selectedPlan ? t(hasLivePaidSubscription ? 'pricing.changePlan' : 'pricing.upgrade', { plan: selectedPlan.name }) : ''}
        onConfirm={confirmUpgrade}
        onClose={closeCheckout}
      >
        {selectedPlan && (
          <>
            <p className="pricing-confirm-detail">
              {t(hasLivePaidSubscription ? 'pricing.confirmChangePlanDetail' : 'pricing.confirmUpgradeDetail', { limit: selectedPlan.limitLabel })}
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

            {!isDowngrade && (
              <div style={{ marginTop: 16 }}>
                {!promoOpen && promoStatus !== 'applied' ? (
                  <button type="button" className="pricing-promoToggle" onClick={() => setPromoOpen(true)}>
                    {t('pricing.checkout.promoLabel')}
                  </button>
                ) : (
                  <div className="pricing-promoField">
                    <TextInput
                      value={promoInput}
                      onChange={e => setPromoInput(e.target.value.toUpperCase())}
                      disabled={promoStatus === 'applied'}
                      autoFocus
                    />
                    {promoStatus === 'applied' ? (
                      <Button type="button" variant="ghost" onClick={removePromoCode}>{t('pricing.checkout.promoRemove')}</Button>
                    ) : (
                      <Button
                        type="button"
                        variant="ghost"
                        icon={<Tag size={14} />}
                        onClick={applyPromoCode}
                        disabled={!promoInput.trim() || promoStatus === 'checking'}
                      >
                        {promoStatus === 'checking' ? t('pricing.checkout.promoChecking') : t('pricing.checkout.promoApply')}
                      </Button>
                    )}
                  </div>
                )}
                {promoStatus === 'error' && promoError && <p className="formMessage formMessage--error" style={{ marginTop: 8 }}>{promoError}</p>}
                {promoStatus === 'applied' && <p className="formMessage formMessage--success" style={{ marginTop: 8 }}>{t('pricing.checkout.promoApplied', { code: appliedPromo!.code })}</p>}
              </div>
            )}
          </>
        )}
      </ConfirmDialog>
    </div>
  );
}
