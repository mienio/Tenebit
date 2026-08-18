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
    const params = new URLSearchParams(window.location.hash.replace(/^#/, ''));
    const returnUrl = params.get('returnUrl') ?? '/dashboard';
    const safeReturnUrl = returnUrl.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/dashboard';

    // The backend sets only an HttpOnly refresh cookie. Exchange it explicitly so an access token
    // left by a previously signed-in account can never survive the OAuth callback.
    if (params.get('oauthSuccess') === 'true') {
      if (handled.current) return;
      handled.current = true;
      window.history.replaceState(null, '', window.location.pathname);
      void auth.completeExternalLogin().then(success => {
        if (success) {
          navigate(safeReturnUrl, { replace: true });
        } else {
          setError(t('auth.socialLoginFailed'));
        }
      });
      return;
    }

    if (handled.current) return;
    handled.current = true;

    if (params.get('requiresTwoFactor') === 'true') {
      const challengeToken = params.get('challengeToken');
      if (challengeToken) {
        window.history.replaceState(null, '', window.location.pathname);
        navigate('/login', { replace: true, state: { challengeToken, from: safeReturnUrl } });
        return;
      }
    }

    const code = params.get('error');
    if (code === 'oauth_expired') {
      setError(t('auth.socialLoginExpired'));
    } else {
      setError(t('auth.socialLoginFailed'));
    }
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
