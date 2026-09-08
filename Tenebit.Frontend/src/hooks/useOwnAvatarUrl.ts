import { useEffect, useState } from 'react';
import { api } from '../api/endpoints';
import { useAuth } from '../auth/AuthProvider';

// Refetches only when avatarVersion changes (bumped by upload/removal), not on every render - the blob
// endpoint requires an Authorization header so it cannot be used directly as an <img src>.
export function useOwnAvatarUrl(): string | null {
  const { avatarVersion } = useAuth();
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!avatarVersion) {
      setUrl(null);
      return;
    }
    let cancelled = false;
    let objectUrl: string | null = null;
    api.avatarBlob()
      .then(blob => {
        if (cancelled) return;
        objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
      })
      .catch(() => { if (!cancelled) setUrl(null); });
    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [avatarVersion]);

  return url;
}
