import { useCallback, useEffect, useState } from 'react';

export type AssetColumnKey = 'category' | 'assetTag' | 'status' | 'person' | 'location' | 'value' | 'warranty';

export const ASSET_COLUMNS: { key: AssetColumnKey; labelKey: string }[] = [
  { key: 'category', labelKey: 'assets.colCategory' },
  { key: 'assetTag', labelKey: 'assets.colTag' },
  { key: 'status', labelKey: 'assets.statusLabel' },
  { key: 'person', labelKey: 'assets.colPerson' },
  { key: 'location', labelKey: 'assets.colLocation' },
  { key: 'value', labelKey: 'assets.colValue' },
  { key: 'warranty', labelKey: 'assets.colWarranty' },
];

// Value is off by default: it is the column people most often do not want on screen (and it is still
// one click away in the detail panel). Category is off because the row already carries its icon.
const DEFAULTS: Record<AssetColumnKey, boolean> = {
  category: false,
  assetTag: true,
  status: true,
  person: true,
  location: true,
  value: false,
  warranty: true,
};

const STORAGE_KEY = 'tenebit_asset_columns';

/**
 * Per-viewer choice of which asset columns are shown.
 *
 * Kept in localStorage rather than on the server: it is a personal display preference, not tenant data,
 * so it needs no migration, no API round trip, and no cross-user coordination. Reads and writes are
 * wrapped because storage throws outright in some privacy modes.
 */
export function useAssetColumns() {
  const [visible, setVisible] = useState<Record<AssetColumnKey, boolean>>(() => {
    try {
      const raw = window.localStorage.getItem(STORAGE_KEY);
      if (!raw) return DEFAULTS;
      const parsed = JSON.parse(raw) as Partial<Record<AssetColumnKey, boolean>>;
      // Merge over defaults so a column added in a later release appears instead of silently vanishing.
      return { ...DEFAULTS, ...parsed };
    } catch {
      return DEFAULTS;
    }
  });

  useEffect(() => {
    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(visible));
    } catch {
      // Storage unavailable - the choice simply does not persist across reloads.
    }
  }, [visible]);

  const toggle = useCallback((key: AssetColumnKey) => {
    setVisible(current => ({ ...current, [key]: !current[key] }));
  }, []);

  const reset = useCallback(() => setVisible(DEFAULTS), []);

  return { visible, toggle, reset };
}
