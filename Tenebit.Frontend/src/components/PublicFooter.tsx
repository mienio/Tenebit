import { Link } from 'react-router-dom';
import { legalConfig } from '../config/legal';
import { StorageNotice } from './StorageNotice';
import { useI18n } from '../i18n/I18nProvider';
import { legalContentFor } from '../legal/legalContent';

export function PublicFooter({ compact = false }: { compact?: boolean }) {
  const { language } = useI18n();
  const ui = legalContentFor(language).ui;

  return (
    <>
      <footer className={`publicFooter${compact ? ' publicFooter--compact' : ''}`}>
        <div className="publicFooter__brand">
          <strong>Tenebit</strong>
          <span>© {new Date().getFullYear()} {ui.footerRights}</span>
          {legalConfig.supportEmail ? (
            <span className="publicFooter__contactPrompt">
              {ui.contactPrompt} <a href={`mailto:${legalConfig.supportEmail}`}>{legalConfig.supportEmail}</a>
            </span>
          ) : null}
        </div>
        <nav className="publicFooter__links" aria-label={ui.contact}>
          <Link to="/privacy">{ui.privacy}</Link>
          <Link to="/terms">{ui.terms}</Link>
          <Link to="/cookies">{ui.cookies}</Link>
        </nav>
      </footer>
      <StorageNotice />
    </>
  );
}
