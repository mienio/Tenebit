import { FormEvent, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ListPlus, Plus, Save, Search, Trash2 } from 'lucide-react';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { Modal } from '../components/Modal';
import { Field, SelectInput, TextArea, TextInput } from '../components/FormFields';
import { IconPicker } from '../components/IconPicker';
import { LocationsManager } from '../components/LocationsManager';
import { PageHeader } from '../components/PageHeader';
import { Pagination, paginate } from '../components/Pagination';
import { EmptyState, ErrorState, LoadingState } from '../components/StateViews';
import { useAsyncData } from '../hooks/useAsyncData';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import type { AssetCategory, AssetCategoryType, AssetFieldType, AssetStatusSetting, JobProfile, OrganizationUser } from '../types/domain';
import { categoryTypeValues } from '../utils/labels';
import { toNullable } from '../utils/format';
import { CategoryIcon } from '../utils/categoryIcons';
import { useI18n } from '../i18n/I18nProvider';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';

const fieldTypeValues: AssetFieldType[] = ['Text', 'Number', 'Date', 'Boolean', 'Select', 'Sensitive'];

function slugifyKey(label: string): string {
  return label
    .normalize('NFD')
    .replace(new RegExp('[\\u0300-\\u036f]', 'g'), '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '');
}

interface FieldRow {
  key: string;
  label: string;
  fieldType: AssetFieldType;
  options: string;
  required: boolean;
}

type Tab = 'company' | 'locations' | 'customFields' | 'profiles' | 'users';
type SettingsMessage = { type: 'success' | 'error'; text: string } | null;
type DeleteTarget = { kind: 'category'; item: AssetCategory } | { kind: 'profile'; item: JobProfile } | null;
const pageSize = 20;
const validTabs: Tab[] = ['company', 'locations', 'customFields', 'profiles', 'users'];

export function SettingsPage() {
  const { t } = useI18n();
  const categoryTypeLabels: Record<AssetCategoryType, string> = Object.fromEntries(categoryTypeValues.map(value => [value, t(`categoryType.${value}`)])) as Record<AssetCategoryType, string>;
  const organization = useAsyncData(api.organization, []);
  const categories = useAsyncData(api.categories, []);
  const statuses = useAsyncData(api.assetStatuses, []);
  const procedures = useAsyncData(() => api.procedures(), []);
  const people = useAsyncData(() => api.people(), []);
  const profiles = useAsyncData(api.jobProfiles, []);
  const users = useAsyncData(api.users, []);
  const roles = useAsyncData(api.roles, []);
  const [searchParams, setSearchParams] = useSearchParams();
  const initialTab = searchParams.get('tab') as Tab | null;
  const [tab, setTab] = useState<Tab>(initialTab && validTabs.includes(initialTab) ? initialTab : 'company');
  const [modal, setModal] = useState<'category' | 'profile' | 'user' | null>(null);
  const [editingCategory, setEditingCategory] = useState<AssetCategory | null>(null);
  const [categoryIcon, setCategoryIcon] = useState('');
  const [editingProfile, setEditingProfile] = useState<JobProfile | null>(null);
  const [editingUser, setEditingUser] = useState<OrganizationUser | null>(null);
  const [message, setMessage] = useState<SettingsMessage>(null);
  const [deleteTarget, setDeleteTarget] = useState<DeleteTarget>(null);
  const [fieldsTarget, setFieldsTarget] = useState<AssetCategory | null>(null);
  const [fieldRows, setFieldRows] = useState<FieldRow[]>([]);
  const [fieldsSaving, setFieldsSaving] = useState(false);
  const [settingsSearch, setSettingsSearch] = useState('');
  const [page, setPage] = useState(1);
  const debouncedSearch = useDebouncedValue(settingsSearch.trim().toLowerCase(), 250);

  const filteredCategories = useMemo(() => (categories.data ?? []).filter(item => `${item.name} ${categoryTypeLabels[item.type]} ${item.description ?? ''}`.toLowerCase().includes(debouncedSearch)), [categories.data, debouncedSearch, categoryTypeLabels]);
  const filteredProfiles = useMemo(() => (profiles.data ?? []).filter(item => `${item.name} ${item.description ?? ''}`.toLowerCase().includes(debouncedSearch)), [profiles.data, debouncedSearch]);
  const filteredUsers = useMemo(() => (users.data ?? []).filter(item => `${item.displayName} ${item.email} ${item.roles.join(' ')}`.toLowerCase().includes(debouncedSearch)), [debouncedSearch, users.data]);
  const pagedCategories = useMemo(() => paginate(filteredCategories, page, pageSize), [filteredCategories, page]);
  const pagedProfiles = useMemo(() => paginate(filteredProfiles, page, pageSize), [filteredProfiles, page]);
  const pagedUsers = useMemo(() => paginate(filteredUsers, page, pageSize), [filteredUsers, page]);

  const tabButtons: [Tab, string][] = [
    ['company', t('settings.company')],
    ['locations', t('settings.locations')],
    ['customFields', t('settings.customFields')],
    ['profiles', t('settings.profiles')],
    ['users', t('settings.users')]
  ];

  function success(text: string) { setMessage({ type: 'success', text }); }
  function failure(error: unknown, fallback: string) { setMessage({ type: 'error', text: error instanceof Error ? error.message : fallback }); }
  function switchTab(next: Tab) { setTab(next); setPage(1); setSettingsSearch(''); setSearchParams(next === 'company' ? {} : { tab: next }, { replace: true }); }

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

  async function saveCategory(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const body = { name: String(form.get('name') ?? '').trim(), type: String(form.get('type') ?? 'Physical') as AssetCategoryType, description: toNullable(String(form.get('description') ?? '')), icon: toNullable(String(form.get('icon') ?? '')) };
    if (!body.name) return setMessage({ type: 'error', text: t('settings.categoryNameRequired') });
    try {
      editingCategory ? await api.updateCategory(editingCategory.id, body) : await api.createCategory(body);
      success(t('settings.categorySaved'));
      setModal(null);
      setEditingCategory(null);
      await categories.reload();
    } catch (error) { failure(error, t('settings.categorySaveFailed')); }
  }

  function openFields(category: AssetCategory) {
    setFieldsTarget(category);
    setFieldRows(category.fieldDefinitions.map(f => ({ key: f.key, label: f.label, fieldType: f.fieldType, options: f.options.join(';'), required: f.required })));
  }

  function addFieldRow() {
    setFieldRows(current => [...current, { key: '', label: '', fieldType: 'Text', options: '', required: false }]);
  }

  function updateFieldRow(index: number, patch: Partial<FieldRow>) {
    setFieldRows(current => current.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  }

  function removeFieldRow(index: number) {
    setFieldRows(current => current.filter((_, i) => i !== index));
  }

  async function saveFields() {
    if (!fieldsTarget) return;
    const body = fieldRows.map(row => ({
      key: row.key.trim() || slugifyKey(row.label),
      label: row.label.trim(),
      fieldType: row.fieldType,
      options: row.fieldType === 'Select' ? row.options.trim() : null,
      required: row.required
    }));
    if (body.some(row => !row.label || !row.key)) return setMessage({ type: 'error', text: t('settings.fieldLabelRequired') });
    setFieldsSaving(true);
    try {
      await api.saveCategoryFields(fieldsTarget.id, body);
      success(t('settings.fieldsSaved'));
      setFieldsTarget(null);
      await categories.reload();
    } catch (error) {
      failure(error, t('settings.fieldsSaveFailed'));
    } finally {
      setFieldsSaving(false);
    }
  }

  async function saveStatuses(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const body = (statuses.data ?? []).map((item, index) => ({ statusKey: item.statusKey, label: String(form.get(`label-${item.statusKey}`) ?? item.label).trim(), sortOrder: Number(form.get(`sort-${item.statusKey}`) ?? index * 10), isEnabled: form.get(`enabled-${item.statusKey}`) === 'on' } as AssetStatusSetting));
    try { await api.saveAssetStatuses(body); success(t('settings.statusesSaved')); await statuses.reload(); }
    catch (error) { failure(error, t('settings.statusesSaveFailed')); }
  }

  async function saveProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const body = { name: String(form.get('name') ?? '').trim(), description: toNullable(String(form.get('description') ?? '')), defaultManagerId: toNullable(String(form.get('defaultManagerId') ?? '')), assetCategoryIds: form.getAll('assetCategoryIds').map(String), procedureIds: form.getAll('procedureIds').map(String) };
    if (!body.name) return setMessage({ type: 'error', text: t('settings.profileNameRequired') });
    try {
      editingProfile ? await api.updateJobProfile(editingProfile.id, body) : await api.createJobProfile(body);
      success(t('settings.profileSaved'));
      setModal(null);
      setEditingProfile(null);
      await profiles.reload();
    } catch (error) { failure(error, t('settings.profileSaveFailed')); }
  }

  async function saveUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const body = { email: String(form.get('email') ?? '').trim(), displayName: String(form.get('displayName') ?? '').trim(), isActive: form.get('isActive') === 'on', roles: form.getAll('roles').map(String) };
    if (!body.email) return setMessage({ type: 'error', text: t('settings.emailRequired') });
    try {
      editingUser ? await api.updateUser(editingUser.id, body) : await api.createUser(body);
      success(t('settings.userSaved'));
      setModal(null);
      setEditingUser(null);
      await users.reload();
    } catch (error) { failure(error, t('settings.userSaveFailed')); }
  }

  async function confirmDelete() {
    if (!deleteTarget) return;
    try {
      if (deleteTarget.kind === 'category') {
        await api.deleteCategory(deleteTarget.item.id);
        success(t('settings.categoryDeleted'));
        await categories.reload();
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
      <div className="tabs" role="tablist" aria-label={t('settings.sectionsAria')}>{tabButtons.map(([key, label]) => <button key={key} type="button" role="tab" aria-selected={tab === key} className={tab === key ? 'tab tab--active' : 'tab'} onClick={() => switchTab(key)}>{label}</button>)}</div>

      {tab === 'company' ? <Card>
        <div className="sectionTitle"><div><h2>{t('settings.interfaceLanguage')}</h2></div></div>
        <LanguageSwitcher />
        <div className="sectionTitle" style={{ marginTop: '24px' }}><div><h2>{t('settings.company')}</h2></div></div>
        <form className="formGrid" onSubmit={updateOrganization}>
          <Field label={t('settings.nameLabel')}><TextInput name="name" defaultValue={organization.data.name} required /></Field>
          <Field label={t('settings.countryLabel')}><TextInput name="country" defaultValue={organization.data.country} /></Field>
          <Field label={t('settings.currencyLabel')}><TextInput name="currency" defaultValue={organization.data.currency} /></Field>
          <Field label={t('settings.timeZoneLabel')}><TextInput name="timeZone" defaultValue={organization.data.timeZone} /></Field>
          <Field label={t('settings.logoUrlLabel')}><TextInput name="logoUrl" defaultValue={organization.data.logoUrl ?? ''} /></Field>
          <div className="formActions formActions--split"><span className="muted">{t('settings.futureProtocolsHint')}</span><Button icon={<Save size={16} />}>{t('settings.save')}</Button></div>
        </form>
      </Card> : null}

      {tab === 'locations' ? <LocationsManager /> : null}

      {tab === 'customFields' ? (
        <>
          <Card>
            <div className="sectionTitle"><div><h2>{t('settings.assetCategories')}</h2><p>{t('settings.customFieldsCategoriesHint')}</p></div><button type="button" className="iconButton iconButton--add" aria-label={t('settings.addCategory')} title={t('settings.addCategory')} onClick={() => { setEditingCategory(null); setCategoryIcon(''); setModal('category'); }}><Plus size={18} /></button></div>
            <SettingsSearch value={settingsSearch} onChange={value => { setSettingsSearch(value); setPage(1); }} total={filteredCategories.length} />
            {categories.isLoading ? <p className="muted">{t('settings.loadingCategories')}</p> : categories.error ? <ErrorState message={categories.error} onRetry={categories.reload} /> : !filteredCategories.length ? <EmptyState title={t('settings.emptyCategoriesTitle')} description={t('settings.emptyCategoriesDesc')} /> : (
              <>
                <div className="tableWrap"><table><thead><tr><th>{t('settings.nameLabel')}</th><th>{t('settings.colType')}</th><th>{t('settings.colDescription')}</th><th></th></tr></thead><tbody>
                  {pagedCategories.items.map(category => <tr key={category.id}><td><span style={{ display: 'flex', alignItems: 'center', gap: '8px' }}><CategoryIcon icon={category.icon} /><strong>{category.name}</strong></span></td><td>{categoryTypeLabels[category.type]}</td><td>{category.description ?? '—'}</td><td className="rowActions"><Button variant="secondary" icon={<ListPlus size={16} />} onClick={() => openFields(category)}>{t('settings.manageFields')}{category.fieldDefinitions.length > 0 ? ` (${category.fieldDefinitions.length})` : ''}</Button><Button variant="ghost" onClick={() => { setEditingCategory(category); setCategoryIcon(category.icon ?? ''); setModal('category'); }}>{t('common.edit')}</Button><button className="iconButton" aria-label={t('settings.deleteCategoryAria', { name: category.name })} onClick={() => setDeleteTarget({ kind: 'category', item: category })}><Trash2 size={16} /></button></td></tr>)}
                </tbody></table></div>
                <Pagination page={pagedCategories.page} total={pagedCategories.total} pageSize={pageSize} onPageChange={setPage} />
              </>
            )}
          </Card>

          <Card>
            <div className="sectionTitle"><div><h2>{t('settings.assetStatuses')}</h2><p>{t('settings.customFieldsStatusesHint')}</p></div></div>
            {statuses.isLoading ? <p className="muted">{t('settings.loadingStatuses')}</p> : statuses.error ? <ErrorState message={statuses.error} onRetry={statuses.reload} /> : (
              <form onSubmit={saveStatuses}>
                <div className="tableWrap"><table><thead><tr><th>{t('settings.colTechnicalStatus')}</th><th>{t('settings.colLabel')}</th><th>{t('settings.colOrder')}</th><th>{t('settings.colActive')}</th></tr></thead><tbody>
                  {statuses.data?.map(item => <tr key={item.statusKey}><td>{item.statusKey}</td><td><TextInput name={`label-${item.statusKey}`} defaultValue={item.label} /></td><td><TextInput name={`sort-${item.statusKey}`} type="number" defaultValue={item.sortOrder} /></td><td><input name={`enabled-${item.statusKey}`} type="checkbox" defaultChecked={item.isEnabled} /></td></tr>)}
                </tbody></table></div>
                <div className="formActions"><Button icon={<Save size={16} />}>{t('settings.saveStatuses')}</Button></div>
              </form>
            )}
          </Card>
        </>
      ) : null}

      {tab === 'profiles' ? <Card>
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
      </Card> : null}

      {tab === 'users' ? <Card>
        <div className="sectionTitle"><div><h2>{t('settings.logins')}</h2></div><Button icon={<Plus size={16} />} onClick={() => { setEditingUser(null); setModal('user'); }}>{t('settings.addLogin')}</Button></div>
        <SettingsSearch value={settingsSearch} onChange={value => { setSettingsSearch(value); setPage(1); }} total={filteredUsers.length} />
        {users.isLoading ? <p className="muted">{t('settings.loadingLogins')}</p> : users.error ? <ErrorState message={users.error} onRetry={users.reload} /> : !filteredUsers.length ? <EmptyState title={t('settings.emptyLoginsTitle')} description={t('settings.emptyLoginsDesc')} /> : (
          <>
            <div className="tableWrap"><table><thead><tr><th>{t('settings.colUser')}</th><th>{t('settings.colRoles')}</th><th>{t('assets.statusLabel')}</th><th></th></tr></thead><tbody>
              {pagedUsers.items.map(user => <tr key={user.id}><td><strong>{user.displayName || user.email}</strong><small>{user.email}</small></td><td>{user.roles.join(', ') || '—'}</td><td>{user.isActive ? t('settings.active') : t('settings.inactive')}</td><td><Button variant="ghost" onClick={() => { setEditingUser(user); setModal('user'); }}>{t('common.edit')}</Button></td></tr>)}
            </tbody></table></div>
            <Pagination page={pagedUsers.page} total={pagedUsers.total} pageSize={pageSize} onPageChange={setPage} />
          </>
        )}
      </Card> : null}

      <Modal open={modal === 'category'} title={editingCategory ? t('settings.editCategory') : t('settings.addCategoryTitle')} onClose={() => setModal(null)}>
        <form className="formGrid" onSubmit={saveCategory}>
          <Field label={t('settings.nameLabel')}><TextInput name="name" defaultValue={editingCategory?.name ?? ''} required /></Field>
          <Field label={t('settings.typeLabel')}><SelectInput name="type" defaultValue={editingCategory?.type ?? 'Physical'}>{categoryTypeValues.map(type => <option value={type} key={type}>{categoryTypeLabels[type]}</option>)}</SelectInput></Field>
          <Field label={t('settings.iconLabel')}><input type="hidden" name="icon" value={categoryIcon} /><IconPicker value={categoryIcon} onChange={setCategoryIcon} /></Field>
          <Field label={t('settings.descriptionLabel')}><TextArea name="description" defaultValue={editingCategory?.description ?? ''} /></Field>
          <div className="formActions formActions--split"><Button type="button" variant="ghost" onClick={() => setModal(null)}>{t('common.cancel')}</Button><Button>{t('settings.save')}</Button></div>
        </form>
      </Modal>

      <Modal open={!!fieldsTarget} title={t('settings.manageFieldsTitle', { name: fieldsTarget?.name ?? '' })} onClose={() => setFieldsTarget(null)} width="wide">
        <div className="formGrid">
          <p className="muted">{t('settings.manageFieldsDesc')}</p>
          {fieldRows.map((row, index) => (
            <Card className="card--flat" key={index}>
              <div className="formGrid">
                <Field label={t('settings.fieldLabelLabel')}><TextInput value={row.label} onChange={event => updateFieldRow(index, { label: event.target.value, key: row.key || slugifyKey(event.target.value) })} /></Field>
                <Field label={t('settings.fieldKeyLabel')}><TextInput value={row.key} onChange={event => updateFieldRow(index, { key: event.target.value })} /></Field>
                <Field label={t('settings.fieldTypeLabel')}>
                  <SelectInput value={row.fieldType} onChange={event => updateFieldRow(index, { fieldType: event.target.value as AssetFieldType })}>
                    {fieldTypeValues.map(value => <option key={value} value={value}>{t(`assetFieldType.${value}`)}</option>)}
                  </SelectInput>
                </Field>
                {row.fieldType === 'Select' && (
                  <Field label={t('settings.fieldOptionsLabel')} info={t('settings.fieldOptionsHint')}>
                    <TextInput value={row.options} onChange={event => updateFieldRow(index, { options: event.target.value })} placeholder={t('settings.fieldOptionsPlaceholder')} />
                  </Field>
                )}
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <input type="checkbox" checked={row.required} onChange={event => updateFieldRow(index, { required: event.target.checked })} />
                  {t('settings.fieldRequiredLabel')}
                </label>
                <div className="formActions">
                  <Button type="button" variant="ghost" onClick={() => removeFieldRow(index)} icon={<Trash2 size={16} />}>{t('common.delete')}</Button>
                </div>
              </div>
            </Card>
          ))}
          <Button type="button" variant="secondary" onClick={addFieldRow} icon={<Plus size={16} />}>{t('settings.addField')}</Button>
          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={() => setFieldsTarget(null)}>{t('common.cancel')}</Button>
            <Button type="button" disabled={fieldsSaving} onClick={saveFields} icon={<Save size={16} />}>{fieldsSaving ? t('common.saving') : t('settings.save')}</Button>
          </div>
        </div>
      </Modal>

      <Modal open={modal === 'profile'} title={editingProfile ? t('settings.editProfile') : t('settings.addProfileTitle')} onClose={() => setModal(null)} width="wide">
        <form className="formGrid" onSubmit={saveProfile}>
          <Field label={t('settings.nameLabel')}><TextInput name="name" defaultValue={editingProfile?.name ?? ''} required /></Field>
          <Field label={t('settings.descriptionLabel')}><TextArea name="description" defaultValue={editingProfile?.description ?? ''} /></Field>
          <Field label={t('settings.defaultManagerLabel')}><SelectInput name="defaultManagerId" defaultValue={editingProfile?.defaultManagerId ?? ''}><option value="">{t('settings.noneOption')}</option>{people.data?.map(p => <option key={p.id} value={p.id}>{p.fullName}</option>)}</SelectInput></Field>
          <fieldset className="checkboxGroup"><legend>{t('settings.colEquipmentCategories')}</legend>{categories.data?.map(c => <label key={c.id}><input name="assetCategoryIds" value={c.id} type="checkbox" defaultChecked={editingProfile?.assetCategoryIds.includes(c.id)} /> {c.name} <small>{categoryTypeLabels[c.type]}</small></label>)}</fieldset>
          <fieldset className="checkboxGroup"><legend>{t('settings.colProcedures')}</legend>{procedures.data?.map(p => <label key={p.id}><input name="procedureIds" value={p.id} type="checkbox" defaultChecked={editingProfile?.procedureIds.includes(p.id)} /> {p.title}</label>)}</fieldset>
          <div className="formActions formActions--split"><Button type="button" variant="ghost" onClick={() => setModal(null)}>{t('common.cancel')}</Button><Button>{t('settings.saveProfile')}</Button></div>
        </form>
      </Modal>
      <Modal open={modal === 'user'} title={editingUser ? t('settings.editLogin') : t('settings.addLoginTitle')} onClose={() => setModal(null)}>
        <form className="formGrid" onSubmit={saveUser}>
          <Field label={t('settings.emailLabel')}><TextInput name="email" type="email" defaultValue={editingUser?.email ?? ''} required /></Field>
          <Field label={t('settings.displayNameLabel')}><TextInput name="displayName" defaultValue={editingUser?.displayName ?? ''} /></Field>
          <label className="checkField"><input name="isActive" type="checkbox" defaultChecked={editingUser?.isActive ?? true} /> {t('settings.accountActive')}</label>
          <fieldset className="checkboxGroup"><legend>{t('settings.rolesLegend')}</legend>{roles.data?.map(role => <label key={role.key} title={role.description}><input name="roles" value={role.key} type="checkbox" defaultChecked={editingUser?.roles.includes(role.key)} /> {role.label}</label>)}</fieldset>
          <div className="formActions formActions--split"><Button type="button" variant="ghost" onClick={() => setModal(null)}>{t('common.cancel')}</Button><Button>{t('settings.saveLogin')}</Button></div>
        </form>
      </Modal>

      <ConfirmDialog
        open={!!deleteTarget}
        title={deleteTarget?.kind === 'category' ? t('settings.deleteCategoryTitle') : t('settings.deleteProfileTitle')}
        description={deleteTarget?.kind === 'category' ? t('settings.deleteCategoryDesc', { name: deleteTarget.item.name }) : t('settings.deleteProfileDesc', { name: deleteTarget?.item.name ?? '' })}
        confirmLabel={t('common.delete')}
        onConfirm={confirmDelete}
        onClose={() => setDeleteTarget(null)}
      />
    </div>
  );
}

function SettingsSearch({ value, onChange, total }: { value: string; onChange: (value: string) => void; total: number }) {
  const { t } = useI18n();
  return (
    <div className="filters filters--single settingsSearch">
      <Field label={t('settings.searchInSection')}><TextInput value={value} onChange={event => onChange(event.target.value)} placeholder={t('settings.searchPlaceholder')} /></Field>
      <span className="toolbarHint"><Search size={16} /> {total} {t('common.results')}</span>
    </div>
  );
}
