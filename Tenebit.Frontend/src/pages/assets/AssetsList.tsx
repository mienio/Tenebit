import { ArrowDown, ArrowUp, Columns3, Plus, X } from 'lucide-react';
import { useState } from 'react';
import { Avatar } from '../../components/Avatar';
import { Button } from '../../components/Button';
import { Card } from '../../components/Card';
import { EmptyState } from '../../components/StateViews';
import { Pagination } from '../../components/Pagination';
import { StatusBadge } from '../../components/StatusBadge';
import { useI18n } from '../../i18n/I18nProvider';
import type { Asset, AssetCategory } from '../../types/domain';
import { CategoryIcon } from '../../utils/categoryIcons';
import { formatDate, formatMoney } from '../../utils/format';
import type { AssetSortKey } from './useAssetFilters';
import { ASSET_COLUMNS, useAssetColumns } from './useAssetColumns';

interface AssetsListProps {
  rows: Asset[];
  categories: AssetCategory[];
  statusSettingByKey: Map<string, { label: string; color: string; backgroundColor: string }>;
  isLoading: boolean;
  totalAssets: number;
  page: number;
  pageSize: number;
  filtersActive: boolean;
  onClearFilters(): void;
  onCreate(): void;
  onSelect(asset: Asset): void;
  onViewPerson(id: string): void;
  onViewLocation(location: string): void;
  selectedIds: Set<string>;
  allOnPageSelected: boolean;
  onToggleSelected(id: string): void;
  onToggleSelectAll(): void;
  sort: { key: AssetSortKey; dir: 1 | -1 } | null;
  onToggleSort(key: AssetSortKey): void;
  onPageChange(page: number): void;
}

const SORT_OPTIONS: { key: AssetSortKey; labelKey: string }[] = [
  { key: 'name', labelKey: 'assets.colName' },
  { key: 'assetTag', labelKey: 'assets.colTag' },
  { key: 'status', labelKey: 'assets.statusLabel' },
  { key: 'person', labelKey: 'assets.colPerson' },
  { key: 'location', labelKey: 'assets.colLocation' },
  { key: 'value', labelKey: 'assets.colValue' },
  { key: 'warranty', labelKey: 'assets.colWarranty' },
];

/**
 * Assets are shown as one tile per record, stacked vertically, and every tile is exactly one line tall.
 *
 * This is a browse surface: you scan it to find something, then click through for the detail panel.
 * Anything that would make a tile grow - wrapping text, a stacked sub-label, a responsive card layout -
 * is deliberately prevented, because the moment records take two lines the list stops being scannable.
 * Fields that do not fit are dropped via the column picker, never wrapped onto another line.
 */
