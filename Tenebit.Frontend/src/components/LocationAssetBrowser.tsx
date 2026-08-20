import { Building2, ChevronDown, ChevronRight, Loader2 } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { api } from '../api/endpoints';
import { Avatar } from './Avatar';
import { TextInput } from './FormFields';
import { StatusBadge } from './StatusBadge';
import { useI18n } from '../i18n/I18nProvider';
import type { AssetCategory, LocationInventory, LocationNode } from '../types/domain';
import { CategoryIcon } from '../utils/categoryIcons';
import { formatDate, formatMoney } from '../utils/format';

interface LocationAssetNodeProps {
  node: LocationNode;
  byParent: Map<string, LocationNode[]>;
  byId: Map<string, LocationNode>;
  expanded: Set<string>;
  loading: Set<string>;
  inventory: Record<string, LocationInventory>;
  categories: AssetCategory[];
  visibleIds: Set<string> | null;
  ancestorIds: Set<string>;
  onToggle: (node: LocationNode) => void;
  onSelectAsset: (assetId: string) => void;
}

function LocationAssetNode({ node, byParent, byId, expanded, loading, inventory, categories, visibleIds, ancestorIds, onToggle, onSelectAsset }: LocationAssetNodeProps) {
  const { t } = useI18n();
  if (visibleIds !== null && !visibleIds.has(node.id)) return null;

  const isOpen = expanded.has(node.id) || (visibleIds !== null && ancestorIds.has(node.id));
  const isLoading = loading.has(node.id);
  const inv = inventory[node.id];
  const childLocations = byParent.get(node.id) ?? [];
  const assetsHere = inv?.assets ?? [];
  const showEmpty = isOpen && !isLoading && childLocations.length === 0 && assetsHere.length === 0;

  return (
    <div className="locationGroup">
      <div className={isOpen ? 'locationRow locationRow--active' : 'locationRow'}>
        <button
          type="button"
          className="locationRow__main"
          aria-expanded={isOpen}
          onClick={() => onToggle(node)}
        >
          {isOpen ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
          <Building2 size={16} />
          <span>
            <strong>{node.name}</strong>
            <small>{t('locations.assetsCount', { count: node.assetCount })} · {t('locations.peopleCount', { count: node.personCount })}</small>
          </span>
        </button>
      </div>
      {isOpen && (
        <div className="locationGroup__children">
          {isLoading && (
            <p className="muted"><Loader2 className="spin" size={14} /> {t('common.loading')}</p>
          )}
          {childLocations.map(child => (
            <LocationAssetNode
              key={child.id}
              node={child}
              byParent={byParent}
              byId={byId}
              expanded={expanded}
              loading={loading}
              inventory={inventory}
              categories={categories}
              visibleIds={visibleIds}
              ancestorIds={ancestorIds}
              onToggle={onToggle}
              onSelectAsset={onSelectAsset}
            />
          ))}
          {assetsHere.length > 0 && (
            <div className="tableWrap tableWrap--cards">
              <table className="dense-table">
                <tbody>
                  {assetsHere.map(asset => {
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
          {showEmpty && (
            <p className="muted">{t('assets.locationEmpty')}</p>
          )}
        </div>
      )}
    </div>
  );
}

export function LocationAssetBrowser({ locations, categories, onSelectAsset }: { locations: LocationNode[]; categories: AssetCategory[]; onSelectAsset: (assetId: string) => void }) {
  const { t } = useI18n();
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set());
  const [loading, setLoading] = useState<Set<string>>(() => new Set());
  const [inventory, setInventory] = useState<Record<string, LocationInventory>>({});
  const [search, setSearch] = useState('');

  const byParent = useMemo(() => {
    const map = new Map<string, LocationNode[]>();
    for (const location of locations) {
      if (!location.parentId) continue;
      const siblings = map.get(location.parentId) ?? [];
      siblings.push(location);
      map.set(location.parentId, siblings);
    }
    return map;
  }, [locations]);

  const byId = useMemo(() => {
    const map = new Map<string, LocationNode>();
    for (const location of locations) map.set(location.id, location);
    return map;
  }, [locations]);

  const roots = useMemo(() => locations.filter(location => !location.parentId), [locations]);

  const trimmed = search.trim().toLowerCase();
  const { matchingIds, ancestorIds, visibleIds } = useMemo(() => {
    if (!trimmed) {
      return { matchingIds: new Set<string>(), ancestorIds: new Set<string>(), visibleIds: null as Set<string> | null };
    }
    const matching = new Set<string>();
    const ancestors = new Set<string>();
    for (const location of locations) {
      if (location.name.toLowerCase().includes(trimmed)) matching.add(location.id);
    }
    for (const id of matching) {
      let current = byId.get(id);
      while (current?.parentId) {
        if (ancestors.has(current.parentId)) break;
        ancestors.add(current.parentId);
        current = byId.get(current.parentId);
      }
    }
    const visible = new Set<string>([...matching, ...ancestors]);
    return { matchingIds: matching, ancestorIds: ancestors, visibleIds: visible };
  }, [trimmed, locations, byId]);

  function toggleNode(node: LocationNode) {
    setExpanded(prev => {
      const next = new Set(prev);
      if (next.has(node.id)) next.delete(node.id);
      else next.add(node.id);
      return next;
    });
  }

  useEffect(() => {
    const idsToLoad = Array.from(expanded).filter(id => !inventory[id] && !loading.has(id));
    if (!idsToLoad.length) return;
    const controllers: AbortController[] = [];
    for (const id of idsToLoad) {
      setLoading(prev => new Set(prev).add(id));
      const controller = new AbortController();
      controllers.push(controller);
      api.locationInventory(id)
        .then(result => {
          setInventory(prev => ({ ...prev, [id]: result }));
        })
        .catch(() => {})
        .finally(() => {
          setLoading(prev => {
            const next = new Set(prev);
            next.delete(id);
            return next;
          });
        });
    }
    return () => { for (const c of controllers) c.abort(); };
  }, [expanded, inventory, loading]);

  if (!roots.length) {
    return <p className="muted">{t('assets.noLocationsYet')}</p>;
  }

  return (
    <div>
      <div style={{ marginBottom: '12px' }}>
        <TextInput value={search} onChange={event => setSearch(event.target.value)} placeholder={t('assets.locationSearchPlaceholder')} />
      </div>
      {visibleIds !== null && matchingIds.size === 0 ? (
        <p className="muted">{t('assets.locationSearchEmpty')}</p>
      ) : (
        <div className="locationGroups">
          {roots.map(root => (
            <LocationAssetNode
              key={root.id}
              node={root}
              byParent={byParent}
              byId={byId}
              expanded={expanded}
              loading={loading}
              inventory={inventory}
              categories={categories}
              visibleIds={visibleIds}
              ancestorIds={ancestorIds}
              onToggle={toggleNode}
              onSelectAsset={onSelectAsset}
            />
          ))}
        </div>
      )}
    </div>
  );
}
