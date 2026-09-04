import { FormEvent, useEffect, useMemo, useState } from 'react';
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
  | { kind: 'internal'; assetId: string }
  | { kind: 'public'; organizationName: string }
  | { kind: 'error'; message: string };

/**
 * Dwie postacie adresu. Nowa - /s/:code - niesie dziesiecioznakowy losowy kod z etykiety; to on
 * pozwala zmiescic caly URL w trybie alfanumerycznym QR i zejsc z kodem do 33x33 modulow
 * (patrz AppLinkBuilder.BuildAssetScanLink). Stara - /scan/:organizationId/:assetId - zostaje, bo
 * etykiety wydrukowane wczesniej maja ja wypalona w kodzie i musza dzialac dalej.
 */
type ScanTarget =
  | { kind: 'code'; code: string }
  | { kind: 'ids'; organizationId: string; assetId: string }
  | null;

function readScanTarget(params: { organizationId?: string; assetId?: string; code?: string }): ScanTarget {
  if (params.code) {
    const code = params.code.toUpperCase();
    return /^[0-9A-HJKMNP-TV-Z]{10}$/.test(code) ? { kind: 'code', code } : null;
  }
  if (params.organizationId && params.assetId) {
    return { kind: 'ids', organizationId: params.organizationId.toLowerCase(), assetId: params.assetId.toLowerCase() };
  }
  return null;
}

export function PublicAssetScanPage() {
  const { t } = useI18n();
  const auth = useAuth();
  const params = useParams<{ organizationId?: string; assetId?: string; code?: string }>();
  const target = useMemo(
    () => readScanTarget({ organizationId: params.organizationId, assetId: params.assetId, code: params.code }),
    [params.organizationId, params.assetId, params.code],
  );
  const [state, setState] = useState<ScanState>({ kind: 'loading' });
  const [message, setMessage] = useState('');
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);

  useEffect(() => {
    if (!target) return;
    let cancelled = false;

    async function load() {
      // Zalogowany uzytkownik tej organizacji trafia prosto na karte sprzetu. Rozpoznanie idzie przez
      // API, wiec kod z etykiety innego najemcy wyglada identycznie jak kod nieistniejacy.
      if (auth.isAuthenticated) {
        try {
          const assetId = target!.kind === 'code'
            ? await api.resolveScanCode(target!.code)
            : target!.assetId;
          await api.getAsset(assetId);
          if (!cancelled) setState({ kind: 'internal', assetId });
          return;
        } catch {
          // Nie ta sesja albo brak dostepu - schodzimy do widoku publicznego ponizej.
        }
      }

      try {
        const data = target!.kind === 'code'
          ? await api.publicAssetScanByCode(target!.code)
          : await api.publicAssetScan(target!.organizationId, target!.assetId);
        if (!cancelled) setState({ kind: 'public', organizationName: data.organizationName });
      } catch (error) {
        if (!cancelled) setState({ kind: 'error', message: error instanceof Error ? error.message : t('scan.invalidCode') });
      }
    }

    void load();
    return () => { cancelled = true; };
  }, [target, auth.isAuthenticated, t]);

  async function submitReport(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!target || !message.trim()) return;
    setSending(true);
    setSendError(null);
    try {
      await (target.kind === 'code'
        ? api.reportAssetIssueByCode(target.code, message.trim())
        : api.reportAssetIssue(target.organizationId, target.assetId, message.trim()));
      setSent(true);
    } catch (error) {
      setSendError(error instanceof Error ? error.message : t('scan.reportFailed'));
    } finally {
      setSending(false);
    }
  }

  if (!target) return <ErrorState message={t('scan.invalidCode')} />;
  if (state.kind === 'loading') return <LoadingState title={t('scan.loadingTitle')} description={t('scan.loadingDesc')} />;
  if (state.kind === 'internal') return <Navigate to={`/assets?openAssetId=${state.assetId}`} replace />;
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
