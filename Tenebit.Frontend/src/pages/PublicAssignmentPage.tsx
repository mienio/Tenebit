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
  const [consentChecked, setConsentChecked] = useState(false);
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

  function saveBlob(blob: Blob, fileName: string) {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  }

  async function download() {
    if (!organizationId || !assignmentId) return;
    try {
      const blob = await api.downloadPublicAssignmentProtocol(organizationId, assignmentId);
      saveBlob(blob, `${data?.protocolNumber ?? 'protokol'}.pdf`);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('publicAssignment.downloadFailed'));
    }
  }

  async function downloadProcedureDocument(procedureId: string, documentId: string, fileName: string) {
    if (!organizationId || !assignmentId) return;
    try {
      const blob = await api.downloadPublicProcedureDocument(organizationId, assignmentId, procedureId, documentId);
      saveBlob(blob, fileName);
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

        {data.proceduresRequiringAcceptance.length > 0 && (
          <>
            <div className="formSectionTitle">{t('publicAssignment.proceduresTitle')}</div>
            <div className="listRows">
              {data.proceduresRequiringAcceptance.map(procedure => (
                <div className="listRow" key={procedure.id}>
                  <div>
                    <strong>{procedure.title}</strong>
                    <small>{t('publicAssignment.procedureVersion', { version: procedure.version })}</small>
                  </div>
                  <div className="rowActions" style={{ flexDirection: 'column', alignItems: 'flex-end', gap: '4px' }}>
                    {procedure.documents.length ? procedure.documents.map(doc => (
                      <button key={doc.id} type="button" className="inlineAction" onClick={() => downloadProcedureDocument(procedure.id, doc.id, doc.fileName)}>
                        <Download size={13} /> {doc.fileName}
                      </button>
                    )) : <small className="muted">{t('publicAssignment.noDocuments')}</small>}
                  </div>
                </div>
              ))}
            </div>
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
          <>
            <label className="checkField">
              <input type="checkbox" checked={consentChecked} onChange={event => setConsentChecked(event.target.checked)} />
              {' '}{t('publicAssignment.consentLabel')}
            </label>
            <div className="formActions formActions--split">
              <span />
              <Button disabled={accepting || !consentChecked} onClick={accept} icon={<CheckCircle2 size={16} />}>
                {accepting ? t('publicAssignment.accepting') : t('publicAssignment.acceptButton')}
              </Button>
            </div>
          </>
        ) : (
          <p className="muted">{t('publicAssignment.notAcceptable')}</p>
        )}
      </section>
    </main>
  );
}
