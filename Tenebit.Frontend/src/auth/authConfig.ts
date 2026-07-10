const TOKEN_KEY = 'tenebit_token';

export function getStoredToken(): string | null {
  return window.localStorage.getItem(TOKEN_KEY);
}

export function setStoredToken(token: string) {
  window.localStorage.setItem(TOKEN_KEY, token);
}

export function clearStoredToken() {
  window.localStorage.removeItem(TOKEN_KEY);
}

export type TokenPayload = {
  sub: string;
  organization_id: string;
  organization_name: string;
  name: string;
  email: string;
  email_verified?: string;
  two_factor_enabled?: string;
  roles?: string | string[];
  exp: number;
};

export function decodeToken(token: string): TokenPayload | null {
  try {
    const [, payload] = token.split('.');
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    return JSON.parse(json) as TokenPayload;
  } catch {
    return null;
  }
}

export function isTokenExpired(payload: TokenPayload): boolean {
  return payload.exp * 1000 <= Date.now();
}
