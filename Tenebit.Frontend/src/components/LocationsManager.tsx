import { FormEvent, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft, ArrowRight, Building2, Pencil, Plus, Save, Trash2 } from 'lucide-react';
import { api } from '../api/endpoints';
import { Button } from './Button';
import { Card } from './Card';
import { ConfirmDialog } from './ConfirmDialog';
import { Field, SelectInput, TextInput } from './FormFields';
import { Modal } from './Modal';
import { EmptyState, ErrorState, LoadingState } from './StateViews';
import { useAsyncData } from '../hooks/useAsyncData';
import type { LocationNode, LocationType } from '../types/domain';
import { locationTypeValues } from '../utils/labels';
import { useI18n } from '../i18n/I18nProvider';
import { useCelebration } from '../celebration/CelebrationProvider';

const maxTreeDepth = 8;
const customTypesStorageKey = 'tenebit_custom_location_types';

type LocationDialog =
  | { mode: 'create'; step: 'parent' | 'details'; parentId: string | null; hasParentStep: boolean }
  | { mode: 'edit'; location: LocationNode }
  | null;

function LocationGroup({
  node,
  byParent,
  depth,
  typeLabel,
  selectedId,
  onSelect,
  onAddChild,
  onEdit,
  onDelete
}: {
  node: LocationNode;
  byParent: Map<string, LocationNode[]>;
  depth: number;
  typeLabel: (type: LocationType) => string;
  selectedId: string | null;
  onSelect: (id: string) => void;
  onAddChild: (parent: LocationNode) => void;
  onEdit: (location: LocationNode) => void;
  onDelete: (location: LocationNode) => void;
}) {
  const { t } = useI18n();
  const children = depth < maxTreeDepth ? byParent.get(node.id) ?? [] : [];

  return (
    <div className="locationGroup">
      <div className={selectedId === node.id ? 'locationRow locationRow--active' : 'locationRow'}>
        <button type="button" className="locationRow__main" onClick={() => onSelect(node.id)}>
          <Building2 size={16} />
          <span>
            <strong>{node.name}</strong>
            <small>
              {typeLabel(node.type)} · {t('locations.assetsCount', { count: node.assetCount })} · {t('locations.peopleCount', { count: node.personCount })}
              {!node.isActive ? ` · ${t('locations.inactiveBadge')}` : ''}
            </small>
          </span>
        </button>
        <div className="rowActions">
          <button className="iconButton" aria-label={t('common.edit')} title={t('common.edit')} onClick={() => onEdit(node)}><Pencil size={16} /></button>
          <button className="iconButton" aria-label={t('locations.delete')} title={t('locations.delete')} onClick={() => onDelete(node)}><Trash2 size={16} /></button>
        </div>
      </div>
      {depth < maxTreeDepth && (
        <div className="locationGroup__children">
          {children.map(child => (
            <LocationGroup
              key={child.id}
              node={child}
              byParent={byParent}
              depth={depth + 1}
              typeLabel={typeLabel}
              selectedId={selectedId}
              onSelect={onSelect}
              onAddChild={onAddChild}
              onEdit={onEdit}
              onDelete={onDelete}
            />
          ))}
          <button type="button" className="locationGroup__addChild" aria-label={t('locations.addChildAria', { name: node.name })} title={t('locations.addChildAria', { name: node.name })} onClick={() => onAddChild(node)}>
            <Plus size={14} /> {t('locations.addChildLabel')}
          </button>
        </div>
      )}
    </div>
  );
}

