import { FormEvent, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { CheckCircle2, Download, ExternalLink, Eye, Link2, PackageCheck, Plus, RotateCcw, Search } from 'lucide-react';
import { api, type EvidencePhoto } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { DetailGrid, DetailItem } from '../components/DetailGrid';
import { EvidenceGallery, EvidencePhotoPicker } from '../components/Evidence';
import { Modal } from '../components/Modal';
import { Field, SelectInput, TextArea, TextInput } from '../components/FormFields';
import { PageHeader } from '../components/PageHeader';
import { PersonPreviewModal } from '../components/PersonPreviewModal';
import { Pagination } from '../components/Pagination';
import { EmptyState, ErrorState, LoadingState } from '../components/StateViews';
import { StatusBadge } from '../components/StatusBadge';
import { useAsyncData } from '../hooks/useAsyncData';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import type { AssetEvidence, Assignment, AssignmentStatus } from '../types/domain';
import { assignmentStatusValues } from '../utils/labels';
import { formatDate, formatDateTime, toNullable } from '../utils/format';
import { useI18n } from '../i18n/I18nProvider';
import { useCelebration } from '../celebration/CelebrationProvider';

const pageSize = 10;

const todayIso = () => new Date().toISOString().slice(0, 10);

export function AssignmentsPage() {
  const { t, tPlural } = useI18n();
  const { celebrate } = useCelebration();
  const people = useAsyncData(() => api.people(), []);
  const assets = useAsyncData(() => api.assets({ status: 'InStock' }), []);
  const procedures = useAsyncData(() => api.procedures(), []);
  const locations = useAsyncData(api.locations, []);
  const [drawerMode, setDrawerMode] = useState<'create' | 'details' | 'return' | null>(null);
  const [selectedAssignment, setSelectedAssignment] = useState<Assignment | null>(null);
  const [selectedAssetIds, setSelectedAssetIds] = useState<string[]>([]);
  const [selectedProcedureIds, setSelectedProcedureIds] = useState<string[]>([]);
  const [issueConditions, setIssueConditions] = useState<Record<string, string>>({});
  const [issueEvidence, setIssueEvidence] = useState<Record<string, File[]>>({});
  const [returnEvidence, setReturnEvidence] = useState<Record<string, File[]>>({});
  const [creating, setCreating] = useState(false);
  const [returning, setReturning] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<AssignmentStatus | ''>('');
  const [page, setPage] = useState(1);
  const [viewPersonId, setViewPersonId] = useState<string | null>(null);
  const [assetFilter, setAssetFilter] = useState('');
  const debouncedSearch = useDebouncedValue(search.trim().toLowerCase(), 250);
  const assignmentsLoader = useMemo(
    () => () => api.assignmentsPaged({ search: debouncedSearch || undefined, status: status || undefined, page, pageSize }),
    [debouncedSearch, status, page]
  );
  const assignments = useAsyncData(assignmentsLoader, [assignmentsLoader]);
  const visibleStockAssets = useMemo(() => {
    const query = assetFilter.trim().toLowerCase();
    const rows = assets.data ?? [];
    return query ? rows.filter(asset => `${asset.name} ${asset.assetTag}`.toLowerCase().includes(query)) : rows;
  }, [assets.data, assetFilter]);
  const isLoading = assignments.isLoading || people.isLoading || assets.isLoading || procedures.isLoading || locations.isLoading;
  const error = assignments.error || people.error || assets.error || procedures.error || locations.error;
  const publishedProcedures = useMemo(() => procedures.data?.filter(item => item.status === 'Published') ?? [], [procedures.data]);
  const rows = assignments.data?.items ?? [];
  const totalAssignments = assignments.data?.total ?? 0;

  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(() => setMessage(null), message.type === 'success' ? 3500 : 6500);
    return () => window.clearTimeout(timeout);
  }, [message]);

  function toggle(list: string[], value: string) {
    return list.includes(value) ? list.filter(item => item !== value) : [...list, value];
  }

  function reloadAll() {
    void Promise.all([assignments.reload(), people.reload(), assets.reload(), procedures.reload(), locations.reload()]);
  }

  async function createAssignment(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const personId = String(form.get('personId') ?? '');
    if (!personId || selectedAssetIds.length === 0) return setMessage({ type: 'error', text: t('assignments.selectPersonAndAsset') });
    setCreating(true);
    try {
      const body = {
        personId,
        assets: selectedAssetIds.map(assetId => ({ assetId, issueCondition: t(`issueCondition.${issueConditions[assetId] ?? 'ok'}`) })),
        procedureIds: selectedProcedureIds,
        dueDate: toNullable(String(form.get('dueDate') ?? '')),
        notes: toNullable(String(form.get('notes') ?? ''))
      };
      const photos: EvidencePhoto[] = selectedAssetIds.flatMap(assetId => (issueEvidence[assetId] ?? []).map(file => ({ assetId, caption: null, file })));
      if (photos.length) await api.createAssignmentWithEvidence(body, photos);
      else await api.createAssignment(body);
      setSelectedAssetIds([]);
      setSelectedProcedureIds([]);
      setIssueConditions({});
      setIssueEvidence({});
      setDrawerMode(null);
      setMessage({ type: 'success', text: t('assignments.created') });
      celebrate(t('celebration.assignmentCreated'));
      await Promise.all([assignments.reload(), assets.reload()]);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('assignments.createFailed') });
    } finally {
      setCreating(false);
    }
  }

  async function acceptAssignment(item: Assignment) {
    try {
      await api.acceptAssignment(item.id);
      setMessage({ type: 'success', text: t('assignments.accepted') });
      await assignments.reload();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('assignments.acceptFailed') });
    }
  }

  async function returnAssignment(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedAssignment) return;
    const form = new FormData(event.currentTarget);
    const destinationLocation = toNullable(String(form.get('destinationLocation') ?? ''));
    setReturning(true);
    try {
      const hasPhotos = selectedAssignment.assets.some(asset => (returnEvidence[asset.assetId] ?? []).length > 0);
      if (hasPhotos) {
        for (const asset of selectedAssignment.assets) {
          await api.returnAssetWithEvidence(
            selectedAssignment.id,
            asset.assetId,
            {
              resolution: 'Returned',
              returnCondition: toNullable(String(form.get(`returnCondition__${asset.assetId}`) ?? '')),
              returnLocation: destinationLocation,
              notes: null
            },
            returnEvidence[asset.assetId] ?? []
          );
        }
      } else {
        await api.returnAssignment(selectedAssignment.id, {
          returnCondition: null,
          destinationLocation,
          assets: selectedAssignment.assets.map(asset => ({
            assetId: asset.assetId,
            returnCondition: toNullable(String(form.get(`returnCondition__${asset.assetId}`) ?? ''))
          }))
        });
      }
      setDrawerMode(null);
      setSelectedAssignment(null);
      setReturnEvidence({});
      setMessage({ type: 'success', text: t('assignments.returned') });
      await Promise.all([assignments.reload(), assets.reload()]);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('assignments.returnFailed') });
    } finally {
      setReturning(false);
    }
  }

  async function downloadProtocol(item: Assignment) {
    try {
      const blob = await api.downloadAssignmentProtocol(item.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${item.protocolNumber}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('assignments.downloadProtocolFailed') });
    }
  }

  if (isLoading && !assignments.data) return <LoadingState title={t('assignments.loadingTitle')} description={t('assignments.loadingDesc')} />;
  if (error) return <ErrorState message={error} onRetry={reloadAll} />;

  return (
    <div className="pageStack">
      <PageHeader eyebrow={t('page.assignments.eyebrow')} title={t('page.assignments.title')} actions={<Button onClick={() => setDrawerMode('create')} icon={<Plus size={16} />}>{t('assignments.newAssignment')}</Button>} />

      {message ? <div className="toastStack" aria-live="polite"><div className={`toast toast--${message.type}`}>{message.text}</div></div> : null}

      <Card className="toolbarCard">
        <div className="filters filters--three">
          <Field label={t('common.search')}><TextInput value={search} onChange={event => { setSearch(event.target.value); setPage(1); }} placeholder={t('assignments.searchPlaceholder')} /></Field>
          <Field label={t('assets.statusLabel')}>
            <SelectInput value={status} onChange={event => { setStatus(event.target.value as AssignmentStatus | ''); setPage(1); }}>
              <option value="">{t('assets.allStatuses')}</option>
              {assignmentStatusValues.map(value => <option key={value} value={value}>{t(`status.${value}`)}</option>)}
            </SelectInput>
          </Field>
          <span className="toolbarHint"><Search size={16} /> {totalAssignments} {tPlural('count.results', totalAssignments)}</span>
        </div>
      </Card>

      {!rows.length ? <EmptyState title={t('assignments.emptyTitle')} description={t('assignments.emptyDesc')} action={(debouncedSearch || status) ? <Button variant="secondary" onClick={() => { setSearch(''); setStatus(''); }}>{t('common.clearFilters')}</Button> : <Button onClick={() => setDrawerMode('create')} icon={<Plus size={16} />}>{t('assignments.newAssignment')}</Button>} /> : (
        <Card>
          <div className="tableWrap tableWrap--cards"><table><thead><tr><th>{t('assignments.colProtocol')}</th><th>{t('assignments.colPerson')}</th><th>{t('assets.statusLabel')}</th><th>{t('assignments.colAssets')}</th><th>{t('assignments.colDueDate')}</th><th></th></tr></thead><tbody>
            {rows.map(item => <tr key={item.id}>
              <td data-label={t('assignments.colProtocol')}><strong>{item.protocolNumber}</strong><small>{formatDateTime(item.issuedAt)}</small></td>
              <td data-label={t('assignments.colPerson')}>{item.personId ? <button type="button" className="inlineAction" onClick={() => setViewPersonId(item.personId)}>{item.personName ?? t('assignments.unknownPerson')}</button> : (item.personName ?? t('assignments.unknownPerson'))}</td>
              <td data-label={t('assets.statusLabel')}><StatusBadge status={item.status} /></td>
              <td data-label={t('assignments.colAssets')}>{item.assets.length}</td>
              <td data-label={t('assignments.colDueDate')}>{formatDate(item.dueDate)}</td>
              <td><div className="tableActions"><button aria-label={t('assignments.detailsAria')} className="iconButton" onClick={() => { setSelectedAssignment(item); setDrawerMode('details'); }}><Eye size={16} /></button>{item.status === 'AwaitingAcceptance' ? <button aria-label={t('assignments.acceptAria')} className="iconButton iconButton--success" onClick={() => acceptAssignment(item)}><CheckCircle2 size={16} /></button> : null}{item.status === 'Accepted' || item.status === 'Overdue' ? <button aria-label={t('assignments.returnAria')} className="iconButton" onClick={() => { setSelectedAssignment(item); setReturnEvidence({}); setDrawerMode('return'); }}><RotateCcw size={16} /></button> : null}</div></td>
            </tr>)}
          </tbody></table></div>
          <Pagination page={page} total={totalAssignments} pageSize={pageSize} onPageChange={setPage} />
        </Card>
      )}

      <Modal open={drawerMode === 'create'} title={t('assignments.newAssignment')} onClose={() => setDrawerMode(null)} width="wide">
        <form className="formGrid" onSubmit={createAssignment}>
          <Field label={t('assignments.personLabel')}><SelectInput name="personId" required><option value="">{t('assignments.choosePersonOption')}</option>{people.data?.filter(person => person.employmentStatus === 'Active').map(person => <option key={person.id} value={person.id}>{person.fullName}</option>)}</SelectInput></Field>
          <Field label={t('assignments.dueDateLabel')}><TextInput name="dueDate" type="date" min={todayIso()} /></Field>
          <Field label={t('assignments.notesLabel')}><TextArea name="notes" /></Field>
          <fieldset className="choiceBox"><legend>{t('assignments.assetsFromStock')}</legend>{!assets.data?.length ? <p className="muted">{t('assignments.noAssetsInStock')}</p> : <>
          {assets.data.length > 8 && <TextInput value={assetFilter} onChange={event => setAssetFilter(event.target.value)} placeholder={t('common.filterList')} />}
          {visibleStockAssets.map(asset => (
            <div key={asset.id} style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
              <label style={{ flex: '1 1 auto' }}><input type="checkbox" checked={selectedAssetIds.includes(asset.id)} onChange={() => setSelectedAssetIds(current => toggle(current, asset.id))} /> {asset.name} · {asset.assetTag}</label>
              {selectedAssetIds.includes(asset.id) && (
                <SelectInput aria-label={t('issueCondition.label')} value={issueConditions[asset.id] ?? 'ok'} onChange={event => setIssueConditions(current => ({ ...current, [asset.id]: event.target.value }))} style={{ maxWidth: '220px' }}>
                  <option value="ok">{t('issueCondition.ok')}</option>
                  <option value="used">{t('issueCondition.used')}</option>
                  <option value="damaged">{t('issueCondition.damaged')}</option>
                </SelectInput>
              )}
              {selectedAssetIds.includes(asset.id) && (
                <div style={{ flexBasis: '100%' }}>
                  <EvidencePhotoPicker files={issueEvidence[asset.id] ?? []} onChange={files => setIssueEvidence(current => ({ ...current, [asset.id]: files }))} />
                </div>
              )}
            </div>
          ))}</>}</fieldset>
          <fieldset className="choiceBox"><legend>{t('assignments.proceduresLegend')}</legend>{!publishedProcedures.length ? <p className="muted">{t('assignments.noPublishedProcedures')}</p> : publishedProcedures.map(procedure => <label key={procedure.id}><input type="checkbox" checked={selectedProcedureIds.includes(procedure.id)} onChange={() => setSelectedProcedureIds(current => toggle(current, procedure.id))} /> {procedure.title}</label>)}</fieldset>
          <div className="formActions formActions--split"><Button type="button" variant="ghost" onClick={() => setDrawerMode(null)}>{t('common.cancel')}</Button><Button disabled={creating} icon={<PackageCheck size={16} />}>{creating ? t('common.saving') : t('assignments.create')}</Button></div>
        </form>
      </Modal>

      <Modal open={drawerMode === 'return'} title={t('assignments.returnTitle')} description={selectedAssignment ? selectedAssignment.protocolNumber : undefined} onClose={() => setDrawerMode(null)}>
        <form className="formGrid" onSubmit={returnAssignment} key={selectedAssignment?.id ?? 'return'}>
          {selectedAssignment?.assets.map(asset => (
            <div key={asset.assetId} style={{ gridColumn: '1 / -1', display: 'grid', gap: '8px' }}>
              <Field label={t('assignments.returnConditionFor', { name: asset.assetName ?? asset.assetTag ?? '—' })}>
                <TextArea name={`returnCondition__${asset.assetId}`} placeholder={t('assignments.returnConditionPlaceholder')} required rows={2} />
              </Field>
              <EvidencePhotoPicker files={returnEvidence[asset.assetId] ?? []} onChange={files => setReturnEvidence(current => ({ ...current, [asset.assetId]: files }))} />
            </div>
          ))}
          <Field label={t('assignments.destinationLocationLabel')}><SelectInput name="destinationLocation"><option value="">{t('assignments.mainWarehouseOption')}</option>{locations.data?.map(item => <option key={item.id} value={item.fullPath}>{item.fullPath}</option>)}</SelectInput></Field>
          <div className="formActions formActions--split"><Button type="button" variant="ghost" onClick={() => setDrawerMode(null)}>{t('common.cancel')}</Button><Button disabled={returning} icon={<RotateCcw size={16} />}>{returning ? t('common.saving') : t('assignments.saveReturn')}</Button></div>
        </form>
      </Modal>

      <Modal open={drawerMode === 'details'} title={t('assignments.detailsTitle')} onClose={() => setDrawerMode(null)} width="wide">{selectedAssignment ? <AssignmentDetails assignment={selectedAssignment} onDownload={downloadProtocol} onViewPerson={setViewPersonId} /> : null}</Modal>
      {viewPersonId && <PersonPreviewModal personId={viewPersonId} onClose={() => setViewPersonId(null)} />}
    </div>
  );
}

