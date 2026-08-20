import { ChevronDown, ChevronRight, Loader2 } from 'lucide-react';
import { ReactNode, useEffect, useState } from 'react';
import { Avatar } from './Avatar';
import { TextInput } from './FormFields';
import { StatusBadge } from './StatusBadge';
import { useI18n } from '../i18n/I18nProvider';
import type { Asset, AssetCategory } from '../types/domain';
import { CategoryIcon } from '../utils/categoryIcons';
import { formatDate, formatMoney } from '../utils/format';

export interface AssetGroup {
  id: string;
  label: string;
  sublabel?: string | null;
}

export type GroupAssetRow = Asset;

interface GroupedAssetBrowserProps {
  groups: AssetGroup[];
  icon: ReactNode;
  categories: AssetCategory[];
  fetchGroupAssets: (groupId: string) => Promise<{ items: GroupAssetRow[]; total: number }>;
  onSelectAsset: (assetId: string) => void;
}

export function GroupedAssetBrowser({ groups, icon, categories, fetchGroupAssets, onSelectAsset }: GroupedAssetBrowserProps) {
  const { t } = useI18n();
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set());
  const [loading, setLoading] = useState<Set<string>>(() => new Set());
  const [cache, setCache] = useState<Record<string, { items: GroupAssetRow[]; total: number }>>({});
  const [search, setSearch] = useState('');

  const trimmed = search.trim().toLowerCase();
  const visibleGroups = trimmed
    ? groups.filter(group => group.label.toLowerCase().includes(trimmed) || group.sublabel?.toLowerCase().includes(trimmed))
    : groups;

  function toggle(id: string) {
    setExpanded(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  useEffect(() => {
    const idsToLoad = Array.from(expanded).filter(id => !cache[id] && !loading.has(id));
    if (!idsToLoad.length) return;
    for (const id of idsToLoad) {
      setLoading(prev => new Set(prev).add(id));
      fetchGroupAssets(id)
        .then(result => setCache(prev => ({ ...prev, [id]: result })))
        .catch(() => setCache(prev => ({ ...prev, [id]: { items: [], total: 0 } })))
        .finally(() => setLoading(prev => {
          const next = new Set(prev);
          next.delete(id);
          return next;
        }));
    }
  }, [expanded, cache, loading, fetchGroupAssets]);

  if (!groups.length) {
    return <p className="muted">{t('assets.browseNoGroups')}</p>;
  }

  return (
    <div>
      <div style={{ marginBottom: '12px' }}>
        <TextInput value={search} onChange={event => setSearch(event.target.value)} placeholder={t('assets.browseSearchPlaceholder')} />
      </div>
      {trimmed && !visibleGroups.length ? (
        <p className="muted">{t('assets.browseSearchEmpty')}</p>
      ) : (
        <div className="locationGroups">
          {visibleGroups.map(group => {
            const isOpen = expanded.has(group.id);
            const isLoading = loading.has(group.id);
            const result = cache[group.id];
            const items = result?.items ?? [];
            const total = result?.total ?? 0;
            return (
              <div className="locationGroup" key={group.id}>
                <div className={isOpen ? 'locationRow locationRow--active' : 'locationRow'}>
                  <button type="button" className="locationRow__main" aria-expanded={isOpen} onClick={() => toggle(group.id)}>
                    {isOpen ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                    {icon}
                    <span>
                      <strong>{group.label}</strong>
                      {group.sublabel && <small>{group.sublabel}</small>}
                    </span>
                  </button>
                </div>
                {isOpen && (
                  <div className="locationGroup__children">
                    {isLoading && <p className="muted"><Loader2 className="spin" size={14} /> {t('common.loading')}</p>}
                    {items.length > 0 && (
                      <div className="tableWrap tableWrap--cards">
                        <table className="dense-table">
                          <tbody>
                            {items.map(asset => {
                              const category = categories.find(item => item.id === asset.categoryId);
                              return (
                                <tr key={asset.id} tabIndex={0} onClick={() => onSelectAsset(asset.id)}>
                                  <td className="cell-icon"><div className="table-icon"><CategoryIcon icon={category?.icon} size={16} /></div></td>
                                  <td data-label={t('assets.colName')}><strong>{asset.name}</strong><small>{asset.categoryName}</small></td>
                                  <td data-label={t('assets.colTag')}>{asset.assetTag}</td>
                                  <td data-label={t('assets.statusLabel')}><StatusBadge status={asset.status} /></td>
                                  <td data-label={t('assets.colPerson')}>{asset.assignedPersonName ? <span className="personChip"><Avatar name={asset.assignedPersonName} size={22} /><span className="personChip__sep">•</span>{asset.assignedPersonName}</span> : t('common.unassigned')}</td>
                                  <td data-label={t('assets.colLocation')}>{asset.location ?? '-'}</td>
                                  <td data-label={t('assets.colValue')} style={{ textAlign: 'right' }}>{asset.purchasePrice != null ? formatMoney(asset.purchasePrice, asset.currency ?? 'PLN') : '-'}</td>
                                  <td data-label={t('assets.colWarranty')}>{formatDate(asset.warrantyUntil)}</td>
                                </tr>
                              );
                            })}
                          </tbody>
                        </table>
                      </div>
                    )}
                    {!isLoading && result && items.length === 0 && <p className="muted">{t('assets.browseGroupEmpty')}</p>}
                    {!isLoading && total > items.length && (
                      <p className="muted">{t('assets.browseMoreHint', { count: total - items.length })}</p>
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
