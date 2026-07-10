import { useCallback, useEffect, useState } from 'react';
import { ApiError } from '../api/apiClient';

export function useAsyncData<T>(loader: () => Promise<T>, dependencies: unknown[] = []) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const reload = useCallback(() => {
    setIsLoading(true);
    setError(null);
    loader()
      .then(setData)
      .catch((err: unknown) => setError(err instanceof ApiError ? err.message : 'Wystąpił błąd podczas pobierania danych.'))
      .finally(() => setIsLoading(false));
  }, dependencies);

  useEffect(() => { reload(); }, [reload]);

  return { data, error, isLoading, reload };
}
