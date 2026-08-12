import { ChangeEvent, DragEvent, FormEvent, useEffect, useMemo, useState } from 'react';
import { Archive, Download, FileCheck2, Pencil, Plus, Search, Send, Trash2 } from 'lucide-react';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { Field, TextArea, TextInput } from '../components/FormFields';
import { Modal } from '../components/Modal';
import { PageHeader } from '../components/PageHeader';
import { Pagination } from '../components/Pagination';
import { EmptyState, ErrorState, LoadingState } from '../components/StateViews';
import { StatusBadge } from '../components/StatusBadge';
import { useAsyncData } from '../hooks/useAsyncData';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import type { Procedure } from '../types/domain';
import { formatDate, toNullable } from '../utils/format';
import { useI18n } from '../i18n/I18nProvider';
import { useCelebration } from '../celebration/CelebrationProvider';

function formatBytes(value?: number | null) {
  if (!value) return '';
  if (value < 1024 * 1024) return `${Math.round(value / 1024)} KB`;
  return `${(value / (1024 * 1024)).toFixed(1)} MB`;
}

type ProcedureDialog =
  | { mode: 'create' }
  | { mode: 'edit'; procedure: Procedure }
  | null;

type ConfirmAction =
  | { kind: 'publish'; procedure: Procedure }
  | { kind: 'archive'; procedure: Procedure }
  | { kind: 'removeDoc'; documentId: string; fileName: string }
  | null;

const pageSize = 9;

