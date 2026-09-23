import { ArrowLeft, ArrowRight } from 'lucide-react';
import { Link } from 'react-router-dom';
import { BrandMark } from '../components/BrandMark';
import { PricingCards } from '../components/PricingCards';
import { PublicFooter } from '../components/PublicFooter';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';
import { useI18n } from '../i18n/I18nProvider';
import { legalContentFor } from '../legal/legalContent';

/**
 * The public, linkable pricing page.
 *
 * The landing page already shows the same cards under `#cennik`, but that anchor is not a usable address:
 * the landing nav scrolls to it with preventDefault and never puts the hash in the URL, and nothing scrolls
 * to it when the page is opened at that hash from outside, so a visitor following such a link lands on the
 * hero with no idea the prices are further down. Paddle's domain review asks for a pricing page URL and
 * checks it by opening it, so it needs an address that shows prices on its own - hence this page rather
 * than a link into the middle of the landing page.
 *
 * It is deliberately separate from the in-app /pricing screen, which lives behind the auth guard because it
 * drives real checkouts and plan changes for the signed-in owner. This one sells; that one bills.
 */
export function PublicPricingPage() {
  const { t, language } = useI18n();
  const ui = legalContentFor(language).ui;

  return (
    <main className="legalShell">
      <header className="legalHeader">
        <Link to="/" className="landing__brand" aria-label={ui.home}>
          <span className="brand__mark"><BrandMark /></span>
          <strong>Tenebit</strong>
        </Link>
        <div className="legalHeader__actions">
          <Link to="/" className="button button--ghost"><ArrowLeft size={16} /> {ui.home}</Link>
          <Link to="/login" className="button button--ghost">{t('landing.navLoginBtn')}</Link>
          <LanguageSwitcher />
        </div>
      </header>

      <section className="landing__pricing" id="cennik">
        <h1>{t('landing.pricingHeadline')}</h1>
        <PricingCards
          renderCta={plan => (
            <Link
              to="/register"
              className={`button ${plan.badge === 'recommended' ? 'button--primary' : 'button--ghost'}`}
            >
              {t('landing.ctaStart')} <ArrowRight size={16} />
            </Link>
          )}
        />
      </section>

      <PublicFooter />
    </main>
  );
}
