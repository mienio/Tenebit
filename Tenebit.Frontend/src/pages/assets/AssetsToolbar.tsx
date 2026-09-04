import { ChevronDown, FileSpreadsheet, Printer, Search, SlidersHorizontal, X } from 'lucide-react';
import { useState } from 'react';
import { Button } from '../../components/Button';
import { Card } from '../../components/Card';
import { SelectInput, TextInput } from '../../components/FormFields';
import { useI18n } from '../../i18n/I18nProvider';
import type { AssetStatus, LocationNode, Team } from '../../types/domain';

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
}

export function AssetsToolbar(props: AssetsToolbarProps) {
  const { t } = useI18n();
  // Advanced filters start collapsed and open themselves only if something is already filtering, so a
  // shared link or a restored URL does not hide why the list looks incomplete.
  const activeCount = [props.status, props.location, props.team].filter(Boolean).length;
  const [advancedOpen, setAdvancedOpen] = useState(activeCount > 0);

  function clearAdvanced() {
    props.setStatus('');
    props.setLocation('');
    props.setTeam('');
  }

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

      {/*
        Search is one line. Status/location/team are secondary - most searches never touch them - so they
        live behind a toggle instead of permanently occupying four labelled rows, which on a narrow window
        stacked into half a screen of chrome above the actual list.
      */}
      <Card className="toolbarCard">
        <form className="searchBar" onSubmit={event => event.preventDefault()}>
          <div className="searchBar__field">
            <Search size={16} />
            <TextInput
              value={props.search}
              onChange={event => props.setSearch(event.target.value)}
              placeholder={t('assets.searchPlaceholder')}
              aria-label={t('assets.searchLabel')}
            />
            {props.search ? (
              <button type="button" className="searchBar__clear" aria-label={t('common.clearFilters')} onClick={() => props.setSearch('')}>
                <X size={14} />
              </button>
            ) : null}
          </div>

          <button
            type="button"
            className={advancedOpen || activeCount > 0 ? 'searchBar__toggle searchBar__toggle--active' : 'searchBar__toggle'}
            aria-expanded={advancedOpen}
            onClick={() => setAdvancedOpen(open => !open)}
          >
            <SlidersHorizontal size={15} />
            <span>{t('assets.filtersAdvanced')}</span>
            {activeCount > 0 ? <span className="searchBar__badge">{activeCount}</span> : null}
            <ChevronDown size={14} style={{ transform: advancedOpen ? 'rotate(180deg)' : undefined, transition: 'transform .15s' }} />
          </button>
        </form>

        {advancedOpen ? (
          <div className="searchBar__advanced">
            <label className="searchBar__advancedField">
              <span>{t('assets.statusLabel')}</span>
              <SelectInput value={props.status} onChange={event => props.setStatus(event.target.value as AssetStatus | '')}>
                {props.statuses.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
              </SelectInput>
            </label>
            <label className="searchBar__advancedField">
              <span>{t('assets.locationLabel')}</span>
              <SelectInput value={props.location} onChange={event => props.setLocation(event.target.value)}>
                <option value="">{t('assets.allLocations')}</option>
                {props.locations.map(item => <option key={item.id} value={item.fullPath}>{item.fullPath}</option>)}
              </SelectInput>
            </label>
            <label className="searchBar__advancedField">
              <span>{t('assets.teamLabel')}</span>
              <SelectInput value={props.team} onChange={event => props.setTeam(event.target.value)}>
                <option value="">{t('assets.allTeams')}</option>
                {props.teams.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
              </SelectInput>
            </label>
            {activeCount > 0 ? (
              <button type="button" className="linkButton searchBar__clearAll" onClick={clearAdvanced}>
                {t('common.clearFilters')}
              </button>
            ) : null}
          </div>
        ) : null}
      </Card>
    </>
  );
}
