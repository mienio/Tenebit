import { beforeEach, describe, expect, it, vi } from 'vitest';
import { clearStoredToken, decodeToken, getStoredToken, isTokenExpired, setStoredToken, type TokenPayload } from './authConfig';

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

function encodeBase64Url(value: string): string {
  return Buffer.from(value, 'utf-8').toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function buildToken(payload: Partial<TokenPayload>): string {
  const header = encodeBase64Url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const body = encodeBase64Url(JSON.stringify(payload));
  return `${header}.${body}.fake-signature`;
}

beforeEach(() => {
  vi.stubGlobal('window', { localStorage: createFakeLocalStorage() });
});

describe('token storage', () => {
  it('returns null when nothing is stored', () => {
    expect(getStoredToken()).toBeNull();
  });

  it('round-trips a stored token', () => {
    setStoredToken('abc.def.ghi');
    expect(getStoredToken()).toBe('abc.def.ghi');
  });

  it('clears the stored token', () => {
    setStoredToken('abc.def.ghi');
    clearStoredToken();
    expect(getStoredToken()).toBeNull();
  });
});

describe('decodeToken', () => {
  it('decodes a well-formed JWT payload', () => {
    const token = buildToken({ sub: 'user-1', email: 'user@acme.test', exp: 9999999999 });
    const payload = decodeToken(token);
    expect(payload?.sub).toBe('user-1');
    expect(payload?.email).toBe('user@acme.test');
  });

  it('returns null for a malformed token', () => {
    expect(decodeToken('not-a-valid-jwt')).toBeNull();
  });
});

describe('isTokenExpired', () => {
  it('treats a past exp as expired', () => {
    expect(isTokenExpired({ exp: 1 } as TokenPayload)).toBe(true);
  });

  it('treats a far-future exp as not expired', () => {
    expect(isTokenExpired({ exp: 9999999999 } as TokenPayload)).toBe(false);
  });
});
