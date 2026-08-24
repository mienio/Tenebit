import { FormEvent, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { GripVertical, Plus, Save, Search, Trash2 } from 'lucide-react';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { Modal } from '../components/Modal';
import { Field, SelectInput, TextArea, TextInput } from '../components/FormFields';
import { IconPicker } from '../components/IconPicker';
import { AlertsSettings } from '../components/AlertsSettings';
import { LocationsManager } from '../components/LocationsManager';
import { PageHeader } from '../components/PageHeader';
import { Pagination, paginate } from '../components/Pagination';
import { EmptyState, ErrorState, LoadingState } from '../components/StateViews';
import { EmailVerificationNotice } from '../components/EmailVerificationBanner';
import { ProfileCard } from '../components/ProfileCard';
import { TwoFactorCard } from '../components/TwoFactorCard';
import { AccountLinksCard } from '../components/AccountLinksCard';
import { useAsyncData } from '../hooks/useAsyncData';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import type { AssetCategory, AssetCategoryType, AssetStatusSetting, JobProfile, OrganizationUser, PersonRelationTypeOption, QrLabelSettings, Team } from '../types/domain';
import { categoryTypeValues } from '../utils/labels';
import { toNullable } from '../utils/format';
import { CategoryIcon } from '../utils/categoryIcons';
import { useAuth } from '../auth/AuthProvider';
import { useI18n } from '../i18n/I18nProvider';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';

type Tab = 'account' | 'company' | 'locations' | 'customFields' | 'profiles' | 'users' | 'permissions' | 'alerts';
type SettingsMessage = { type: 'success' | 'error'; text: string } | null;
type DeleteTarget = { kind: 'category'; item: AssetCategory } | { kind: 'profile'; item: JobProfile } | { kind: 'relationType'; item: PersonRelationTypeOption } | { kind: 'team'; item: Team } | null;
const pageSize = 20;
const validTabs: Tab[] = ['account', 'company', 'locations', 'customFields', 'profiles', 'users', 'permissions', 'alerts'];
const organizationOnlyTabs: Tab[] = ['company', 'locations', 'customFields', 'profiles', 'users', 'permissions', 'alerts'];

export function SettingsPage() {
  const { t } = useI18n();
  const auth = useAuth();
  // Account security (2FA, linked logins, language) is every user's own - only the
  // organization-wide tabs below need owner/admin, so this page stays reachable for everyone.
  const canManageOrganization = auth.roles.includes('owner') || auth.roles.includes('admin');
  const categoryTypeLabels: Record<AssetCategoryType, string> = Object.fromEntries(categoryTypeValues.map(value => [value, t(`categoryType.${value}`)])) as Record<AssetCategoryType, string>;
  const [searchParams, setSearchParams] = useSearchParams();
  const initialTab = searchParams.get('tab') as Tab | null;
  const initialTabAllowed = initialTab && validTabs.includes(initialTab) && (canManageOrganization || !organizationOnlyTabs.includes(initialTab));
  const [tab, setTab] = useState<Tab>(initialTabAllowed ? initialTab! : canManageOrganization ? 'company' : 'account');
  // Each tab loads only its own data (the organization gates the whole page, so it stays eager).
  const categoriesNeeded = tab === 'customFields' || tab === 'profiles';
  const statusesNeeded = tab === 'customFields';
  const profilesNeeded = tab === 'profiles';
  const usersNeeded = tab === 'users';
  const permissionsNeeded = tab === 'permissions';
  const organization = useAsyncData(api.organization, []);
  const categories = useAsyncData(() => (categoriesNeeded ? api.categories() : Promise.resolve(null)), [categoriesNeeded]);
  const statuses = useAsyncData(() => (statusesNeeded ? api.assetStatuses() : Promise.resolve(null)), [statusesNeeded]);
  const qrLabelSettings = useAsyncData(() => (statusesNeeded ? api.qrLabelSettings() : Promise.resolve(null)), [statusesNeeded]);
  const relationTypeSettings = useAsyncData(() => (statusesNeeded ? api.personRelationTypes() : Promise.resolve(null)), [statusesNeeded]);
  const teamSettings = useAsyncData(() => (statusesNeeded ? api.teams() : Promise.resolve(null)), [statusesNeeded]);
  const procedures = useAsyncData(() => (profilesNeeded ? api.procedures() : Promise.resolve(null)), [profilesNeeded]);
  const people = useAsyncData(() => (profilesNeeded || usersNeeded ? api.people() : Promise.resolve(null)), [profilesNeeded, usersNeeded]);
  const profiles = useAsyncData(() => (profilesNeeded ? api.jobProfiles() : Promise.resolve(null)), [profilesNeeded]);
  const users = useAsyncData(() => (usersNeeded ? api.users() : Promise.resolve(null)), [usersNeeded]);
  const roles = useAsyncData(() => (usersNeeded || permissionsNeeded ? api.roles() : Promise.resolve(null)), [usersNeeded, permissionsNeeded]);
  const rolePermissions = useAsyncData(() => (permissionsNeeded ? api.rolePermissions() : Promise.resolve(null)), [permissionsNeeded]);
  const [selectedRoleKey, setSelectedRoleKey] = useState<string>('owner');
  const [permissionSaving, setPermissionSaving] = useState<string | null>(null);
  const [modal, setModal] = useState<'profile' | 'user' | null>(null);
  const [editingProfile, setEditingProfile] = useState<JobProfile | null>(null);
  const [editingUser, setEditingUser] = useState<OrganizationUser | null>(null);
  const [userCreateDefaults, setUserCreateDefaults] = useState<{ email: string; displayName: string }>({ email: '', displayName: '' });
  const [profileSaving, setProfileSaving] = useState(false);
  const [userSaving, setUserSaving] = useState(false);
  const [message, setMessage] = useState<SettingsMessage>(null);
  const [deleteTarget, setDeleteTarget] = useState<DeleteTarget>(null);
  const [categoryDrafts, setCategoryDrafts] = useState<Record<string, { name: string; type: AssetCategoryType; description: string; depreciationMonths: string }>>({});
  const [creatingCategory, setCreatingCategory] = useState(false);
  const [justCreatedCategoryId, setJustCreatedCategoryId] = useState<string | null>(null);
  const [relationTypeDrafts, setRelationTypeDrafts] = useState<Record<string, string>>({});
  const [creatingRelationType, setCreatingRelationType] = useState(false);
  const [justCreatedRelationTypeId, setJustCreatedRelationTypeId] = useState<string | null>(null);
  const [teamDrafts, setTeamDrafts] = useState<Record<string, string>>({});
  const [creatingTeam, setCreatingTeam] = useState(false);
  const [justCreatedTeamId, setJustCreatedTeamId] = useState<string | null>(null);
  const [iconPickerFor, setIconPickerFor] = useState<string | null>(null);
  const [settingsSearch, setSettingsSearch] = useState('');
  const [page, setPage] = useState(1);
  const debouncedSearch = useDebouncedValue(settingsSearch.trim().toLowerCase(), 250);
  const [statusRows, setStatusRows] = useState<AssetStatusSetting[]>([]);
  const [statusDragOver, setStatusDragOver] = useState<number | null>(null);
  const [statusSaving, setStatusSaving] = useState(false);
  const statusDragIndex = useRef<number | null>(null);
  const [qrLabelDraft, setQrLabelDraft] = useState<QrLabelSettings>({ showName: true, showTag: true });
  const [qrLabelSaving, setQrLabelSaving] = useState(false);

  useEffect(() => {
    const inviteEmail = searchParams.get('inviteEmail');
    if (!inviteEmail) return;
    setUserCreateDefaults({ email: inviteEmail, displayName: searchParams.get('inviteName') ?? '' });
    setEditingUser(null);
    setModal('user');
    const next = new URLSearchParams(searchParams);
    next.delete('inviteEmail');
    next.delete('inviteName');
    setSearchParams(next, { replace: true });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  useEffect(() => { if (statuses.data) setStatusRows(statuses.data); }, [statuses.data]);
  useEffect(() => { if (qrLabelSettings.data) setQrLabelDraft(qrLabelSettings.data); }, [qrLabelSettings.data]);
  useEffect(() => {
    if (!categories.data) return;
    setCategoryDrafts(current => {
      const next: typeof current = {};
      for (const category of categories.data!) {
        next[category.id] = current[category.id] ?? { name: category.name, type: category.type, description: category.description ?? '' };
      }
      return next;
    });
  }, [categories.data]);
  useEffect(() => {
    if (!relationTypeSettings.data) return;
    setRelationTypeDrafts(current => {
      const next: typeof current = {};
      for (const item of relationTypeSettings.data!) {
        next[item.id] = current[item.id] ?? item.name;
      }
      return next;
    });
  }, [relationTypeSettings.data]);
  useEffect(() => {
    if (!teamSettings.data) return;
    setTeamDrafts(current => {
      const next: typeof current = {};
      for (const item of teamSettings.data!) {
        next[item.id] = current[item.id] ?? item.name;
      }
      return next;
    });
  }, [teamSettings.data]);

  const filteredCategories = useMemo(() => (categories.data ?? []).filter(item => `${item.name} ${categoryTypeLabels[item.type]} ${item.description ?? ''}`.toLowerCase().includes(debouncedSearch)), [categories.data, debouncedSearch, categoryTypeLabels]);
  const filteredProfiles = useMemo(() => (profiles.data ?? []).filter(item => `${item.name} ${item.description ?? ''}`.toLowerCase().includes(debouncedSearch)), [profiles.data, debouncedSearch]);
  const filteredUsers = useMemo(() => (users.data ?? []).filter(item => `${item.displayName} ${item.email} ${item.roles.join(' ')}`.toLowerCase().includes(debouncedSearch)), [debouncedSearch, users.data]);
  const pagedProfiles = useMemo(() => paginate(filteredProfiles, page, pageSize), [filteredProfiles, page]);
  const pagedUsers = useMemo(() => paginate(filteredUsers, page, pageSize), [filteredUsers, page]);

  const tabButtons: [Tab, string][] = [
    ['account', t('settings.account')],
    ...(canManageOrganization ? [
      ['company', t('settings.company')],
      ['locations', t('settings.locations')],
      ['customFields', t('settings.customFields')],
      ['profiles', t('settings.profiles')],
      ['users', t('settings.users')],
      ['permissions', t('settings.rolePermissions')],
      ['alerts', t('settings.alerts')]
    ] as [Tab, string][] : [])
  ];

  function success(text: string) { setMessage({ type: 'success', text }); }
  function failure(error: unknown, fallback: string) { setMessage({ type: 'error', text: error instanceof Error ? error.message : fallback }); }
  function switchTab(next: Tab) {
    if (tab === 'customFields' && next !== 'customFields' && JSON.stringify(statusRows) !== JSON.stringify(statuses.data ?? [])) {
      if (!window.confirm(t('settings.unsavedStatusChangesConfirm'))) return;
    }
    setTab(next); setPage(1); setSettingsSearch(''); setSearchParams(next === 'account' ? {} : { tab: next }, { replace: true });
  }

  function handleTabKeyDown(event: React.KeyboardEvent<HTMLButtonElement>) {
    const keys = tabButtons.map(([key]) => key);
    const currentIndex = keys.indexOf(tab);
    let next: Tab | null = null;
    if (event.key === 'ArrowRight') next = keys[(currentIndex + 1) % keys.length];
    else if (event.key === 'ArrowLeft') next = keys[(currentIndex - 1 + keys.length) % keys.length];
    else if (event.key === 'Home') next = keys[0];
    else if (event.key === 'End') next = keys[keys.length - 1];
    if (!next) return;
    event.preventDefault();
    switchTab(next);
    window.setTimeout(() => document.getElementById(`settings-tab-${next}`)?.focus(), 0);
  }

  async function updateOrganization(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    try {
      await api.updateOrganization({
        name: String(form.get('name') ?? '').trim(),
        country: String(form.get('country') ?? 'PL').trim(),
        language: organization.data?.language ?? 'pl',
        currency: String(form.get('currency') ?? 'PLN').trim(),
        timeZone: String(form.get('timeZone') ?? 'Europe/Warsaw').trim(),
        logoUrl: toNullable(String(form.get('logoUrl') ?? ''))
      });
      success(t('settings.companySaved'));
      await organization.reload();
    } catch (error) { failure(error, t('settings.companySaveFailed')); }
  }

  function updateCategoryDraft(id: string, patch: Partial<{ name: string; type: AssetCategoryType; description: string; depreciationMonths: string }>) {
    setCategoryDrafts(current => ({ ...current, [id]: { ...current[id], ...patch } }));
  }

  async function addCategory() {
    setCreatingCategory(true);
    try {
      const created = await api.createCategory({ name: t('settings.newCategoryDefaultName'), type: 'Physical', description: null, icon: null });
      setJustCreatedCategoryId(created.id);
      await categories.reload();
    } catch (error) {
      failure(error, t('settings.categorySaveFailed'));
    } finally {
      setCreatingCategory(false);
    }
  }

  async function saveCategoryDraft(id: string) {
    const draft = categoryDrafts[id];
    const original = categories.data?.find(item => item.id === id);
    if (!draft || !original) return;
    if (!draft.name.trim()) return setMessage({ type: 'error', text: t('settings.categoryNameRequired') });
    if (draft.name === original.name && draft.type === original.type && draft.description === (original.description ?? '')) return;
    try {
      await api.updateCategory(id, { name: draft.name.trim(), type: draft.type, description: toNullable(draft.description), icon: original.icon ?? null, depreciationMonths: draft.depreciationMonths.trim() ? Number(draft.depreciationMonths) : null });
      success(t('settings.categorySaved'));
      await categories.reload();
    } catch (error) { failure(error, t('settings.categorySaveFailed')); }
  }

  function updateRelationTypeDraft(id: string, name: string) {
    setRelationTypeDrafts(current => ({ ...current, [id]: name }));
  }

  async function addRelationType() {
    setCreatingRelationType(true);
    try {
      const created = await api.createPersonRelationType({ name: t('settings.newRelationTypeDefaultName') });
      setJustCreatedRelationTypeId(created.id);
      await relationTypeSettings.reload();
    } catch (error) {
      failure(error, t('settings.relationTypeSaveFailed'));
    } finally {
      setCreatingRelationType(false);
    }
  }

  async function saveRelationTypeDraft(id: string) {
    const draft = relationTypeDrafts[id];
    const original = relationTypeSettings.data?.find(item => item.id === id);
    if (draft === undefined || !original) return;
    if (!draft.trim()) return setMessage({ type: 'error', text: t('settings.relationTypeNameRequired') });
    if (draft === original.name) return;
    try {
      await api.updatePersonRelationType(id, { name: draft.trim() });
      success(t('settings.relationTypeSaved'));
      await relationTypeSettings.reload();
    } catch (error) { failure(error, t('settings.relationTypeSaveFailed')); }
  }

  function updateTeamDraft(id: string, name: string) {
    setTeamDrafts(current => ({ ...current, [id]: name }));
  }

  async function addTeamSetting() {
    setCreatingTeam(true);
    try {
      const created = await api.createTeam({ name: t('settings.newTeamDefaultName'), managerId: null, costCenter: null });
      setJustCreatedTeamId(created.id);
      await teamSettings.reload();
    } catch (error) {
      failure(error, t('settings.teamSaveFailed'));
    } finally {
      setCreatingTeam(false);
    }
  }

  async function saveTeamDraft(id: string) {
    const draft = teamDrafts[id];
    const original = teamSettings.data?.find(item => item.id === id);
    if (draft === undefined || !original) return;
    if (!draft.trim()) return setMessage({ type: 'error', text: t('settings.teamNameRequired') });
    if (draft === original.name) return;
    try {
      await api.updateTeam(id, { name: draft.trim(), managerId: original.managerId ?? null, costCenter: original.costCenter ?? null });
      success(t('settings.teamSaved'));
      await teamSettings.reload();
    } catch (error) { failure(error, t('settings.teamSaveFailed')); }
  }

  async function updateCategoryType(id: string, type: AssetCategoryType) {
    updateCategoryDraft(id, { type });
    const original = categories.data?.find(item => item.id === id);
    if (!original) return;
    try {
      await api.updateCategory(id, { name: original.name, type, description: original.description ?? null, icon: original.icon ?? null, depreciationMonths: original.depreciationMonths ?? null });
      success(t('settings.categorySaved'));
      await categories.reload();
    } catch (error) { failure(error, t('settings.categorySaveFailed')); }
  }

  async function updateCategoryIcon(id: string, icon: string) {
    const original = categories.data?.find(item => item.id === id);
    setIconPickerFor(null);
    if (!original) return;
    try {
      await api.updateCategory(id, { name: original.name, type: original.type, description: original.description ?? null, icon });
      success(t('settings.categorySaved'));
      await categories.reload();
    } catch (error) { failure(error, t('settings.categorySaveFailed')); }
  }

  function updateStatusRow(index: number, patch: Partial<AssetStatusSetting>) {
    setStatusRows(current => current.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  }

  function moveStatusRow(from: number, to: number) {
    setStatusRows(current => {
      if (from < 0 || to < 0 || from >= current.length || to >= current.length || from === to) return current;
      const next = [...current];
      const [moved] = next.splice(from, 1);
      next.splice(to, 0, moved);
      return next;
    });
  }

  async function saveStatuses() {
    const body = statusRows.map((item, index) => ({ ...item, sortOrder: (index + 1) * 10 }));
    setStatusSaving(true);
    try {
      const saved = await api.saveAssetStatuses(body);
      setStatusRows(saved);
      success(t('settings.statusesSaved'));
    } catch (error) {
      failure(error, t('settings.statusesSaveFailed'));
    } finally {
      setStatusSaving(false);
    }
  }

  async function saveQrLabelSettings() {
    setQrLabelSaving(true);
    try {
      const saved = await api.saveQrLabelSettings(qrLabelDraft);
      setQrLabelDraft(saved);
      success(t('settings.qrLabelSaved'));
    } catch (error) {
      failure(error, t('settings.qrLabelSaveFailed'));
    } finally {
      setQrLabelSaving(false);
    }
  }

  async function saveProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const body = { name: String(form.get('name') ?? '').trim(), description: toNullable(String(form.get('description') ?? '')), defaultManagerId: toNullable(String(form.get('defaultManagerId') ?? '')), assetCategoryIds: form.getAll('assetCategoryIds').map(String), procedureIds: form.getAll('procedureIds').map(String) };
    if (!body.name) return setMessage({ type: 'error', text: t('settings.profileNameRequired') });
    setProfileSaving(true);
    try {
      if (editingProfile) { await api.updateJobProfile(editingProfile.id, body); } else { await api.createJobProfile(body); }
      success(t('settings.profileSaved'));
      setModal(null);
      setEditingProfile(null);
      await profiles.reload();
    } catch (error) {
      failure(error, t('settings.profileSaveFailed'));
    } finally {
      setProfileSaving(false);
    }
  }

  async function saveUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const body = { email: String(form.get('email') ?? '').trim(), displayName: String(form.get('displayName') ?? '').trim(), isActive: form.get('isActive') === 'on', roles: form.getAll('roles').map(String), personId: toNullable(String(form.get('personId') ?? '')) };
    if (!body.email) return setMessage({ type: 'error', text: t('settings.emailRequired') });
    if (!body.roles.length) return setMessage({ type: 'error', text: t('settings.roleRequired') });
    if (editingUser && editingUser.email.toLowerCase() === auth.userEmail.toLowerCase() && !body.isActive) {
      return setMessage({ type: 'error', text: t('settings.cannotDeactivateSelf') });
    }
    setUserSaving(true);
    try {
      if (editingUser) { await api.updateUser(editingUser.id, body); } else { await api.createUser(body); }
      success(editingUser ? t('settings.userSaved') : t('settings.userInvited'));
      setModal(null);
      setEditingUser(null);
      await users.reload();
    } catch (error) {
      failure(error, t('settings.userSaveFailed'));
    } finally {
      setUserSaving(false);
    }
  }

  async function togglePermission(roleKey: string, permissionKey: string, allowed: boolean) {
    setPermissionSaving(permissionKey);
    try {
      await api.setRolePermission({ roleKey, permissionKey, allowed });
      success(t('settings.rolePermissionsSaved'));
      await rolePermissions.reload();
    } catch (error) {
      failure(error, t('settings.rolePermissionsSaveFailed'));
    } finally {
      setPermissionSaving(null);
    }
  }

  async function confirmDelete() {
    if (!deleteTarget) return;
    try {
      if (deleteTarget.kind === 'category') {
        await api.deleteCategory(deleteTarget.item.id);
        success(t('settings.categoryDeleted'));
        await categories.reload();
      } else if (deleteTarget.kind === 'relationType') {
        await api.deletePersonRelationType(deleteTarget.item.id);
        success(t('settings.relationTypeDeleted'));
        await relationTypeSettings.reload();
      } else if (deleteTarget.kind === 'team') {
        await api.deleteTeam(deleteTarget.item.id);
        success(t('settings.teamDeleted'));
        await teamSettings.reload();
      } else {
        await api.deleteJobProfile(deleteTarget.item.id);
        success(t('settings.profileDeleted'));
        await profiles.reload();
      }
    } catch (error) {
      failure(error, t('settings.deleteFailed'));
    } finally {
      setDeleteTarget(null);
    }
  }

  if (organization.isLoading && !organization.data) return <LoadingState title={t('settings.loadingTitle')} description={t('settings.loadingDesc')} />;
  if (organization.error || !organization.data) return <ErrorState message={organization.error ?? t('settings.noOrganization')} onRetry={organization.reload} />;

  return (
    <div className="pageStack">
      <PageHeader eyebrow={t('page.settings.eyebrow')} title={t('page.settings.title')} />
      {message ? <p className={`formMessage formMessage--${message.type}`} aria-live="polite">{message.text}</p> : null}
      <div className="tabs" role="tablist" aria-label={t('settings.sectionsAria')}>{tabButtons.map(([key, label]) => <button key={key} id={`settings-tab-${key}`} type="button" role="tab" aria-selected={tab === key} aria-controls={`settings-tabpanel-${key}`} tabIndex={tab === key ? 0 : -1} className={tab === key ? 'tab tab--active' : 'tab'} onClick={() => switchTab(key)} onKeyDown={handleTabKeyDown}>{label}</button>)}</div>

      {tab === 'account' ? <div role="tabpanel" id="settings-tabpanel-account" aria-labelledby="settings-tab-account">
        <Card>
          <EmailVerificationNotice />
          <div className="sectionTitle"><div><h2>{t('settings.interfaceLanguage')}</h2></div></div>
          <LanguageSwitcher />
        </Card>
        <ProfileCard />
        <TwoFactorCard />
        <AccountLinksCard />
      </div> : null}

      {tab === 'company' && canManageOrganization ? <div role="tabpanel" id="settings-tabpanel-company" aria-labelledby="settings-tab-company"><Card>
        <div className="sectionTitle"><div><h2>{t('settings.company')}</h2></div></div>
        <form className="formGrid" onSubmit={updateOrganization}>
          <Field label={t('settings.nameLabel')}><TextInput name="name" defaultValue={organization.data.name} required /></Field>
          <Field label={t('settings.countryLabel')}><TextInput name="country" defaultValue={organization.data.country} /></Field>
          <Field label={t('settings.currencyLabel')}><TextInput name="currency" defaultValue={organization.data.currency} /></Field>
          <Field label={t('settings.timeZoneLabel')}><TextInput name="timeZone" defaultValue={organization.data.timeZone} /></Field>
          <Field label={t('settings.logoUrlLabel')}><TextInput name="logoUrl" defaultValue={organization.data.logoUrl ?? ''} /></Field>
          <div className="formActions formActions--split"><span className="muted">{t('settings.futureProtocolsHint')}</span><Button icon={<Save size={16} />}>{t('settings.save')}</Button></div>
        </form>
      </Card></div> : null}

      {tab === 'locations' && canManageOrganization ? <div role="tabpanel" id="settings-tabpanel-locations" aria-labelledby="settings-tab-locations"><LocationsManager /></div> : null}

      {tab === 'customFields' && canManageOrganization ? (
        <div role="tabpanel" id="settings-tabpanel-customFields" aria-labelledby="settings-tab-customFields">
          <Card>
            <div className="sectionTitle"><div><h2>{t('settings.assetCategories')}</h2><p>{t('settings.customFieldsCategoriesHint')}</p></div></div>
            <SettingsSearch value={settingsSearch} onChange={value => { setSettingsSearch(value); setPage(1); }} total={filteredCategories.length} />
            {categories.isLoading ? <p className="muted">{t('settings.loadingCategories')}</p> : categories.error ? <ErrorState message={categories.error} onRetry={categories.reload} /> : !filteredCategories.length ? <EmptyState title={t('settings.emptyCategoriesTitle')} description={t('settings.emptyCategoriesDesc')} /> : (
              <div className="statusList">
                {filteredCategories.map(category => {
                  const draft = categoryDrafts[category.id] ?? { name: category.name, type: category.type, description: category.description ?? '', depreciationMonths: category.depreciationMonths?.toString() ?? '' };
                  return (
                    <div className="statusTile categoryTile" key={category.id}>
                      <button type="button" className="iconButton" aria-label={t('settings.iconLabel')} title={t('settings.iconLabel')} onClick={() => setIconPickerFor(category.id)}>
                        <CategoryIcon icon={category.icon} />
                      </button>
                      <input
                        className="categoryTile__name"
                        aria-label={t('settings.nameLabel')}
                        value={draft.name}
                        autoFocus={justCreatedCategoryId === category.id}
                        onChange={event => updateCategoryDraft(category.id, { name: event.target.value })}
                        onBlur={() => { saveCategoryDraft(category.id); setJustCreatedCategoryId(current => current === category.id ? null : current); }}
                      />
                      <select aria-label={t('settings.typeLabel')} value={draft.type} onChange={event => updateCategoryType(category.id, event.target.value as AssetCategoryType)}>
                        {categoryTypeValues.map(type => <option key={type} value={type}>{categoryTypeLabels[type]}</option>)}
                      </select>
                      <input
                        className="categoryTile__description"
                        aria-label={t('settings.descriptionLabel')}
                        placeholder={t('settings.descriptionLabel')}
                        value={draft.description}
                        onChange={event => updateCategoryDraft(category.id, { description: event.target.value })}
                        onBlur={() => saveCategoryDraft(category.id)}
                      />
                      <input
                        className="categoryTile__depreciation"
                        type="number"
                        min={1}
                        max={1200}
                        aria-label={t('settings.depreciationLabel')}
                        title={t('settings.depreciationHint')}
                        placeholder={t('settings.depreciationPlaceholder')}
                        value={draft.depreciationMonths}
                        onChange={event => updateCategoryDraft(category.id, { depreciationMonths: event.target.value })}
                        onBlur={() => saveCategoryDraft(category.id)}
                      />
                      <button type="button" className="iconButton" aria-label={t('settings.deleteCategoryAria', { name: category.name })} onClick={() => setDeleteTarget({ kind: 'category', item: category })}><Trash2 size={16} /></button>
                    </div>
                  );
                })}
              </div>
            )}
            <div className="formActions"><Button type="button" variant="secondary" disabled={creatingCategory} onClick={addCategory} icon={<Plus size={16} />}>{creatingCategory ? t('common.saving') : t('settings.addCategory')}</Button></div>
          </Card>

          <Card>
            <div className="sectionTitle"><div><h2>{t('settings.assetStatuses')}</h2><p>{t('settings.customFieldsStatusesHint')}</p></div></div>
            {statuses.isLoading ? <p className="muted">{t('settings.loadingStatuses')}</p> : statuses.error ? <ErrorState message={statuses.error} onRetry={statuses.reload} /> : (
              <>
                <div className="statusList">
                  {statusRows.map((item, index) => (
                    <div
                      key={item.statusKey}
                      className={`statusTile${statusDragOver === index ? ' statusTile--over' : ''}${item.isEnabled ? '' : ' statusTile--disabled'}`}
                      title={item.statusKey}
                      draggable
                      onDragStart={() => { statusDragIndex.current = index; }}
                      onDragEnter={() => setStatusDragOver(index)}
                      onDragOver={event => event.preventDefault()}
                      onDragEnd={() => setStatusDragOver(null)}
                      onDrop={event => { event.preventDefault(); moveStatusRow(statusDragIndex.current ?? index, index); setStatusDragOver(null); }}
                    >
                      <span className="statusTile__handle" draggable={false} aria-hidden="true"><GripVertical size={16} /></span>

                      <span className="statusTile__label" style={{ background: item.backgroundColor, color: item.color }} data-value={item.label || ' '}>
                        <input
                          aria-label={t('settings.colLabel')}
                          value={item.label}
                          onChange={event => updateStatusRow(index, { label: event.target.value })}
                        />
                      </span>

                      <label className="statusTile__dot" style={{ background: item.color }} title={t('settings.colTextColor')}>
                        <input type="color" aria-label={t('settings.colTextColor')} value={item.color} onChange={event => updateStatusRow(index, { color: event.target.value })} />
                      </label>
                      <label className="statusTile__dot" style={{ background: item.backgroundColor }} title={t('settings.colBackgroundColor')}>
                        <input type="color" aria-label={t('settings.colBackgroundColor')} value={item.backgroundColor} onChange={event => updateStatusRow(index, { backgroundColor: event.target.value })} />
                      </label>

                      {item.isEnabled ? (
                        <button type="button" className="iconButton" aria-label={t('settings.removeStatus')} title={t('settings.removeStatusHint')} onClick={() => updateStatusRow(index, { isEnabled: false })}><Trash2 size={16} /></button>
                      ) : (
                        <Button type="button" variant="secondary" title={t('settings.statusDisabledHint')} onClick={() => updateStatusRow(index, { isEnabled: true })}>{t('settings.restoreStatus')}</Button>
                      )}
                    </div>
                  ))}
                </div>
                <div className="formActions"><Button disabled={statusSaving} onClick={saveStatuses} icon={<Save size={16} />}>{statusSaving ? t('common.saving') : t('settings.saveStatuses')}</Button></div>
              </>
            )}
          </Card>

          <Card>
            <div className="sectionTitle"><div><h2>{t('settings.qrLabel')}</h2><p>{t('settings.qrLabelHint')}</p></div></div>
            {qrLabelSettings.isLoading ? <p className="muted">{t('common.loading')}</p> : qrLabelSettings.error ? <ErrorState message={qrLabelSettings.error} onRetry={qrLabelSettings.reload} /> : (
              <>
                <div className="formGrid">
                  <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <input type="checkbox" checked={qrLabelDraft.showName} onChange={event => setQrLabelDraft(current => ({ ...current, showName: event.target.checked }))} />
                    {t('settings.qrLabelShowName')}
                  </label>
                  <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <input type="checkbox" checked={qrLabelDraft.showTag} onChange={event => setQrLabelDraft(current => ({ ...current, showTag: event.target.checked }))} />
                    {t('settings.qrLabelShowTag')}
                  </label>
                </div>
                <div className="formActions"><Button disabled={qrLabelSaving} onClick={saveQrLabelSettings} icon={<Save size={16} />}>{qrLabelSaving ? t('common.saving') : t('settings.save')}</Button></div>
              </>
            )}
          </Card>

          <Card>
            <div className="sectionTitle"><div><h2>{t('settings.relationTypes')}</h2><p>{t('settings.relationTypesHint')}</p></div></div>
            {relationTypeSettings.isLoading ? <p className="muted">{t('settings.loadingRelationTypes')}</p> : relationTypeSettings.error ? <ErrorState message={relationTypeSettings.error} onRetry={relationTypeSettings.reload} /> : !relationTypeSettings.data?.length ? <EmptyState title={t('settings.emptyRelationTypesTitle')} description={t('settings.emptyRelationTypesDesc')} /> : (
              <div className="statusList">
                {relationTypeSettings.data.map(item => (
                  <div className="statusTile categoryTile" key={item.id}>
                    <input
                      className="categoryTile__name"
                      aria-label={t('people.relationTypeNameLabel')}
                      value={relationTypeDrafts[item.id] ?? item.name}
                      autoFocus={justCreatedRelationTypeId === item.id}
                      onChange={event => updateRelationTypeDraft(item.id, event.target.value)}
                      onBlur={() => { saveRelationTypeDraft(item.id); setJustCreatedRelationTypeId(current => current === item.id ? null : current); }}
                    />
                    <button type="button" className="iconButton" aria-label={t('settings.deleteRelationTypeAria', { name: item.name })} onClick={() => setDeleteTarget({ kind: 'relationType', item })}><Trash2 size={16} /></button>
                  </div>
                ))}
              </div>
            )}
            <div className="formActions"><Button type="button" variant="secondary" disabled={creatingRelationType} onClick={addRelationType} icon={<Plus size={16} />}>{creatingRelationType ? t('common.saving') : t('settings.addRelationType')}</Button></div>
          </Card>

          <Card>
            <div className="sectionTitle"><div><h2>{t('settings.teams')}</h2><p>{t('settings.teamsHint')}</p></div></div>
            {teamSettings.isLoading ? <p className="muted">{t('settings.loadingTeams')}</p> : teamSettings.error ? <ErrorState message={teamSettings.error} onRetry={teamSettings.reload} /> : !teamSettings.data?.length ? <EmptyState title={t('settings.emptyTeamsTitle')} description={t('settings.emptyTeamsDesc')} /> : (
              <div className="statusList">
                {teamSettings.data.map(item => (
                  <div className="statusTile categoryTile" key={item.id}>
                    <input
                      className="categoryTile__name"
                      aria-label={t('people.teamNameLabel')}
                      value={teamDrafts[item.id] ?? item.name}
                      autoFocus={justCreatedTeamId === item.id}
                      onChange={event => updateTeamDraft(item.id, event.target.value)}
                      onBlur={() => { saveTeamDraft(item.id); setJustCreatedTeamId(current => current === item.id ? null : current); }}
                    />
                    <button type="button" className="iconButton" aria-label={t('settings.deleteTeamAria', { name: item.name })} onClick={() => setDeleteTarget({ kind: 'team', item })}><Trash2 size={16} /></button>
                  </div>
                ))}
              </div>
            )}
            <div className="formActions"><Button type="button" variant="secondary" disabled={creatingTeam} onClick={addTeamSetting} icon={<Plus size={16} />}>{creatingTeam ? t('common.saving') : t('settings.addTeam')}</Button></div>
          </Card>
        </div>
      ) : null}

      {tab === 'profiles' && canManageOrganization ? <div role="tabpanel" id="settings-tabpanel-profiles" aria-labelledby="settings-tab-profiles"><Card>
        <div className="sectionTitle"><div><h2>{t('settings.jobProfiles')}</h2></div><Button icon={<Plus size={16} />} onClick={() => { setEditingProfile(null); setModal('profile'); }}>{t('settings.addProfile')}</Button></div>
        <SettingsSearch value={settingsSearch} onChange={value => { setSettingsSearch(value); setPage(1); }} total={filteredProfiles.length} />
        {profiles.isLoading ? <p className="muted">{t('settings.loadingProfiles')}</p> : profiles.error ? <ErrorState message={profiles.error} onRetry={profiles.reload} /> : !filteredProfiles.length ? <EmptyState title={t('settings.emptyProfilesTitle')} description={t('settings.emptyProfilesDesc')} /> : (
          <>
            <div className="tableWrap"><table><thead><tr><th>{t('settings.nameLabel')}</th><th>{t('settings.colEquipmentCategories')}</th><th>{t('settings.colProcedures')}</th><th></th></tr></thead><tbody>
              {pagedProfiles.items.map(profile => <tr key={profile.id}><td><strong>{profile.name}</strong><small>{profile.description}</small></td><td>{profile.assetCategoryIds.length}</td><td>{profile.procedureIds.length}</td><td className="rowActions"><Button variant="ghost" onClick={() => { setEditingProfile(profile); setModal('profile'); }}>{t('common.edit')}</Button><button className="iconButton" aria-label={t('settings.deleteProfileAria', { name: profile.name })} onClick={() => setDeleteTarget({ kind: 'profile', item: profile })}><Trash2 size={16} /></button></td></tr>)}
            </tbody></table></div>
            <Pagination page={pagedProfiles.page} total={pagedProfiles.total} pageSize={pageSize} onPageChange={setPage} />
          </>
        )}
      </Card></div> : null}

      {tab === 'users' && canManageOrganization ? <div role="tabpanel" id="settings-tabpanel-users" aria-labelledby="settings-tab-users"><Card>
        <div className="sectionTitle"><div><h2>{t('settings.logins')}</h2></div><Button icon={<Plus size={16} />} onClick={() => { setEditingUser(null); setUserCreateDefaults({ email: '', displayName: '' }); setModal('user'); }}>{t('settings.addLogin')}</Button></div>
        <SettingsSearch value={settingsSearch} onChange={value => { setSettingsSearch(value); setPage(1); }} total={filteredUsers.length} />
        {users.isLoading ? <p className="muted">{t('settings.loadingLogins')}</p> : users.error ? <ErrorState message={users.error} onRetry={users.reload} /> : !filteredUsers.length ? <EmptyState title={t('settings.emptyLoginsTitle')} description={t('settings.emptyLoginsDesc')} /> : (
          <>
            <div className="tableWrap"><table><thead><tr><th>{t('settings.colUser')}</th><th>{t('settings.colRoles')}</th><th>{t('assets.statusLabel')}</th><th></th></tr></thead><tbody>
              {pagedUsers.items.map(user => <tr key={user.id}><td><strong>{user.displayName || user.email}</strong><small>{user.email}</small></td><td>{user.roles.join(', ') || '-'}</td><td>{user.isActive ? t('settings.active') : t('settings.inactive')}</td><td><Button variant="ghost" onClick={() => { setEditingUser(user); setModal('user'); }}>{t('common.edit')}</Button></td></tr>)}
            </tbody></table></div>
            <Pagination page={pagedUsers.page} total={pagedUsers.total} pageSize={pageSize} onPageChange={setPage} />
          </>
        )}
      </Card></div> : null}

      {tab === 'alerts' && canManageOrganization ? <div role="tabpanel" id="settings-tabpanel-alerts" aria-labelledby="settings-tab-alerts"><AlertsSettings /></div> : null}

      {tab === 'permissions' && canManageOrganization ? <div role="tabpanel" id="settings-tabpanel-permissions" aria-labelledby="settings-tab-permissions"><Card>
        <div className="sectionTitle"><div><h2>{t('settings.rolePermissions')}</h2></div></div>
        {roles.isLoading || rolePermissions.isLoading ? <p className="muted">{t('settings.rolePermissionsLoading')}</p> : rolePermissions.error ? <ErrorState message={rolePermissions.error} onRetry={rolePermissions.reload} /> : (
          <div className="roleSplit">
            <nav className="roleRail" aria-label={t('settings.rolePermissions')}>
              {roles.data?.map(role => (
                <button
                  key={role.key}
                  type="button"
                  className={role.key === selectedRoleKey ? 'roleRail__item--active' : ''}
                  onClick={() => setSelectedRoleKey(role.key)}
                  title={role.description}
                >
                  {role.label}
                </button>
              ))}
            </nav>
            <div className="permissionPanel">
              <h3 className="permissionPanel__heading">{roles.data?.find(r => r.key === selectedRoleKey)?.label}</h3>
              <p className="permissionPanel__hint">{roles.data?.find(r => r.key === selectedRoleKey)?.description ?? t('settings.rolePermissionsHint')}</p>
              {(rolePermissions.data ?? []).filter(p => p.roleKey === selectedRoleKey).map(permission => (
                <div className="permissionRow" key={permission.permissionKey}>
                  <div>
                    <div className="permissionRow__label">{permission.permissionLabel}</div>
                    <div className="permissionRow__desc">{permission.permissionDescription}</div>
                  </div>
                  <label className="toggleSwitch">
                    <input
                      type="checkbox"
                      checked={permission.allowed}
                      disabled={permissionSaving === permission.permissionKey}
                      onChange={event => togglePermission(permission.roleKey, permission.permissionKey, event.target.checked)}
                      aria-label={permission.permissionLabel}
                    />
                    <span className="toggleSwitch__track" />
                    <span className="toggleSwitch__thumb" />
                  </label>
                </div>
              ))}
            </div>
          </div>
        )}
      </Card></div> : null}

      <Modal open={!!iconPickerFor} title={t('settings.iconLabel')} onClose={() => setIconPickerFor(null)}>
        <IconPicker
          value={categories.data?.find(item => item.id === iconPickerFor)?.icon ?? ''}
          onChange={icon => { if (iconPickerFor) updateCategoryIcon(iconPickerFor, icon); }}
        />
      </Modal>

      <Modal open={modal === 'profile'} title={editingProfile ? t('settings.editProfile') : t('settings.addProfileTitle')} onClose={() => setModal(null)} width="wide">
        <form className="formGrid" onSubmit={saveProfile}>
          <Field label={t('settings.nameLabel')}><TextInput name="name" defaultValue={editingProfile?.name ?? ''} required /></Field>
          <Field label={t('settings.descriptionLabel')}><TextArea name="description" defaultValue={editingProfile?.description ?? ''} /></Field>
          <Field label={t('settings.defaultManagerLabel')}><SelectInput name="defaultManagerId" defaultValue={editingProfile?.defaultManagerId ?? ''}><option value="">{t('settings.noneOption')}</option>{people.data?.map(p => <option key={p.id} value={p.id}>{p.fullName}</option>)}</SelectInput></Field>
          <fieldset className="checkboxGroup"><legend>{t('settings.colEquipmentCategories')}</legend>{categories.data?.map(c => <label key={c.id}><input name="assetCategoryIds" value={c.id} type="checkbox" defaultChecked={editingProfile?.assetCategoryIds.includes(c.id)} /> {c.name} <small>{categoryTypeLabels[c.type]}</small></label>)}</fieldset>
          <fieldset className="checkboxGroup"><legend>{t('settings.colProcedures')}</legend>{procedures.data?.map(p => <label key={p.id}><input name="procedureIds" value={p.id} type="checkbox" defaultChecked={editingProfile?.procedureIds.includes(p.id)} /> {p.title}</label>)}</fieldset>
          <div className="formActions formActions--split"><Button type="button" variant="ghost" onClick={() => setModal(null)}>{t('common.cancel')}</Button><Button disabled={profileSaving}>{profileSaving ? t('common.saving') : t('settings.saveProfile')}</Button></div>
        </form>
      </Modal>
      <Modal open={modal === 'user'} title={editingUser ? t('settings.editLogin') : t('settings.addLoginTitle')} onClose={() => setModal(null)}>
        <form className="formGrid" onSubmit={saveUser}>
          <Field label={t('settings.emailLabel')}><TextInput name="email" type="email" defaultValue={editingUser?.email ?? userCreateDefaults.email} required /></Field>
          <Field label={t('settings.displayNameLabel')}><TextInput name="displayName" defaultValue={editingUser?.displayName ?? userCreateDefaults.displayName} /></Field>
          <Field label={t('settings.linkedPersonLabel')}>
            <SelectInput name="personId" defaultValue={editingUser?.personId ?? ''}>
              <option value="">{t('settings.linkedPersonAutoOption')}</option>
              {people.data?.map(person => <option key={person.id} value={person.id}>{person.fullName} · {person.email}</option>)}
            </SelectInput>
          </Field>
          <label className="checkField"><input name="isActive" type="checkbox" defaultChecked={editingUser?.isActive ?? true} /> {t('settings.accountActive')}</label>
          <fieldset className="checkboxGroup"><legend>{t('settings.rolesLegend')}</legend>{roles.data?.map(role => <label key={role.key} title={role.description}><input name="roles" value={role.key} type="checkbox" defaultChecked={editingUser?.roles.includes(role.key)} /> {role.label}</label>)}</fieldset>
          <div className="formActions formActions--split"><Button type="button" variant="ghost" onClick={() => setModal(null)}>{t('common.cancel')}</Button><Button disabled={userSaving}>{userSaving ? t('common.saving') : t('settings.saveLogin')}</Button></div>
        </form>
      </Modal>

      <ConfirmDialog
        open={!!deleteTarget}
        title={deleteTarget?.kind === 'category' ? t('settings.deleteCategoryTitle') : deleteTarget?.kind === 'relationType' ? t('settings.deleteRelationTypeTitle') : deleteTarget?.kind === 'team' ? t('settings.deleteTeamTitle') : t('settings.deleteProfileTitle')}
        description={deleteTarget?.kind === 'category' ? t('settings.deleteCategoryDesc', { name: deleteTarget.item.name }) : deleteTarget?.kind === 'relationType' ? t('settings.deleteRelationTypeDesc', { name: deleteTarget.item.name }) : deleteTarget?.kind === 'team' ? t('settings.deleteTeamDesc', { name: deleteTarget.item.name }) : t('settings.deleteProfileDesc', { name: deleteTarget?.item.name ?? '' })}
        confirmLabel={t('common.delete')}
        onConfirm={confirmDelete}
        onClose={() => setDeleteTarget(null)}
      />
    </div>
  );
}

function SettingsSearch({ value, onChange, total }: { value: string; onChange: (value: string) => void; total: number }) {
  const { t, tPlural } = useI18n();
  return (
    <div className="filters filters--single settingsSearch">
      <Field label={t('settings.searchInSection')}><TextInput value={value} onChange={event => onChange(event.target.value)} placeholder={t('settings.searchPlaceholder')} /></Field>
      <span className="toolbarHint"><Search size={16} /> {total} {tPlural('count.results', total)}</span>
    </div>
  );
}
