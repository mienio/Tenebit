import { BarChart3, Boxes, Check, ClipboardCheck, Headphones, Laptop, Monitor, PackageCheck, QrCode, Smartphone, Users } from 'lucide-react';
import { Link } from 'react-router-dom';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';
import { useI18n } from '../i18n/I18nProvider';
import { StatusBadge } from '../components/StatusBadge';

const previewRows = [
  { icon: Laptop, name: 'MacBook Pro 14"', tag: 'AST-0142', status: 'Assigned', person: 'Anna Kowalska', value: '9 800 zł' },
  { icon: Smartphone, name: 'iPhone 15', tag: 'AST-0198', status: 'Assigned', person: 'Piotr Nowak', value: '4 200 zł' },
  { icon: Monitor, name: 'Dell UltraSharp 27"', tag: 'AST-0071', status: 'InStock', person: '—', value: '1 650 zł' },
  { icon: Headphones, name: 'Sony WH-1000XM5', tag: 'AST-0233', status: 'InService', person: '—', value: '1 400 zł' }
];

const features = [
  { icon: PackageCheck, key: 'assignments' },
  { icon: ClipboardCheck, key: 'procedures' },
  { icon: Boxes, key: 'assets' },
  { icon: Users, key: 'people' },
  { icon: QrCode, key: 'qr' },
  { icon: BarChart3, key: 'reports' }
];

const steps = ['step1', 'step2', 'step3'];

export function LandingPage() {
  const { t } = useI18n();

  return (
    <div className="landing">
      <header className="landing__nav">
        <div className="landing__brand">
          <div className="brand__mark"><QrCode size={20} /></div>
          <strong>Tenebit</strong>
        </div>
        <nav className="landing__navLinks">
          <a href="#funkcje">{t('landing.navFeatures')}</a>
          <a href="#cennik">{t('landing.navPricing')}</a>
        </nav>
        <div className="landing__navActions">
          <LanguageSwitcher />
          <Link to="/login" className="button button--ghost">{t('landing.navLoginBtn')}</Link>
          <Link to="/register" className="button button--primary">{t('landing.navRegisterBtn')}</Link>
        </div>
      </header>

      <section className="landing__hero">
        <p className="eyebrow">{t('landing.eyebrow')}</p>
        <h1>{t('landing.headline')}</h1>
        <p className="landing__lead">{t('landing.lead')}</p>
        <div className="landing__heroActions">
          <Link to="/register" className="button button--primary">{t('landing.ctaStart')}</Link>
          <Link to="/login" className="button button--secondary">{t('landing.ctaLogin')}</Link>
        </div>
      </section>

      <section className="landing__preview">
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
                    <td style={{ textAlign: 'right' }}>{row.value}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </section>

      <section className="landing__features" id="funkcje">
        <h2>{t('landing.featuresHeadline')}</h2>
        <div className="landing__featureGrid">
          {features.map(feature => (
            <div className="landing__featureCard" key={feature.key}>
              <feature.icon size={22} />
              <h3>{t(`landing.feature.${feature.key}.title`)}</h3>
              <p>{t(`landing.feature.${feature.key}.text`)}</p>
            </div>
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
        <Link to="/register" className="button button--primary">{t('landing.ctaStart')}</Link>
      </section>

      <footer className="landing__footer">
        <span>© {new Date().getFullYear()} Tenebit</span>
        <a href="mailto:kontakt@tenebit.app">kontakt@tenebit.app</a>
      </footer>
    </div>
  );
}
