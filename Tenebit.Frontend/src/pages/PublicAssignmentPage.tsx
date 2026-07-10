import { CheckCircle2, Download, PackageCheck } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { ErrorState, LoadingState } from '../components/StateViews';
import { useAsyncData } from '../hooks/useAsyncData';
import { useI18n } from '../i18n/I18nProvider';

export function PublicAssignmentPage() {
  const { t } = useI18n();
  const { organizationId, assignmentId } = useParams<{ organizationId: string; assignmentId: string }>();
  const loader = useMemo(() => () => api.publicAssignment(organizationId!, assignmentId!), [organizationId, assignmentId]);
  const { data, error, isLoading, reload } = useAsyncData(loader, [loader]);
  const [accepting, setAccepting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  async function accept() {
    if (!organizationId || !assignmentId) return;
    setAccepting(true);
    setMessage(null);
    try {
      await api.acceptPublicAssignment(organizationId, assignmentId);
      await reload();
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('publicAssignment.acceptFailed'));
    } finally {
      setAccepting(false);
    }
  }

  async function download() {
    if (!organizationId || !assignmentId) return;
    try {
      const blob = await api.downloadPublicAssignmentProtocol(organizationId, assignmentId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${data?.protocolNumber ?? 'protokol'}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('publicAssignment.downloadFailed'));
    }
  }

  if (!organizationId || !assignmentId) return <ErrorState message={t('publicAssignment.invalidLink')} />;
  if (isLoading && !data) return <LoadingState title={t('publicAssignment.loadingTitle')} description={t('publicAssignment.loadingDesc')} />;
  if (error || !data) return <ErrorState message={error ?? t('publicAssignment.invalidLink')} onRetry={reload} />;

  const canAccept = data.status === 'AwaitingAcceptance' || data.status === 'Overdue';
  const accepted = data.status === 'Accepted' || data.status === 'Returned';

  return (
    <main className="authShell">
      <section className="authCard" style={{ width: 'min(560px, 100%)' }}>
        <div className="authTop">
          <div className="authIcon"><PackageCheck size={24} /></div>
        </div>
        <h1>{t('publicAssignment.greeting', { name: data.personFirstName })}</h1>
        <p>{t('publicAssignment.intro', { org: data.organizationName, protocol: data.protocolNumber })}</p>

        <div className="formSectionTitle">{t('publicAssignment.assetsTitle')}</div>
        <div className="listRows">
          {data.assets.map(asset => (
            <div className="listRow" key={asset.assetTag}>
              <div><strong>{asset.name}</strong><small>{asset.assetTag}</small></div>
              <span>{asset.issueCondition}</span>
            </div>
          ))}
        </div>

        {data.procedureTitlesRequiringAcceptance.length > 0 && (
          <>
            <div className="formSectionTitle">{t('publicAssignment.proceduresTitle')}</div>
            <ul>
              {data.procedureTitlesRequiringAcceptance.map(title => <li key={title}>{title}</li>)}
            </ul>
          </>
        )}

        {message && <p className="formMessage formMessage--error">{message}</p>}

        {accepted ? (
          <div className="formActions formActions--split">
            <p style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--success)' }}>
              <CheckCircle2 size={18} /> {t('publicAssignment.alreadyAccepted')}
            </p>
            <Button variant="secondary" onClick={download} icon={<Download size={16} />}>{t('publicAssignment.downloadProtocol')}</Button>
          </div>
        ) : canAccept ? (
          <div className="formActions formActions--split">
            <span />
            <Button disabled={accepting} onClick={accept} icon={<CheckCircle2 size={16} />}>
              {accepting ? t('publicAssignment.accepting') : t('publicAssignment.acceptButton')}
            </Button>
          </div>
        ) : (
          <p className="muted">{t('publicAssignment.notAcceptable')}</p>
        )}
      </section>
    </main>
  );
}
