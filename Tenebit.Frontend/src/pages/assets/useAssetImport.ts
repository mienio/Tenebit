import { useCallback, useState } from 'react';

export function useAssetImport() {
  const [importOpen, setImportOpen] = useState(false);
  const openImport = useCallback(() => setImportOpen(true), []);
  const closeImport = useCallback(() => setImportOpen(false), []);
  return { importOpen, openImport, closeImport };
}
