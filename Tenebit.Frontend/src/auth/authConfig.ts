const TOKEN_KEY = 'tenebit_token';

// Access token lives only in memory (XSS cannot read it from storage); after a page
// reload the session is restored via the HttpOnly refresh cookie. The localStorage
// entry is only removed here to clean up sessions created by older builds.
let accessToken: string | null = null;
let legacyCleared = false;

function clearLegacyStorage() {
  if (legacyCleared) return;
  legacyCleared = true;
  try { window.localStorage.removeItem(TOKEN_KEY); } catch { /* storage unavailable */ }
}

export function getStoredToken(): string | null {
  clearLegacyStorage();
  return accessToken;
}

export function setStoredToken(token: string) {
  clearLegacyStorage();
  accessToken = token;
}

export function clearStoredToken() {
  accessToken = null;
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
