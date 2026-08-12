import { Eye, EyeOff, KeyRound, Lock, Pencil, Plus, Trash2, UserMinus, UserPlus } from 'lucide-react';
import { FormEvent, useEffect, useState } from 'react';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { Field, SelectInput, TextArea, TextInput } from '../components/FormFields';
import { Modal } from '../components/Modal';
import { PageHeader } from '../components/PageHeader';
import { SlidePanel } from '../components/SlidePanel';
import { EmptyState, ErrorState, LoadingState } from '../components/StateViews';
import { useAsyncData } from '../hooks/useAsyncData';
import { toNullable } from '../utils/format';
import type { License } from '../types/domain';
import { useI18n } from '../i18n/I18nProvider';

export function LicensesPage() {
  const { t } = useI18n();
  const licenses = useAsyncData(api.licenses, []);
  const people = useAsyncData(() => api.people(), []);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<License | null>(null);
  const [saving, setSaving] = useState(false);
  const [selected, setSelected] = useState<License | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<License | null>(null);
  const [revealedKeys, setRevealedKeys] = useState<Set<string>>(new Set());
  const [selectedPersonId, setSelectedPersonId] = useState('');
  const [seatSaving, setSeatSaving] = useState(false);

  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(() => setMessage(null), message.type === 'success' ? 3500 : 6500);
    return () => window.clearTimeout(timeout);
  }, [message]);

  useEffect(() => {
    if (!selected) return;
    const fresh = licenses.data?.find(item => item.id === selected.id);
    if (fresh) setSelected(fresh);
  }, [licenses.data, selected?.id]);

  function success(text: string) { setMessage({ type: 'success', text }); }
  function failure(error: unknown, fallback: string) { setMessage({ type: 'error', text: error instanceof Error ? error.message : fallback }); }

  function openCreate() {
    setEditing(null);
    setSelected(null);
    setModalOpen(true);
  }

  function openEdit(license: License) {
    setEditing(license);
    setModalOpen(true);
  }

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const body = {
      name: String(form.get('name') ?? '').trim(),
      vendor: toNullable(String(form.get('vendor') ?? '')),
      licenseKey: toNullable(String(form.get('licenseKey') ?? '')),
      seatsTotal: Number(form.get('seatsTotal') ?? 0),
      expiresAt: toNullable(String(form.get('expiresAt') ?? '')),
      notes: toNullable(String(form.get('notes') ?? ''))
    };
    if (!body.name) return setMessage({ type: 'error', text: t('licenses.nameRequired') });
    setSaving(true);
    try {
      editing ? await api.updateLicense(editing.id, body) : await api.createLicense(body);
      success(editing ? t('licenses.saved') : t('licenses.created'));
      setModalOpen(false);
      setEditing(null);
      await licenses.reload();
    } catch (error) {
      failure(error, t('licenses.saveFailed'));
    } finally {
      setSaving(false);
    }
  }

  async function confirmDelete() {
    if (!deleteTarget) return;
    try {
      await api.deleteLicense(deleteTarget.id);
      success(t('licenses.deleted'));
      setSelected(current => current?.id === deleteTarget.id ? null : current);
      setDeleteTarget(null);
      await licenses.reload();
    } catch (error) {
      failure(error, t('licenses.deleteFailed'));
      setDeleteTarget(null);
    }
  }

  async function assignSeat() {
    if (!selected || !selectedPersonId) return;
    setSeatSaving(true);
    try {
      await api.assignLicenseSeat(selected.id, selectedPersonId);
      success(t('licenses.seatAssigned'));
      setSelectedPersonId('');
      await licenses.reload();
    } catch (error) {
      failure(error, t('licenses.seatAssignFailed'));
    } finally {
      setSeatSaving(false);
    }
  }

  async function unassignSeat(personId: string) {
    if (!selected) return;
    try {
      await api.unassignLicenseSeat(selected.id, personId);
      success(t('licenses.seatUnassigned'));
      await licenses.reload();
    } catch (error) {
      failure(error, t('licenses.seatUnassignFailed'));
    }
  }

  function toggleReveal(id: string) {
    setRevealedKeys(current => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  }

  if (licenses.isLoading && !licenses.data) return <LoadingState title={t('licenses.loadingTitle')} />;
  if (licenses.error) return <ErrorState message={licenses.error} onRetry={licenses.reload} />;

  const rows = licenses.data ?? [];
  const availablePeople = (people.data ?? []).filter(p => !selected?.seats.some(seat => seat.personId === p.id));

  return (
    <div className="pageStack">
      <PageHeader
        eyebrow={t('page.licenses.eyebrow')}
        title={t('page.licenses.title')}
        actions={<Button onClick={openCreate} icon={<Plus size={16} />}>{t('licenses.add')}</Button>}
      />

      {message && (
        <div className="toastStack" aria-live="polite">
          <div className={`toast toast--${message.type}`}>{message.text}</div>
        </div>
      )}

      <Card>
        {!rows.length ? (
          <EmptyState title={t('licenses.emptyTitle')} description={t('licenses.emptyDesc')} action={<Button onClick={openCreate} icon={<Plus size={16} />}>{t('licenses.add')}</Button>} />
        ) : (
          <div className="tableWrap tableWrap--cards">
            <table className="dense-table">
              <thead>
                <tr>
                  <th></th>
                  <th>{t('licenses.colName')}</th>
                  <th>{t('licenses.colVendor')}</th>
                  <th>{t('licenses.colSeats')}</th>
                  <th>{t('licenses.colKey')}</th>
                  <th>{t('licenses.colExpires')}</th>
                </tr>
              </thead>
              <tbody>
                {rows.map(license => (
                  <tr key={license.id} onClick={() => setSelected(license)}>
                    <td className="cell-icon"><div className="table-icon"><KeyRound size={16} /></div></td>
                    <td data-label={t('licenses.colName')}><strong>{license.name}</strong></td>
                    <td data-label={t('licenses.colVendor')}>{license.vendor ?? '—'}</td>
                    <td data-label={t('licenses.colSeats')}>{license.seatsAssigned}/{license.seatsTotal}</td>
                    <td data-label={t('licenses.colKey')}>
                      {!license.hasLicenseKey ? '—' : !license.canViewLicenseKey ? (
                        <span title={t('licenses.keyHiddenHint')}><Lock size={14} /> {t('licenses.keyHidden')}</span>
                      ) : revealedKeys.has(license.id) ? (
                        <code>{license.licenseKey}</code>
                      ) : '••••••••'}
                    </td>
                    <td data-label={t('licenses.colExpires')}>{license.expiresAt ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <SlidePanel open={!!selected} title={selected?.name ?? ''} description={selected?.vendor ?? undefined} onClose={() => setSelected(null)}>
        {selected && (
          <div className="modalDetails">
            <div className="modalToolbar">
              <div>
                <strong>{selected.seatsAssigned}/{selected.seatsTotal} {t('licenses.seatsLabel')}</strong>
                <small>{selected.expiresAt ? t('licenses.expiresOn', { date: selected.expiresAt }) : t('licenses.noExpiry')}</small>
              </div>
              <div className="rowActions">
                <Button variant="secondary" onClick={() => openEdit(selected)} icon={<Pencil size={16} />}>{t('common.edit')}</Button>
                <Button variant="secondary" onClick={() => setDeleteTarget(selected)} icon={<Trash2 size={16} />}>{t('common.delete')}</Button>
              </div>
            </div>

            <div className="formSectionTitle">{t('licenses.keyLabel')}</div>
            {!selected.hasLicenseKey ? (
              <p className="muted">{t('licenses.noKey')}</p>
            ) : !selected.canViewLicenseKey ? (
              <p className="emptyInline"><Lock size={14} /> {t('licenses.keyHiddenHint')}</p>
            ) : (
              <div className="fieldWithAdd">
                <code style={{ flex: 1, padding: '10px 12px', border: '1px solid var(--border)', borderRadius: 'var(--radius)', background: 'var(--surface-strong)', overflowWrap: 'anywhere' }}>
                  {revealedKeys.has(selected.id) ? selected.licenseKey : '••••••••••••••••'}
                </code>
                <button type="button" className="iconButton" aria-label={t('licenses.toggleReveal')} onClick={() => toggleReveal(selected.id)}>
                  {revealedKeys.has(selected.id) ? <EyeOff size={16} /> : <Eye size={16} />}
                </button>
              </div>
            )}

            {selected.notes && <><div className="formSectionTitle">{t('licenses.notesLabel')}</div><p>{selected.notes}</p></>}

            <div className="formSectionTitle">{t('licenses.seatsTitle')}</div>
            {!selected.seats.length ? (
              <p className="muted">{t('licenses.noSeats')}</p>
            ) : (
              <div className="listRows">
                {selected.seats.map(seat => (
                  <div className="listRow" key={seat.personId}>
                    <div><strong>{seat.personName}</strong></div>
                    <button type="button" className="iconButton" aria-label={t('licenses.unassignSeatAria', { name: seat.personName })} onClick={() => unassignSeat(seat.personId)}><UserMinus size={16} /></button>
                  </div>
                ))}
              </div>
            )}

            {selected.seatsAssigned < selected.seatsTotal && (
              <div className="fieldWithAdd">
                <SelectInput value={selectedPersonId} onChange={event => setSelectedPersonId(event.target.value)}>
                  <option value="">{t('licenses.choosePerson')}</option>
                  {availablePeople.map(person => <option key={person.id} value={person.id}>{person.fullName}</option>)}
                </SelectInput>
                <button type="button" className="iconButton iconButton--add" aria-label={t('licenses.assignSeat')} title={t('licenses.assignSeat')} disabled={!selectedPersonId || seatSaving} onClick={assignSeat}><UserPlus size={18} /></button>
              </div>
            )}
          </div>
        )}
      </SlidePanel>

      <Modal open={modalOpen} title={editing ? t('licenses.editTitle') : t('licenses.addTitle')} onClose={() => setModalOpen(false)}>
        <form className="formGrid" onSubmit={handleSave} key={editing?.id ?? 'new-license'}>
          <Field label={t('licenses.nameLabel')}><TextInput name="name" defaultValue={editing?.name ?? ''} required /></Field>
          <Field label={t('licenses.vendorLabel')}><TextInput name="vendor" defaultValue={editing?.vendor ?? ''} /></Field>
          <Field label={t('licenses.keyLabel')} info={t('licenses.keyFieldHint')}><TextInput name="licenseKey" defaultValue={editing?.canViewLicenseKey ? editing?.licenseKey ?? '' : ''} placeholder={editing && editing.hasLicenseKey && !editing.canViewLicenseKey ? t('licenses.keyHidden') : undefined} /></Field>
          <Field label={t('licenses.seatsTotalLabel')}><TextInput name="seatsTotal" type="number" min={editing?.seatsAssigned ?? 0} defaultValue={editing?.seatsTotal ?? 1} required /></Field>
          <Field label={t('licenses.expiresAtLabel')}><TextInput name="expiresAt" type="date" defaultValue={editing?.expiresAt ?? ''} /></Field>
          <Field label={t('licenses.notesLabel')}><TextArea name="notes" defaultValue={editing?.notes ?? ''} /></Field>
          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={() => setModalOpen(false)}>{t('common.cancel')}</Button>
            <Button disabled={saving}>{saving ? t('common.saving') : t('licenses.save')}</Button>
          </div>
        </form>
      </Modal>

      <ConfirmDialog
        open={!!deleteTarget}
        title={t('licenses.deleteConfirmTitle')}
        description={t('licenses.deleteConfirmDesc', { name: deleteTarget?.name ?? '' })}
        confirmLabel={t('common.delete')}
        onConfirm={confirmDelete}
        onClose={() => setDeleteTarget(null)}
      />
    </div>
  );
}
