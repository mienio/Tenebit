import { CheckCircle2, Download, FileText, Users } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { canSee, nav } from '../components/Layout';
import { LocationInventoryModal } from '../components/LocationInventoryModal';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { EmptyState, ErrorState, LoadingState } from '../components/StateViews';
import { useAuth } from '../auth/AuthProvider';
import { useAsyncData } from '../hooks/useAsyncData';
import { formatDate, formatDateTime } from '../utils/format';
import { CategoryIcon } from '../utils/categoryIcons';
import type { MyAssignment, MyProcedure } from '../types/domain';
import { useI18n } from '../i18n/I18nProvider';

const peopleNavRoles = nav.find(item => item.to === '/people')?.roles ?? [];

export function MyWorkspacePage() {
  const { t } = useI18n();
  const auth = useAuth();
  const workspace = useAsyncData(api.myWorkspace, []);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [accepting, setAccepting] = useState<string | null>(null);
  const [viewLocation, setViewLocation] = useState<string | null>(null);

  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(() => setMessage(null), message.type === 'success' ? 3500 : 6500);
    return () => window.clearTimeout(timeout);
  }, [message]);

  async function accept(assignment: MyAssignment) {
    setAccepting(assignment.id);
    setMessage(null);
    try {
      await api.acceptAssignment(assignment.id);
      setMessage({ type: 'success', text: t('myWorkspace.confirmed') });
      await workspace.reload();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('myWorkspace.confirmFailed') });
    } finally {
      setAccepting(null);
    }
  }

  async function downloadProtocol(assignment: MyAssignment) {
    try {
      const blob = await api.downloadAssignmentProtocol(assignment.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${assignment.protocolNumber}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('assignments.downloadProtocolFailed') });
    }
  }

  async function download(procedure: MyProcedure) {
    if (!procedure.documentId) return;
    try {
      const blob = await api.downloadProcedureDocument(procedure.procedureId, procedure.documentId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = procedure.documentFileName ?? `${procedure.title ?? 'procedura'}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('myWorkspace.downloadFailed') });
    }
  }

  if (workspace.isLoading && !workspace.data) return <LoadingState title={t('myWorkspace.loadingTitle')} description={t('myWorkspace.loadingDesc')} />;
  if (workspace.error || !workspace.data) return <ErrorState message={workspace.error ?? t('myWorkspace.noData')} onRetry={workspace.reload} />;

  const data = workspace.data;
  // No GET /api/offboarding personId filter exists in the backend route/DTO contract, so this page does not load per-person offboarding cards.

  if (!data.hasPersonRecord) {
    const canManagePeople = canSee(peopleNavRoles, auth.roles);
    const description = !auth.userEmail
      ? t('myWorkspace.noPersonDesc')
      : canManagePeople
        ? t('myWorkspace.noPersonDescSelfWithEmail', { email: auth.userEmail })
        : t('myWorkspace.noPersonDescWithEmail', { email: auth.userEmail });
    return (
      <div className="pageStack pageStack--narrow">
        <PageHeader eyebrow={t('page.my.eyebrow')} title={t('nav.my')} />
        <Card>
          <EmptyState
            title={t('myWorkspace.noPersonTitle')}
            description={description}
            action={canManagePeople ? (
              <Link to="/people?addSelf=1" className="button button--primary">
                <span className="button__icon"><Users size={16} /></span>
                <span>{t('myWorkspace.addSelf')}</span>
              </Link>
            ) : undefined}
          />
        </Card>
      </div>
    );
  }

  const pendingAssignments = data.assignments.filter(item => item.status === 'AwaitingAcceptance');
  const pastAssignments = data.assignments.filter(item => item.status !== 'AwaitingAcceptance');

  const procedureMap = new Map<string, MyProcedure>();
  for (const assignment of data.assignments) {
    for (const procedure of assignment.procedures) {
      procedureMap.set(procedure.procedureId, procedure);
    }
  }
  const procedures = Array.from(procedureMap.values());

  return (
    <div className="pageStack pageStack--narrow">
      <PageHeader eyebrow={t('page.my.eyebrow')} title={`${t('page.my.greeting')}, ${data.personName ?? ''}`} />

      {message ? <div className="toastStack" aria-live="polite"><div className={`toast toast--${message.type}`}>{message.text}</div></div> : null}

      {pendingAssignments.length > 0 && (
        <Card>
          <div className="sectionTitle"><div><h2>{t('myWorkspace.pending')}</h2></div></div>
          <div className="listRows">
            {pendingAssignments.map(assignment => (
              <div className="listRow" key={assignment.id}>
                <div>
                  <strong>{assignment.protocolNumber}</strong>
                  <small>{assignment.assetNames.join(', ')}</small>
                </div>
                <div className="rowActions">
                  <button className="iconButton" aria-label={t('assignments.downloadProtocol')} title={t('assignments.downloadProtocol')} onClick={() => downloadProtocol(assignment)}><Download size={16} /></button>
                  <Button disabled={accepting === assignment.id} onClick={() => accept(assignment)} icon={<CheckCircle2 size={16} />}>
                    {accepting === assignment.id ? t('myWorkspace.confirming') : t('myWorkspace.confirmReceipt')}
                  </Button>
                </div>
              </div>
            ))}
          </div>
        </Card>
      )}

      <Card>
        <div className="sectionTitle"><div><h2>{t('myWorkspace.yourEquipment')}</h2></div></div>
        {!data.assets.length ? <p className="muted">{t('myWorkspace.noEquipment')}</p> : (
          <div className="listRows">
            {data.assets.map(asset => (
              <div className="listRow" key={asset.id}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                  <div className="table-icon"><CategoryIcon icon={asset.categoryIcon} size={16} /></div>
                  <div><strong>{asset.name}</strong><small>{asset.assetTag} · {asset.categoryName ?? t('myWorkspace.noCategory')}</small></div>
                </div>
                {asset.location ? (
                  <button type="button" className="inlineAction" onClick={() => setViewLocation(asset.location ?? null)}>{asset.location}</button>
                ) : <span>{t('common.noLocation')}</span>}
              </div>
            ))}
          </div>
        )}
      </Card>

      <Card>
        <div className="sectionTitle"><div><h2>{t('myWorkspace.yourProcedures')}</h2></div></div>
        {!procedures.length ? <p className="muted">{t('myWorkspace.noProcedures')}</p> : (
          <div className="listRows">
            {procedures.map(procedure => (
              <div className="listRow" key={procedure.procedureId}>
                <div><FileText size={16} /> <strong>{procedure.title ?? t('myWorkspace.unnamedProcedure')}</strong></div>
                <div className="rowActions">
                  <StatusBadge status={procedure.status} />
                  {procedure.documentId ? (
                    <button className="iconButton" aria-label={t('myWorkspace.downloadAria', { file: procedure.title ?? '' })} onClick={() => download(procedure)}><Download size={16} /></button>
                  ) : null}
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>

      {pastAssignments.length > 0 && (
        <Card>
          <div className="sectionTitle"><div><h2>{t('myWorkspace.history')}</h2></div></div>
          <div className="listRows">
            {pastAssignments.map(assignment => (
              <div className="listRow" key={assignment.id}>
                <div><strong>{assignment.protocolNumber}</strong><small>{formatDateTime(assignment.issuedAt)} · {formatDate(assignment.dueDate)}</small></div>
                <div className="rowActions">
                  <button className="iconButton" aria-label={t('assignments.downloadProtocol')} title={t('assignments.downloadProtocol')} onClick={() => downloadProtocol(assignment)}><Download size={16} /></button>
                  <StatusBadge status={assignment.status} />
                </div>
              </div>
            ))}
          </div>
        </Card>
      )}

      {viewLocation && <LocationInventoryModal locationPath={viewLocation} onClose={() => setViewLocation(null)} />}
    </div>
  );
}
