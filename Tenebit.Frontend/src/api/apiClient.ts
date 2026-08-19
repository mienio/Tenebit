import { setStoredToken } from '../auth/authConfig';
import { translations, type Language } from '../i18n/translations';

export class ApiError extends Error {
  status: number;
  code: string;

  constructor(message: string, status = 0, code = 'NETWORK_ERROR') {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

type TokenProvider = () => Promise<string | null>;
type LanguageProvider = () => string;

let tokenProvider: TokenProvider | null = null;
let languageProvider: LanguageProvider | null = null;

export function setAccessTokenProvider(provider: TokenProvider | null) {
  tokenProvider = provider;
}

export function setLanguageProvider(provider: LanguageProvider | null) {
  languageProvider = provider;
}

function tApi(key: string): string {
  const language = (languageProvider ? languageProvider() : 'en') as Language;
  return translations[language]?.[key] ?? translations.en[key] ?? key;
}

export const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');

let refreshPromise: Promise<string | null> | null = null;

export function refreshAccessToken(): Promise<string | null> {
  if (!refreshPromise) {
    refreshPromise = fetch(`${apiBaseUrl}/api/auth/refresh`, { method: 'POST', credentials: 'include' })
      .then(async res => {
        if (!res.ok) return null;
        const data = await res.json().catch(() => null);
        if (typeof data?.token !== 'string') return null;
        setStoredToken(data.token);
        return data.token as string;
      })
      .catch(() => null)
      .finally(() => { refreshPromise = null; });
  }
  return refreshPromise;
}

async function performFetch(path: string, init: RequestInit, token: string | null): Promise<Response> {
  const headers = new Headers(init.headers);
  const hasBody = init.body !== undefined && init.body !== null;

  if (hasBody && !headers.has('Content-Type') && !(init.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json');
  }

  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json');
  }

  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  if (languageProvider) {
    headers.set('X-Ui-Language', languageProvider());
  }

  try {
    return await fetch(`${apiBaseUrl}${path}`, { ...init, headers, credentials: 'include' });
  } catch {
    throw new ApiError(tApi('api.networkError'));
  }
}

export async function apiRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = tokenProvider ? await tokenProvider() : null;
  let response = await performFetch(path, init, token);

  if (response.status === 401 && path !== '/api/auth/refresh' && !path.startsWith('/api/auth/login') && !path.startsWith('/api/auth/register')) {
    const newToken = await refreshAccessToken();
    if (newToken) {
      response = await performFetch(path, init, newToken);
    } else if (token) {
      window.dispatchEvent(new Event('tenebit:session-expired'));
    }
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const contentType = response.headers.get('content-type') ?? '';
  const raw = await response.text();
  const data = raw && contentType.includes('application/json') ? JSON.parse(raw) : raw;

  if (!response.ok) {
    const message = typeof data === 'object' && data && 'message' in data
      ? String((data as { message: unknown }).message)
      : tApi('api.genericError');
    const code = typeof data === 'object' && data && 'code' in data
      ? String((data as { code: unknown }).code)
      : 'HTTP_ERROR';
    throw new ApiError(message, response.status, code);
  }

  return data as T;
}

export async function apiBlob(path: string): Promise<Blob> {
  const token = tokenProvider ? await tokenProvider() : null;
  const init: RequestInit = { headers: { Accept: 'application/octet-stream,*/*' } };
  let response = await performFetch(path, init, token);

  if (response.status === 401) {
    const newToken = await refreshAccessToken();
    if (newToken) {
      response = await performFetch(path, init, newToken);
    } else if (token) {
      window.dispatchEvent(new Event('tenebit:session-expired'));
    }
  }

  if (!response.ok) throw new ApiError(tApi('api.downloadFailed'), response.status, 'HTTP_ERROR');
  return response.blob();
}