export function LocationsManager() {
  const { t } = useI18n();
  const { celebrate } = useCelebration();
  const locationTypes: { value: LocationType; label: string }[] = locationTypeValues.map(value => ({ value, label: t(`locationType.${value}`) }));
  const typeLabel = (type: string) => locationTypes.find(item => item.value === type)?.label ?? type;
  const locations = useAsyncData(api.locations, []);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [dialog, setDialog] = useState<LocationDialog>(null);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [saving, setSaving] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<LocationNode | null>(null);
  const [typeValue, setTypeValue] = useState<string>('Room');
  const [typeQuickAdd, setTypeQuickAdd] = useState(false);
  const [sessionCustomTypes, setSessionCustomTypes] = useState<string[]>(() => {
    try {
      const raw = window.sessionStorage.getItem(customTypesStorageKey);
      return raw ? (JSON.parse(raw) as string[]) : [];
    } catch {
      return [];
    }
  });

  const usedCustomTypes = useMemo(
    () => Array.from(new Set((locations.data ?? []).map(item => item.type))).filter(value => !locationTypeValues.includes(value as LocationType)),
    [locations.data]
  );
  const allTypeOptions = useMemo(
    () => Array.from(new Set([...locationTypeValues, ...usedCustomTypes, ...sessionCustomTypes])),
    [usedCustomTypes, sessionCustomTypes]
  );

  function addCustomType(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const value = String(form.get('typeName') ?? '').trim();
    if (!value) return;
    setSessionCustomTypes(current => {
      const next = Array.from(new Set([...current, value]));
      try { window.sessionStorage.setItem(customTypesStorageKey, JSON.stringify(next)); } catch {}
      return next;
    });
    setTypeValue(value);
    setTypeQuickAdd(false);
  }

  const byParent = useMemo(() => {
    const map = new Map<string, LocationNode[]>();
    for (const location of locations.data ?? []) {
      if (!location.parentId) continue;
      const siblings = map.get(location.parentId) ?? [];
      siblings.push(location);
      map.set(location.parentId, siblings);
    }
    return map;
  }, [locations.data]);

  const roots = useMemo(() => (locations.data ?? []).filter(location => !location.parentId), [locations.data]);
  const selectedLocation = useMemo(() => locations.data?.find(item => item.id === selectedId) ?? null, [locations.data, selectedId]);
  const inventory = useAsyncData(() => selectedLocation ? api.locationInventory(selectedLocation.id) : Promise.resolve(null), [selectedLocation?.id]);

  function openWizard() {
    setMessage(null);
    if (!roots.length) {
      setTypeValue('Building');
      setDialog({ mode: 'create', step: 'details', parentId: null, hasParentStep: false });
    } else {
      setDialog({ mode: 'create', step: 'parent', parentId: roots[0].id, hasParentStep: true });
    }
  }

  function openAddChild(parent: LocationNode) {
    setMessage(null);
    setTypeValue('Room');
    setDialog({ mode: 'create', step: 'details', parentId: parent.id, hasParentStep: false });
  }

  function setWizardParent(parentId: string | null) {
    setDialog(prev => prev && prev.mode === 'create' ? { ...prev, parentId } : prev);
  }

  function goToDetailsStep(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (dialog?.mode === 'create') {
      setTypeValue(dialog.parentId ? 'Room' : 'Building');
    }
    setDialog(prev => prev && prev.mode === 'create' ? { ...prev, step: 'details' } : prev);
  }

  async function submitLocation(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!dialog) return;
    const form = new FormData(event.currentTarget);
    const name = String(form.get('name') ?? '').trim();
    if (!name) return setMessage({ type: 'error', text: t('locations.nameRequired') });
    setSaving(true);
    setMessage(null);
    try {
      if (dialog.mode === 'edit') {
        const updated = await api.updateLocation(dialog.location.id, {
          name,
          type: String(form.get('type') ?? dialog.location.type) as LocationType,
          parentId: String(form.get('parentId') || '') || null,
          isActive: form.get('isActive') === 'on'
        });
        setSelectedId(updated.id);
        setMessage({ type: 'success', text: t('locations.updated') });
      } else {
        const created = await api.createLocation({
          name,
          type: String(form.get('type') ?? (dialog.parentId ? 'Room' : 'Building')) as LocationType,
          parentId: dialog.parentId
        });
        setSelectedId(created.id);
        setMessage({ type: 'success', text: t('locations.created') });
        celebrate(t('celebration.locationAdded'));
      }
      setDialog(null);
      await locations.reload();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('locations.createFailed') });
    } finally {
      setSaving(false);
    }
  }

  async function deleteLocation() {
    if (!deleteTarget) return;
    try {
      await api.deleteLocation(deleteTarget.id);
      setMessage({ type: 'success', text: t('locations.deleted') });
      setDeleteTarget(null);
      setSelectedId(null);
      await locations.reload();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('locations.deleteFailed') });
      setDeleteTarget(null);
    }
  }

  if (locations.isLoading) return <LoadingState title={t('locations.loadingTitle')} />;
  if (locations.error) return <ErrorState message={locations.error} onRetry={locations.reload} />;

  const parentName = dialog?.mode === 'create' && dialog.parentId ? locations.data?.find(item => item.id === dialog.parentId)?.name ?? '' : '';
  const dialogTitle = !dialog
    ? ''
    : dialog.mode === 'edit'
      ? t('locations.editTitle')
      : dialog.step === 'parent'
        ? t('locations.wizard.chooseTitle')
        : dialog.parentId
          ? t('locations.addChildTitle', { parent: parentName })
          : t('locations.addBuildingTitle');

  return (
    <div className="locationsWorkspace">
      <Card>
        <div className="sectionTitle">
          <div><h2>{t('locations.title')}</h2><p>{t('locations.desc')}</p></div>
          <Button onClick={openWizard} icon={<Plus size={16} />}>{t('locations.add')}</Button>
        </div>
        {message ? <p className={`formMessage formMessage--${message.type}`} aria-live="polite">{message.text}</p> : null}
        {!roots.length ? (
          <EmptyState
            title={t('locations.emptyTitle')}
            description={t('locations.emptyDesc')}
            action={<Button onClick={openWizard} icon={<Plus size={16} />}>{t('locations.addBuilding')}</Button>}
          />
        ) : (
          <div className="locationGroups">
            {roots.map(root => (
              <LocationGroup
                key={root.id}
                node={root}
                byParent={byParent}
                depth={0}
                typeLabel={typeLabel}
                selectedId={selectedId}
                onSelect={setSelectedId}
                onAddChild={openAddChild}
                onEdit={location => { setTypeValue(location.type); setDialog({ mode: 'edit', location }); }}
                onDelete={setDeleteTarget}
              />
            ))}
          </div>
        )}
      </Card>

      <Card>
        <div className="sectionTitle"><h2>{t('locations.contentTitle')}</h2><p>{selectedLocation?.fullPath ?? t('locations.chooseFromList')}</p></div>
        {!selectedLocation ? <p className="muted">{t('locations.noneSelected')}</p> : inventory.isLoading ? <p className="muted">{t('locations.loadingContent')}</p> : (
          <div className="twoColumns inventoryColumns">
            <div>
              <h3>{t('locations.assetsHeading')}</h3>
              {!inventory.data?.assets.length ? <p className="muted">{t('locations.noAssetsHere')}</p> : (
                <div className="listRows">{inventory.data.assets.map(asset => (
                  <Link className="listRow" to={`/assets?search=${encodeURIComponent(asset.assetTag)}`} key={asset.id}>
                    <div><strong>{asset.name}</strong><small>{asset.assetTag}</small></div>
                    <span>{asset.assignedPersonName ?? t('common.unassigned')}</span>
                  </Link>
                ))}</div>
              )}
            </div>
            <div>
              <h3>{t('locations.peopleHeading')}</h3>
              {!inventory.data?.people.length ? <p className="muted">{t('locations.noPeopleHere')}</p> : (
                <div className="listRows">{inventory.data.people.map(person => (
                  <Link className="listRow" to={`/people?search=${encodeURIComponent(person.email)}`} key={person.id}>
                    <div><strong>{person.fullName}</strong><small>{person.email}</small></div>
                    <span>{person.jobTitle ?? t('locations.noJobTitle')}</span>
                  </Link>
                ))}</div>
              )}
            </div>
          </div>
        )}
      </Card>

      <Modal open={!!dialog} title={dialogTitle} onClose={() => setDialog(null)}>
        {dialog?.mode === 'create' && dialog.hasParentStep && (
          <div className="wizardProgress">
            <span className={dialog.step === 'parent' ? 'wizardProgress__step wizardProgress__step--active' : 'wizardProgress__step wizardProgress__step--done'}>1. {t('locations.wizard.stepParent')}</span>
            <span className="wizardProgress__line" />
            <span className={dialog.step === 'details' ? 'wizardProgress__step wizardProgress__step--active' : 'wizardProgress__step'}>2. {t('locations.wizard.stepDetails')}</span>
          </div>
        )}

        {dialog?.mode === 'create' && dialog.step === 'parent' && (
          <form className="wizardChoiceForm" onSubmit={goToDetailsStep}>
            <p className="wizardHint">{t('locations.wizard.parentHint')}</p>
            <div className="choiceCards">
              {roots.map(root => (
                <label key={root.id} className={dialog.parentId === root.id ? 'choiceCard choiceCard--active' : 'choiceCard'}>
                  <input type="radio" name="parentChoice" checked={dialog.parentId === root.id} onChange={() => setWizardParent(root.id)} />
                  <Building2 size={20} />
                  <span>{root.name}</span>
                </label>
              ))}
              <label className={dialog.parentId === null ? 'choiceCard choiceCard--active' : 'choiceCard'}>
                <input type="radio" name="parentChoice" checked={dialog.parentId === null} onChange={() => setWizardParent(null)} />
                <Plus size={20} />
                <span>{t('locations.addBuilding')}</span>
              </label>
            </div>
            <div className="formActions formActions--split">
              <Button type="button" variant="ghost" onClick={() => setDialog(null)}>{t('common.close')}</Button>
              <Button icon={<ArrowRight size={16} />}>{t('locations.wizard.next')}</Button>
            </div>
          </form>
        )}

        {dialog?.mode === 'create' && dialog.step === 'details' && (
          <form className="formGrid" onSubmit={submitLocation} key={`create-${dialog.parentId ?? 'root'}`}>
            <Field label={t('locations.nameLabel')}>
              <TextInput name="name" required autoFocus placeholder={t('locations.namePlaceholder')} />
            </Field>
            <Field label={t('locations.typeLabel')}>
              <div className="fieldWithAdd">
                <SelectInput name="type" value={typeValue} onChange={event => setTypeValue(event.target.value)}>
                  {allTypeOptions.map(value => <option key={value} value={value}>{typeLabel(value)}</option>)}
                </SelectInput>
                <button type="button" className="iconButton iconButton--add" aria-label={t('locations.addType')} title={t('locations.addType')} onClick={() => setTypeQuickAdd(true)}><Plus size={18} /></button>
              </div>
            </Field>
            <div className="formActions formActions--split">
              {dialog.hasParentStep ? (
                <Button type="button" variant="ghost" onClick={() => setDialog(prev => prev && prev.mode === 'create' ? { ...prev, step: 'parent' } : prev)} icon={<ArrowLeft size={16} />}>{t('locations.wizard.back')}</Button>
              ) : (
                <Button type="button" variant="ghost" onClick={() => setDialog(null)}>{t('common.close')}</Button>
              )}
              <Button disabled={saving} icon={<Plus size={16} />}>{saving ? t('common.saving') : t('locations.add')}</Button>
            </div>
          </form>
        )}

        {dialog?.mode === 'edit' && (
          <form className="formGrid" onSubmit={submitLocation} key={dialog.location.id}>
            <Field label={t('locations.nameLabel')}><TextInput name="name" required defaultValue={dialog.location.name} /></Field>
            <Field label={t('locations.typeLabel')}>
              <div className="fieldWithAdd">
                <SelectInput name="type" value={typeValue} onChange={event => setTypeValue(event.target.value)}>
                  {allTypeOptions.map(value => <option key={value} value={value}>{typeLabel(value)}</option>)}
                </SelectInput>
                <button type="button" className="iconButton iconButton--add" aria-label={t('locations.addType')} title={t('locations.addType')} onClick={() => setTypeQuickAdd(true)}><Plus size={18} /></button>
              </div>
            </Field>
            <Field label={t('locations.parentFieldLabel')}>
              <SelectInput name="parentId" defaultValue={dialog.location.parentId ?? ''}>
                <option value="">{t('locations.noneOption')}</option>
                {locations.data?.filter(location => location.id !== dialog.location.id).map(location => <option key={location.id} value={location.id}>{location.fullPath}</option>)}
              </SelectInput>
            </Field>
            <label className="checkField"><input name="isActive" type="checkbox" defaultChecked={dialog.location.isActive} /> {t('locations.activeLabel')}</label>
            <div className="formActions formActions--split">
              <Button type="button" variant="ghost" onClick={() => setDialog(null)}>{t('common.close')}</Button>
              <Button disabled={saving} icon={<Save size={16} />}>{saving ? t('common.saving') : t('locations.save')}</Button>
            </div>
          </form>
        )}
      </Modal>

      <ConfirmDialog open={!!deleteTarget} title={t('locations.deleteConfirmTitle')} description={t('locations.deleteConfirmDesc')} confirmLabel={t('locations.delete')} onConfirm={deleteLocation} onClose={() => setDeleteTarget(null)} />

      <Modal open={typeQuickAdd} title={t('locations.addTypeTitle')} onClose={() => setTypeQuickAdd(false)}>
        <form className="formGrid" onSubmit={addCustomType}>
          <Field label={t('locations.typeNameLabel')}><TextInput name="typeName" required autoFocus /></Field>
          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={() => setTypeQuickAdd(false)}>{t('common.cancel')}</Button>
            <Button>{t('locations.addType')}</Button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