export function AssetsList(props: AssetsListProps) {
  const { t, tPlural } = useI18n();
  const { visible, toggle, reset } = useAssetColumns();
  const [pickerOpen, setPickerOpen] = useState(false);

  return (
    <Card>
      <div className="sectionTitle">
        <div>
          <h2>{t('assets.listTitle')}</h2>
          <p>{t('assets.countSummary', { count: props.totalAssets, pageSize: props.pageSize, noun: tPlural('count.assets', props.totalAssets) })}</p>
        </div>
        <div className="columnPicker">
          <Button variant="secondary" icon={<Columns3 size={16} />} onClick={() => setPickerOpen(open => !open)}>
            {t('assets.columns')}
          </Button>
          {pickerOpen ? (
            <>
              <button type="button" className="columnPicker__scrim" aria-label={t('common.close')} onClick={() => setPickerOpen(false)} />
              <div className="columnPicker__menu">
                <p className="columnPicker__title">{t('assets.columnsHint')}</p>
                {ASSET_COLUMNS.map(column => (
                  <label key={column.key} className="columnPicker__row">
                    <input type="checkbox" checked={visible[column.key]} onChange={() => toggle(column.key)} />
                    <span>{t(column.labelKey)}</span>
                  </label>
                ))}
                <button type="button" className="linkButton columnPicker__reset" onClick={reset}>{t('assets.columnsReset')}</button>
              </div>
            </>
          ) : null}
        </div>
      </div>

      {props.isLoading && <p className="muted">{t('assets.refreshing')}</p>}

      {!props.rows.length ? (
        <EmptyState
          title={t('assets.emptyTitle')}
          description={t('assets.emptyDesc')}
          action={props.filtersActive ? (
            <Button variant="secondary" icon={<X size={16} />} onClick={props.onClearFilters}>{t('common.clearFilters')}</Button>
          ) : (
            <Button onClick={props.onCreate} icon={<Plus size={16} />}>{t('assets.add')}</Button>
          )}
        />
      ) : (
        <>
          {/* Sorting lives here because there is no header row to click on any more. */}
          <div className="tileToolbar">
            <label className="tileToolbar__all">
              <input
                type="checkbox"
                checked={props.allOnPageSelected}
                onChange={props.onToggleSelectAll}
                aria-label={t('assets.bulkSelectAll')}
              />
              <span>{t('assets.bulkSelectAll')}</span>
            </label>
            <div className="tileToolbar__sort">
              <span className="tileToolbar__sortLabel">{t('assets.sortBy')}</span>
              {SORT_OPTIONS.map(option => {
                const active = props.sort?.key === option.key;
                return (
                  <button
                    key={option.key}
                    type="button"
                    className={active ? 'sortChip sortChip--active' : 'sortChip'}
                    onClick={() => props.onToggleSort(option.key)}
                  >
                    {t(option.labelKey)}
                    {active ? (props.sort!.dir === 1 ? <ArrowUp size={12} /> : <ArrowDown size={12} />) : null}
                  </button>
                );
              })}
            </div>
          </div>

          <ul className="assetTiles">
            {props.rows.map(asset => {
              const category = props.categories.find(item => item.id === asset.categoryId);
              const statusSetting = props.statusSettingByKey.get(asset.status);
              return (
                <li key={asset.id}>
                  <div
                    className={props.selectedIds.has(asset.id) ? 'assetTile assetTile--selected' : 'assetTile'}
                    role="button"
                    tabIndex={0}
                    onClick={() => props.onSelect(asset)}
                    onKeyDown={event => {
                      if (event.target !== event.currentTarget) return;
                      if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); props.onSelect(asset); }
                    }}
                  >
                    <input
                      type="checkbox"
                      className="assetTile__check"
                      checked={props.selectedIds.has(asset.id)}
                      onChange={() => props.onToggleSelected(asset.id)}
                      onClick={event => event.stopPropagation()}
                      aria-label={t('assets.bulkSelectOne', { name: asset.name })}
                    />

                    <span className="assetTile__icon"><CategoryIcon icon={category?.icon} size={16} /></span>

                    <span className="assetTile__name" title={asset.name}>{asset.name}</span>

                    {visible.category ? (
                      <span className="assetTile__field assetTile__field--category" title={asset.categoryName ?? undefined}>{asset.categoryName ?? '-'}</span>
                    ) : null}

                    {visible.assetTag ? (
                      <span className="assetTile__field assetTile__field--mono assetTile__field--tag" title={asset.assetTag}>{asset.assetTag}</span>
                    ) : null}

                    {visible.status ? (
                      <span className="assetTile__status">
                        <StatusBadge status={asset.status} label={statusSetting?.label} color={statusSetting?.color} backgroundColor={statusSetting?.backgroundColor} />
                      </span>
                    ) : null}

                    {visible.person ? (
                      <span className="assetTile__field assetTile__field--person" onClick={event => event.stopPropagation()}>
                        {asset.assignedPersonId && asset.assignedPersonName ? (
                          <span className="personChip">
                            <Avatar name={asset.assignedPersonName} size={20} />
                            <button type="button" className="inlineAction" onClick={() => props.onViewPerson(asset.assignedPersonId!)}>{asset.assignedPersonName}</button>
                          </span>
                        ) : <span className="assetTile__muted">{t('common.unassigned')}</span>}
                      </span>
                    ) : null}

                    {visible.location ? (
                      <span className="assetTile__field assetTile__field--location" onClick={event => event.stopPropagation()}>
                        {asset.location ? (
                          <button type="button" className="inlineAction" title={asset.location} onClick={() => props.onViewLocation(asset.location!)}>{asset.location}</button>
                        ) : <span className="assetTile__muted">-</span>}
                      </span>
                    ) : null}

                    {visible.value ? (
                      <span className="assetTile__field assetTile__field--right assetTile__field--value">
                        {asset.purchasePrice != null ? formatMoney(asset.purchasePrice, asset.currency ?? 'PLN') : <span className="assetTile__muted">-</span>}
                      </span>
                    ) : null}

                    {visible.warranty ? (
                      <span className="assetTile__field assetTile__field--right assetTile__field--warranty">{formatDate(asset.warrantyUntil)}</span>
                    ) : null}
                  </div>
                </li>
              );
            })}
          </ul>

          <Pagination page={props.page} total={props.totalAssets} pageSize={props.pageSize} onPageChange={props.onPageChange} />
        </>
      )}
    </Card>
  );
}
