import {
  ArrowRight,
  BarChart3,
  Boxes,
  Check,
  ClipboardCheck,
  Headphones,
  KeyRound,
  Laptop,
  MapPin,
  Monitor,
  PackageCheck,
  QrCode,
  Smartphone,
  Users
} from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { BrandMark } from '../components/BrandMark';
import { PricingCards } from '../components/PricingCards';
import { PublicFooter } from '../components/PublicFooter';
import { StatusBadge } from '../components/StatusBadge';
import { useI18n } from '../i18n/I18nProvider';
import type { Language } from '../i18n/translations';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';

const previewAssetRows = [
  { icon: Laptop, name: 'MacBook Pro 14"', tag: 'AST-0142', status: 'Assigned', person: 'Alex Morgan', value: 9800 },
  { icon: Smartphone, name: 'iPhone 15', tag: 'AST-0198', status: 'Assigned', person: 'Jamie Lee', value: 4200 },
  { icon: Monitor, name: 'Dell UltraSharp 27"', tag: 'AST-0071', status: 'InStock', person: '-', value: 1650 },
  { icon: Headphones, name: 'Sony WH-1000XM5', tag: 'AST-0233', status: 'InService', person: '-', value: 1400 }
];

const previewLicenseRows = [
  { name: 'Windows 11 Pro', vendor: 'Microsoft', seatsUsed: 64, seatsTotal: 70, status: 'Active' },
  { name: 'Microsoft 365', vendor: 'Microsoft', seatsUsed: 50, seatsTotal: 50, status: 'Active' },
  { name: 'Adobe Creative Cloud', vendor: 'Adobe', seatsUsed: 6, seatsTotal: 15, status: 'Active' },
  { name: 'JetBrains All Products', vendor: 'JetBrains', seatsUsed: 18, seatsTotal: 20, status: 'Expired' }
];

// One office per language/market, split Building -> 2 Floors -> 2 Rooms each, to demo the location
// hierarchy without implying the company has multiple real-world sites.
const previewOfficeByLanguage: Record<Language, { building: string; floor1: string; floor2: string; room101: string; room102: string; room201: string; room202: string }> = {
  pl: { building: 'Warszawa, ul. Prosta 20', floor1: 'Piętro 1', floor2: 'Piętro 2', room101: 'Pokój 101', room102: 'Pokój 102', room201: 'Pokój 201', room202: 'Pokój 202' },
  en: { building: 'London, 24 Borough High St', floor1: 'Floor 1', floor2: 'Floor 2', room101: 'Room 101', room102: 'Room 102', room201: 'Room 201', room202: 'Room 202' },
  es: { building: 'Madrid, Calle de Alcalá 45', floor1: 'Planta 1', floor2: 'Planta 2', room101: 'Sala 101', room102: 'Sala 102', room201: 'Sala 201', room202: 'Sala 202' },
  de: { building: 'Berlin, Torstraße 15', floor1: 'Etage 1', floor2: 'Etage 2', room101: 'Raum 101', room102: 'Raum 102', room201: 'Raum 201', room202: 'Raum 202' }
};

const previewPeopleByLanguage: Record<Language, { name: string; jobTitle: string; team: string }[]> = {
  pl: [
    { name: 'Anna Kowalska', jobTitle: 'Office Manager', team: 'Administracja' },
    { name: 'Marek Wiśniewski', jobTitle: 'DevOps Engineer', team: 'IT' },
    { name: 'Julia Nowak', jobTitle: 'Account Executive', team: 'Sprzedaż' },
    { name: 'Tomasz Zieliński', jobTitle: 'HR Specialist', team: 'Kadry' }
  ],
  en: [
    { name: 'Olivia Bennett', jobTitle: 'Office Manager', team: 'Admin' },
    { name: 'James Carter', jobTitle: 'DevOps Engineer', team: 'IT' },
    { name: 'Sophie Turner', jobTitle: 'Account Executive', team: 'Sales' },
    { name: 'Daniel Wright', jobTitle: 'HR Specialist', team: 'People' }
  ],
  es: [
    { name: 'Lucía Fernández', jobTitle: 'Office Manager', team: 'Administración' },
    { name: 'Javier Martín', jobTitle: 'DevOps Engineer', team: 'IT' },
    { name: 'Marta Sánchez', jobTitle: 'Account Executive', team: 'Ventas' },
    { name: 'Pablo Ruiz', jobTitle: 'HR Specialist', team: 'RRHH' }
  ],
  de: [
    { name: 'Hannah Fischer', jobTitle: 'Office Manager', team: 'Verwaltung' },
    { name: 'Lukas Weber', jobTitle: 'DevOps Engineer', team: 'IT' },
    { name: 'Laura Schmidt', jobTitle: 'Account Executive', team: 'Vertrieb' },
    { name: 'Felix Wagner', jobTitle: 'HR Specialist', team: 'Personal' }
  ]
};

