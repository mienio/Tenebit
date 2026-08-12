import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '../api/apiClient';
import { useI18n } from '../i18n/I18nProvider';

export function useAsyncData<T>(loader: () => Promise<T>, dependencies: unknown[] = []) {
  const { t } = useI18n();
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const requestIdRef = useRef(0);

  const reload = useCallback(() => {
    const requestId = ++requestIdRef.current;
    setIsLoading(true);
    setError(null);
    return loader()
      .then(result => { if (requestId === requestIdRef.current) setData(result); })
      .catch((err: unknown) => {
        if (requestId === requestIdRef.current) setError(err instanceof ApiError ? err.message : t('common.loadError'));
      })
      .finally(() => { if (requestId === requestIdRef.current) setIsLoading(false); });
  }, dependencies);

  useEffect(() => { reload(); }, [reload]);

  return { data, error, isLoading, reload };
}
