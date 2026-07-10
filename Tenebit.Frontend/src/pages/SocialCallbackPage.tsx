import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthProvider';
import { useI18n } from '../i18n/I18nProvider';

export function SocialCallbackPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const { t } = useI18n();
  const [error, setError] = useState<string | null>(null);
  const handled = useRef(false);

  useEffect(() => {
    if (handled.current) return;
    handled.current = true;

    const params = new URLSearchParams(window.location.hash.replace(/^#/, ''));
    const token = params.get('token');
    const returnUrl = params.get('returnUrl') ?? '/dashboard';

    if (token && auth.loginWithToken(token)) {
      navigate(returnUrl.startsWith('/') ? returnUrl : '/dashboard', { replace: true });
      return;
    }

    setError(t('auth.socialLoginFailed'));
  }, [auth, navigate, t]);

  return (
    <main className="authShell">
      <section className="authCard">
        <h1>{t('auth.loginTitle')}</h1>
        {error ? (
          <>
            <p className="formMessage formMessage--error">{error}</p>
            <p className="authFooter"><a href="/login">{t('auth.loginLink')}</a></p>
          </>
        ) : (
          <p>{t('auth.loginLoading')}</p>
        )}
      </section>
    </main>
  );
}
