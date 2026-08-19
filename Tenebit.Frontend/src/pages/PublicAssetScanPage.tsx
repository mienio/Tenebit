import { FormEvent, useEffect, useState } from 'react';
import { Navigate, useParams } from 'react-router-dom';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { ErrorState, LoadingState } from '../components/StateViews';
import { PublicFooter } from '../components/PublicFooter';
import { Field, TextArea } from '../components/FormFields';
import { QrCode, Send } from 'lucide-react';
import { useAuth } from '../auth/AuthProvider';
import { useI18n } from '../i18n/I18nProvider';

type ScanState =
  | { kind: 'loading' }
  | { kind: 'internal' }
  | { kind: 'public'; organizationName: string }
  | { kind: 'error'; message: string };

export function PublicAssetScanPage() {
  const { t } = useI18n();
  const auth = useAuth();
  const { organizationId, assetId } = useParams<{ organizationId: string; assetId: string }>();
  const [state, setState] = useState<ScanState>({ kind: 'loading' });
  const [message, setMessage] = useState('');
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);

  useEffect(() => {
    if (!organizationId || !assetId) return;
    let cancelled = false;

    async function load() {
      if (auth.isAuthenticated) {
        try {
          await api.getAsset(assetId!);
          if (!cancelled) setState({ kind: 'internal' });
          return;
        } catch {
          // Not this organization's session or no access - fall back to the public view below.
        }
      }

      try {
        const data = await api.publicAssetScan(organizationId!, assetId!);
        if (!cancelled) setState({ kind: 'public', organizationName: data.organizationName });
      } catch (error) {
        if (!cancelled) setState({ kind: 'error', message: error instanceof Error ? error.message : t('scan.invalidCode') });
      }
    }

    void load();
    return () => { cancelled = true; };
  }, [organizationId, assetId, auth.isAuthenticated, t]);

  async function submitReport(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!organizationId || !assetId || !message.trim()) return;
    setSending(true);
    setSendError(null);
    try {
      await api.reportAssetIssue(organizationId, assetId, message.trim());
      setSent(true);
    } catch (error) {
      setSendError(error instanceof Error ? error.message : t('scan.reportFailed'));
    } finally {
      setSending(false);
    }
  }

  if (!organizationId || !assetId) return <ErrorState message={t('scan.invalidCode')} />;
  if (state.kind === 'loading') return <LoadingState title={t('scan.loadingTitle')} description={t('scan.loadingDesc')} />;
  if (state.kind === 'internal') return <Navigate to={`/assets?openAssetId=${assetId}`} replace />;
  if (state.kind === 'error') return <ErrorState message={state.message} />;

  return (
    <main className="authShell">
      <section className="authCard" style={{ width: 'min(480px, 100%)' }}>
        <div className="authTop">
          <div className="authIcon"><QrCode size={24} /></div>
        </div>
        <h1>{t('scan.belongsTo', { org: state.organizationName })}</h1>
        <p className="muted">{t('scan.anonymousNotice')}</p>

        {sent ? (
          <p className="formMessage formMessage--success">{t('scan.reportSent')}</p>
        ) : (
          <form className="formGrid" onSubmit={submitReport}>
            <Field label={t('scan.reportLabel')}>
              <TextArea value={message} onChange={event => setMessage(event.target.value)} placeholder={t('scan.reportPlaceholder')} required />
            </Field>
            {sendError && <p className="formMessage formMessage--error">{sendError}</p>}
            <div className="formActions formActions--split">
              <span />
              <Button disabled={sending || !message.trim()} icon={<Send size={16} />}>
                {sending ? t('scan.sending') : t('scan.sendReport')}
              </Button>
            </div>
          </form>
        )}
      </section>
      <PublicFooter compact />
    </main>
  );
}
