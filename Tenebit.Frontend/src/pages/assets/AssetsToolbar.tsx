import { Building2, CircleDot, FileSpreadsheet, List, Printer, Tag, Users, X } from 'lucide-react';
import type { Dispatch, SetStateAction } from 'react';
import { Button } from '../../components/Button';
import { Card } from '../../components/Card';
import { Field, SelectInput, TextInput } from '../../components/FormFields';
import { useI18n } from '../../i18n/I18nProvider';
import type { AssetStatus, LocationNode, Team } from '../../types/domain';

type ViewMode = 'list' | 'location' | 'person' | 'status' | 'category';

type StatusOption = { value: AssetStatus | ''; label: string };

interface AssetsToolbarProps {
  selectedCount: number;
  batchQrLoading: boolean;
  onBulkStatus(): void;
  onBulkLocation(): void;
  onExportSelected(): void;
  onBatchQr(): void;
  onClearSelection(): void;
  owner: string;
  setOwner(value: string): void;
  warranty: string;
  setWarranty(value: string): void;
  search: string;
  setSearch(value: string): void;
  status: AssetStatus | '';
  setStatus(value: AssetStatus | ''): void;
  location: string;
  setLocation(value: string): void;
  team: string;
  setTeam(value: string): void;
  statuses: StatusOption[];
  locations: LocationNode[];
  teams: Team[];
  viewMode: ViewMode;
  setViewMode: Dispatch<SetStateAction<ViewMode>>;
}

export function AssetsToolbar(props: AssetsToolbarProps) {
  const { t } = useI18n();
  return (
    <>
      {props.selectedCount > 0 && (
        <Card className="toolbarCard">
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
            <strong>{t('assets.bulkSelectedCount', { count: props.selectedCount })}</strong>
            <Button variant="secondary" onClick={props.onBulkStatus}>{t('assets.bulkChangeStatus')}</Button>
            <Button variant="secondary" onClick={props.onBulkLocation}>{t('assets.bulkMove')}</Button>
            <Button variant="secondary" onClick={props.onExportSelected} icon={<FileSpreadsheet size={16} />}>{t('assets.bulkExport')}</Button>
            <Button variant="secondary" disabled={props.batchQrLoading} onClick={props.onBatchQr} icon={<Printer size={16} />}>{props.batchQrLoading ? t('common.loading') : t('assets.bulkPrintQr')}</Button>
            <Button variant="ghost" onClick={props.onClearSelection} icon={<X size={16} />}>{t('assets.bulkClear')}</Button>
          </div>
        </Card>
      )}

      {(props.owner === 'none' || props.warranty === 'expiring') && (
        <Card className="toolbarCard">
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
            {props.owner === 'none' && <Button variant="secondary" icon={<X size={14} />} onClick={() => props.setOwner('')}>{t('assets.filterOwnerNone')}</Button>}
            {props.warranty === 'expiring' && <Button variant="secondary" icon={<X size={14} />} onClick={() => props.setWarranty('')}>{t('assets.filterWarrantyExpiring')}</Button>}
          </div>
        </Card>
      )}

      <Card className="toolbarCard">
        <form className="filters filters--four" onSubmit={event => event.preventDefault()}>
          <Field label={t('assets.searchLabel')}>
            <TextInput value={props.search} onChange={event => props.setSearch(event.target.value)} placeholder={t('assets.searchPlaceholder')} />
          </Field>
          <Field label={t('assets.statusLabel')}>
            <SelectInput value={props.status} onChange={event => props.setStatus(event.target.value as AssetStatus | '')}>
              {props.statuses.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
            </SelectInput>
          </Field>
          <Field label={t('assets.locationLabel')}>
            <SelectInput value={props.location} onChange={event => props.setLocation(event.target.value)}>
              <option value="">{t('assets.allLocations')}</option>
              {props.locations.map(item => <option key={item.id} value={item.fullPath}>{item.fullPath}</option>)}
            </SelectInput>
          </Field>
          <Field label={t('assets.teamLabel')}>
            <SelectInput value={props.team} onChange={event => props.setTeam(event.target.value)}>
              <option value="">{t('assets.allTeams')}</option>
              {props.teams.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
            </SelectInput>
          </Field>
        </form>
      </Card>

      <Card className="toolbarCard">
        <div className="tabs" role="tablist" aria-label={t('assets.listTitle')}>
          <ViewTab active={props.viewMode === 'list'} onClick={() => props.setViewMode('list')} icon={<List size={16} />} label={t('assets.viewList')} />
          <ViewTab active={props.viewMode === 'location'} onClick={() => props.setViewMode('location')} icon={<Building2 size={16} />} label={t('assets.browseByLocation')} />
          <ViewTab active={props.viewMode === 'person'} onClick={() => props.setViewMode('person')} icon={<Users size={16} />} label={t('assets.viewPerson')} />
          <ViewTab active={props.viewMode === 'status'} onClick={() => props.setViewMode('status')} icon={<CircleDot size={16} />} label={t('assets.viewStatus')} />
          <ViewTab active={props.viewMode === 'category'} onClick={() => props.setViewMode('category')} icon={<Tag size={16} />} label={t('assets.viewCategory')} />
        </div>
      </Card>
    </>
  );
}

function ViewTab({ active, onClick, icon, label }: { active: boolean; onClick(): void; icon: React.ReactNode; label: string }) {
  return (
    <button type="button" role="tab" aria-selected={active} className={active ? 'tab tab--active' : 'tab'} onClick={onClick}>
      <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>{icon}{label}</span>
    </button>
  );
}
