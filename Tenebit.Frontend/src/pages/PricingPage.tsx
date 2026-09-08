import { Tag, Zap } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { api } from '../api/endpoints';
import { ensurePaddleReady, openPaddleCheckout } from '../api/paddleClient';
import { Button } from '../components/Button';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { TextInput } from '../components/FormFields';
import { PLANS, PricingCards, type BillingInterval, type PlanDef } from '../components/PricingCards';
import { useAsyncData } from '../hooks/useAsyncData';
import { useI18n } from '../i18n/I18nProvider';
import type { PlanChangePreview, PromoCodeValidation } from '../types/domain';
import { formatDate } from '../utils/format';

function planPrice(plan: PlanDef, interval: BillingInterval): number {
  return interval === 'annual' ? plan.annualPrice : plan.price;
}

export function PricingPage() {
  const { t, language } = useI18n();
  const subscription = useAsyncData(api.subscription, []);
  const [upgrading, setUpgrading] = useState(false);
  const [selectedPlan, setSelectedPlan] = useState<PlanDef | null>(null);
  const [selectedInterval, setSelectedInterval] = useState<BillingInterval>('monthly');
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [promoOpen, setPromoOpen] = useState(false);
  const [promoInput, setPromoInput] = useState('');
  const [promoStatus, setPromoStatus] = useState<'idle' | 'checking' | 'applied' | 'error'>('idle');
  const [promoError, setPromoError] = useState<string | null>(null);
  const [appliedPromo, setAppliedPromo] = useState<PromoCodeValidation | null>(null);
  const [portalLoading, setPortalLoading] = useState(false);
  const [cancellingScheduled, setCancellingScheduled] = useState(false);
  const [preview, setPreview] = useState<PlanChangePreview | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const previewRequestRef = useRef(0);
  const currentPlanKey = subscription.data?.planKey.toLowerCase() ?? null;
  const currentInterval = (subscription.data?.billingInterval.toLowerCase() ?? 'monthly') as BillingInterval;
  // A live paid Paddle subscription already exists - switching plans must reuse it (Paddle proration)
  // instead of a fresh checkout, which only ever creates a first subscription.
  const hasLivePaidSubscription = !!subscription.data && subscription.data.planKey !== 'free' && subscription.data.status !== 'Cancelled';
  const currentPlan = currentPlanKey ? PLANS.find(p => p.key === currentPlanKey) ?? null : null;
  // A plan change to a cheaper (plan, interval) combination is scheduled for the end of the paid period,
  // not charged now (see SubscriptionService.ChangePlanAsync's price comparison, which now compares the
  // actual price of the requested interval, not always MonthlyPrice) - a promo code has nothing to
  // discount there, so only offer it for a real upgrade (equal-or-higher price now), which bills
  // immediately.
  const isDowngrade = hasLivePaidSubscription && !!selectedPlan && !!currentPlan
    && planPrice(selectedPlan, selectedInterval) < planPrice(currentPlan, currentInterval);

  // Paddle's overlay checkout never navigates the browser on its own after a successful payment (unlike a
  // classic hosted-redirect flow) - without this, the app keeps showing whatever plan/asset-limit state it
  // had cached before the purchase (Layout's own sidebar subscription fetch in particular only ever runs
  // once per app session) until the customer manually hard-refreshes. A full navigation here guarantees
  // every cached bit of subscription state across the whole app is fetched fresh.
  function handlePaddleCheckoutCompleted() {
    window.location.href = '/dashboard?checkout=success';
  }

  // Paddle.js is only ever needed for a brand-new checkout (no live paid subscription yet) - loading it
  // eagerly for every visitor keeps the pricing page paying that cost even for existing paid customers who
  // will only ever use the in-app plan-change/preview flow below.
  useEffect(() => {
    if (hasLivePaidSubscription) return;
    api.paddleConfig()
      .then(config => { if (config.clientToken) ensurePaddleReady(config.clientToken, config.environment, handlePaddleCheckoutCompleted); })
      .catch(() => { /* Paddle not configured yet - checkout will surface its own error when attempted. */ });
  }, [hasLivePaidSubscription]);

  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(() => setMessage(null), message.type === 'success' ? 3500 : 6500);
    return () => window.clearTimeout(timeout);
  }, [message]);

  function openCheckout(plan: PlanDef, interval: BillingInterval) {
    setPromoOpen(false);
    setPromoInput('');
    setPromoStatus('idle');
    setPromoError(null);
    setAppliedPromo(null);
    setSelectedPlan(plan);
    setSelectedInterval(interval);
    setPreview(null);
    setPreviewError(null);
    if (!hasLivePaidSubscription) return;

    // An existing paid subscription changes via real-time Paddle proration (credit for the old plan's
    // unused time against the new plan) rather than the new plan's flat list price - fetch the exact
    // number before the customer commits, instead of applying it silently and hoping they trust the
    // toast (the amount was always correct, but with no number shown a real charge read as "nothing
    // happened").
    const requestId = ++previewRequestRef.current;
    setPreviewLoading(true);
    api.previewPlanChange(plan.key, interval)
      .then(result => { if (previewRequestRef.current === requestId) setPreview(result); })
      .catch(error => { if (previewRequestRef.current === requestId) setPreviewError(error instanceof Error ? error.message : String(error)); })
      .finally(() => { if (previewRequestRef.current === requestId) setPreviewLoading(false); });
  }

  function closeCheckout() {
    setSelectedPlan(null);
  }

  async function applyPromoCode() {
    if (!selectedPlan || !promoInput.trim()) return;
    setPromoStatus('checking');
    setPromoError(null);
    try {
      const validation = await api.validatePromoCode(selectedPlan.key, selectedInterval, promoInput.trim());
      setAppliedPromo(validation);
      setPromoStatus('applied');
    } catch (error) {
      setAppliedPromo(null);
      setPromoStatus('error');
      setPromoError(error instanceof Error ? error.message : String(error));
    }
  }

  function promoDurationText(promo: PromoCodeValidation): string {
    if (promo.durationType === 'Forever') return t('pricing.checkout.promoDurationForever');
    if (promo.durationType === 'Repeating') return t('pricing.checkout.promoDurationMonths', { months: promo.durationInMonths ?? 0 });
    return t('pricing.checkout.promoDurationOnce');
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
        const result = await api.changeSubscriptionPlan(plan.key, selectedInterval, promoCode);
        await subscription.reload();
        const successText = result.lastChargeAmount != null
          ? t('pricing.changePlanChargedSuccess', { plan: plan.name, amount: result.lastChargeAmount.toFixed(2), currency: result.lastChargeCurrency ?? 'EUR' })
          : result.pendingPlanEffectiveAt
            ? t('pricing.scheduledChange', { plan: result.pendingPlanName ?? plan.name, date: formatDate(result.pendingPlanEffectiveAt) })
            : t('pricing.changePlanSuccess', { plan: plan.name });
        setMessage({ type: 'success', text: successText });
        setUpgrading(false);
      } else {
        const config = await api.paddleConfig();
        if (!config.clientToken) throw new Error('Paddle is not configured yet.');
        const paddle = await ensurePaddleReady(config.clientToken, config.environment, handlePaddleCheckoutCompleted);
        const params = await api.checkoutParams(plan.key, selectedInterval, promoCode);
        openPaddleCheckout(paddle, {
          items: [{ priceId: params.priceId, quantity: 1 }],
          customer: { id: params.customerId },
          discountId: params.discountId,
          customData: params.affiliateCode ? { affiliate_code: params.affiliateCode } : undefined,
          settings: { successUrl: `${window.location.origin}/dashboard?checkout=success`, allowQuantity: false }
        });
        setUpgrading(false);
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
      const portalUrl = await api.createBillingPortalSession();
      window.location.assign(portalUrl);
    } catch (error) {
      setMessage({ type: 'error', text: t('pricing.upgradeError', { error: String(error) }) });
      setPortalLoading(false);
    }
  }

  const totalPrice = appliedPromo ? appliedPromo.discountedPrice : selectedPlan ? planPrice(selectedPlan, selectedInterval) : 0;
  const periodSuffix = selectedInterval === 'annual' ? t('pricing.billing.perYear') : t('landing.perMonth');

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
        renderCta={(plan, interval) => {
          const isCurrent = currentPlanKey === plan.key && currentInterval === interval;
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
                onClick={() => openCheckout(plan, interval)}
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
        description={selectedPlan ? t(hasLivePaidSubscription ? 'pricing.confirmChangePlan' : 'pricing.confirmUpgrade', { plan: selectedPlan.name, price: totalPrice.toFixed(2), period: periodSuffix }) : ''}
        confirmLabel={selectedPlan ? t(hasLivePaidSubscription ? 'pricing.changePlan' : 'pricing.upgrade', { plan: selectedPlan.name }) : ''}
        confirmDisabled={hasLivePaidSubscription && (previewLoading || !!previewError || !preview)}
        onConfirm={confirmUpgrade}
        onClose={closeCheckout}
      >
        {selectedPlan && (
          <>
            <p className="pricing-confirm-detail">
              {t(hasLivePaidSubscription ? 'pricing.confirmChangePlanDetail' : 'pricing.confirmUpgradeDetail', { limit: new Intl.NumberFormat(language).format(selectedPlan.limit) })}
            </p>

            {hasLivePaidSubscription ? (
              <div style={{ marginTop: 14 }}>
                {previewLoading ? (
                  <p className="pricing-confirm-detail">{t('pricing.checkout.previewLoading')}</p>
                ) : previewError ? (
                  <p className="formMessage formMessage--error">{previewError}</p>
                ) : preview?.chargesNow ? (
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 16, fontWeight: 700, paddingTop: 8, borderTop: '1px solid var(--border)' }}>
                    <span>{t('pricing.checkout.dueNow')}</span>
                    <span>{preview.amountDue.toFixed(2)} {preview.currency}</span>
                  </div>
                ) : preview ? (
                  <p className="pricing-confirm-detail">
                    {t('pricing.checkout.downgradeNotice', { date: formatDate(preview.effectiveAt!) })}
                  </p>
                ) : null}
              </div>
            ) : (
              <>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 14, marginTop: 14 }}>
                  <span>{t('pricing.checkout.subtotal')}</span>
                  <span>{planPrice(selectedPlan, selectedInterval).toFixed(2)} €{periodSuffix}</span>
                </div>
                {appliedPromo && (
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 14, marginTop: 6, color: 'var(--success, #047857)' }}>
                    <span>{t('pricing.checkout.discount')} ({appliedPromo.code})</span>
                    <span>-{(appliedPromo.originalPrice - appliedPromo.discountedPrice).toFixed(2)} €</span>
                  </div>
                )}
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 16, fontWeight: 700, marginTop: 8, paddingTop: 8, borderTop: '1px solid var(--border)' }}>
                  <span>{t('pricing.checkout.total')}</span>
                  <span>{totalPrice.toFixed(2)} €{periodSuffix}</span>
                </div>
              </>
            )}

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
                {promoStatus === 'applied' && appliedPromo && (
                  <>
                    <p className="formMessage formMessage--success" style={{ marginTop: 8 }}>{t('pricing.checkout.promoApplied', { code: appliedPromo.code })}</p>
                    <p className="pricing-confirm-detail">{promoDurationText(appliedPromo)}</p>
                    {appliedPromo.description && <p className="pricing-confirm-detail">{appliedPromo.description}</p>}
                  </>
                )}
              </div>
            )}
          </>
        )}
      </ConfirmDialog>
    </div>
  );
}
