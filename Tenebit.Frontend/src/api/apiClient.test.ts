import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiRequest, ApiError, setAccessTokenProvider } from './apiClient';

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });
}

function createFakeLocalStorage() {
  const store = new Map<string, string>();
  return {
    getItem: (key: string) => store.get(key) ?? null,
    setItem: (key: string, value: string) => { store.set(key, value); },
    removeItem: (key: string) => { store.delete(key); },
    clear: () => store.clear(),
    key: (index: number) => Array.from(store.keys())[index] ?? null,
    get length() { return store.size; }
  } as Storage;
}

beforeEach(() => {
  vi.stubGlobal('window', { localStorage: createFakeLocalStorage() });
  setAccessTokenProvider(async () => null);
});

describe('apiRequest', () => {
  it('returns parsed JSON on success', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(200, { status: 'ok' })));

    const result = await apiRequest<{ status: string }>('/api/health');

    expect(result.status).toBe('ok');
  });

  it('returns undefined for a 204 response', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 204 })));

    const result = await apiRequest('/api/something');

    expect(result).toBeUndefined();
  });

  it('throws ApiError with the backend message and code on failure', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(400, { message: 'Nieprawidłowe dane.', code: 'VALIDATION_ERROR' })));

    await expect(apiRequest('/api/something')).rejects.toMatchObject({
      message: 'Nieprawidłowe dane.',
      code: 'VALIDATION_ERROR'
    } satisfies Partial<ApiError>);
  });

  it('retries once via /api/auth/refresh after a 401 and succeeds with the new token', async () => {
    const calls: string[] = [];
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      calls.push(url);
      if (url.includes('/api/auth/refresh')) {
        return jsonResponse(200, { token: 'fresh-token' });
      }
      if (url.includes('/api/protected')) {
        const isFirstCall = calls.filter(c => c.includes('/api/protected')).length === 1;
        return isFirstCall ? new Response(null, { status: 401 }) : jsonResponse(200, { data: 'secret' });
      }
      throw new Error(`unexpected fetch: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    const result = await apiRequest<{ data: string }>('/api/protected');

    expect(result.data).toBe('secret');
    expect(calls.filter(c => c.includes('/api/protected'))).toHaveLength(2);
    expect(calls.some(c => c.includes('/api/auth/refresh'))).toBe(true);
  });

  it('does not attempt a refresh retry for the login endpoint itself', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 401 }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(apiRequest('/api/auth/login')).rejects.toBeInstanceOf(ApiError);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
