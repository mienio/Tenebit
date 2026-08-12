import { SearchX, ShieldAlert } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useI18n } from '../i18n/I18nProvider';

export function ForbiddenPage() {
  const { t } = useI18n();
  return (
    <div className="stateBox stateBox--error" role="alert">
      <ShieldAlert size={30} />
      <h2>{t('errors.forbiddenTitle')}</h2>
      <p>{t('errors.forbiddenDesc')}</p>
      <Link className="button button--secondary" to="/dashboard">{t('errors.goHome')}</Link>
    </div>
  );
}

export function NotFoundPage() {
  const { t } = useI18n();
  return (
    <main className="authShell">
      <section className="authCard">
        <div className="authTop">
          <div className="authIcon"><SearchX size={24} /></div>
        </div>
        <h1>{t('errors.notFoundTitle')}</h1>
        <p>{t('errors.notFoundDesc')}</p>
        <Link className="button button--secondary" to="/">{t('errors.goHome')}</Link>
      </section>
    </main>
  );
}
