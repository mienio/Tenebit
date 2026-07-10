import { useEffect, useState, type JSX } from 'react';
import { apiBaseUrl, apiRequest } from '../api/apiClient';
import { useI18n } from '../i18n/I18nProvider';

type ProvidersResponse = { providers: string[] };

function GoogleIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 18 18" aria-hidden="true">
      <path fill="#4285F4" d="M17.64 9.2c0-.64-.06-1.25-.16-1.84H9v3.48h4.84a4.14 4.14 0 0 1-1.8 2.72v2.26h2.9c1.7-1.57 2.7-3.88 2.7-6.62z" />
      <path fill="#34A853" d="M9 18c2.43 0 4.47-.8 5.96-2.18l-2.9-2.26c-.81.54-1.84.86-3.06.86-2.35 0-4.34-1.59-5.05-3.72H.98v2.33A9 9 0 0 0 9 18z" />
      <path fill="#FBBC05" d="M3.95 10.7A5.4 5.4 0 0 1 3.67 9c0-.59.1-1.17.28-1.7V4.97H.98A9 9 0 0 0 0 9c0 1.45.35 2.83.98 4.03z" />
      <path fill="#EA4335" d="M9 3.58c1.32 0 2.51.46 3.44 1.35l2.58-2.58C13.46.89 11.43 0 9 0A9 9 0 0 0 .98 4.97L3.95 7.3C4.66 5.17 6.65 3.58 9 3.58z" />
    </svg>
  );
}

function MicrosoftIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 18 18" aria-hidden="true">
      <rect x="0" y="0" width="8.5" height="8.5" fill="#F25022" />
      <rect x="9.5" y="0" width="8.5" height="8.5" fill="#7FBA00" />
      <rect x="0" y="9.5" width="8.5" height="8.5" fill="#00A4EF" />
      <rect x="9.5" y="9.5" width="8.5" height="8.5" fill="#FFB900" />
    </svg>
  );
}

function FacebookIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 18 18" aria-hidden="true">
      <circle cx="9" cy="9" r="9" fill="#1877F2" />
      <path fill="#fff" d="M11.2 9.35h-1.6V15H7.4V9.35H6.2V7.2h1.2V5.9c0-1.34.63-2.63 2.5-2.63h1.7v1.9h-1.24c-.24 0-.56.12-.56.62v1.41h1.83l-.2 2.15z" />
    </svg>
  );
}

function AppleIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 18 18" aria-hidden="true">
      <path
        fill="currentColor"
        d="M13.15 9.53c-.02-1.7 1.4-2.51 1.46-2.55-.8-1.17-2.05-1.33-2.5-1.35-1.06-.11-2.08.62-2.62.62-.55 0-1.38-.6-2.27-.59-1.16.02-2.24.68-2.84 1.72-1.21 2.1-.31 5.2.87 6.9.58.83 1.26 1.76 2.16 1.73.87-.03 1.2-.56 2.25-.56s1.35.56 2.27.55c.94-.02 1.53-.85 2.1-1.68.66-.97.93-1.9.95-1.95-.02-.01-1.81-.7-1.83-2.79z"
      />
      <path fill="currentColor" d="M11.5 4.4c.48-.58.8-1.38.71-2.19-.69.03-1.53.46-2.02 1.03-.44.5-.83 1.32-.73 2.1.77.06 1.56-.39 2.04-.94z" />
    </svg>
  );
}

const PROVIDER_META: Record<string, { label: string; icon: JSX.Element }> = {
  google: { label: 'Google', icon: <GoogleIcon /> },
  microsoft: { label: 'Microsoft', icon: <MicrosoftIcon /> },
  facebook: { label: 'Facebook', icon: <FacebookIcon /> },
  apple: { label: 'Apple', icon: <AppleIcon /> }
};

export function SocialLoginButtons({ returnUrl = '/dashboard' }: { returnUrl?: string }) {
  const { t } = useI18n();
  const [providers, setProviders] = useState<string[]>([]);

  useEffect(() => {
    let cancelled = false;
    apiRequest<ProvidersResponse>('/api/auth/external/providers')
      .then(response => { if (!cancelled) setProviders(response.providers); })
      .catch(() => {});
    return () => { cancelled = true; };
  }, []);

  if (providers.length === 0) return null;

  function startLogin(provider: string) {
    window.location.href = `${apiBaseUrl}/api/auth/external/${provider}/start?returnUrl=${encodeURIComponent(returnUrl)}`;
  }

  return (
    <div className="socialAuth">
      <div className="socialButtons">
        {providers.map(provider => {
          const meta = PROVIDER_META[provider];
          if (!meta) return null;
          return (
            <button key={provider} type="button" className="socialButton" onClick={() => startLogin(provider)}>
              {meta.icon}
              <span>{t('auth.continueWith', { provider: meta.label })}</span>
            </button>
          );
        })}
      </div>
      <div className="authDivider"><span>{t('auth.orDivider')}</span></div>
    </div>
  );
}
