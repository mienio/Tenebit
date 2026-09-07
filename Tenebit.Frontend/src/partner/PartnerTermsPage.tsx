import { useEffect } from 'react';
import { Link } from 'react-router-dom';
import { affiliateTermsSectionsByLocale, AFFILIATE_TERMS_VERSION } from './affiliateTermsContent';
import { PartnerLanguageSwitch, usePartnerLocale, type PartnerLocale } from './i18n';
import './partner.css';

const content: Record<PartnerLocale, { back: string; heading: string; versionPrefix: string; dateLocale: string }> = {
  en: {
    back: '← Back to registration', heading: 'TENEB.IT PARTNERS affiliate program terms',
    versionPrefix: 'Version dated', dateLocale: 'en-GB',
  },
  pl: {
    back: '← Wróć do rejestracji', heading: 'Regulamin programu partnerskiego TENEB.IT PARTNERS',
    versionPrefix: 'Wersja z', dateLocale: 'pl-PL',
  },
};

export function PartnerTermsPage() {
  const { path, locale } = usePartnerLocale();
  const t = content[locale];
  const sections = affiliateTermsSectionsByLocale[locale];

  useEffect(() => {
    const meta = document.createElement('meta');
    meta.name = 'robots';
    meta.content = 'noindex, nofollow';
    document.head.appendChild(meta);
    return () => { document.head.removeChild(meta); };
  }, []);

  return (
    <main style={{ maxWidth: 720, margin: '0 auto', padding: '48px 24px' }}>
      <p style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Link to={path('register')}>{t.back}</Link>
        <PartnerLanguageSwitch />
      </p>
      <h1>{t.heading}</h1>
      <p className="adminMuted">{t.versionPrefix} {new Date(AFFILIATE_TERMS_VERSION).toLocaleDateString(t.dateLocale)}</p>
      {sections.map(section => (
        <section key={section.title} style={{ marginTop: 28 }}>
          <h2 style={{ fontSize: 18 }}>{section.title}</h2>
          {section.paragraphs?.map((p, i) => <p key={i} style={{ lineHeight: 1.6 }}>{p}</p>)}
          {section.bullets ? (
            <ul style={{ lineHeight: 1.6 }}>
              {section.bullets.map((b, i) => <li key={i}>{b}</li>)}
            </ul>
          ) : null}
        </section>
      ))}
    </main>
  );
}