export function ProceduresPage() {
  const { t, tPlural } = useI18n();
  const { celebrate } = useCelebration();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const debouncedSearch = useDebouncedValue(search.trim().toLowerCase(), 250);
  const proceduresLoader = useMemo(() => () => api.proceduresPaged({ search: debouncedSearch, page, pageSize }), [debouncedSearch, page]);
  const procedures = useAsyncData(proceduresLoader, [proceduresLoader]);
  const [dialog, setDialog] = useState<ProcedureDialog>(null);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [dragActive, setDragActive] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [confirmAction, setConfirmAction] = useState<ConfirmAction>(null);

  const editedProcedure = dialog?.mode === 'edit'
    ? procedures.data?.items.find(item => item.id === dialog.procedure.id) ?? dialog.procedure
    : null;

  const showAcceptances = Boolean(editedProcedure && editedProcedure.status === 'Published' && editedProcedure.requiresAcceptance);
  const acceptanceLoader = useMemo(
    () => () => (editedProcedure && showAcceptances ? api.procedureAcceptances(editedProcedure.id) : Promise.resolve([])),
    [editedProcedure?.id, showAcceptances]
  );
  const acceptances = useAsyncData(acceptanceLoader, [acceptanceLoader]);

  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(() => setMessage(null), message.type === 'success' ? 3500 : 6500);
    return () => window.clearTimeout(timeout);
  }, [message]);
  const rows = procedures.data?.items ?? [];
  const totalProcedures = procedures.data?.total ?? 0;

  async function saveProcedure(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const body = {
      title: String(form.get('title') ?? '').trim(),
      version: String(form.get('version') ?? '1.0').trim() || '1.0',
      owner: String(form.get('owner') ?? 'HR / IT').trim() || 'HR / IT',
      appliesTo: toNullable(String(form.get('appliesTo') ?? '')),
      reviewDate: toNullable(String(form.get('reviewDate') ?? '')),
      requiresAcceptance: form.get('requiresAcceptance') === 'on'
    };

    if (!body.title) return setMessage({ type: 'error', text: t('procedures.titleRequired') });

    const wasCreate = !editedProcedure;
    setSaving(true);
    setMessage(null);
    try {
      const saved = editedProcedure
        ? await api.updateProcedure(editedProcedure.id, body)
        : await api.createProcedure(body);

      setDialog({ mode: 'edit', procedure: saved });
      setMessage({ type: 'success', text: t('procedures.saved') });
      if (wasCreate) celebrate(t('celebration.procedureAdded'));
      await procedures.reload();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('procedures.saveFailed') });
    } finally {
      setSaving(false);
    }
  }

  function requestPublish(item: Procedure) {
    if (item.status !== 'Draft') return;
    if (!item.documents.length) {
      setDialog({ mode: 'edit', procedure: item });
      setMessage({ type: 'error', text: t('procedures.needsFileToPublish') });
      return;
    }
    setConfirmAction({ kind: 'publish', procedure: item });
  }

  async function publish(item: Procedure) {
    if (item.status !== 'Draft') return;
    setMessage(null);
    try {
      await api.publishProcedure(item.id);
      setMessage({ type: 'success', text: t('procedures.published') });
      await procedures.reload();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('procedures.publishFailed') });
    }
  }

  async function archive(item: Procedure) {
    if (item.status !== 'Published') return;
    setMessage(null);
    try {
      await api.archiveProcedure(item.id);
      setMessage({ type: 'success', text: t('procedures.archived') });
      await procedures.reload();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('procedures.archiveFailed') });
    }
  }

  async function uploadFiles(files: File[]) {
    if (!files.length || !editedProcedure || editedProcedure.status !== 'Draft') return;
    setUploading(true);
    setMessage(null);
    try {
      for (const file of files) {
        await api.uploadProcedureDocument(editedProcedure.id, file);
      }
      setMessage({ type: 'success', text: files.length > 1 ? t('procedures.filesAdded', { count: files.length }) : t('procedures.fileAdded') });
      await procedures.reload();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('procedures.uploadFailed') });
    } finally {
      setUploading(false);
    }
  }

  async function uploadDocument(event: ChangeEvent<HTMLInputElement>) {
    const files = Array.from(event.target.files ?? []);
    event.target.value = '';
    await uploadFiles(files);
  }

  function handleDragOver(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    if (!uploading) setDragActive(true);
  }

  function handleDragLeave(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setDragActive(false);
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setDragActive(false);
    if (uploading) return;
    void uploadFiles(Array.from(event.dataTransfer.files ?? []));
  }

  async function removeDocument(documentId: string) {
    if (!editedProcedure) return;
    try {
      await api.deleteProcedureDocument(editedProcedure.id, documentId);
      setMessage({ type: 'success', text: t('procedures.fileRemoved') });
      await procedures.reload();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('procedures.removeFailed') });
    }
  }

  async function download(procedureId: string, documentId: string, fileName: string) {
    try {
      const blob = await api.downloadProcedureDocument(procedureId, documentId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('procedures.downloadFailed') });
    }
  }

  if (procedures.isLoading && !procedures.data) return <LoadingState title={t('procedures.loadingTitle')} description={t('procedures.loadingDesc')} />;
  if (procedures.error) return <ErrorState message={procedures.error} onRetry={procedures.reload} />;

  return (
    <div className="pageStack">
      <PageHeader
        eyebrow={t('page.procedures.eyebrow')}
        title={t('page.procedures.title')}
        actions={<Button onClick={() => setDialog({ mode: 'create' })} icon={<Plus size={16} />}>{t('procedures.newSet')}</Button>}
      />
      {message ? <div className="toastStack" aria-live="polite"><div className={`toast toast--${message.type}`}>{message.text}</div></div> : null}

      <Card className="toolbarCard">
        <div className="filters filters--single">
          <Field label={t('procedures.searchLabel')}><TextInput value={search} onChange={event => { setSearch(event.target.value); setPage(1); }} placeholder={t('procedures.searchPlaceholder')} /></Field>
          <span className="toolbarHint"><Search size={16} /> {totalProcedures} {tPlural('count.results', totalProcedures)}</span>
        </div>
      </Card>

      <Card>
        <div className="sectionTitle"><div><h2>{t('procedures.listTitle')}</h2></div></div>
        {!rows.length ? <EmptyState title={t('procedures.emptyTitle')} description={t('procedures.emptyDesc')} action={search.trim() ? <Button variant="secondary" onClick={() => setSearch('')}>{t('common.clearFilters')}</Button> : <Button onClick={() => setDialog({ mode: 'create' })} icon={<Plus size={16} />}>{t('procedures.newSet')}</Button>} /> : (
          <>
            <div className="cardsList cardsList--grid">
              {rows.map(item => <article className="procedureCard" key={item.id}>
                <div><strong>{item.title}</strong><StatusBadge status={item.status} /></div>
                <span>{t('procedures.version', { version: item.version })} · {item.owner}</span>
                <small>{item.appliesTo ?? t('procedures.noScope')} · {t('procedures.reviewDate', { date: formatDate(item.reviewDate) })}</small>
                <small>{item.documents.length ? (item.documents.length === 1 ? t('procedures.fileCountOne') : t('procedures.fileCountMany', { count: item.documents.length })) : t('procedures.noFiles')}</small>
                <div className="rowActions">
                  <Button variant="ghost" onClick={() => setDialog({ mode: 'edit', procedure: item })} icon={<Pencil size={16} />}>{t('procedures.openSet')}</Button>
                  {item.status === 'Draft' ? <Button variant="secondary" onClick={() => requestPublish(item)} icon={<Send size={16} />}>{t('procedures.publish')}</Button> : null}
                  {item.status === 'Published' ? <Button variant="secondary" onClick={() => setConfirmAction({ kind: 'archive', procedure: item })} icon={<Archive size={16} />}>{t('procedures.archive')}</Button> : null}
                </div>
              </article>)}
            </div>
            <Pagination page={page} total={totalProcedures} pageSize={pageSize} onPageChange={setPage} />
          </>
        )}
      </Card>

      <Modal open={dialog?.mode === 'create' || dialog?.mode === 'edit'} title={editedProcedure ? t('procedures.editSet', { title: editedProcedure.title }) : t('procedures.newSetTitle')} onClose={() => setDialog(null)} width="wide">
        {editedProcedure?.status === 'Published' ? (
          <p className="formMessage">{t('procedures.lockedForEditing')}</p>
        ) : (
          <form className="formGrid" onSubmit={saveProcedure} key={editedProcedure?.id ?? 'new-procedure'}>
            <Field label={t('procedures.titleLabel')}><TextInput name="title" defaultValue={editedProcedure?.title ?? ''} required /></Field>
            <Field label={t('procedures.versionLabel')}><TextInput name="version" defaultValue={editedProcedure?.version ?? '1.0'} /></Field>
            <Field label={t('procedures.ownerLabel')}><TextInput name="owner" defaultValue={editedProcedure?.owner ?? 'HR / IT'} /></Field>
            <Field label={t('procedures.reviewDateLabel')}><TextInput name="reviewDate" type="date" defaultValue={editedProcedure?.reviewDate ?? ''} /></Field>
            <Field label={t('procedures.scopeLabel')}><TextArea name="appliesTo" defaultValue={editedProcedure?.appliesTo ?? ''} /></Field>
            <label className="checkField"><input name="requiresAcceptance" type="checkbox" defaultChecked={editedProcedure?.requiresAcceptance ?? true} /> {t('procedures.requiresAcceptance')}</label>
            <div className="formActions formActions--split"><Button type="button" variant="ghost" onClick={() => setDialog(null)}>{t('common.close')}</Button><Button disabled={saving} icon={<FileCheck2 size={16} />}>{saving ? t('common.saving') : t('procedures.save')}</Button></div>
          </form>
        )}

        {editedProcedure ? (
          <div className="procedureFiles">
            <div className="formSectionTitle">{t('procedures.filesInSet')}</div>
            {editedProcedure.documents.length ? (
              <div className="listRows">
                {editedProcedure.documents.map(doc => (
                  <div className="listRow" key={doc.id}>
                    <div><strong>{doc.fileName}</strong><small>{formatBytes(doc.sizeBytes)} · {formatDate(doc.uploadedAt)}</small></div>
                    <div className="rowActions">
                      <button className="iconButton" aria-label={t('procedures.downloadAria', { file: doc.fileName })} onClick={() => download(editedProcedure.id, doc.id, doc.fileName)}><Download size={16} /></button>
                      {editedProcedure.status === 'Draft' ? <button className="iconButton" aria-label={t('procedures.deleteAria', { file: doc.fileName })} onClick={() => setConfirmAction({ kind: 'removeDoc', documentId: doc.id, fileName: doc.fileName })}><Trash2 size={16} /></button> : null}
                    </div>
                  </div>
                ))}
              </div>
            ) : <p className="emptyInline">{t('procedures.noFilesInSet')}</p>}
            {editedProcedure.status === 'Draft' ? (
              <div
                className={`fileDropzone${dragActive ? ' fileDropzone--active' : ''}`}
                onDragOver={handleDragOver}
                onDragLeave={handleDragLeave}
                onDrop={handleDrop}
              >
                <span className="fileDropzone__hint">{dragActive ? t('procedures.dropHintActive') : t('procedures.dropHint')}</span>
                <label className="button button--secondary">
                  {uploading ? t('procedures.uploading') : t('procedures.addFiles')}
                  <input type="file" multiple style={{ display: 'none' }} disabled={uploading} onChange={uploadDocument} accept=".pdf,.doc,.docx,.txt,application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,text/plain" />
                </label>
              </div>
            ) : <p className="muted">{t('procedures.filesLocked')}</p>}

            {showAcceptances && (
              <>
                <div className="formSectionTitle">{t('procedures.acceptanceStatusTitle')}</div>
                {acceptances.isLoading ? <p className="muted">{t('common.loading')}</p> : !acceptances.data?.length ? (
                  <p className="muted">{t('procedures.noAcceptancesYet')}</p>
                ) : (
                  <div className="listRows">
                    {acceptances.data.map(item => (
                      <div className="listRow" key={`${item.personId}-${item.protocolNumber}`}>
                        <div><strong>{item.personName}</strong><small>{item.protocolNumber ?? '—'}</small></div>
                        <StatusBadge status={item.status} />
                      </div>
                    ))}
                  </div>
                )}
              </>
            )}
          </div>
        ) : null}
      </Modal>

      <ConfirmDialog
        open={!!confirmAction}
        title={confirmAction?.kind === 'publish' ? t('procedures.publishConfirmTitle') : confirmAction?.kind === 'archive' ? t('procedures.archiveConfirmTitle') : t('procedures.removeFileConfirmTitle')}
        description={confirmAction?.kind === 'publish'
          ? t('procedures.publishConfirmDesc', { title: confirmAction.procedure.title, version: confirmAction.procedure.version })
          : confirmAction?.kind === 'archive'
            ? t('procedures.archiveConfirmDesc', { title: confirmAction.procedure.title, version: confirmAction.procedure.version })
            : confirmAction?.kind === 'removeDoc'
              ? t('procedures.removeFileConfirmDesc', { file: confirmAction.fileName })
              : ''}
        confirmLabel={confirmAction?.kind === 'publish' ? t('procedures.publish') : confirmAction?.kind === 'archive' ? t('procedures.archive') : t('common.delete')}
        onConfirm={() => {
          const action = confirmAction;
          setConfirmAction(null);
          if (!action) return;
          if (action.kind === 'publish') void publish(action.procedure);
          else if (action.kind === 'archive') void archive(action.procedure);
          else void removeDocument(action.documentId);
        }}
        onClose={() => setConfirmAction(null)}
      />
    </div>
  );
}
