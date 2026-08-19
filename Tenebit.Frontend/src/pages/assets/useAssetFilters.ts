import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useDebouncedValue } from '../../hooks/useDebouncedValue';
import type { AssetStatus } from '../../types/domain';

export type AssetSortKey = 'name' | 'assetTag' | 'status' | 'person' | 'location' | 'value' | 'warranty';

export function useAssetFilters() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [search, setSearch] = useState(searchParams.get('search') ?? '');
  const [status, setStatus] = useState<AssetStatus | ''>((searchParams.get('status') as AssetStatus | null) ?? '');
  const [location, setLocation] = useState(searchParams.get('location') ?? '');
  const [team, setTeam] = useState(searchParams.get('team') ?? '');
  const [owner, setOwner] = useState(searchParams.get('owner') ?? '');
  const [warranty, setWarranty] = useState(searchParams.get('warranty') ?? '');
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState<{ key: AssetSortKey; dir: 1 | -1 } | null>(null);
  const debouncedSearch = useDebouncedValue(search.trim(), 320);

  useEffect(() => {
    setSearchParams(current => {
      const params = new URLSearchParams();
      const openAssetId = current.get('openAssetId');
      if (openAssetId) params.set('openAssetId', openAssetId);
      if (debouncedSearch) params.set('search', debouncedSearch);
      if (status) params.set('status', status);
      if (location) params.set('location', location);
      if (team) params.set('team', team);
      if (owner) params.set('owner', owner);
      if (warranty) params.set('warranty', warranty);
      return params;
    }, { replace: true });
    setPage(1);
  }, [debouncedSearch, location, owner, setSearchParams, status, team, warranty]);

  const toggleSort = useCallback((key: AssetSortKey) => {
    setSort(current => (current?.key === key ? (current.dir === 1 ? { key, dir: -1 } : null) : { key, dir: 1 }));
    setPage(1);
  }, []);

  const clearFilters = useCallback(() => {
    setSearch('');
    setStatus('');
    setLocation('');
    setTeam('');
    setOwner('');
    setWarranty('');
  }, []);

  return {
    search,
    setSearch,
    status,
    setStatus,
    location,
    setLocation,
    team,
    setTeam,
    owner,
    setOwner,
    warranty,
    setWarranty,
    page,
    setPage,
    sort,
    toggleSort,
    debouncedSearch,
    clearFilters,
    hasFilters: Boolean(debouncedSearch || status || location || team || owner || warranty),
    openAssetId: searchParams.get('openAssetId')
  };
}