function AssignmentDetails({ assignment, onDownload, onViewPerson }: { assignment: Assignment; onDownload: (assignment: Assignment) => void; onViewPerson: (personId: string) => void }) {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [linkCopied, setLinkCopied] = useState(false);
  const [linkCopyFailed, setLinkCopyFailed] = useState(false);
  async function copyLink() {
    try {
      await navigator.clipboard.writeText(assignment.acceptanceLink);
      setLinkCopyFailed(false);
      setLinkCopied(true);
      window.setTimeout(() => setLinkCopied(false), 2500);
    } catch {
      setLinkCopyFailed(true);
      window.setTimeout(() => setLinkCopyFailed(false), 4000);
    }
  }
  return (
    <div className="pageStack">
      {assignment.status === 'AwaitingAcceptance' || assignment.status === 'Overdue' ? (
        <p className="formHint">{t('assignments.acceptanceLinkHint')}</p>
      ) : null}
      <div className="formActions formActions--split">
        {assignment.status === 'AwaitingAcceptance' || assignment.status === 'Overdue' ? (
          <Button type="button" variant="ghost" onClick={copyLink} icon={<Link2 size={16} />}>{linkCopied ? t('assignments.linkCopied') : linkCopyFailed ? t('assignments.linkCopyFailed') : t('assignments.copyAcceptanceLink')}</Button>
        ) : <div />}
        <Button variant="secondary" onClick={() => onDownload(assignment)} icon={<Download size={16} />}>{t('assignments.downloadProtocol')}</Button>
      </div>
      <DetailGrid>
        <DetailItem label={t('assignments.colProtocol')} value={assignment.protocolNumber} />
        <DetailItem label={t('assets.statusLabel')} value={<StatusBadge status={assignment.status} />} />
        <DetailItem label={t('assignments.colPerson')} value={assignment.personId ? <button type="button" className="inlineAction" onClick={() => onViewPerson(assignment.personId)}>{assignment.personName ?? t('assignments.unknownPerson')}</button> : (assignment.personName ?? t('assignments.unknownPerson'))} />
        <DetailItem label={t('assignments.issuedLabel')} value={formatDateTime(assignment.issuedAt)} />
        <DetailItem label={t('assignments.dueLabel')} value={formatDate(assignment.dueDate)} />
        <DetailItem label={t('assignments.notesFieldLabel')} value={assignment.notes ?? t('assignments.notesFallback')} />
      </DetailGrid>
      <Card className="card--flat"><div className="sectionTitle"><div><h2>{t('assignments.assetsSectionTitle')}</h2><p>{t('assignments.assetsSectionDesc')}</p></div></div><div className="listRows">{assignment.assets.map(asset => <div className="listRow" key={asset.assetId}><div><strong>{asset.assetName ?? t('assignments.unnamedAsset')}</strong><small>{asset.assetTag ?? '—'}</small></div><span>{asset.returnCondition ?? asset.issueCondition}</span></div>)}</div></Card>
      <AssignmentEvidence assignment={assignment} />
      <Card className="card--flat"><div className="sectionTitle"><div><h2>{t('assignments.proceduresSectionTitle')}</h2><p>{t('assignments.proceduresSectionDesc')}</p></div></div><div className="listRows">{assignment.procedureAcceptances.length ? assignment.procedureAcceptances.map(item => <div className="listRow" key={item.id}><div><strong>{item.procedureTitle ?? t('assignments.unnamedProcedure')}</strong><small>{formatDateTime(item.sentAt)}</small></div><div className="rowActions"><StatusBadge status={item.status} /><button type="button" className="iconButton" aria-label={t('assignments.openProcedureAria')} onClick={() => navigate(`/procedures?open=${item.procedureId}`)}><ExternalLink size={16} /></button></div></div>) : <p className="muted">{t('assignments.noProceduresInPackage')}</p>}</div></Card>
      <Card className="card--flat">
        <div className="sectionTitle"><div><h2>{t('assignments.proofTitle')}</h2></div></div>
        {assignment.acceptedAt ? (
          <DetailGrid>
            <DetailItem label={t('assignments.issuedLabel')} value={formatDateTime(assignment.acceptedAt)} />
            <DetailItem label={t('assignments.proofIp')} value={assignment.acceptedIp ?? '—'} />
            <DetailItem label={t('assignments.proofHash')} value={<code style={{ wordBreak: 'break-all', fontSize: '0.75em' }}>{assignment.acceptanceHash ?? '—'}</code>} />
            <DetailItem label={t('assignments.proofVerified')} value={assignment.isIntegrityVerified ? '✓' : t('assignments.proofTampered')} />
          </DetailGrid>
        ) : (
          <p className="muted">{t('assignments.proofNotSigned')}</p>
        )}
      </Card>
    </div>
  );
}

