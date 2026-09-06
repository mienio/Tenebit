import { ClipboardList, Info, KeyRound, MapPin, Package, User, Users } from 'lucide-react';
import type { ComponentType, ReactNode } from 'react';
import { useI18n } from '../i18n/I18nProvider';
import { Card } from './Card';

export interface PlanDef {
  key: string;
  name: string;
  price: number;
  /** Real, enforced ceiling - applies independently to each of the 8 categories below (see
   * OrganizationSubscription.GetResourceLimit on the backend), not a shared pool. */
  limit: number;
  badge?: 'free' | 'recommended' | 'max';
}

export const PLANS: PlanDef[] = [
  { key: 'free', name: 'Free', price: 0, limit: 10, badge: 'free' },
  { key: 'starter', name: 'Starter', price: 11.95, limit: 100 },
  { key: 'growth', name: 'Growth', price: 28.95, limit: 300, badge: 'recommended' },
  { key: 'business', name: 'Business', price: 58.95, limit: 1000 },
  { key: 'enterprise', name: 'Max', price: 98.95, limit: 10000, badge: 'max' },
];

// Every plan's limit is enforced separately for each of these - adding one asset never eats into the
// employee, license or procedure allowance. Shown once as a legend above the cards instead of spelling
// out "100 assets / 100 employees / 100 licenses / ..." on every single plan card.
// Job profiles and equipment categories are enforced by the same limit too (GetResourceLimit applies
// there as well), but stay off this list on purpose - an org realistically never has enough of either
// to make that limit a selling point, so calling it out would just be noise.
const LIMIT_CATEGORIES: { key: string; icon: ComponentType<{ size?: number }> }[] = [
  { key: 'assets', icon: Package },
  { key: 'people', icon: User },
  { key: 'procedures', icon: ClipboardList },
  { key: 'licenses', icon: KeyRound },
  { key: 'locations', icon: MapPin },
  { key: 'teams', icon: Users },
];

export function PricingCards({ renderCta }: { renderCta: (plan: PlanDef) => ReactNode }) {
  const { t, language } = useI18n();
  const formatLimit = (limit: number) => new Intl.NumberFormat(language).format(limit);

  return (
    <>
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
        </div>
      </div>

      <div className="pricing-cards">
        {PLANS.map((plan) => (
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
                  {plan.price === 0 ? '0 €' : `${plan.price} €`}
                  <small>{t('landing.perMonth')}</small>
                </span>
              </div>
              <p style={{ marginTop: '12px', color: 'var(--muted)' }}>
                {t(`pricing.${plan.key}.desc`)}
              </p>
            </div>

            <div className="pricing-hero-limit">
              <div className="pricing-hero-limit__num">{t('pricing.upToPrefix')} {formatLimit(plan.limit)}</div>
              <div className="pricing-hero-limit__cap">{t('pricing.perCategory')}</div>
            </div>

            {renderCta(plan)}
          </Card>
        ))}
      </div>
    </>
  );
}
