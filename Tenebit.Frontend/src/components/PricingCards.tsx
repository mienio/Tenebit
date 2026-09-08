import { ClipboardList, Info, KeyRound, MapPin, Package, Sparkles, User, Users, type LucideIcon } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { useI18n } from '../i18n/I18nProvider';
import { Card } from './Card';

export type BillingInterval = 'monthly' | 'annual';

export interface PlanDef {
  key: string;
  name: string;
  price: number;
  /** Always exactly 10x price ("2 months free") - see SubscriptionPlan.AnnualPrice on the backend, the
   * source of truth this mirrors. */
  annualPrice: number;
  /** Real, enforced ceiling - applies independently to every resource type (see
   * OrganizationSubscription.GetResourceLimit on the backend), not a shared pool. */
  limit: number;
  badge?: 'free' | 'recommended' | 'max';
}

export const PLANS: PlanDef[] = [
  { key: 'free', name: 'Free', price: 0, annualPrice: 0, limit: 10, badge: 'free' },
  { key: 'starter', name: 'Starter', price: 11.95, annualPrice: 119.5, limit: 100 },
  { key: 'growth', name: 'Growth', price: 28.95, annualPrice: 289.5, limit: 300, badge: 'recommended' },
  { key: 'business', name: 'Business', price: 58.95, annualPrice: 589.5, limit: 1000 },
  { key: 'enterprise', name: 'Max', price: 98.95, annualPrice: 989.5, limit: 10000, badge: 'max' },
];

// Every plan's limit is enforced separately for each of these - adding one asset never eats into the
// employee, license or procedure allowance. Shown once as a legend above the cards instead of spelling
// out "100 assets / 100 employees / 100 licenses / ..." on every single plan card.
// Job profiles and equipment categories are enforced by the same limit too (GetResourceLimit applies
// there as well), but stay off this list on purpose - an org realistically never has enough of either
// to make that limit a selling point, so calling it out would just be noise.
const LIMIT_CATEGORIES: { key: string; icon: LucideIcon }[] = [
  { key: 'assets', icon: Package },
  { key: 'people', icon: User },
  { key: 'procedures', icon: ClipboardList },
  { key: 'licenses', icon: KeyRound },
  { key: 'locations', icon: MapPin },
  { key: 'teams', icon: Users },
];

export function PricingCards({ renderCta }: { renderCta: (plan: PlanDef, interval: BillingInterval) => ReactNode }) {
  const { t, language } = useI18n();
  const [billingInterval, setBillingInterval] = useState<BillingInterval>('monthly');
  const formatLimit = (limit: number) => new Intl.NumberFormat(language).format(limit);
  const formatPrice = (amount: number) => new Intl.NumberFormat(language, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(amount);

  return (
    <div className="pricing-section">
      <div className="pricing-legend">
        <div className="pricing-legend__head">
          <Info size={20} />
          <div>
            <div className="pricing-legend__title">{t('pricing.legend.title')}</div>
            <div className="pricing-legend__body">{t('pricing.legend.body')}</div>
          </div>
        </div>
        <div className="pricing-legend__grid">
          {LIMIT_CATEGORIES.map(({ key, icon: Icon }) => (
            <span className="pricing-chip" key={key}>
              <Icon size={14} />
              <span>{t(`pricing.legend.${key}`)}</span>
            </span>
          ))}
          <span className="pricing-chip pricing-chip--more">{t('pricing.legend.more')}</span>
        </div>
      </div>

      <div className="pricing-billingToggle">
        <div className="pricing-billingToggle__group" role="tablist" aria-label={t('pricing.billing.toggleLabel')}>
          <button
            type="button"
            role="tab"
            aria-selected={billingInterval === 'monthly'}
            className={`pricing-billingToggle__pill${billingInterval === 'monthly' ? ' pricing-billingToggle__pill--active' : ''}`}
            onClick={() => setBillingInterval('monthly')}
          >
            {t('pricing.billing.monthly')}
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={billingInterval === 'annual'}
            className={`pricing-billingToggle__pill pricing-billingToggle__pill--annual${billingInterval === 'annual' ? ' pricing-billingToggle__pill--active' : ''}`}
            onClick={() => setBillingInterval('annual')}
          >
            {t('pricing.billing.annual')}
            <span className="pricing-billingToggle__badge">
              <Sparkles size={13} />
              {t('pricing.billing.annualBadge')}
            </span>
          </button>
        </div>
      </div>

      <div className="pricing-cards">
        {PLANS.map((plan) => {
          const isAnnual = billingInterval === 'annual' && plan.price > 0;
          const headlinePrice = isAnnual ? plan.annualPrice : plan.price;
          const monthlyEquivalent = plan.annualPrice / 12;

          return (
            <Card
              key={plan.key}
              className={`pricing-card${plan.badge === 'recommended' ? ' pricing-card--featured' : ''}${plan.badge === 'max' ? ' pricing-card--max' : ''}`}
            >
              {plan.badge === 'recommended' && (
                <span className="pricing-card__badge pricing-card__badge--recommended">{t('pricing.badge.recommended')}</span>
              )}
              {plan.badge === 'free' && (
                <span className="pricing-card__badge pricing-card__badge--free">{t('pricing.badge.free')}</span>
              )}
              {plan.badge === 'max' && (
                <span className="pricing-card__badge pricing-card__badge--max">{t('pricing.badge.max')}</span>
              )}

              <div>
                <h3>{plan.name}</h3>
                <div style={{ marginTop: '8px' }}>
                  <span className="pricing-price">
                    {headlinePrice === 0 ? '0 €' : `${formatPrice(headlinePrice)} €`}
                    <small>{isAnnual ? t('pricing.billing.perYear') : t('landing.perMonth')}</small>
                  </span>
                </div>
                {isAnnual && (
                  <div className="pricing-price__equivalent">
                    {t('pricing.billing.equivalentPerMonth', { amount: formatPrice(monthlyEquivalent) })}
                  </div>
                )}
                <p style={{ marginTop: '12px', color: 'var(--muted)' }}>
                  {t(`pricing.${plan.key}.desc`)}
                </p>
              </div>

              <div className="pricing-hero-limit">
                <div className="pricing-hero-limit__num">{t('pricing.upToPrefix')} {formatLimit(plan.limit)}</div>
                <div className="pricing-hero-limit__cap">{t('pricing.perCategory')}</div>
              </div>

              {renderCta(plan, billingInterval)}
            </Card>
          );
        })}
      </div>
    </div>
  );
}