const previewTabs = [
  { key: 'assets', icon: Boxes },
  { key: 'licenses', icon: KeyRound },
  { key: 'people', icon: Users },
  { key: 'locations', icon: MapPin }
] as const;

type PreviewTab = typeof previewTabs[number]['key'];

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
  const [previewTab, setPreviewTab] = useState<PreviewTab>('assets');
  const office = previewOfficeByLanguage[language];
  const previewPeopleRows = previewPeopleByLanguage[language].map((person, index) => ({
    ...person,
    location: index === 0
      ? `${office.floor1} · ${office.room101}`
      : index === 1
        ? `${office.floor1} · ${office.room102}`
        : index === 2
          ? `${office.floor2} · ${office.room201}`
          : `${office.floor2} · ${office.room202}`
  }));
  const previewLocationRows = [
    { name: office.building, type: 'Building', assetCount: 70, personCount: 23 },
    { name: `${office.building} · ${office.floor1}`, type: 'Floor', assetCount: 32, personCount: 11 },
    { name: `${office.building} · ${office.floor1} · ${office.room101}`, type: 'Room', assetCount: 18, personCount: 6 },
    { name: `${office.building} · ${office.floor1} · ${office.room102}`, type: 'Room', assetCount: 14, personCount: 5 },
    { name: `${office.building} · ${office.floor2}`, type: 'Floor', assetCount: 38, personCount: 12 },
    { name: `${office.building} · ${office.floor2} · ${office.room201}`, type: 'Room', assetCount: 22, personCount: 7 },
    { name: `${office.building} · ${office.floor2} · ${office.room202}`, type: 'Room', assetCount: 16, personCount: 5 }
  ];
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
          <div className="landing__previewTabs">
            {previewTabs.map(tab => (
              <button
                key={tab.key}
                type="button"
                className={`landing__previewTab${previewTab === tab.key ? ' landing__previewTab--active' : ''}`}
                onClick={() => setPreviewTab(tab.key)}
              >
                <tab.icon size={14} /> {t(`landing.previewTab.${tab.key}`)}
              </button>
            ))}
          </div>
          <div className="tableWrap">
            {previewTab === 'assets' && (
              <table className="dense-table">
                <thead>
                  <tr><th></th><th>{t('assets.nameLabel')}</th><th>{t('assets.colTag')}</th><th>{t('assets.statusLabel')}</th><th>{t('assets.colPerson')}</th><th style={{ textAlign: 'right' }}>{t('assets.colValue')}</th></tr>
                </thead>
                <tbody>
                  {previewAssetRows.map(row => (
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
            )}
            {previewTab === 'licenses' && (
              <table className="dense-table">
                <thead>
                  <tr><th></th><th>{t('licenses.colName')}</th><th>{t('licenses.colVendor')}</th><th>{t('licenses.colSeats')}</th><th>{t('assets.statusLabel')}</th></tr>
                </thead>
                <tbody>
                  {previewLicenseRows.map(row => (
                    <tr key={row.name}>
                      <td><div className="table-icon"><KeyRound size={16} /></div></td>
                      <td><strong>{row.name}</strong></td>
                      <td>{row.vendor}</td>
                      <td>{row.seatsUsed}/{row.seatsTotal}</td>
                      <td><StatusBadge status={row.status} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            {previewTab === 'people' && (
              <table className="dense-table">
                <thead>
                  <tr><th></th><th>{t('people.colFullName')}</th><th>{t('people.colJobTitle')}</th><th>{t('people.colTeam')}</th><th>{t('landing.preview.colLocation')}</th></tr>
                </thead>
                <tbody>
                  {previewPeopleRows.map(row => (
                    <tr key={row.name}>
                      <td><div className="table-icon"><Users size={16} /></div></td>
                      <td><strong>{row.name}</strong></td>
                      <td>{row.jobTitle}</td>
                      <td>{row.team}</td>
                      <td>{row.location}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            {previewTab === 'locations' && (
              <table className="dense-table">
                <thead>
                  <tr><th></th><th>{t('assets.nameLabel')}</th><th>{t('landing.preview.colType')}</th><th style={{ textAlign: 'right' }}>{t('landing.preview.colAssets')}</th><th style={{ textAlign: 'right' }}>{t('landing.preview.colPeople')}</th></tr>
                </thead>
                <tbody>
                  {previewLocationRows.map(row => (
                    <tr key={row.name}>
                      <td><div className="table-icon"><MapPin size={16} /></div></td>
                      <td><strong>{row.name}</strong></td>
                      <td>{t(`locationType.${row.type}`)}</td>
                      <td style={{ textAlign: 'right' }}>{row.assetCount}</td>
                      <td style={{ textAlign: 'right' }}>{row.personCount}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
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
        <PricingCards renderCta={() => null} />
        <Link to="/register" className="button button--primary">{t('landing.ctaStart')} <ArrowRight size={16} /></Link>
      </section>

      <PublicFooter />
    </div>
  );
}
