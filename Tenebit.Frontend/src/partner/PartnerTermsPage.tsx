import { useEffect } from 'react';
import { Link } from 'react-router-dom';
import { affiliateTermsSections, AFFILIATE_TERMS_VERSION } from './affiliateTermsContent';
import './partner.css';

export function PartnerTermsPage() {
  useEffect(() => {
    const meta = document.createElement('meta');
    meta.name = 'robots';
    meta.content = 'noindex, nofollow';
    document.head.appendChild(meta);
    return () => { document.head.removeChild(meta); };
  }, []);

  return (
    <main style={{ maxWidth: 720, margin: '0 auto', padding: '48px 24px' }}>
      <p><Link to="/partner/register">← Wróć do rejestracji</Link></p>
      <h1>Regulamin programu partnerskiego TENEB.IT PARTNERS</h1>
      <p className="adminMuted">Wersja z {new Date(AFFILIATE_TERMS_VERSION).toLocaleDateString('pl-PL')}</p>
      {affiliateTermsSections.map(section => (
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
