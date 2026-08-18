import { FileSpreadsheet, KeyRound, Pencil, Plus, RefreshCw, Trash2, User, UserCheck, UserRoundX, UserX, Mail, Phone, Briefcase, Upload, X } from 'lucide-react';
import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { Field, SelectInput, TextInput } from '../components/FormFields';
import { ImportModal } from '../components/ImportModal';
import { LocationInventoryModal } from '../components/LocationInventoryModal';
import { Modal } from '../components/Modal';
import { PageHeader } from '../components/PageHeader';
import { PersonPreviewModal } from '../components/PersonPreviewModal';
import { Pagination } from '../components/Pagination';
import { SlidePanel } from '../components/SlidePanel';
import { StatusBadge } from '../components/StatusBadge';
import { EmptyState, ErrorState, LoadingState } from '../components/StateViews';
import { useAsyncData } from '../hooks/useAsyncData';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import { getEmploymentStatusPresentation, type Person } from '../types/domain';
import { csvCell, formatDateTime, toNullable } from '../utils/format';
import { useI18n } from '../i18n/I18nProvider';
import { languages } from '../i18n/translations';
import { useCelebration } from '../celebration/CelebrationProvider';
import { useAuth } from '../auth/AuthProvider';

const pageSize = 25;

export function PeoplePage() {
  const { t, tPlural } = useI18n();
  const { celebrate } = useCelebration();
  const auth = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();
  const [search, setSearch] = useState(searchParams.get('search') ?? '');
  const [page, setPage] = useState(1);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [selected, setSelected] = useState<Person | null>(null);
  const [editing, setEditing] = useState<Person | null>(null);
  const [personModalOpen, setPersonModalOpen] = useState(false);
  const [createDefaults, setCreateDefaults] = useState<{ firstName: string; lastName: string; email: string }>({ firstName: '', lastName: '', email: '' });
  const [deleteTarget, setDeleteTarget] = useState<Person | null>(null);
  const [deactivateTarget, setDeactivateTarget] = useState<Person | null>(null);
  const [teamQuickAdd, setTeamQuickAdd] = useState(false);
  const [teamSaving, setTeamSaving] = useState(false);
  const [selectedTeamId, setSelectedTeamId] = useState('');
  const [relationTypeQuickAdd, setRelationTypeQuickAdd] = useState(false);
  const [relationTypeSaving, setRelationTypeSaving] = useState(false);
  const [selectedRelationType, setSelectedRelationType] = useState('');
  const [importOpen, setImportOpen] = useState(false);
  const [viewLocation, setViewLocation] = useState<string | null>(null);
  const [viewPersonId, setViewPersonId] = useState<string | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [bulkTeamModal, setBulkTeamModal] = useState(false);
  const [bulkSaving, setBulkSaving] = useState(false);
  const debouncedSearch = useDebouncedValue(search.trim(), 320);

  const peopleLoader = useMemo(() => () => api.peoplePaged({ search: debouncedSearch, page, pageSize }), [debouncedSearch, page]);
  const people = useAsyncData(peopleLoader, [peopleLoader]);
  const allPeople = useAsyncData(() => api.people(), []);
  const teams = useAsyncData(api.teams, []);
  const relationTypes = useAsyncData(api.personRelationTypes, []);
  const locations = useAsyncData(api.locations, []);
  const rows = useMemo(() => people.data?.items ?? [], [people.data]);
  const totalPeople = people.data?.total ?? 0;
  const managerCandidates = useMemo(() => (allPeople.data ?? []).filter(p => p.id !== editing?.id), [allPeople.data, editing]);
  const selectedPeople = useMemo(() => rows.filter(person => selectedIds.has(person.id)), [rows, selectedIds]);
  const allOnPageSelected = rows.length > 0 && rows.every(person => selectedIds.has(person.id));
  const personWorkspaceLoader = useMemo(() => () => (selected ? api.personWorkspace(selected.id) : Promise.resolve(null)), [selected]);
  const personWorkspace = useAsyncData(personWorkspaceLoader, [personWorkspaceLoader]);

  useEffect(() => {
    const params = new URLSearchParams();
    if (debouncedSearch) params.set('search', debouncedSearch);
    setSearchParams(params, { replace: true });
    setPage(1);
    setSelectedIds(new Set());
  }, [debouncedSearch, setSearchParams]);

  useEffect(() => {
    setSelectedIds(new Set());
  }, [page]);

  useEffect(() => {
    if (searchParams.get('addSelf') !== '1') return;
    const [firstName, ...rest] = (auth.userName ?? '').trim().split(/\s+/);
    setCreateDefaults({ firstName: firstName ?? '', lastName: rest.join(' '), email: auth.userEmail ?? '' });
    setEditing(null);
    setSelected(null);
    setSelectedTeamId('');
    setPersonModalOpen(true);
    const next = new URLSearchParams(searchParams);
    next.delete('addSelf');
    setSearchParams(next, { replace: true });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (editing || selectedRelationType || !relationTypes.data?.length) return;
    setSelectedRelationType(relationTypes.data[0].name);
  }, [relationTypes.data, editing, selectedRelationType]);

  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(() => setMessage(null), message.type === 'success' ? 3500 : 6500);
    return () => window.clearTimeout(timeout);
  }, [message]);

  function openCreate() {
    setEditing(null);
    setSelected(null);
    setSelectedTeamId('');
    setSelectedRelationType(relationTypes.data?.[0]?.name ?? '');
    setCreateDefaults({ firstName: '', lastName: '', email: '' });
    setPersonModalOpen(true);
  }

  function openEdit(person: Person) {
    setEditing(person);
    setSelected(null);
    setSelectedTeamId(person.teamId ?? '');
    setSelectedRelationType(person.relationType);
    setPersonModalOpen(true);
  }

  function closePersonModal() {
    setPersonModalOpen(false);
    setEditing(null);
  }

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);

    const body = {
      firstName: String(form.get('firstName') ?? '').trim(),
      lastName: String(form.get('lastName') ?? '').trim(),
      email: String(form.get('email') ?? '').trim(),
      phone: toNullable(String(form.get('phone') ?? '')),
      employeeNumber: toNullable(String(form.get('employeeNumber') ?? '')),
      relationType: String(form.get('relationType') ?? '').trim(),
      jobTitle: toNullable(String(form.get('jobTitle') ?? '')),
      teamId: toNullable(String(form.get('teamId') ?? '')),
      managerId: toNullable(String(form.get('managerId') ?? '')),
      location: toNullable(String(form.get('location') ?? '')),
      costCenter: toNullable(String(form.get('costCenter') ?? '')),
      preferredLanguage: toNullable(String(form.get('preferredLanguage') ?? '')),
      isActive: editing ? editing.isActive : true
    };

    if (!body.firstName || !body.lastName || !body.email) {
      return setMessage({ type: 'error', text: t('people.requiredFields') });
    }

    setSaving(true);
    setMessage(null);
    try {
      await (editing ? api.updatePerson(editing.id, body) : api.createPerson(body));
      setPersonModalOpen(false);
      setEditing(null);
      setSelected(null);
      setMessage({ type: 'success', text: editing ? t('people.saved') : t('people.created') });
      if (!editing) celebrate(t('celebration.personAdded'));
      await Promise.all([people.reload(), allPeople.reload()]);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('people.saveFailed') });
    } finally {
      setSaving(false);
    }
  }

  async function toggleActive(person: Person) {
    try {
      await api.updatePerson(person.id, {
        firstName: person.firstName,
        lastName: person.lastName,
        email: person.email,
        phone: person.phone,
        employeeNumber: person.employeeNumber,
        relationType: person.relationType,
        jobTitle: person.jobTitle,
        teamId: person.teamId,
        managerId: person.managerId,
        location: person.location,
        costCenter: person.costCenter,
        preferredLanguage: person.preferredLanguage,
        isActive: !person.isActive
      });
      setMessage({ type: 'success', text: person.isActive ? t('people.deactivated') : t('people.activated') });
      setSelected(null);
      await people.reload();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('people.saveFailed') });
    }
  }

  async function deletePerson() {
    if (!deleteTarget) return;
    try {
      await api.deletePerson(deleteTarget.id);
      setMessage({ type: 'success', text: t('people.deleted') });
      setSelected(current => current?.id === deleteTarget.id ? null : current);
      setEditing(current => current?.id === deleteTarget.id ? null : current);
      setDeleteTarget(null);
      await Promise.all([people.reload(), allPeople.reload()]);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('people.deleteFailed') });
      setDeleteTarget(null);
    }
  }

  function toggleSelected(id: string) {
    setSelectedIds(current => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  }

  function toggleSelectAllOnPage() {
    setSelectedIds(current => {
      const next = new Set(current);
      if (allOnPageSelected) rows.forEach(person => next.delete(person.id));
      else rows.forEach(person => next.add(person.id));
      return next;
    });
  }

  async function applyBulkTeamAssign(teamId: string | null) {
    setBulkSaving(true);
    let success = 0;
    let failed = 0;
    const failedIds: string[] = [];
    for (const person of selectedPeople) {
      try {
        await api.updatePerson(person.id, {
          firstName: person.firstName,
          lastName: person.lastName,
          email: person.email,
          phone: person.phone,
          employeeNumber: person.employeeNumber,
          relationType: person.relationType,
          jobTitle: person.jobTitle,
          teamId,
          managerId: person.managerId,
          location: person.location,
          costCenter: person.costCenter,
          preferredLanguage: person.preferredLanguage,
          isActive: person.isActive
        });
        success++;
      } catch {
        failed++;
        failedIds.push(person.id);
      }
    }
    setBulkSaving(false);
    setBulkTeamModal(false);
    setMessage({ type: failed ? 'error' : 'success', text: t(failed ? 'people.bulkFailedKept' : 'people.bulkResult', { success, failed }) });
    setSelectedIds(new Set(failedIds));
    await people.reload();
  }

  function exportSelectedCsv() {
    const header = [t('people.csvFirstName'), t('people.csvLastName'), t('people.csvEmail'), t('people.csvPhone'), t('people.csvJobTitle'), t('people.csvTeam'), t('people.csvStatus')];
    const rows = selectedPeople.map(person => [person.firstName, person.lastName, person.email, person.phone ?? '', person.jobTitle ?? '', person.teamName ?? '', t(getEmploymentStatusPresentation(person.employmentStatus).labelKey)]);
    const csv = [header, ...rows].map(row => row.map(csvCell).join(',')).join('\r\n');
    const blob = new Blob(['﻿' + csv], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = t('people.csvFileName');
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  }

  async function saveQuickTeam(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const name = String(form.get('name') ?? '').trim();
    if (!name) return setMessage({ type: 'error', text: t('people.teamNameRequired') });
    setTeamSaving(true);
    try {
      const created = await api.createTeam({ name, managerId: null, costCenter: null });
      setMessage({ type: 'success', text: t('people.teamCreated') });
      setTeamQuickAdd(false);
      setSelectedTeamId(created.id);
      await teams.reload();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('people.teamCreateFailed') });
    } finally {
      setTeamSaving(false);
    }
  }

  async function saveQuickRelationType(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const name = String(form.get('name') ?? '').trim();
    if (!name) return setMessage({ type: 'error', text: t('people.relationTypeNameRequired') });
    setRelationTypeSaving(true);
    try {
      const created = await api.createPersonRelationType({ name });
      setMessage({ type: 'success', text: t('people.relationTypeCreated') });
      setRelationTypeQuickAdd(false);
      setSelectedRelationType(created.name);
      await relationTypes.reload();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('people.relationTypeCreateFailed') });
    } finally {
      setRelationTypeSaving(false);
    }
  }

  if ((people.isLoading && !people.data) || teams.isLoading) return <LoadingState title={t('people.loadingTitle')} description={t('people.loadingDesc')} />;
  if (people.error) return <ErrorState message={people.error} onRetry={people.reload} />;


  return (
    <div className="pageStack">
      <PageHeader
        eyebrow={t('page.people.eyebrow')}
        title={t('page.people.title')}
        actions={
          <>
            <Button variant="secondary" onClick={people.reload} icon={<RefreshCw size={16} />}>
              {t('common.refresh')}
            </Button>
            <Button variant="secondary" onClick={() => setImportOpen(true)} icon={<Upload size={16} />}>
              {t('people.import')}
            </Button>
            <Button onClick={openCreate} icon={<Plus size={16} />}>
              {t('people.add')}
            </Button>
          </>
        }
      />

      {message && (
        <div className="toastStack" aria-live="polite">
          <div className={`toast toast--${message.type}`}>{message.text}</div>
        </div>
      )}

      {selectedIds.size > 0 && (
        <Card className="toolbarCard">
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
            <strong>{t('people.bulkSelectedCount', { count: selectedIds.size })}</strong>
            <Button variant="secondary" onClick={() => setBulkTeamModal(true)}>{t('people.bulkAssignTeam')}</Button>
            <Button variant="secondary" onClick={exportSelectedCsv} icon={<FileSpreadsheet size={16} />}>{t('people.bulkExport')}</Button>
            <Button variant="ghost" onClick={() => setSelectedIds(new Set())} icon={<X size={16} />}>{t('people.bulkClear')}</Button>
          </div>
        </Card>
      )}

      <Card className="toolbarCard">
        <form className="filters filters--single" onSubmit={event => event.preventDefault()}>
          <Field label={t('common.search')}>
            <TextInput value={search} onChange={event => setSearch(event.target.value)} placeholder={t('people.searchPlaceholder')} />
          </Field>
        </form>
      </Card>

      <Card>
        <div className="sectionTitle">
          <div>
            <h2>{t('people.listTitle')}</h2>
            <p>{t('people.countSummary', { count: totalPeople, pageSize, noun: tPlural('count.people', totalPeople) })}</p>
          </div>
        </div>

        {people.isLoading && <p className="muted">{t('people.refreshing')}</p>}

        {!rows.length ? (
          <EmptyState
            title={t('people.emptyTitle')}
            description={t('people.emptyDesc')}
            action={debouncedSearch ? (
              <Button variant="secondary" icon={<X size={16} />} onClick={() => setSearch('')}>{t('common.clearFilters')}</Button>
            ) : (
              <Button onClick={openCreate} icon={<Plus size={16} />}>{t('people.add')}</Button>
            )}
          />
        ) : (
          <>
            <div className="tableWrap tableWrap--cards">
              <table className="dense-table">
                <thead>
                  <tr>
                    <th style={{ width: '32px' }}><input type="checkbox" checked={allOnPageSelected} onChange={toggleSelectAllOnPage} onClick={event => event.stopPropagation()} aria-label={t('people.bulkSelectAll')} /></th>
                    <th style={{ width: '40px' }}></th>
                    <th>{t('people.colFullName')}</th>
                    <th>{t('settings.emailLabel')}</th>
                    <th>{t('people.colPhone')}</th>
                    <th>{t('people.colType')}</th>
                    <th>{t('people.colJobTitle')}</th>
                    <th>{t('people.colTeam')}</th>
                    <th>{t('people.colStatus')}</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map(person => (
                    <tr
                      key={person.id}
                      tabIndex={0}
                      onClick={() => setSelected(person)}
                      onKeyDown={event => {
                        if (event.target !== event.currentTarget) return;
                        if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); setSelected(person); }
                      }}
                    >
                      <td onClick={event => event.stopPropagation()}>
                        <input type="checkbox" checked={selectedIds.has(person.id)} onChange={() => toggleSelected(person.id)} aria-label={t('people.bulkSelectOne', { name: person.fullName })} />
                      </td>
                      <td className="cell-icon">
                        <div className="table-icon">
                          <User size={16} />
                        </div>
                      </td>
                      <td data-label={t('people.colFullName')}>
                        <strong>{person.fullName}</strong>
                        {person.employeeNumber && <small>#{person.employeeNumber}</small>}
                      </td>
                      <td data-label={t('settings.emailLabel')}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px' }}>
                          <Mail size={12} style={{ color: 'var(--muted)' }} />
                          {person.email}
                        </div>
                      </td>
                      <td data-label={t('people.colPhone')}>
                        {person.phone ? (
                          <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px' }}>
                            <Phone size={12} style={{ color: 'var(--muted)' }} />
                            {person.phone}
                          </div>
                        ) : (
                          <span style={{ color: 'var(--muted)' }}>—</span>
                        )}
                      </td>
                      <td data-label={t('people.colType')}>
                        <span className="status">{person.relationType}</span>
                      </td>
                      <td data-label={t('people.colJobTitle')}>
                        {person.jobTitle ? (
                          <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px' }}>
                            <Briefcase size={12} style={{ color: 'var(--muted)' }} />
                            {person.jobTitle}
                          </div>
                        ) : (
                          <span style={{ color: 'var(--muted)' }}>—</span>
                        )}
                      </td>
                      <td data-label={t('people.colTeam')}>{person.teamName ?? <span style={{ color: 'var(--muted)' }}>—</span>}</td>
                      <td data-label={t('people.colStatus')}>
                        <span className={`status ${getEmploymentStatusPresentation(person.employmentStatus).badgeClass}`}>
                          {t(getEmploymentStatusPresentation(person.employmentStatus).labelKey)}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination page={page} total={totalPeople} pageSize={pageSize} onPageChange={setPage} />
          </>
        )}
      </Card>

      <SlidePanel open={!!selected} title={selected?.fullName ?? t('people.addTitle')} onClose={() => setSelected(null)}>
        {selected && (
          <div className="modalDetails">
            <div className="modalToolbar">
              <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                <div className="table-icon" style={{ width: '48px', height: '48px' }}>
                  <User size={24} />
                </div>
                <div>
                  <strong>{selected.email}</strong>
                  <small>{selected.relationType}</small>
                </div>
              </div>
              <div className="rowActions">
                <Button variant="secondary" onClick={() => openEdit(selected)} icon={<Pencil size={16} />}>
                  {t('common.edit')}
                </Button>
                <Button variant="secondary" onClick={() => getEmploymentStatusPresentation(selected.employmentStatus).action === 'deactivate' ? setDeactivateTarget(selected) : toggleActive(selected)} icon={getEmploymentStatusPresentation(selected.employmentStatus).action === 'deactivate' ? <UserX size={16} /> : <UserCheck size={16} />}>
                  {getEmploymentStatusPresentation(selected.employmentStatus).action === 'deactivate' ? t('people.deactivate') : t('people.activate')}
                </Button>
                <Button
                  variant="secondary"
                  onClick={() => {
                    setDeleteTarget(selected);
                    setSelected(null);
                  }}
                  icon={<Trash2 size={16} />}
                >
                  {t('common.delete')}
                </Button>
                {(auth.roles.includes('owner') || auth.roles.includes('admin')) && (
                  <Link
                    to={`/settings?tab=users&inviteEmail=${encodeURIComponent(selected.email)}&inviteName=${encodeURIComponent(selected.fullName)}`}
                    className="button button--secondary"
                  >
                    <span className="button__icon"><KeyRound size={16} /></span>
                    <span>{t('people.createLoginAccount')}</span>
                  </Link>
                )}
                {selected.employmentStatus === 'Active' && (
                  <Link to={`/offboarding?new=1&personId=${selected.id}`} className="button button--secondary">
                    <span className="button__icon"><UserRoundX size={16} /></span>
                    <span>{t('people.startOffboarding')}</span>
                  </Link>
                )}
              </div>
            </div>

            <dl className="detailGrid">
              <div>
                <dt>{t('people.firstName')}</dt>
                <dd>{selected.firstName}</dd>
              </div>
              <div>
                <dt>{t('people.lastName')}</dt>
                <dd>{selected.lastName}</dd>
              </div>
              <div>
                <dt>{t('settings.emailLabel')}</dt>
                <dd>{selected.email}</dd>
              </div>
              <div>
                <dt>{t('people.phoneLabel')}</dt>
                <dd>{selected.phone ?? t('common.none')}</dd>
              </div>
              <div>
                <dt>{t('people.employeeNumber')}</dt>
                <dd>{selected.employeeNumber ?? t('common.none')}</dd>
              </div>
              <div>
                <dt>{t('people.jobTitleLabel')}</dt>
                <dd>{selected.jobTitle ?? t('common.none')}</dd>
              </div>
              <div>
                <dt>{t('people.teamLabel')}</dt>
                <dd>{selected.teamName ?? t('common.none')}</dd>
              </div>
              <div>
                <dt>{t('people.managerLabel')}</dt>
                <dd>
                  {selected.managerId && allPeople.data?.find(p => p.id === selected.managerId) ? (
                    <button type="button" className="inlineAction" onClick={() => setViewPersonId(selected.managerId ?? null)}>{allPeople.data?.find(p => p.id === selected.managerId)?.fullName}</button>
                  ) : t('common.none')}
                </dd>
              </div>
              <div>
                <dt>{t('people.locationLabel')}</dt>
                <dd>
                  {selected.location ? (
                    <button type="button" className="inlineAction" onClick={() => setViewLocation(selected.location ?? null)}>{selected.location}</button>
                  ) : t('common.none')}
                </dd>
              </div>
              <div>
                <dt>{t('people.costCenter')}</dt>
                <dd>{selected.costCenter ?? t('common.none')}</dd>
              </div>
              <div>
                <dt>{t('people.colStatus')}</dt>
                <dd>
                  <span className={`status ${getEmploymentStatusPresentation(selected.employmentStatus).badgeClass}`}>
                    {t(getEmploymentStatusPresentation(selected.employmentStatus).labelKey)}
                  </span>
                </dd>
              </div>
              <div>
                <dt>{t('people.preferredLanguage')}</dt>
                <dd>{languages.find(language => language.value === selected.preferredLanguage)?.label ?? t('people.preferredLanguageFallback')}</dd>
              </div>
              {selected.employmentEndsAt && <div>
                <dt>{t('people.employmentEndsAt')}</dt>
                <dd>{formatDateTime(selected.employmentEndsAt)}</dd>
              </div>}
              {selected.deactivatedAt && <div>
                <dt>{t('people.deactivatedAt')}</dt>
                <dd>{formatDateTime(selected.deactivatedAt)}</dd>
              </div>}
            </dl>

            <div className="formSectionTitle">{t('people.assignedAssetsTitle')}</div>
            {personWorkspace.isLoading ? <p className="muted">{t('people.loadingWorkspace')}</p> : !personWorkspace.data?.assets?.length ? (
              <p className="muted">{t('people.noAssignedAssets')}</p>
            ) : (
              <div className="listRows">
                {personWorkspace.data.assets.map(asset => (
                  <Link className="listRow" to={`/assets?search=${encodeURIComponent(asset.assetTag)}`} key={asset.id}>
                    <div><strong>{asset.name}</strong><small>{asset.assetTag}</small></div>
                    <span>{asset.categoryName ?? t('common.none')}</span>
                  </Link>
                ))}
              </div>
            )}

            <div className="formSectionTitle">{t('people.assignmentsTitle')}</div>
            {personWorkspace.isLoading ? <p className="muted">{t('people.loadingWorkspace')}</p> : !personWorkspace.data?.assignments?.length ? (
              <p className="muted">{t('people.noAssignments')}</p>
            ) : (
              <div className="listRows">
                {personWorkspace.data.assignments.map(assignment => (
                  <div className="listRow" key={assignment.id}>
                    <div><strong>{assignment.protocolNumber}</strong><small>{assignment.assetNames.join(', ')}</small></div>
                    <StatusBadge status={assignment.status} />
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </SlidePanel>

      <Modal
        open={personModalOpen}
        title={editing ? t('people.editTitle') : t('people.addTitle')}
        onClose={closePersonModal}
        width="wide"
      >
        <form className="formGrid" onSubmit={handleSave} key={editing?.id ?? 'new-person'}>
          <div className="formSectionTitle">{t('people.personalData')}</div>
          <Field label={t('people.firstName')}><TextInput name="firstName" defaultValue={editing?.firstName ?? createDefaults.firstName} required /></Field>
          <Field label={t('people.lastName')}><TextInput name="lastName" defaultValue={editing?.lastName ?? createDefaults.lastName} required /></Field>
          <Field label={t('settings.emailLabel')}><TextInput name="email" type="email" defaultValue={editing?.email ?? createDefaults.email} required /></Field>
          <Field label={t('people.phoneLabel')}><TextInput name="phone" defaultValue={editing?.phone ?? ''} /></Field>
          <Field label={t('people.employeeNumber')}><TextInput name="employeeNumber" defaultValue={editing?.employeeNumber ?? ''} /></Field>
          <Field label={t('people.relationType')}>
            <div className="fieldWithAdd">
              <SelectInput name="relationType" value={selectedRelationType} onChange={event => setSelectedRelationType(event.target.value)} required>
                {editing?.relationType && !relationTypes.data?.some(item => item.name === editing.relationType) ? (
                  <option value={editing.relationType}>{editing.relationType}</option>
                ) : null}
                {relationTypes.data?.map(type => <option key={type.id} value={type.name}>{type.name}</option>)}
              </SelectInput>
              <button type="button" className="iconButton iconButton--add" aria-label={t('people.addRelationTypeTitle')} title={t('people.addRelationTypeTitle')} onClick={() => setRelationTypeQuickAdd(true)}><Plus size={18} /></button>
            </div>
          </Field>

          <div className="formSectionTitle">{t('people.organization')}</div>
          <Field label={t('people.jobTitleLabel')}><TextInput name="jobTitle" defaultValue={editing?.jobTitle ?? ''} /></Field>
          <Field label={t('people.teamLabel')}>
            <div className="fieldWithAdd">
              <SelectInput name="teamId" value={selectedTeamId} onChange={event => setSelectedTeamId(event.target.value)}>
                <option value="">{t('people.noTeam')}</option>
                {teams.data?.map(team => <option key={team.id} value={team.id}>{team.name}</option>)}
              </SelectInput>
              <button type="button" className="iconButton iconButton--add" aria-label={t('people.addTeamTitle')} title={t('people.addTeamTitle')} onClick={() => setTeamQuickAdd(true)}><Plus size={18} /></button>
            </div>
          </Field>
          <Field label={t('people.managerLabel')}>
            <SelectInput name="managerId" defaultValue={editing?.managerId ?? ''}>
              <option value="">{t('people.noManager')}</option>
              {managerCandidates.map(person => <option key={person.id} value={person.id}>{person.fullName}</option>)}
            </SelectInput>
          </Field>
          <Field label={t('people.locationLabel')}>
            <SelectInput name="location" defaultValue={editing?.location ?? ''}>
              <option value="">{t('common.noLocation')}</option>
              {editing?.location && !locations.data?.some(item => item.fullPath === editing.location) ? (
                <option value={editing.location}>{editing.location}</option>
              ) : null}
              {locations.data?.map(item => <option key={item.id} value={item.fullPath}>{item.fullPath}</option>)}
            </SelectInput>
          </Field>
          <Field label={t('people.costCenter')}><TextInput name="costCenter" defaultValue={editing?.costCenter ?? ''} /></Field>
          <Field label={t('people.preferredLanguage')}>
            <SelectInput name="preferredLanguage" defaultValue={editing?.preferredLanguage ?? ''}>
              <option value="">{t('people.preferredLanguageFallback')}</option>
              {languages.map(language => <option key={language.value} value={language.value}>{language.label}</option>)}
            </SelectInput>
          </Field>

          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={closePersonModal}>{t('common.cancel')}</Button>
            <Button disabled={saving} icon={editing ? <Pencil size={16} /> : <Plus size={16} />}>
              {saving ? t('common.saving') : editing ? t('people.save') : t('people.add')}
            </Button>
          </div>
        </form>
      </Modal>

      <ConfirmDialog
        open={!!deleteTarget}
        title={t('people.deleteConfirmTitle')}
        description={t('people.deleteConfirmDesc')}
        confirmLabel={t('common.delete')}
        onConfirm={deletePerson}
        onClose={() => setDeleteTarget(null)}
      />

      <ConfirmDialog
        open={!!deactivateTarget}
        title={t('people.deactivateConfirmTitle')}
        description={t('people.deactivateConfirmDesc', { name: deactivateTarget?.fullName ?? '' })}
        confirmLabel={t('people.deactivate')}
        onConfirm={() => { if (deactivateTarget) void toggleActive(deactivateTarget); setDeactivateTarget(null); }}
        onClose={() => setDeactivateTarget(null)}
      />

      <Modal open={teamQuickAdd} title={t('people.addTeamTitle')} onClose={() => setTeamQuickAdd(false)}>
        <form className="formGrid" onSubmit={saveQuickTeam}>
          <Field label={t('people.teamNameLabel')}><TextInput name="name" required /></Field>
          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={() => setTeamQuickAdd(false)}>{t('common.cancel')}</Button>
            <Button disabled={teamSaving}>{teamSaving ? t('common.saving') : t('people.addTeamTitle')}</Button>
          </div>
        </form>
      </Modal>

      <Modal open={relationTypeQuickAdd} title={t('people.addRelationTypeTitle')} onClose={() => setRelationTypeQuickAdd(false)}>
        <form className="formGrid" onSubmit={saveQuickRelationType}>
          <Field label={t('people.relationTypeNameLabel')}><TextInput name="name" required /></Field>
          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={() => setRelationTypeQuickAdd(false)}>{t('common.cancel')}</Button>
            <Button disabled={relationTypeSaving}>{relationTypeSaving ? t('common.saving') : t('people.addRelationTypeTitle')}</Button>
          </div>
        </form>
      </Modal>

      <ImportModal
        open={importOpen}
        entity="people"
        existingKeys={(allPeople.data ?? []).map(item => item.email.toLowerCase())}
        teams={teams.data ?? []}
        onClose={() => setImportOpen(false)}
        onDone={people.reload}
      />

      {viewLocation && <LocationInventoryModal locationPath={viewLocation} onClose={() => setViewLocation(null)} />}
      {viewPersonId && <PersonPreviewModal personId={viewPersonId} onClose={() => setViewPersonId(null)} />}

      <Modal open={bulkTeamModal} title={t('people.bulkAssignTeamTitle')} onClose={() => setBulkTeamModal(false)}>
        <form className="formGrid" onSubmit={event => { event.preventDefault(); const form = new FormData(event.currentTarget); void applyBulkTeamAssign(toNullable(String(form.get('teamId') ?? ''))); }}>
          <Field label={t('people.teamLabel')}>
            <SelectInput name="teamId" defaultValue="">
              <option value="">{t('people.noTeam')}</option>
              {teams.data?.map(team => <option key={team.id} value={team.id}>{team.name}</option>)}
            </SelectInput>
          </Field>
          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={() => setBulkTeamModal(false)}>{t('common.cancel')}</Button>
            <Button disabled={bulkSaving}>{bulkSaving ? t('common.saving') : t('assets.bulkApply')}</Button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
