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

export const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000').replace(/\/$/, '');

export async function apiRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  const hasBody = init.body !== undefined && init.body !== null;

  if (hasBody && !headers.has('Content-Type') && !(init.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json');
  }

  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json');
  }

  const token = tokenProvider ? await tokenProvider() : null;
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  if (languageProvider) {
    headers.set('X-Ui-Language', languageProvider());
  }

  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}${path}`, { ...init, headers });
  } catch {
    throw new ApiError('Nie można połączyć się z backendem. Sprawdź VITE_API_BASE_URL i działanie API.');
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
      : 'Operacja zakończyła się błędem.';
    const code = typeof data === 'object' && data && 'code' in data
      ? String((data as { code: unknown }).code)
      : 'HTTP_ERROR';
    throw new ApiError(message, response.status, code);
  }

  return data as T;
}

export async function apiBlob(path: string): Promise<Blob> {
  const headers = new Headers();
  const token = tokenProvider ? await tokenProvider() : null;
  if (token) headers.set('Authorization', `Bearer ${token}`);
  if (languageProvider) headers.set('X-Ui-Language', languageProvider());
  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}${path}`, { headers });
  } catch {
    throw new ApiError('Nie można połączyć się z backendem.');
  }
  if (!response.ok) throw new ApiError('Nie udało się pobrać pliku.', response.status, 'HTTP_ERROR');
  return response.blob();
}
