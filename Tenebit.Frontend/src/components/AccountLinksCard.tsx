import { Link2, Link2Off } from 'lucide-react';
import { useEffect, useState } from 'react';
import { apiBaseUrl, apiRequest } from '../api/apiClient';
import { useI18n } from '../i18n/I18nProvider';
import { Button } from './Button';
import { Card } from './Card';

const PROVIDER_LABELS: Record<string, string> = { google: 'Google', microsoft: 'Microsoft', facebook: 'Facebook', apple: 'Apple' };
type Message = { type: 'success' | 'error'; text: string } | null;

export function AccountLinksCard() {
  const { t } = useI18n();
  const [enabledProviders, setEnabledProviders] = useState<string[]>([]);
  const [linkedProviders, setLinkedProviders] = useState<string[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [message, setMessage] = useState<Message>(null);
  const [busyProvider, setBusyProvider] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    Promise.all([
      apiRequest<{ providers: string[] }>('/api/auth/external/providers'),
      apiRequest<{ providers: string[] }>('/api/auth/external/links')
    ]).then(([providersRes, linksRes]) => {
      if (cancelled) return;
      setEnabledProviders(providersRes.providers);
      setLinkedProviders(linksRes.providers);
    }).catch(() => {}).finally(() => { if (!cancelled) setLoaded(true); });
    return () => { cancelled = true; };
  }, []);

  async function unlink(provider: string) {
    setBusyProvider(provider);
    setMessage(null);
    try {
      await apiRequest(`/api/auth/external/${provider}/unlink`, { method: 'POST' });
      setLinkedProviders(current => current.filter(p => p !== provider));
      setMessage({ type: 'success', text: t('accountLinks.unlinked') });
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('accountLinks.unlinkFailed') });
    } finally {
      setBusyProvider(null);
    }
  }

  function connect(provider: string) {
    window.location.href = `${apiBaseUrl}/api/auth/external/${provider}/start?returnUrl=${encodeURIComponent('/my')}`;
  }

  if (!loaded || enabledProviders.length === 0) return null;

  return (
    <Card>
      <div className="sectionTitle"><div><h2>{t('accountLinks.title')}</h2><p>{t('accountLinks.description')}</p></div></div>
      {message ? <p className={`formMessage formMessage--${message.type}`}>{message.text}</p> : null}
      <div className="listRows">
        {enabledProviders.map(provider => {
          const linked = linkedProviders.includes(provider);
          return (
            <div className="listRow" key={provider}>
              <div><strong>{PROVIDER_LABELS[provider] ?? provider}</strong><small>{linked ? t('accountLinks.connected') : t('accountLinks.notConnected')}</small></div>
              {linked ? (
                <Button variant="ghost" type="button" disabled={busyProvider === provider} onClick={() => unlink(provider)} icon={<Link2Off size={16} />}>{t('accountLinks.disconnect')}</Button>
              ) : (
                <Button variant="secondary" type="button" onClick={() => connect(provider)} icon={<Link2 size={16} />}>{t('accountLinks.connect')}</Button>
              )}
            </div>
          );
        })}
      </div>
    </Card>
  );
}
