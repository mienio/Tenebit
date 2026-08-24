import { ArrowDown, ArrowUp, Columns3, Plus, X } from 'lucide-react';
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
import { useState } from 'react';
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
          <div className="tableWrap">
            <table className="dense-table dense-table--oneLine">
              <thead>
                <tr>
                  <th style={{ width: '32px' }}><input type="checkbox" checked={props.allOnPageSelected} onChange={props.onToggleSelectAll} onClick={event => event.stopPropagation()} aria-label={t('assets.bulkSelectAll')} /></th>
                  <th style={{ width: '40px' }}></th>
                  <SortableTh sortKey="name" sort={props.sort} onToggle={props.onToggleSort}>{t('assets.colName')}</SortableTh>
                  {visible.category ? <th>{t('assets.colCategory')}</th> : null}
                  {visible.assetTag ? <SortableTh sortKey="assetTag" sort={props.sort} onToggle={props.onToggleSort}>{t('assets.colTag')}</SortableTh> : null}
                  {visible.status ? <SortableTh sortKey="status" sort={props.sort} onToggle={props.onToggleSort}>{t('assets.statusLabel')}</SortableTh> : null}
                  {visible.person ? <SortableTh sortKey="person" sort={props.sort} onToggle={props.onToggleSort}>{t('assets.colPerson')}</SortableTh> : null}
                  {visible.location ? <SortableTh sortKey="location" sort={props.sort} onToggle={props.onToggleSort}>{t('assets.colLocation')}</SortableTh> : null}
                  {visible.value ? <SortableTh sortKey="value" sort={props.sort} onToggle={props.onToggleSort} align="right">{t('assets.colValue')}</SortableTh> : null}
                  {visible.warranty ? <SortableTh sortKey="warranty" sort={props.sort} onToggle={props.onToggleSort}>{t('assets.colWarranty')}</SortableTh> : null}
                </tr>
              </thead>
              <tbody>
                {props.rows.map(asset => {
                  const category = props.categories.find(item => item.id === asset.categoryId);
                  const statusSetting = props.statusSettingByKey.get(asset.status);
                  return (
                    <tr
                      key={asset.id}
                      tabIndex={0}
                      onClick={() => props.onSelect(asset)}
                      onKeyDown={event => {
                        if (event.target !== event.currentTarget) return;
                        if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); props.onSelect(asset); }
                      }}
                    >
                      <td onClick={event => event.stopPropagation()}>
                        <input type="checkbox" checked={props.selectedIds.has(asset.id)} onChange={() => props.onToggleSelected(asset.id)} aria-label={t('assets.bulkSelectOne', { name: asset.name })} />
                      </td>
                      <td className="cell-icon"><div className="table-icon"><CategoryIcon icon={category?.icon} size={16} /></div></td>
                      <td data-label={t('assets.colName')} title={asset.name}><strong>{asset.name}</strong></td>
                      {visible.category ? <td data-label={t('assets.colCategory')} title={asset.categoryName ?? undefined}>{asset.categoryName ?? '-'}</td> : null}
                      {visible.assetTag ? <td data-label={t('assets.colTag')}>{asset.assetTag}</td> : null}
                      {visible.status ? <td data-label={t('assets.statusLabel')}><StatusBadge status={asset.status} label={statusSetting?.label} color={statusSetting?.color} backgroundColor={statusSetting?.backgroundColor} /></td> : null}
                      {visible.person ? (
                      <td data-label={t('assets.colPerson')} onClick={event => event.stopPropagation()}>
                        {asset.assignedPersonId && asset.assignedPersonName ? (
                          <span className="personChip">
                            <Avatar name={asset.assignedPersonName} size={22} />
                            <span className="personChip__sep">•</span>
                            <button type="button" className="inlineAction" onClick={() => props.onViewPerson(asset.assignedPersonId!)}>{asset.assignedPersonName}</button>
                          </span>
                        ) : <span style={{ color: 'var(--muted)' }}>{t('common.unassigned')}</span>}
                      </td>
                      ) : null}
                      {visible.location ? (
                      <td data-label={t('assets.colLocation')} onClick={event => event.stopPropagation()}>
                        {asset.location ? (
                          <button type="button" className="inlineAction" onClick={() => props.onViewLocation(asset.location!)}>{asset.location}</button>
                        ) : <span style={{ color: 'var(--muted)' }}>-</span>}
                      </td>
                      ) : null}
                      {visible.value ? <td data-label={t('assets.colValue')} style={{ textAlign: 'right' }}>{asset.purchasePrice != null ? formatMoney(asset.purchasePrice, asset.currency ?? 'PLN') : '-'}</td> : null}
                      {visible.warranty ? <td data-label={t('assets.colWarranty')}>{formatDate(asset.warrantyUntil)}</td> : null}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <Pagination page={props.page} total={props.totalAssets} pageSize={props.pageSize} onPageChange={props.onPageChange} />
        </>
      )}
    </Card>
  );
}

function SortableTh({ sortKey, sort, onToggle, children, align }: {
  sortKey: AssetSortKey;
  sort: { key: AssetSortKey; dir: 1 | -1 } | null;
  onToggle(key: AssetSortKey): void;
  children: React.ReactNode;
  align?: 'right';
}) {
  const active = sort?.key === sortKey;
  return (
    <th style={align === 'right' ? { textAlign: 'right' } : undefined} aria-sort={active ? (sort.dir === 1 ? 'ascending' : 'descending') : undefined}>
      <button type="button" className={active ? 'thSort thSort--active' : 'thSort'} onClick={() => onToggle(sortKey)}>
        {children}
        {active ? (sort.dir === 1 ? <ArrowUp size={12} /> : <ArrowDown size={12} />) : null}
      </button>
    </th>
  );
}