function AssignmentEvidence({ assignment }: { assignment: Assignment }) {
  const { t } = useI18n();
  const [byAsset, setByAsset] = useState<Record<string, AssetEvidence[]>>({});
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    Promise.all(assignment.assets.map(asset =>
      api.assetEvidence(asset.assetId)
        .then(items => items.filter(item => item.assignmentId === assignment.id))
        .catch(() => [] as AssetEvidence[])
        .then(items => ({ assetId: asset.assetId, items }))
    )).then(results => {
      if (cancelled) return;
      setByAsset(Object.fromEntries(results.map(r => [r.assetId, r.items])));
      setLoading(false);
    });
    return () => { cancelled = true; };
  }, [assignment.id, assignment.assets]);

  const phases: { key: 'Issue' | 'Return'; label: string }[] = [
    { key: 'Issue', label: t('evidence.phase.Issue') },
    { key: 'Return', label: t('evidence.phase.Return') }
  ];
  const hasAny = assignment.assets.some(asset => (byAsset[asset.assetId] ?? []).length > 0);

  return (
    <Card className="card--flat">
      <div className="sectionTitle"><div><h2>{t('evidence.photos')}</h2></div></div>
      {loading ? <p className="muted">{t('common.loading')}</p> : !hasAny ? <p className="muted">{t('evidence.noPhotos')}</p> : (
        <div className="pageStack">
          {assignment.assets.map(asset => {
            const items = byAsset[asset.assetId] ?? [];
            if (!items.length) return null;
            return (
              <div key={asset.assetId}>
                <strong>{asset.assetName ?? asset.assetTag ?? '—'}</strong>
                {phases.map(phase => {
                  const ids = items.filter(item => item.phase === phase.key).map(item => item.id);
                  if (!ids.length) return null;
                  return (
                    <div key={phase.key} style={{ marginTop: '6px' }}>
                      <small className="muted">{phase.label}</small>
                      <EvidenceGallery ids={ids} getBlob={api.evidenceBlob} />
                    </div>
                  );
                })}
              </div>
            );
          })}
        </div>
      )}
    </Card>
  );
}
