import { Building2, Pencil, Plus, Trash2 } from 'lucide-react';
import { useMemo } from 'react';
import type { LocationNode, LocationType } from '../types/domain';
import { locationTypeValues } from '../utils/labels';
import { useI18n } from '../i18n/I18nProvider';

const maxTreeDepth = 8;

interface LocationGroupProps {
  node: LocationNode;
  byParent: Map<string, LocationNode[]>;
  depth: number;
  typeLabel: (type: LocationType) => string;
  selectedId: string | null;
  onSelect: (id: string) => void;
  onAddChild?: (parent: LocationNode) => void;
  onEdit?: (location: LocationNode) => void;
  onDelete?: (location: LocationNode) => void;
}

function LocationGroup({ node, byParent, depth, typeLabel, selectedId, onSelect, onAddChild, onEdit, onDelete }: LocationGroupProps) {
  const { t } = useI18n();
  const children = depth < maxTreeDepth ? byParent.get(node.id) ?? [] : [];
  const canEdit = !!onEdit;
  const canDelete = !!onDelete;
  const canAddChild = !!onAddChild;

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
        {(canEdit || canDelete) && (
          <div className="rowActions">
            {canEdit && (
              <button className="iconButton" aria-label={t('common.edit')} title={t('common.edit')} onClick={() => onEdit!(node)}><Pencil size={16} /></button>
            )}
            {canDelete && (
              <button className="iconButton" aria-label={t('locations.delete')} title={t('locations.delete')} onClick={() => onDelete!(node)}><Trash2 size={16} /></button>
            )}
          </div>
        )}
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
          {canAddChild && (
            <button type="button" className="locationGroup__addChild" aria-label={t('locations.addChildAria', { name: node.name })} title={t('locations.addChildAria', { name: node.name })} onClick={() => onAddChild!(node)}>
              <Plus size={14} /> {t('locations.addChildLabel')}
            </button>
          )}
        </div>
      )}
    </div>
  );
}

export interface LocationTreeProps {
  locations: LocationNode[];
  selectedId: string | null;
  onSelect: (id: string) => void;
  onAddChild?: (parent: LocationNode) => void;
  onEdit?: (location: LocationNode) => void;
  onDelete?: (location: LocationNode) => void;
}

export function LocationTree({ locations, selectedId, onSelect, onAddChild, onEdit, onDelete }: LocationTreeProps) {
  const { t } = useI18n();
  const locationTypes: { value: LocationType; label: string }[] = locationTypeValues.map(value => ({ value, label: t(`locationType.${value}`) }));
  const typeLabel = (type: LocationType) => locationTypes.find(item => item.value === type)?.label ?? type;

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

  const roots = useMemo(() => locations.filter(location => !location.parentId), [locations]);

  if (!roots.length) return null;

  return (
    <div className="locationGroups">
      {roots.map(root => (
        <LocationGroup
          key={root.id}
          node={root}
          byParent={byParent}
          depth={0}
          typeLabel={typeLabel}
          selectedId={selectedId}
          onSelect={onSelect}
          onAddChild={onAddChild}
          onEdit={onEdit}
          onDelete={onDelete}
        />
      ))}
    </div>
  );
}
