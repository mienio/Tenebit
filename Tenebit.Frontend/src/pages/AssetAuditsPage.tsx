import { FormEvent, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { AlertTriangle, CheckCircle2, Download, Eye, Mail, Plus, RefreshCw, RotateCcw, XCircle } from 'lucide-react';
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
import type {
  AssetAuditCampaignDetailsResponse,
  AssetAuditCampaignPreviewResponse,
  AssetAuditCampaignResponse,
  AssetAuditCampaignStatus,
  AssetAuditItemAdminResponse,
  AssetAuditParticipantResponse,
  AssetAuditResolution,
  AssetAuditScopeType,
  Person,
  Team
} from '../types/domain';
import { formatDate, formatDateTime, toNullable } from '../utils/format';

const pageSize = 10;
const statusValues: AssetAuditCampaignStatus[] = ['Draft', 'Active', 'Reviewing', 'Completed', 'Cancelled'];
const scopeTypeValues: AssetAuditScopeType[] = ['Organization', 'Team', 'Location', 'AssetCategory', 'Person'];
const resolutionValues: AssetAuditResolution[] = ['Accepted', 'AssetMarkedLost', 'AssetMarkedDamaged', 'OwnershipCorrected', 'Dismissed'];
const exceptionResponses = ['Missing', 'Damaged', 'WrongOwner'];

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

function todayIso() {
  return new Date().toISOString().slice(0, 10);
}

function campaignCounts(items: AssetAuditItemAdminResponse[]) {
  return {
    confirmed: items.filter(item => item.response === 'Confirmed').length,
    missing: items.filter(item => item.response === 'Missing').length,
    damaged: items.filter(item => item.response === 'Damaged').length,
    wrongOwner: items.filter(item => item.response === 'WrongOwner').length
  };
}

export function AssetAuditsPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const { id } = useParams<{ id?: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const [status, setStatus] = useState<AssetAuditCampaignStatus | ''>((searchParams.get('status') as AssetAuditCampaignStatus | null) ?? '');
  const [page, setPage] = useState(1);
  const [wizardOpen, setWizardOpen] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [activeAction, setActiveAction] = useState('');
  const [cancelTarget, setCancelTarget] = useState<AssetAuditCampaignResponse | null>(null);
  const debouncedStatus = useDebouncedValue(status, 0);

  const listLoader = useMemo(() => () => api.assetAuditsPaged({ status: debouncedStatus, page, pageSize }), [debouncedStatus, page]);
  const list = useAsyncData(listLoader, [listLoader]);
  const detailsLoader = useMemo(() => () => (id ? api.assetAudit(id) : Promise.resolve(null)), [id]);
  const details = useAsyncData(detailsLoader, [detailsLoader]);

  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(() => setMessage(null), message.type === 'success' ? 3500 : 6500);
    return () => window.clearTimeout(timeout);
  }, [message]);

  useEffect(() => {
    const next = new URLSearchParams(searchParams);
    if (status) next.set('status', status); else next.delete('status');
    setSearchParams(next, { replace: true });
  }, [status, searchParams, setSearchParams]);

  async function reloadAll() {
    await Promise.all([list.reload(), details.reload()]);
  }

  async function handleAction(actionKey: string, run: () => Promise<unknown>, successKey: string) {
    setActiveAction(actionKey);
    try {
      await run();
      setMessage({ type: 'success', text: t(successKey) });
      await reloadAll();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('assetAudits.actionFailed') });
    } finally {
      setActiveAction('');
    }
  }

  if (list.isLoading && !list.data) return <LoadingState title={t('assetAudits.loadingTitle')} description={t('assetAudits.loadingDesc')} />;
  if (list.error) return <ErrorState message={list.error} onRetry={list.reload} />;

  const items = list.data?.items ?? [];

  return (
    <div className="pageStack">
      <PageHeader
        eyebrow={t('page.assetAudits.eyebrow')}
        title={t('page.assetAudits.title')}
        actions={
          <div className="rowActions">
            <Button variant="secondary" onClick={() => void reloadAll()} icon={<RefreshCw size={16} />}>{t('common.refresh')}</Button>
            <Button onClick={() => setWizardOpen(true)} icon={<Plus size={16} />}>{t('assetAudits.new')}</Button>
          </div>
        }
      />

      {message ? <div className="toastStack" aria-live="polite"><div className={`toast toast--${message.type}`}>{message.text}</div></div> : null}

      <Card className="toolbarCard">
        <div className="filters filters--three">
          <Field label={t('assetAudits.statusFilter')}>
            <SelectInput value={status} onChange={event => { setStatus(event.target.value as AssetAuditCampaignStatus | ''); setPage(1); }}>
              <option value="">{t('assetAudits.allStatuses')}</option>
              {statusValues.map(value => <option key={value} value={value}>{t(`status.${value}`)}</option>)}
            </SelectInput>
          </Field>
        </div>
      </Card>

      {!items.length ? (
        <EmptyState
          title={t('assetAudits.emptyTitle')}
          description={t('assetAudits.emptyDesc')}
          action={<Button onClick={() => setWizardOpen(true)} icon={<Plus size={16} />}>{t('assetAudits.new')}</Button>}
        />
      ) : (
        <Card>
          <div className="tableWrap tableWrap--oneLine">
            <table>
              <thead>
                <tr>
                  <th>{t('assetAudits.colName')}</th>
                  <th>{t('assetAudits.colDueDate')}</th>
                  <th>{t('assetAudits.colStatus')}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {items.map(item => (
                  <tr key={item.id}>
                    <td data-label={t('assetAudits.colName')}><strong>{item.name}</strong></td>
                    <td data-label={t('assetAudits.colDueDate')}>{formatDate(item.dueDate)}</td>
                    <td data-label={t('assetAudits.colStatus')}><StatusBadge status={item.status} /></td>
                    <td>
                      <div className="tableActions">
                        <button type="button" className="iconButton" aria-label={t('assetAudits.detailsAria')} onClick={() => navigate(`/asset-audits/${item.id}`)}><Eye size={16} /></button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination page={page} total={list.data?.total ?? 0} pageSize={pageSize} onPageChange={setPage} />
        </Card>
      )}

      {id ? (
        details.isLoading && !details.data ? <LoadingState title={t('assetAudits.detailsLoadingTitle')} /> :
          details.error || !details.data ? <ErrorState message={details.error ?? t('assetAudits.detailsLoadFailed')} onRetry={details.reload} /> :
            <AssetAuditDetailsView
              details={details.data}
              actionBusy={activeAction}
              onAction={handleAction}
              onCancel={() => setCancelTarget(details.data?.campaign ?? null)}
            />
      ) : null}

      <NewCampaignWizard open={wizardOpen} onClose={() => setWizardOpen(false)} onCreated={(campaignId) => { setWizardOpen(false); void list.reload(); navigate(`/asset-audits/${campaignId}`); }} />

      <ConfirmDialog
        open={!!cancelTarget}
        title={t('assetAudits.cancelConfirmTitle')}
        description={t('assetAudits.cancelConfirmDesc')}
        confirmLabel={t('assetAudits.cancelAction')}
        onConfirm={() => {
          const current = cancelTarget;
          setCancelTarget(null);
          if (!current) return;
          void handleAction('cancel', () => api.cancelAssetAudit(current.id), 'assetAudits.cancelled');
        }}
        onClose={() => setCancelTarget(null)}
      />
    </div>
  );
}

function NewCampaignWizard({ open, onClose, onCreated }: { open: boolean; onClose: () => void; onCreated: (campaignId: string) => void }) {
  const { t } = useI18n();
  const people = useAsyncData(() => api.people(), []);
  const teams = useAsyncData(() => api.teams(), []);
  const categories = useAsyncData(() => api.categories(), []);
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [dueDate, setDueDate] = useState('');
  const [scopeType, setScopeType] = useState<AssetAuditScopeType>('Organization');
  const [teamIds, setTeamIds] = useState<string[]>([]);
  const [locations, setLocations] = useState<string[]>([]);
  const [categoryIds, setCategoryIds] = useState<string[]>([]);
  const [personIds, setPersonIds] = useState<string[]>([]);
  const [preview, setPreview] = useState<AssetAuditCampaignPreviewResponse | null>(null);
  const [campaignId, setCampaignId] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const locationOptions = useMemo(() => Array.from(new Set((people.data ?? []).map(person => person.location).filter((value): value is string => !!value))), [people.data]);

  useEffect(() => {
    if (!open) {
      setStep(1);
      setName('');
      setDescription('');
      setDueDate('');
      setScopeType('Organization');
      setTeamIds([]);
      setLocations([]);
      setCategoryIds([]);
      setPersonIds([]);
      setPreview(null);
      setCampaignId(null);
      setError(null);
    }
  }, [open]);

  function buildScope() {
    return {
      type: scopeType,
      teamIds: scopeType === 'Team' ? teamIds : undefined,
      locations: scopeType === 'Location' ? locations : undefined,
      assetCategoryIds: scopeType === 'AssetCategory' ? categoryIds : undefined,
      personIds: scopeType === 'Person' ? personIds : undefined
    };
  }

  async function handleDetailsSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!name.trim() || !dueDate) {
      setError(t('assetAudits.formRequired'));
      return;
    }
    setError(null);
    setStep(2);
  }

  async function handleScopeNext() {
    setBusy(true);
    setError(null);
    try {
      const created = campaignId
        ? await api.updateAssetAudit(campaignId, { name: name.trim(), description: toNullable(description), dueDate, scope: buildScope() }).then(() => ({ campaign: { id: campaignId } }))
        : await api.createAssetAudit({ name: name.trim(), description: toNullable(description), dueDate, scope: buildScope() });
      setCampaignId(created.campaign.id);
      const previewResult = await api.previewAssetAudit(created.campaign.id);
      setPreview(previewResult);
      setStep(3);
    } catch (err) {
      setError(err instanceof Error ? err.message : t('assetAudits.createFailed'));
    } finally {
      setBusy(false);
    }
  }

  async function handleStart() {
    if (!campaignId) return;
    setBusy(true);
    setError(null);
    try {
      await api.startAssetAudit(campaignId);
      onCreated(campaignId);
    } catch (err) {
      setError(err instanceof Error ? err.message : t('assetAudits.startFailed'));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal open={open} title={t('assetAudits.wizardTitle')} description={t('assetAudits.wizardStep', { step, total: 3 })} onClose={onClose} width="wide">
      {error ? <p className="formMessage formMessage--error">{error}</p> : null}

      {step === 1 ? (
        <form className="formGrid" onSubmit={handleDetailsSubmit}>
          <Field label={t('assetAudits.nameLabel')}><TextInput value={name} onChange={event => setName(event.target.value)} required /></Field>
          <Field label={t('assetAudits.descriptionLabel')}><TextArea value={description} onChange={event => setDescription(event.target.value)} /></Field>
          <Field label={t('assetAudits.dueDateLabel')}><TextInput type="date" value={dueDate} onChange={event => setDueDate(event.target.value)} min={todayIso()} required /></Field>
          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>
            <Button>{t('common.next')}</Button>
          </div>
        </form>
      ) : null}

      {step === 2 ? (
        <div className="formGrid">
          <Field label={t('assetAudits.scopeTypeLabel')}>
            <SelectInput value={scopeType} onChange={event => setScopeType(event.target.value as AssetAuditScopeType)}>
              {scopeTypeValues.map(value => <option key={value} value={value}>{t(`assetAudits.scopeType.${value}`)}</option>)}
            </SelectInput>
          </Field>

          {scopeType === 'Team' ? (
            <Field label={t('assetAudits.scopeTeamsLabel')}>
              <select multiple value={teamIds} onChange={event => setTeamIds(Array.from(event.target.selectedOptions).map(option => option.value))} size={6}>
                {(teams.data ?? []).map((team: Team) => <option key={team.id} value={team.id}>{team.name}</option>)}
              </select>
            </Field>
          ) : null}

          {scopeType === 'Location' ? (
            <Field label={t('assetAudits.scopeLocationsLabel')}>
              <select multiple value={locations} onChange={event => setLocations(Array.from(event.target.selectedOptions).map(option => option.value))} size={6}>
                {locationOptions.map(location => <option key={location} value={location}>{location}</option>)}
              </select>
            </Field>
          ) : null}

          {scopeType === 'AssetCategory' ? (
            <Field label={t('assetAudits.scopeCategoriesLabel')}>
              <select multiple value={categoryIds} onChange={event => setCategoryIds(Array.from(event.target.selectedOptions).map(option => option.value))} size={6}>
                {(categories.data ?? []).map(category => <option key={category.id} value={category.id}>{category.name}</option>)}
              </select>
            </Field>
          ) : null}

          {scopeType === 'Person' ? (
            <Field label={t('assetAudits.scopePeopleLabel')}>
              <select multiple value={personIds} onChange={event => setPersonIds(Array.from(event.target.selectedOptions).map(option => option.value))} size={6}>
                {(people.data ?? []).map((person: Person) => <option key={person.id} value={person.id}>{person.fullName}</option>)}
              </select>
            </Field>
          ) : null}

          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={() => setStep(1)}>{t('common.back')}</Button>
            <Button type="button" disabled={busy} onClick={() => void handleScopeNext()}>{busy ? t('common.saving') : t('assetAudits.previewButton')}</Button>
          </div>
        </div>
      ) : null}

      {step === 3 && preview ? (
        <div className="formGrid">
          <DetailGrid>
            <DetailItem label={t('assetAudits.previewParticipants')} value={String(preview.participantCount)} />
            <DetailItem label={t('assetAudits.previewAssets')} value={String(preview.assetCount)} />
          </DetailGrid>
          {preview.peopleWithoutEmail.length ? (
            <Card className="card--flat">
              <p className="formMessage formMessage--error">
                <AlertTriangle size={16} /> {t('assetAudits.previewNoEmailWarning', { count: preview.peopleWithoutEmail.length })}
              </p>
              <p className="muted">{preview.peopleWithoutEmail.join(', ')}</p>
            </Card>
          ) : null}
          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={() => setStep(2)}>{t('common.back')}</Button>
            <Button type="button" disabled={busy} onClick={() => void handleStart()}>{busy ? t('common.saving') : t('assetAudits.startCampaign')}</Button>
          </div>
        </div>
      ) : null}
    </Modal>
  );
}

function AssetAuditDetailsView({
  details,
  actionBusy,
  onAction,
  onCancel
}: {
  details: AssetAuditCampaignDetailsResponse;
  actionBusy: string;
  onAction: (actionKey: string, run: () => Promise<unknown>, successKey: string) => Promise<void>;
  onCancel: () => void;
}) {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [tab, setTab] = useState<'participants' | 'exceptions'>('participants');
  const [resolveItem, setResolveItem] = useState<AssetAuditItemAdminResponse | null>(null);
  const people = useAsyncData(() => api.people(), []);

  const campaign = details.campaign;
  const counts = campaignCounts(details.items);
  const exceptions = details.items.filter(item => exceptionResponses.includes(item.response));
  const submittedCount = details.participants.filter(p => p.status === 'Submitted' || p.status === 'Reviewed').length;

  async function downloadCsv() {
    try {
      const blob = await api.downloadAssetAuditCsv(campaign.id);
      saveBlob(blob, `audyt-${campaign.id}.csv`);
    } catch { /* download failed - no explicit error UI for this action */ }
  }

  return (
    <div className="pageStack">
      <Card>
        <div className="sectionTitle">
          <div>
            <h2>{campaign.name}</h2>
            <p>{campaign.description ?? t('common.none')}</p>
          </div>
        </div>
        <DetailGrid>
          <DetailItem label={t('assetAudits.statusLabel')} value={<StatusBadge status={campaign.status} />} />
          <DetailItem label={t('assetAudits.dueDateLabel')} value={formatDate(campaign.dueDate)} />
          <DetailItem label={t('assetAudits.progressLabel')} value={`${submittedCount}/${details.participants.length}`} />
          <DetailItem label={t('assetAudits.confirmedCount')} value={String(counts.confirmed)} />
          <DetailItem label={t('assetAudits.missingCount')} value={String(counts.missing)} />
          <DetailItem label={t('assetAudits.damagedCount')} value={String(counts.damaged)} />
          <DetailItem label={t('assetAudits.wrongOwnerCount')} value={String(counts.wrongOwner)} />
        </DetailGrid>
      </Card>

      <div className="tabs" role="tablist">
        <button type="button" role="tab" aria-selected={tab === 'participants'} className={tab === 'participants' ? 'tab tab--active' : 'tab'} onClick={() => setTab('participants')}>{t('assetAudits.tabParticipants')}</button>
        <button type="button" role="tab" aria-selected={tab === 'exceptions'} className={tab === 'exceptions' ? 'tab tab--active' : 'tab'} onClick={() => setTab('exceptions')}>{t('assetAudits.tabExceptions')} ({exceptions.length})</button>
      </div>

      {tab === 'participants' ? (
        <Card className="card--flat">
          <div className="formActions formActions--split">
            <span />
            <Button variant="secondary" disabled={actionBusy === 'remind'} onClick={() => void onAction('remind', () => api.remindAssetAuditParticipants(campaign.id), 'assetAudits.reminderSent')} icon={<Mail size={16} />}>{t('assetAudits.remindAll')}</Button>
          </div>
          <div className="listRows">
            {details.participants.map(participant => (
              <ParticipantRow key={participant.id} participant={participant} campaignId={campaign.id} actionBusy={actionBusy} onAction={onAction} />
            ))}
          </div>
        </Card>
      ) : (
        <Card className="card--flat">
          {!exceptions.length ? <p className="muted">{t('assetAudits.noExceptions')}</p> : (
            <div className="listRows">
              {exceptions.map(item => (
                <div className="listRow" key={item.id}>
                  <div>
                    <strong>{item.assetName} ({item.assetTag})</strong>
                    <small>{item.participantName ?? '-'} · {t(`assetAudits.response.${item.response}`)}</small>
                    {item.comment ? <small>{item.comment}</small> : null}
                  </div>
                  <div className="rowActions">
                    {item.resolution === 'None' ? (
                      <Button variant="secondary" onClick={() => setResolveItem(item)} icon={<AlertTriangle size={16} />}>{t('assetAudits.resolveAction')}</Button>
                    ) : (
                      <StatusBadge status={item.resolution} label={t(`assetAudits.resolution.${item.resolution}`)} />
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </Card>
      )}

      <Card>
        <div className="formActions" style={{ justifyContent: 'space-between', flexWrap: 'wrap' }}>
          <Button variant="secondary" onClick={() => navigate('/asset-audits')} icon={<RotateCcw size={16} />}>{t('common.back')}</Button>
          <div className="rowActions">
            <Button variant="secondary" onClick={() => void downloadCsv()} icon={<Download size={16} />}>{t('assetAudits.downloadCsv')}</Button>
            {campaign.status !== 'Completed' && campaign.status !== 'Cancelled' ? (
              <Button variant="secondary" onClick={() => void onAction('complete', () => api.completeAssetAudit(campaign.id), 'assetAudits.completed')} icon={<CheckCircle2 size={16} />}>{t('assetAudits.completeCampaign')}</Button>
            ) : null}
            {campaign.status !== 'Completed' && campaign.status !== 'Cancelled' ? (
              <Button variant="secondary" onClick={onCancel} icon={<XCircle size={16} />}>{t('assetAudits.cancelAction')}</Button>
            ) : null}
          </div>
        </div>
      </Card>

      <ResolveItemModal
        item={resolveItem}
        people={people.data ?? []}
        onClose={() => setResolveItem(null)}
        onSubmit={(body) => onAction(`resolve-${resolveItem?.id}`, () => api.resolveAssetAuditItem(campaign.id, resolveItem!.id, body), 'assetAudits.itemResolved')}
      />
    </div>
  );
}

function ParticipantRow({
  participant,
  campaignId,
  actionBusy,
  onAction
}: {
  participant: AssetAuditParticipantResponse;
  campaignId: string;
  actionBusy: string;
  onAction: (actionKey: string, run: () => Promise<unknown>, successKey: string) => Promise<void>;
}) {
  const { t } = useI18n();
  return (
    <div className="listRow">
      <div>
        <strong>{participant.personName ?? participant.email}</strong>
        <small>{participant.email}</small>
        <small>{t('assetAudits.itemCount', { count: participant.itemCount })}</small>
      </div>
      <div className="rowActions">
        <StatusBadge status={participant.status} />
        {participant.lastReminderAt ? <small>{t('assetAudits.lastReminded')}: {formatDateTime(participant.lastReminderAt)}</small> : null}
        {participant.status === 'Submitted' ? (
          <Button
            variant="secondary"
            disabled={actionBusy === `reopen-${participant.id}`}
            onClick={() => void onAction(`reopen-${participant.id}`, () => api.reopenAssetAuditParticipant(campaignId, participant.id), 'assetAudits.participantReopened')}
            icon={<RefreshCw size={16} />}
          >
            {t('assetAudits.reopenAction')}
          </Button>
        ) : null}
      </div>
    </div>
  );
}

function ResolveItemModal({
  item,
  people,
  onClose,
  onSubmit
}: {
  item: AssetAuditItemAdminResponse | null;
  people: Person[];
  onClose: () => void;
  onSubmit: (body: { resolution: AssetAuditResolution; notes?: string | null; newOwnerPersonId?: string | null }) => Promise<void>;
}) {
  const { t } = useI18n();
  const [resolution, setResolution] = useState<AssetAuditResolution>('Accepted');

  useEffect(() => {
    if (item) setResolution('Accepted');
  }, [item]);

  return (
    <Modal open={!!item} title={t('assetAudits.resolveTitle')} description={item ? `${item.assetName} (${item.assetTag})` : undefined} onClose={onClose}>
      <form className="formGrid" onSubmit={event => {
        event.preventDefault();
        const form = new FormData(event.currentTarget);
        void onSubmit({
          resolution,
          notes: toNullable(String(form.get('notes') ?? '')),
          newOwnerPersonId: resolution === 'OwnershipCorrected' ? String(form.get('newOwnerPersonId') ?? '') : null
        }).then(onClose);
      }}>
        <Field label={t('assetAudits.resolutionLabel')}>
          <SelectInput value={resolution} onChange={event => setResolution(event.target.value as AssetAuditResolution)}>
            {resolutionValues.map(value => <option key={value} value={value}>{t(`assetAudits.resolution.${value}`)}</option>)}
          </SelectInput>
        </Field>
        {resolution === 'OwnershipCorrected' ? (
          <Field label={t('assetAudits.newOwnerLabel')}>
            <SelectInput name="newOwnerPersonId" required>
              <option value="">{t('assetAudits.newOwnerChoose')}</option>
              {people.map(person => <option key={person.id} value={person.id}>{person.fullName}</option>)}
            </SelectInput>
          </Field>
        ) : null}
        <Field label={t('assetAudits.resolutionNotesLabel')}><TextArea name="notes" required={resolution !== 'Accepted' && resolution !== 'Dismissed'} /></Field>
        <div className="formActions formActions--split"><Button type="button" variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button>{t('assetAudits.resolveAction')}</Button></div>
      </form>
    </Modal>
  );
}
