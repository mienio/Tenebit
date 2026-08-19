import { FormEvent, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { AlertTriangle, CheckCircle2, Copy, Eye, Link2, Mail, Pencil, Plus, RefreshCw, RotateCcw, ShieldCheck, UserRoundX, Wrench, XCircle } from 'lucide-react';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { DetailGrid, DetailItem } from '../components/DetailGrid';
import { Field, SelectInput, TextArea, TextInput } from '../components/FormFields';
import { Modal } from '../components/Modal';
import { PageHeader } from '../components/PageHeader';
import { Pagination } from '../components/Pagination';
import { StatusBadge } from '../components/StatusBadge';
import { EmptyState, ErrorState, LoadingState } from '../components/StateViews';
import { useAsyncData } from '../hooks/useAsyncData';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import { useI18n } from '../i18n/I18nProvider';
import type { InspectionOutcome, OffboardingCaseDetails, OffboardingCaseStatus, OffboardingCaseSummary, OffboardingItem, OffboardingItemStatus, OffboardingPreview, Person } from '../types/domain';
import { formatDate, formatDateTime, toNullable } from '../utils/format';

const pageSize = 10;
const statusValues: OffboardingCaseStatus[] = ['Draft', 'Active', 'WaitingForReturn', 'ReadyToClose', 'Completed', 'Cancelled'];
const resolutionValues: OffboardingItemStatus[] = ['Missing', 'Damaged', 'Retained'];
const inspectionOutcomeValues: InspectionOutcome[] = ['ReadyForReuse', 'Damaged', 'Retired', 'Disposed'];

const todayIso = () => new Date().toISOString().slice(0, 10);

function requiredProgress(items: OffboardingItem[]) {
  const required = items.filter(item => item.required);
  if (!required.length) return 0;
  const resolved = required.filter(item => ['Returned', 'Released', 'Missing', 'Damaged', 'Retained', 'Waived'].includes(item.status)).length;
  return Math.round((resolved / required.length) * 100);
}

function personStatusLabel(item: OffboardingCaseSummary, t: (key: string, params?: Record<string, string | number>) => string) {
  if (item.personDeactivatedAt) return t('offboarding.personStatusInactive');
  if (item.status === 'Cancelled') return t('offboarding.personStatusRestored');
  if (item.status === 'Draft') return t('offboarding.personStatusDraft');
  return t('offboarding.personStatusOffboarding');
}

function assetSettlementLabel(item: OffboardingCaseSummary, progress: number, t: (key: string, params?: Record<string, string | number>) => string) {
  if (item.status === 'Completed') return t('offboarding.assetSettlementCompleted');
  if (item.status === 'ReadyToClose') return t('offboarding.assetSettlementReady');
  if (item.status === 'WaitingForReturn') return t('offboarding.assetSettlementWaiting');
  return t('offboarding.assetSettlementInProgress', { percent: progress });
}

export function OffboardingPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const { id } = useParams<{ id?: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const [status, setStatus] = useState<OffboardingCaseStatus | ''>((searchParams.get('status') as OffboardingCaseStatus | null) ?? '');
  const [page, setPage] = useState(1);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<OffboardingCaseDetails | null>(null);
  const [prefillPersonId, setPrefillPersonId] = useState(searchParams.get('personId') ?? '');
  const [modalPersonId, setModalPersonId] = useState('');
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [saving, setSaving] = useState(false);
  const [startDialog, setStartDialog] = useState<OffboardingCaseSummary | null>(null);
  const [notifyEmployee, setNotifyEmployee] = useState(true);
  const [cancelTarget, setCancelTarget] = useState<OffboardingCaseSummary | null>(null);
  const [cancelReason, setCancelReason] = useState('');
  const [activeAction, setActiveAction] = useState('');
  const debouncedStatus = useDebouncedValue(status, 0);

  const listLoader = useMemo(() => () => api.offboardingPaged({ status: debouncedStatus, page, pageSize }), [debouncedStatus, page]);
  const list = useAsyncData(listLoader, [listLoader]);
  const people = useAsyncData(() => api.people(), []);
  const detailsLoader = useMemo(() => () => (id ? api.offboarding(id) : Promise.resolve(null)), [id]);
  const details = useAsyncData(detailsLoader, [detailsLoader]);
  const activityLoader = useMemo(() => () => (id ? api.activityLog({ entityType: 'offboarding_case', entityId: id, page: 1, pageSize: 20 }) : Promise.resolve(null)), [id]);
  const activity = useAsyncData(activityLoader, [activityLoader]);
  const previewLoader = useMemo(() => () => (modalOpen && !editing && modalPersonId ? api.offboardingPreview(modalPersonId) : Promise.resolve(null)), [modalOpen, editing, modalPersonId]);
  const preview = useAsyncData(previewLoader, [previewLoader]);

  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(() => setMessage(null), message.type === 'success' ? 3500 : 6500);
    return () => window.clearTimeout(timeout);
  }, [message]);

  useEffect(() => {
    const next = new URLSearchParams(searchParams);
    if (status) next.set('status', status); else next.delete('status');
    if (prefillPersonId) next.set('personId', prefillPersonId); else next.delete('personId');
    setSearchParams(next, { replace: true });
  }, [status, prefillPersonId, searchParams, setSearchParams]);

  useEffect(() => {
    if (!searchParams.get('new')) return;
    setEditing(null);
    setModalPersonId(searchParams.get('personId') ?? '');
    setModalOpen(true);
  }, [searchParams]);

  function openCreate(personId?: string) {
    setEditing(null);
    setPrefillPersonId(personId ?? '');
    setModalPersonId(personId ?? '');
    setModalOpen(true);
  }

  async function reloadAll() {
    await Promise.all([list.reload(), details.reload(), activity.reload(), people.reload()]);
  }

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const body = {
      personId: String(form.get('personId') ?? ''),
      employmentEndsAt: String(form.get('employmentEndsAt') ?? ''),
      returnDueDate: String(form.get('returnDueDate') ?? ''),
      defaultReturnLocation: toNullable(String(form.get('defaultReturnLocation') ?? '')),
      notes: toNullable(String(form.get('notes') ?? '')),
      processOwnerId: toNullable(String(form.get('processOwnerId') ?? '')),
      blockNewReservations: form.get('blockNewReservations') === 'on',
      cancelFutureReservations: form.get('cancelFutureReservations') === 'on',
      autoReleaseLicenses: form.get('autoReleaseLicenses') === 'on'
    };
    if (!body.employmentEndsAt || !body.returnDueDate || (!editing && !body.personId)) {
      return setMessage({ type: 'error', text: t('offboarding.formRequired') });
    }
    setSaving(true);
    try {
      const response = editing
        ? await api.updateOffboarding(editing.case.id, { ...body, personId: undefined } as never)
        : await api.createOffboarding(body);
      setModalOpen(false);
      setEditing(null);
      setMessage({ type: 'success', text: editing ? t('offboarding.saved') : t('offboarding.created') });
      await list.reload();
      if (!editing) navigate(`/offboarding/${response.case.id}`);
      else if (id === editing.case.id) await Promise.all([details.reload(), activity.reload()]);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('offboarding.saveFailed') });
    } finally {
      setSaving(false);
    }
  }

  async function handleCaseAction(actionKey: string, run: () => Promise<unknown>, successKey: string) {
    setActiveAction(actionKey);
    try {
      await run();
      setMessage({ type: 'success', text: t(successKey) });
      await Promise.all([list.reload(), details.reload(), activity.reload(), people.reload()]);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('offboarding.actionFailed') });
    } finally {
      setActiveAction('');
    }
  }

  if (list.isLoading && !list.data) return <LoadingState title={t('offboarding.loadingTitle')} description={t('offboarding.loadingDesc')} />;
  if (list.error) return <ErrorState message={list.error} onRetry={list.reload} />;

  const items = list.data?.items ?? [];
  const now = new Date().toISOString();
  const activeCount = items.filter(item => ['Draft', 'Active', 'WaitingForReturn', 'ReadyToClose'].includes(item.status)).length;
  const overdueCount = items.filter(item => item.status !== 'Completed' && item.status !== 'Cancelled' && item.returnDueDate < now).length;
  const readyCount = items.filter(item => item.status === 'ReadyToClose').length;

  return (
    <div className="pageStack">
      <PageHeader
        eyebrow={t('page.offboarding.eyebrow')}
        title={t('page.offboarding.title')}
        actions={
          <div className="rowActions">
            <Button variant="secondary" onClick={() => void reloadAll()} icon={<RefreshCw size={16} />}>{t('common.refresh')}</Button>
            <Button onClick={() => openCreate(prefillPersonId)} icon={<Plus size={16} />}>{t('offboarding.new')}</Button>
          </div>
        }
      />

      {message ? <div className="toastStack" aria-live="polite"><div className={`toast toast--${message.type}`}>{message.text}</div></div> : null}

      <div style={{ display: 'grid', gap: '16px', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))' }}>
        <Card><div className="sectionTitle"><div><h2>{activeCount}</h2><p>{t('offboarding.counterActive')}</p></div></div></Card>
        <Card><div className="sectionTitle"><div><h2>{overdueCount}</h2><p>{t('offboarding.counterOverdue')}</p></div></div></Card>
        <Card><div className="sectionTitle"><div><h2>{readyCount}</h2><p>{t('offboarding.counterReady')}</p></div></div></Card>
      </div>

      <Card className="toolbarCard">
        <div className="filters filters--three">
          <Field label={t('offboarding.statusFilter')}>
            <SelectInput value={status} onChange={event => { setStatus(event.target.value as OffboardingCaseStatus | ''); setPage(1); }}>
              <option value="">{t('offboarding.allStatuses')}</option>
              {statusValues.map(value => <option key={value} value={value}>{t(`status.${value}`)}</option>)}
            </SelectInput>
          </Field>
        </div>
      </Card>

      {!items.length ? (
        <EmptyState
          title={t('offboarding.emptyTitle')}
          description={t('offboarding.emptyDesc')}
          action={<Button onClick={() => openCreate(prefillPersonId)} icon={<Plus size={16} />}>{t('offboarding.new')}</Button>}
        />
      ) : (
        <Card>
          <div className="tableWrap tableWrap--cards">
            <table>
              <thead>
                <tr>
                  <th>{t('offboarding.colPerson')}</th>
                  <th>{t('offboarding.colEmploymentEnds')}</th>
                  <th>{t('offboarding.colReturnDue')}</th>
                  <th>{t('offboarding.colProgress')}</th>
                  <th>{t('offboarding.colStatus')}</th>
                  <th>{t('offboarding.colOwner')}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {items.map(item => {
                  const progress = details.data?.case.id === item.id ? requiredProgress(details.data.items) : 0;
                  const owner = people.data?.find(person => person.id === item.processOwnerId)?.fullName ?? '-';
                  return (
                    <tr key={item.id}>
                      <td data-label={t('offboarding.colPerson')}><strong>{item.personName ?? '-'}</strong></td>
                      <td data-label={t('offboarding.colEmploymentEnds')}>{formatDate(item.employmentEndsAt)}</td>
                      <td data-label={t('offboarding.colReturnDue')}>{formatDate(item.returnDueDate)}</td>
                      <td data-label={t('offboarding.colProgress')}>{progress}%</td>
                      <td data-label={t('offboarding.colStatus')}><StatusBadge status={item.status} /></td>
                      <td data-label={t('offboarding.colOwner')}>{owner}</td>
                      <td>
                        <div className="tableActions">
                          <button type="button" className="iconButton" aria-label={t('offboarding.detailsAria')} onClick={() => navigate(`/offboarding/${item.id}`)}><Eye size={16} /></button>
                          {item.status === 'Draft' ? <button type="button" className="iconButton iconButton--success" aria-label={t('offboarding.start')} onClick={() => setStartDialog(item)}><CheckCircle2 size={16} /></button> : null}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <Pagination page={page} total={list.data?.total ?? 0} pageSize={pageSize} onPageChange={setPage} />
        </Card>
      )}

      {id ? (
        details.isLoading && !details.data ? <LoadingState title={t('offboarding.detailsLoadingTitle')} /> :
          details.error || !details.data ? <ErrorState message={details.error ?? t('offboarding.detailsLoadFailed')} onRetry={details.reload} /> :
            <OffboardingDetailsView
              details={details.data}
              activityItems={activity.data?.items ?? []}
              people={people.data ?? []}
              actionBusy={activeAction}
              onEdit={() => { setEditing(details.data); setModalOpen(true); }}
              onAction={handleCaseAction}
              onCancel={() => setCancelTarget(details.data?.case ?? null)}
            />
      ) : null}

      <Modal open={modalOpen} title={editing ? t('offboarding.editTitle') : t('offboarding.createTitle')} onClose={() => setModalOpen(false)} width="wide">
        <form className="formGrid" onSubmit={handleSave} key={editing?.case.id ?? 'new-offboarding'}>
          {!editing ? (
            <>
              <Field label={t('offboarding.personLabel')}>
                <SelectInput name="personId" required value={modalPersonId} onChange={event => setModalPersonId(event.target.value)}>
                  <option value="">{t('offboarding.personChoose')}</option>
                  {(people.data ?? []).filter(person => person.employmentStatus === 'Active').map(person => <option key={person.id} value={person.id}>{person.fullName}</option>)}
                </SelectInput>
              </Field>
              {modalPersonId ? <OffboardingPreviewBlock preview={preview.data} isLoading={preview.isLoading} /> : null}
            </>
          ) : null}
          <Field label={t('offboarding.employmentEndsAtLabel')}><TextInput name="employmentEndsAt" type="datetime-local" defaultValue={toLocalDateTimeValue(editing?.case.employmentEndsAt)} min={todayIso()} required /></Field>
          <Field label={t('offboarding.returnDueDateLabel')}><TextInput name="returnDueDate" type="datetime-local" defaultValue={toLocalDateTimeValue(editing?.case.returnDueDate)} min={todayIso()} required /></Field>
          <Field label={t('offboarding.returnLocationLabel')}><TextInput name="defaultReturnLocation" defaultValue={editing?.case.defaultReturnLocation ?? ''} /></Field>
          <Field label={t('offboarding.processOwnerLabel')}>
            <SelectInput name="processOwnerId" defaultValue={editing?.case.processOwnerId ?? ''}>
              <option value="">{t('common.unassigned')}</option>
              {(people.data ?? []).map(person => <option key={person.id} value={person.id}>{person.fullName}</option>)}
            </SelectInput>
          </Field>
          <Field label={t('offboarding.notesLabel')}><TextArea name="notes" defaultValue={editing?.case.notes ?? ''} /></Field>
          <label className="checkField"><input type="checkbox" name="blockNewReservations" defaultChecked={editing?.case.blockNewReservations ?? true} /> {t('offboarding.blockNewReservations')}</label>
          <label className="checkField"><input type="checkbox" name="cancelFutureReservations" defaultChecked={editing?.case.cancelFutureReservations ?? true} /> {t('offboarding.cancelFutureReservations')}</label>
          <label className="checkField"><input type="checkbox" name="autoReleaseLicenses" defaultChecked={editing?.case.autoReleaseLicenses ?? true} /> {t('offboarding.autoReleaseLicenses')}</label>
          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={() => setModalOpen(false)}>{t('common.cancel')}</Button>
            <Button disabled={saving}>{saving ? t('common.saving') : editing ? t('offboarding.save') : t('offboarding.create')}</Button>
          </div>
        </form>
      </Modal>

      <ConfirmDialog
        open={!!startDialog}
        title={t('offboarding.startConfirmTitle')}
        description={t('offboarding.startConfirmDesc')}
        confirmLabel={t('offboarding.start')}
        onConfirm={() => {
          const current = startDialog;
          setStartDialog(null);
          if (!current) return;
          void handleCaseAction('start', () => api.startOffboarding(current.id, { notifyEmployee }), 'offboarding.started');
        }}
        onClose={() => setStartDialog(null)}
      >
        <label className="checkField"><input type="checkbox" checked={notifyEmployee} onChange={event => setNotifyEmployee(event.target.checked)} /> {t('offboarding.notifyEmployee')}</label>
      </ConfirmDialog>

      <Modal open={!!cancelTarget} title={t('offboarding.cancelTitle')} onClose={() => setCancelTarget(null)}>
        <div className="formGrid">
          <Field label={t('offboarding.cancelReasonLabel')}><TextArea value={cancelReason} onChange={event => setCancelReason(event.target.value)} /></Field>
          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={() => setCancelTarget(null)}>{t('common.cancel')}</Button>
            <Button
              type="button"
              disabled={!cancelReason.trim()}
              onClick={() => {
                const current = cancelTarget;
                setCancelTarget(null);
                if (!current) return;
                void handleCaseAction('cancel', () => api.cancelOffboarding(current.id, { reason: cancelReason.trim() }), 'offboarding.cancelled');
                setCancelReason('');
              }}
            >
              {t('offboarding.cancelAction')}
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}

function OffboardingDetailsView({
  details,
  activityItems,
  people,
  actionBusy,
  onEdit,
  onAction,
  onCancel
}: {
  details: OffboardingCaseDetails;
  activityItems: import('../types/domain').ActivityLogEntry[];
  people: Person[];
  actionBusy: string;
  onEdit: () => void;
  onAction: (actionKey: string, run: () => Promise<unknown>, successKey: string) => Promise<void>;
  onCancel: () => void;
}) {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [linkState, setLinkState] = useState<'idle' | 'copied' | 'failed'>('idle');
  const [confirmItem, setConfirmItem] = useState<OffboardingItem | null>(null);
  const [resolveItem, setResolveItem] = useState<OffboardingItem | null>(null);
  const [waiveItem, setWaiveItem] = useState<OffboardingItem | null>(null);
  const [inspectItem, setInspectItem] = useState<OffboardingItem | null>(null);

  const progress = requiredProgress(details.items);
  const caseItem = details.case;
  const owner = people.find(person => person.id === caseItem.processOwnerId)?.fullName ?? t('common.unassigned');
  const equipment = details.items.filter(item => item.type === 'AssetReturn');
  const licenses = details.items.filter(item => item.type === 'LicenseRelease');
  const tasks = details.items.filter(item => item.type === 'ManualTask');

  async function copyLink() {
    try {
      const url = await api.regenerateOffboardingLink(caseItem.id);
      await navigator.clipboard.writeText(url);
      setLinkState('copied');
    } catch {
      setLinkState('failed');
    } finally {
      window.setTimeout(() => setLinkState('idle'), 3000);
    }
  }

  return (
    <div className="pageStack">
      <Card>
        <div className="sectionTitle">
          <div>
            <h2>{caseItem.personName ?? '-'}</h2>
            <p>{t('offboarding.detailsSubtitle', { owner })}</p>
          </div>
          <div className="rowActions">
            <Button variant="secondary" onClick={onEdit} icon={<Pencil size={16} />}>{t('common.edit')}</Button>
            {caseItem.status === 'Draft' ? <Button disabled={actionBusy === 'start'} onClick={() => void onAction('start', () => api.startOffboarding(caseItem.id, { notifyEmployee: true }), 'offboarding.started')} icon={<Mail size={16} />}>{t('offboarding.start')}</Button> : null}
          </div>
        </div>
        <DetailGrid>
          <DetailItem label={t('offboarding.statusLabel')} value={<StatusBadge status={caseItem.status} />} />
          <DetailItem label={t('offboarding.progressLabel')} value={`${progress}%`} />
          <DetailItem label={t('offboarding.personIndicator')} value={personStatusLabel(caseItem, t)} />
          <DetailItem label={t('offboarding.assetIndicator')} value={assetSettlementLabel(caseItem, progress, t)} />
          <DetailItem label={t('offboarding.employmentEndsAtLabel')} value={formatDateTime(caseItem.employmentEndsAt)} />
          <DetailItem label={t('offboarding.returnDueDateLabel')} value={formatDateTime(caseItem.returnDueDate)} />
        </DetailGrid>
        <div style={{ marginTop: '12px', background: 'var(--border)', borderRadius: '999px', height: '10px', overflow: 'hidden' }}>
          <div style={{ width: `${progress}%`, height: '100%', background: 'var(--brand)' }} />
        </div>
      </Card>

      <Card>
        <div className="sectionTitle"><div><h2>{t('offboarding.scheduledActionsTitle')}</h2></div></div>
        <DetailGrid>
          <DetailItem label={t('offboarding.employmentEndsAtLabel')} value={formatDateTime(caseItem.employmentEndsAt)} />
          <DetailItem label={t('offboarding.personDeactivatedAtLabel')} value={caseItem.personDeactivatedAt ? formatDateTime(caseItem.personDeactivatedAt) : t('common.none')} />
          <DetailItem label={t('offboarding.scheduledActionsCompletedAtLabel')} value={caseItem.scheduledActionsCompletedAt ? formatDateTime(caseItem.scheduledActionsCompletedAt) : t('common.none')} />
          <DetailItem label={t('offboarding.scheduledActionsFlags')} value={[caseItem.blockNewReservations ? t('offboarding.blockNewReservationsShort') : null, caseItem.cancelFutureReservations ? t('offboarding.cancelFutureReservationsShort') : null, caseItem.autoReleaseLicenses ? t('offboarding.autoReleaseLicensesShort') : null].filter(Boolean).join(' · ') || t('common.none')} />
        </DetailGrid>
        <div className="formActions formActions--split">
          <span />
          <Button disabled={actionBusy === 'scheduled'} onClick={() => void onAction('scheduled', () => api.executeOffboardingScheduledActions(caseItem.id), 'offboarding.scheduledActionsExecuted')} icon={<RefreshCw size={16} />}>{t('offboarding.executeScheduled')}</Button>
        </div>
      </Card>

      <Card className="card--flat">
        <div className="sectionTitle"><div><h2>{t('offboarding.reservationsTitle')}</h2></div></div>
        {!(details.reservations ?? []).length ? <p className="muted">{t('offboarding.noReservations')}</p> : (
          <div className="listRows">
            {details.reservations.map(reservation => (
              <div className="listRow" key={reservation.id}>
                <div>
                  <strong>{reservation.purpose}</strong>
                  <small>{formatDateTime(reservation.startAt)} – {formatDateTime(reservation.endAt)}</small>
                </div>
                <StatusBadge status={reservation.status} />
              </div>
            ))}
          </div>
        )}
      </Card>

      <Card className="card--flat">
        <div className="sectionTitle"><div><h2>{t('offboarding.equipmentTitle')}</h2></div></div>
        {!equipment.length ? <p className="muted">{t('offboarding.noEquipment')}</p> : (
          <div className="listRows">
            {equipment.map(item => (
              <div className="listRow" key={item.id}>
                <div>
                  <strong>{item.label}</strong>
                  <small>{t('offboarding.employeeResponseLabel')}: {item.employeeResponse ? t(`offboarding.response.${item.employeeResponse}`) : t('common.none')}</small>
                </div>
                <div className="rowActions">
                  <StatusBadge status={item.status} />
                  {(item.status === 'Pending' || item.status === 'EmployeeAcknowledged') ? <Button variant="secondary" onClick={() => setConfirmItem(item)} icon={<CheckCircle2 size={16} />}>{t('offboarding.confirmReturn')}</Button> : null}
                  {item.status === 'Received' ? <Button variant="secondary" onClick={() => setInspectItem(item)} icon={<Wrench size={16} />}>{t('offboarding.completeInspection')}</Button> : null}
                  {!['Returned', 'Waived', 'Missing', 'Damaged', 'Retained'].includes(item.status) ? <Button variant="secondary" onClick={() => setResolveItem(item)} icon={<AlertTriangle size={16} />}>{t('offboarding.resolve')}</Button> : null}
                  {!['Returned', 'Released', 'Missing', 'Damaged', 'Retained', 'Waived'].includes(item.status) ? <Button variant="secondary" onClick={() => setWaiveItem(item)} icon={<XCircle size={16} />}>{t('offboarding.waive')}</Button> : null}
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>

      <Card className="card--flat">
        <div className="sectionTitle"><div><h2>{t('offboarding.licensesTitle')}</h2></div></div>
        {!licenses.length ? <p className="muted">{t('offboarding.noLicenses')}</p> : (
          <div className="listRows">
            {licenses.map(item => (
              <div className="listRow" key={item.id}>
                <div>
                  <strong>{item.label}</strong>
                  <small>{item.automationMode === 'AtEmploymentEnd' ? t('offboarding.automationEmploymentEnd') : t('offboarding.automationManual')}</small>
                </div>
                <div className="rowActions">
                  <StatusBadge status={item.status} />
                  {item.status !== 'Released' ? <Button variant="secondary" onClick={() => void onAction(`release-${item.id}`, () => api.releaseOffboardingLicense(caseItem.id, item.id), 'offboarding.licenseReleased')} icon={<ShieldCheck size={16} />}>{t('offboarding.releaseLicense')}</Button> : null}
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>

      <Card className="card--flat">
        <div className="sectionTitle"><div><h2>{t('offboarding.manualTasksTitle')}</h2></div></div>
        {!tasks.length ? <p className="muted">{t('offboarding.noManualTasks')}</p> : (
          <div className="listRows">
            {tasks.map(item => (
              <div className="listRow" key={item.id}>
                <div><strong>{item.label}</strong><small>{item.resolutionNotes ?? t('common.none')}</small></div>
                <StatusBadge status={item.status} />
              </div>
            ))}
          </div>
        )}
      </Card>

      {activityItems.length ? (
        <Card className="card--flat">
          <div className="sectionTitle"><div><h2>{t('offboarding.timelineTitle')}</h2></div></div>
          <div className="listRows">
            {activityItems.map(entry => (
              <div className="listRow" key={entry.id}>
                <div><strong>{t(`activity.${entry.action}`)}</strong><small>{entry.actorDisplay} · {formatDateTime(entry.createdAt)}</small></div>
                <span>{entry.details ?? '-'}</span>
              </div>
            ))}
          </div>
        </Card>
      ) : null}

      <Card>
        <div className="formActions" style={{ justifyContent: 'space-between', flexWrap: 'wrap' }}>
          <Button variant="secondary" onClick={() => navigate('/offboarding')} icon={<RotateCcw size={16} />}>{t('common.back')}</Button>
          <div className="rowActions">
            <Button variant="secondary" onClick={copyLink} icon={<Link2 size={16} />}>{linkState === 'copied' ? t('offboarding.linkCopied') : linkState === 'failed' ? t('offboarding.linkCopyFailed') : t('offboarding.copyLink')}</Button>
            <Button variant="secondary" onClick={() => void onAction('resend', () => api.resendOffboarding(caseItem.id), 'offboarding.resent')} icon={<Mail size={16} />}>{t('offboarding.resend')}</Button>
            <Button variant="secondary" onClick={() => void onAction('regenerate', () => api.regenerateOffboardingLink(caseItem.id), 'offboarding.linkRegenerated')} icon={<Copy size={16} />}>{t('offboarding.regenerateLink')}</Button>
            <Button variant="secondary" onClick={() => void onAction('restore', () => api.restoreOffboardingEmployment(caseItem.id), 'offboarding.restored')} icon={<UserRoundX size={16} />}>{t('offboarding.restoreEmployment')}</Button>
            <Button variant="secondary" onClick={onCancel} icon={<XCircle size={16} />}>{t('offboarding.cancelAction')}</Button>
            <Button variant="secondary" onClick={() => void onAction('complete', () => api.completeOffboarding(caseItem.id), 'offboarding.completed')} icon={<CheckCircle2 size={16} />}>{t('offboarding.complete')}</Button>
          </div>
        </div>
      </Card>

      <ConfirmReturnModal item={confirmItem} onClose={() => setConfirmItem(null)} onSubmit={(body) => onAction(`confirm-${confirmItem?.id}`, () => api.confirmOffboardingItemReturn(caseItem.id, confirmItem!.id, body), 'offboarding.returnConfirmed')} />
      <ResolveItemModal item={resolveItem} onClose={() => setResolveItem(null)} onSubmit={(body) => onAction(`resolve-${resolveItem?.id}`, () => api.resolveOffboardingItem(caseItem.id, resolveItem!.id, body), 'offboarding.itemResolved')} />
      <WaiveItemModal item={waiveItem} onClose={() => setWaiveItem(null)} onSubmit={(body) => onAction(`waive-${waiveItem?.id}`, () => api.waiveOffboardingItem(caseItem.id, waiveItem!.id, body), 'offboarding.itemWaived')} />
      <InspectionModal item={inspectItem} onClose={() => setInspectItem(null)} onSubmit={(body) => onAction(`inspect-${inspectItem?.id}`, () => api.completeOffboardingInspection(caseItem.id, inspectItem!.id, body), 'offboarding.inspectionCompleted')} />
    </div>
  );
}

function OffboardingPreviewBlock({ preview, isLoading }: { preview: OffboardingPreview | null; isLoading: boolean }) {
  const { t } = useI18n();
  if (isLoading && !preview) return <p className="muted" style={{ gridColumn: '1 / -1' }}>{t('offboarding.previewLoading')}</p>;
  if (!preview) return null;

  const sections = [
    {
      title: t('offboarding.previewHeldAssets'),
      rows: preview.heldAssets.map(asset => ({ key: asset.id, label: asset.name, sub: asset.assetTag, badgeStatus: asset.status as string, badgeLabel: undefined }))
    },
    {
      title: t('offboarding.previewOpenAssignments'),
      rows: preview.openAssignments.map(assignment => ({ key: assignment.id, label: assignment.protocolNumber || '-', sub: formatDateTime(assignment.issuedAt), badgeStatus: assignment.status as string, badgeLabel: undefined }))
    },
    {
      title: t('offboarding.previewLicenseSeats'),
      rows: preview.licenseSeats.map(license => ({ key: license.id, label: license.name, sub: undefined, badgeStatus: undefined, badgeLabel: undefined }))
    },
    {
      title: t('offboarding.previewReservations'),
      rows: preview.reservations.map(reservation => ({ key: reservation.id, label: reservation.purpose, sub: `${formatDateTime(reservation.startAt)} – ${formatDateTime(reservation.endAt)}`, badgeStatus: reservation.status as string, badgeLabel: undefined }))
    },
    {
      title: t('offboarding.previewAuditItems'),
      rows: preview.unresolvedAuditItems.map(item => ({ key: item.id, label: item.assetName, sub: [item.assetTag, item.campaignName].filter(Boolean).join(' · '), badgeStatus: item.response as string, badgeLabel: t(`assetAudits.response.${item.response}`) }))
    }
  ];

  return (
    <div style={{ gridColumn: '1 / -1' }}>
      <p><strong>{t('offboarding.previewTitle')}</strong></p>
      {sections.map(section => (
        <div key={section.title} style={{ marginBottom: '12px' }}>
          <small>{section.title} ({section.rows.length})</small>
          {!section.rows.length ? <p className="muted">{t('offboarding.previewEmpty')}</p> : (
            <div className="listRows">
              {section.rows.map(row => (
                <div className="listRow" key={row.key}>
                  <div><strong>{row.label}</strong>{row.sub ? <small>{row.sub}</small> : null}</div>
                  {row.badgeStatus ? <StatusBadge status={row.badgeStatus} label={row.badgeLabel} /> : null}
                </div>
              ))}
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

function ConfirmReturnModal({ item, onClose, onSubmit }: { item: OffboardingItem | null; onClose: () => void; onSubmit: (body: { returnCondition?: string | null; returnLocation?: string | null; notes?: string | null }) => Promise<void> }) {
  const { t } = useI18n();
  return (
    <Modal open={!!item} title={t('offboarding.confirmReturnTitle')} description={item?.label} onClose={onClose}>
      <form className="formGrid" onSubmit={event => {
        event.preventDefault();
        const form = new FormData(event.currentTarget);
        void onSubmit({
          returnCondition: toNullable(String(form.get('returnCondition') ?? '')),
          returnLocation: toNullable(String(form.get('returnLocation') ?? '')),
          notes: toNullable(String(form.get('notes') ?? ''))
        }).then(onClose);
      }}>
        <Field label={t('offboarding.returnConditionLabel')}><TextArea name="returnCondition" /></Field>
        <Field label={t('offboarding.returnLocationLabel')}><TextInput name="returnLocation" /></Field>
        <Field label={t('offboarding.notesLabel')}><TextArea name="notes" /></Field>
        <div className="formActions formActions--split"><Button type="button" variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button>{t('offboarding.confirmReturn')}</Button></div>
      </form>
    </Modal>
  );
}

function ResolveItemModal({ item, onClose, onSubmit }: { item: OffboardingItem | null; onClose: () => void; onSubmit: (body: { status: string; notes: string }) => Promise<void> }) {
  const { t } = useI18n();
  return (
    <Modal open={!!item} title={t('offboarding.resolveTitle')} description={item?.label} onClose={onClose}>
      <form className="formGrid" onSubmit={event => {
        event.preventDefault();
        const form = new FormData(event.currentTarget);
        void onSubmit({ status: String(form.get('status') ?? ''), notes: String(form.get('notes') ?? '').trim() }).then(onClose);
      }}>
        <Field label={t('offboarding.resolveStatusLabel')}>
          <SelectInput name="status" defaultValue="Missing">
            {resolutionValues.map(value => <option key={value} value={value}>{t(`status.${value}`)}</option>)}
          </SelectInput>
        </Field>
        <Field label={t('offboarding.resolveNotesLabel')}><TextArea name="notes" required /></Field>
        <div className="formActions formActions--split"><Button type="button" variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button>{t('offboarding.resolve')}</Button></div>
      </form>
    </Modal>
  );
}

function WaiveItemModal({ item, onClose, onSubmit }: { item: OffboardingItem | null; onClose: () => void; onSubmit: (body: { reason: string }) => Promise<void> }) {
  const { t } = useI18n();
  return (
    <Modal open={!!item} title={t('offboarding.waiveTitle')} description={item?.label} onClose={onClose}>
      <form className="formGrid" onSubmit={event => {
        event.preventDefault();
        const form = new FormData(event.currentTarget);
        void onSubmit({ reason: String(form.get('reason') ?? '').trim() }).then(onClose);
      }}>
        <Field label={t('offboarding.waiveReasonLabel')}><TextArea name="reason" required /></Field>
        <div className="formActions formActions--split"><Button type="button" variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button>{t('offboarding.waive')}</Button></div>
      </form>
    </Modal>
  );
}

function InspectionModal({ item, onClose, onSubmit }: { item: OffboardingItem | null; onClose: () => void; onSubmit: (body: { outcome: string; serialNumberMatched: boolean; accessoriesComplete: boolean; dataWiped: boolean; functionalTestPassed: boolean; damageAssessmentNotes?: string | null; notes?: string | null }) => Promise<void> }) {
  const { t } = useI18n();
  return (
    <Modal open={!!item} title={t('offboarding.inspectionTitle')} description={item?.label} onClose={onClose}>
      <form className="formGrid" onSubmit={event => {
        event.preventDefault();
        const form = new FormData(event.currentTarget);
        void onSubmit({
          outcome: String(form.get('outcome') ?? ''),
          serialNumberMatched: form.get('serialNumberMatched') === 'on',
          accessoriesComplete: form.get('accessoriesComplete') === 'on',
          dataWiped: form.get('dataWiped') === 'on',
          functionalTestPassed: form.get('functionalTestPassed') === 'on',
          damageAssessmentNotes: toNullable(String(form.get('damageAssessmentNotes') ?? '')),
          notes: toNullable(String(form.get('notes') ?? ''))
        }).then(onClose);
      }}>
        <Field label={t('offboarding.inspectionOutcomeLabel')}>
          <SelectInput name="outcome" defaultValue="ReadyForReuse">
            {inspectionOutcomeValues.map(value => <option key={value} value={value}>{t(`offboarding.outcome.${value}`)}</option>)}
          </SelectInput>
        </Field>
        <label className="checkField"><input type="checkbox" name="serialNumberMatched" defaultChecked /> {t('offboarding.serialNumberMatched')}</label>
        <label className="checkField"><input type="checkbox" name="accessoriesComplete" defaultChecked /> {t('offboarding.accessoriesComplete')}</label>
        <label className="checkField"><input type="checkbox" name="dataWiped" defaultChecked /> {t('offboarding.dataWiped')}</label>
        <label className="checkField"><input type="checkbox" name="functionalTestPassed" defaultChecked /> {t('offboarding.functionalTestPassed')}</label>
        <Field label={t('offboarding.damageAssessmentNotesLabel')}><TextArea name="damageAssessmentNotes" /></Field>
        <Field label={t('offboarding.notesLabel')}><TextArea name="notes" /></Field>
        <div className="formActions formActions--split"><Button type="button" variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button>{t('offboarding.completeInspection')}</Button></div>
      </form>
    </Modal>
  );
}

function toLocalDateTimeValue(value?: string | null) {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const tz = date.getTimezoneOffset() * 60000;
  return new Date(date.getTime() - tz).toISOString().slice(0, 16);
}
