import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { apiRequest } from '../api/apiClient';
import { BackButton } from '../components/BackButton';
import { useI18n } from '../i18n/I18nProvider';

export function VerifyEmailPage() {
  const { t } = useI18n();
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') ?? '';
  const [status, setStatus] = useState<'loading' | 'success' | 'error'>('loading');
  const started = useRef(false);

  useEffect(() => {
    if (started.current) return;
    started.current = true;

    if (!token) {
      setStatus('error');
      return;
    }

    apiRequest('/api/auth/verify-email', { method: 'POST', body: JSON.stringify({ token }) })
      .then(() => setStatus('success'))
      .catch(() => setStatus('error'));
  }, [token]);

  return (
    <main className="authShell">
      <section className="authCard">
        <div className="authTop">
          <BackButton to="/login" />
        </div>
        <h1>{t('auth.verifyTitle')}</h1>
        {status === 'loading' ? <p>{t('auth.verifyLoading')}</p> : null}
        {status === 'success' ? <p className="formMessage formMessage--success">{t('auth.verifySuccess')}</p> : null}
        {status === 'error' ? <p className="formMessage formMessage--error">{t('auth.verifyError')}</p> : null}
        <p className="authFooter"><Link to="/login">{t('auth.backToLogin')}</Link></p>
      </section>
    </main>
  );
}
