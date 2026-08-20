import { Check } from 'lucide-react';
import type { ReactNode } from 'react';
import { useI18n } from '../i18n/I18nProvider';
import { Card } from './Card';

export interface PlanDef {
  key: string;
  name: string;
  price: number;
  limitLabel: string;
  featureCount: number;
  badge?: 'free' | 'recommended';
}

export const PLANS: PlanDef[] = [
  { key: 'free', name: 'Free', price: 0, limitLabel: '10', featureCount: 1, badge: 'free' },
  { key: 'starter', name: 'Starter', price: 12, limitLabel: '100', featureCount: 1 },
  { key: 'growth', name: 'Growth', price: 29, limitLabel: '300', featureCount: 1, badge: 'recommended' },
  { key: 'business', name: 'Business', price: 59, limitLabel: '1000', featureCount: 1 },
  { key: 'enterprise', name: 'Scale', price: 99, limitLabel: '1000+', featureCount: 1 },
];

export function PricingCards({ renderCta }: { renderCta: (plan: PlanDef) => ReactNode }) {
  const { t } = useI18n();

  return (
    <div className="pricing-cards">
      {PLANS.map((plan) => (
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

          {renderCta(plan)}
        </Card>
      ))}
    </div>
  );
}
