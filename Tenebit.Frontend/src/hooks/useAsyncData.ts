import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '../api/apiClient';
import { useI18n } from '../i18n/I18nProvider';

function sameDependencies(a: unknown[], b: unknown[]) {
  return a.length === b.length && a.every((value, index) => Object.is(value, b[index]));
}

export function useAsyncData<T>(loader: () => Promise<T>, dependencies: unknown[] = []) {
  const { t } = useI18n();
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const requestIdRef = useRef(0);
  const loaderRef = useRef(loader);
  const translateRef = useRef(t);
  const dependencyRef = useRef<unknown[] | null>(null);
  loaderRef.current = loader;
  translateRef.current = t;

  const reload = useCallback(() => {
    const requestId = ++requestIdRef.current;
    setIsLoading(true);
    setError(null);
    return loaderRef.current()
      .then(result => { if (requestId === requestIdRef.current) setData(result); })
      .catch((err: unknown) => {
        if (requestId === requestIdRef.current) setError(err instanceof ApiError ? err.message : translateRef.current('common.loadError'));
      })
      .finally(() => { if (requestId === requestIdRef.current) setIsLoading(false); });
  }, []);

  // Deliberately run after each render and compare dependencies ourselves. This preserves the hook's
  // existing API while avoiding a dynamic React dependency array, which React cannot statically verify.
  useEffect(() => {
    if (dependencyRef.current !== null && sameDependencies(dependencyRef.current, dependencies)) return;
    dependencyRef.current = [...dependencies];
    void reload();
  });

  return { data, error, isLoading, reload };
}
