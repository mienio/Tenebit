import {
  ArrowRight,
  BarChart3,
  Boxes,
  Check,
  ClipboardCheck,
  Headphones,
  Laptop,
  Monitor,
  PackageCheck,
  QrCode,
  Smartphone,
  Users
} from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { BrandMark } from '../components/BrandMark';
import { PublicFooter } from '../components/PublicFooter';
import { StatusBadge } from '../components/StatusBadge';
import { useI18n } from '../i18n/I18nProvider';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';

const previewRows = [
  { icon: Laptop, name: 'MacBook Pro 14"', tag: 'AST-0142', status: 'Assigned', person: 'Alex Morgan', value: 9800 },
  { icon: Smartphone, name: 'iPhone 15', tag: 'AST-0198', status: 'Assigned', person: 'Jamie Lee', value: 4200 },
  { icon: Monitor, name: 'Dell UltraSharp 27"', tag: 'AST-0071', status: 'InStock', person: '-', value: 1650 },
  { icon: Headphones, name: 'Sony WH-1000XM5', tag: 'AST-0233', status: 'InService', person: '-', value: 1400 }
];

const features = [
  { icon: Boxes, key: 'assets' },
  { icon: Users, key: 'people' },
  { icon: PackageCheck, key: 'assignments' },
  { icon: ClipboardCheck, key: 'procedures' },
  { icon: QrCode, key: 'qr' },
  { icon: BarChart3, key: 'reports' }
] as const;

const steps = ['step1', 'step2', 'step3'];

export function LandingPage() {
  const { t, language } = useI18n();
  const [scrolled, setScrolled] = useState(false);
  const currencyByLanguage: Record<typeof language, { locale: string; currency: string }> = {
    pl: { locale: 'pl-PL', currency: 'PLN' },
    en: { locale: 'en-US', currency: 'USD' },
    es: { locale: 'es-ES', currency: 'EUR' },
    de: { locale: 'de-DE', currency: 'EUR' }
  };
  const formatPreviewValue = (value: number) => {
    const { locale, currency } = currencyByLanguage[language];
    return new Intl.NumberFormat(locale, { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);
  };

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 8);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  return (
    <div className="landing">
      <header className={`landing__nav${scrolled ? ' landing__nav--scrolled' : ''}`}>
        <Link to="/" className="landing__brand" aria-label="Tenebit">
          <span className="brand__mark"><BrandMark /></span>
          <strong>Tenebit</strong>
        </Link>
        <nav className="landing__navLinks">
          <a href="#funkcje">{t('landing.navFeatures')}</a>
          <a href="#cennik">{t('landing.navPricing')}</a>
        </nav>
        <div className="landing__navActions">
          <LanguageSwitcher />
          <Link to="/login" className="button button--ghost landing__loginButton">{t('landing.navLoginBtn')}</Link>
          <Link to="/register" className="button button--primary">{t('landing.navRegisterBtn')}</Link>
        </div>
      </header>

      <div className="landing__glowWrap">
        <div className="landing__glow landing__glow--one" aria-hidden="true" />
        <div className="landing__glow landing__glow--two" aria-hidden="true" />

        <section className="landing__hero">
          <p className="eyebrow">{t('landing.eyebrow')}</p>
          <h1>{t('landing.headline')}</h1>
          <p className="landing__lead">{t('landing.lead')}</p>
          <div className="landing__heroActions">
            <Link to="/register" className="button button--primary">{t('landing.ctaStart')} <ArrowRight size={16} /></Link>
            <Link to="/login" className="button button--secondary">{t('landing.ctaLogin')}</Link>
          </div>
          <div className="landing__trustRow">
            <span><Check size={14} /> {t('landing.trust1')}</span>
            <span><Check size={14} /> {t('landing.trust2')}</span>
            <span><Check size={14} /> {t('landing.trust3')}</span>
          </div>
        </section>
      </div>

      <section className="landing__preview" aria-label={t('landing.previewAria')}>
        <div className="landing__previewFrame">
          <div className="landing__previewChrome"><span /><span /><span /></div>
          <div className="tableWrap">
            <table className="dense-table">
              <thead>
                <tr><th></th><th>{t('assets.nameLabel')}</th><th>{t('assets.colTag')}</th><th>{t('assets.statusLabel')}</th><th>{t('assets.colPerson')}</th><th style={{ textAlign: 'right' }}>{t('assets.colValue')}</th></tr>
              </thead>
              <tbody>
                {previewRows.map(row => (
                  <tr key={row.tag}>
                    <td><div className="table-icon"><row.icon size={16} /></div></td>
                    <td><strong>{row.name}</strong></td>
                    <td>{row.tag}</td>
                    <td><StatusBadge status={row.status} /></td>
                    <td>{row.person}</td>
                    <td style={{ textAlign: 'right' }}>{formatPreviewValue(row.value)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </section>

      <section className="landing__features" id="funkcje">
        <div className="landing__sectionIntro">
          <p className="eyebrow">{t('landing.featuresEyebrow')}</p>
          <h2>{t('landing.featuresHeadline')}</h2>
          <p>{t('landing.featuresLead')}</p>
        </div>

        <div className="landing__featureGrid">
          {features.map(feature => (
            <article className="landing__featureCard" key={feature.key}>
              <h3>{t(`landing.feature.${feature.key}.title`)}</h3>
              <p>{t(`landing.feature.${feature.key}.text`)}</p>
              <blockquote>{t(`landing.feature.${feature.key}.example`)}</blockquote>
            </article>
          ))}
        </div>
      </section>

      <section className="landing__steps">
        <h2>{t('landing.stepsHeadline')}</h2>
        <div className="landing__stepGrid">
          {steps.map((key, index) => (
            <div className="landing__stepCard" key={key}>
              <span className="landing__stepNumber">{index + 1}</span>
              <h3>{t(`landing.${key}.title`)}</h3>
              <p>{t(`landing.${key}.text`)}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="landing__pricing" id="cennik">
        <h2>{t('landing.pricingHeadline')}</h2>
        <div className="landing__pricingGrid">
          <div className="landing__pricingCard">
            <h3>Free</h3>
            <div className="landing__price">$0<small>{t('landing.perMonth')}</small></div>
            <ul>
              <li><Check size={16} /> {t('landing.free.f1')}</li>
              <li><Check size={16} /> {t('landing.free.f2')}</li>
              <li><Check size={16} /> {t('landing.free.f3')}</li>
            </ul>
          </div>
          <div className="landing__pricingCard landing__pricingCard--featured">
            <h3>Pro</h3>
            <div className="landing__price">$10<small>{t('landing.perMonth')}</small></div>
            <ul>
              <li><Check size={16} /> {t('landing.pro.f1')}</li>
              <li><Check size={16} /> {t('landing.pro.f2')}</li>
              <li><Check size={16} /> {t('landing.pro.f3')}</li>
            </ul>
          </div>
          <div className="landing__pricingCard">
            <h3>Enterprise</h3>
            <div className="landing__price">{t('landing.contact')}</div>
            <ul>
              <li><Check size={16} /> {t('landing.enterprise.f1')}</li>
              <li><Check size={16} /> {t('landing.enterprise.f2')}</li>
            </ul>
          </div>
        </div>
        <Link to="/register" className="button button--primary">{t('landing.ctaStart')} <ArrowRight size={16} /></Link>
      </section>

      <PublicFooter />
    </div>
  );
}
