import { useCallback, useEffect, useMemo, useState } from 'react';
import type { Asset } from '../../types/domain';

export function useAssetSelection(rows: Asset[], resetKey: string) {
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const selectedAssets = useMemo(() => rows.filter(asset => selectedIds.has(asset.id)), [rows, selectedIds]);
  const allOnPageSelected = rows.length > 0 && rows.every(asset => selectedIds.has(asset.id));

  useEffect(() => {
    setSelectedIds(new Set());
  }, [resetKey]);

  const toggleSelected = useCallback((id: string) => {
    setSelectedIds(current => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  }, []);

  const toggleSelectAllOnPage = useCallback(() => {
    setSelectedIds(current => {
      const next = new Set(current);
      if (rows.length > 0 && rows.every(asset => current.has(asset.id))) rows.forEach(asset => next.delete(asset.id));
      else rows.forEach(asset => next.add(asset.id));
      return next;
    });
  }, [rows]);

  const clearSelection = useCallback(() => setSelectedIds(new Set()), []);
  const keepOnly = useCallback((ids: readonly string[]) => setSelectedIds(new Set(ids)), []);

  return { selectedIds, selectedAssets, allOnPageSelected, toggleSelected, toggleSelectAllOnPage, clearSelection, keepOnly };
}
