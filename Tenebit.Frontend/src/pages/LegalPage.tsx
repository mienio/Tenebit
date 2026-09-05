import { ArrowLeft } from 'lucide-react';
import { Link, NavLink } from 'react-router-dom';
import { BrandMark } from '../components/BrandMark';
import { PublicFooter } from '../components/PublicFooter';
import { legalConfig } from '../config/legal';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';
import { useI18n } from '../i18n/I18nProvider';
import { legalContentFor, type LegalDocumentKind } from '../legal/legalContent';
import { clearConsent } from '../analytics/consent';

export function LegalPage({ kind }: { kind: LegalDocumentKind }) {
  const { language, t } = useI18n();
  const content = legalContentFor(language);
  const document = content.documents[kind];
  const ui = content.ui;

  return (
    <main className="legalShell">
      <header className="legalHeader">
        <Link to="/" className="landing__brand" aria-label={ui.home}>
          <span className="brand__mark"><BrandMark /></span>
          <strong>Tenebit</strong>
        </Link>
        <div className="legalHeader__actions">
          <Link to="/" className="button button--ghost"><ArrowLeft size={16} /> {ui.home}</Link>
          <LanguageSwitcher />
        </div>
      </header>

      <div className="legalLayout">
        <aside className="legalNav" aria-label={ui.terms}>
          <NavLink to="/privacy">{ui.privacy}</NavLink>
          <NavLink to="/terms">{ui.terms}</NavLink>
          <NavLink to="/cookies">{ui.cookies}</NavLink>
        </aside>

        <article className="legalDocument">
          <p className="eyebrow">Tenebit</p>
          <h1>{document.title}</h1>
          <p className="legalDocument__lead">{document.description}</p>
          {content.documentsLanguage !== language && (
            <p className="toast toast--error">{t('legal.fallbackNotice')}</p>
          )}

          <dl className="legalMeta">
            <div><dt>{ui.effectiveDate}</dt><dd>{legalConfig.effectiveDate}</dd></div>
            {kind === 'terms' ? <div><dt>{ui.version}</dt><dd>{legalConfig.termsVersion}</dd></div> : null}
          </dl>

          <div className="legalSections">
            {document.sections.map(section => (
              <section key={section.title}>
                <h2>{section.title}</h2>
                {section.paragraphs?.map(paragraph => <p key={paragraph}>{paragraph}</p>)}
                {section.bullets ? <ul>{section.bullets.map(item => <li key={item}>{item}</li>)}</ul> : null}
              </section>
            ))}
            {kind === 'cookies' && (
              <button type="button" className="button button--ghost" onClick={() => { clearConsent(); window.location.reload(); }}>
                {ui.manageConsent}
              </button>
            )}
          </div>
        </article>
      </div>

      <PublicFooter />
    </main>
  );
}
