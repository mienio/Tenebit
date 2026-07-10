import { createContext, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { apiRequest, refreshAccessToken, setAccessTokenProvider } from '../api/apiClient';
import { clearStoredToken, decodeToken, getStoredToken, isTokenExpired, setStoredToken } from './authConfig';

type AuthUser = {
  id: string;
  organizationId: string;
  organizationName: string;
  email: string;
  displayName: string;
  roles: string[];
  isEmailVerified: boolean;
  isTwoFactorEnabled: boolean;
};

type LoginResponse = { token: string; user: AuthUser };
type LoginStartResponse = LoginResponse | { requiresTwoFactor: true; challengeToken: string };
export type LoginOutcome = { requiresTwoFactor: true; challengeToken: string } | { requiresTwoFactor: false };

type AuthContextValue = {
  isAuthenticated: boolean;
  isLoading: boolean;
  userName: string;
  userEmail: string;
  organizationName: string;
  roles: string[];
  isEmailVerified: boolean;
  isTwoFactorEnabled: boolean;
  login: (email: string, password: string) => Promise<LoginOutcome>;
  completeTwoFactorLogin: (challengeToken: string, code: string) => Promise<void>;
  register: (organizationName: string, displayName: string, email: string, password: string, currency: string, language: string) => Promise<void>;
  loginWithToken: (token: string) => boolean;
  logout: () => void;
};

const AuthContext = createContext<AuthContextValue | null>(null);

setAccessTokenProvider(async () => getStoredToken());

function userFromToken(token: string): AuthUser | null {
  const payload = decodeToken(token);
  if (!payload || isTokenExpired(payload)) return null;
  const roles = Array.isArray(payload.roles) ? payload.roles : payload.roles ? [payload.roles] : [];
  return {
    id: payload.sub,
    organizationId: payload.organization_id,
    organizationName: payload.organization_name,
    email: payload.email,
    displayName: payload.name,
    roles,
    isEmailVerified: payload.email_verified === 'true',
    isTwoFactorEnabled: payload.two_factor_enabled === 'true'
  };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => {
    const token = getStoredToken();
    if (!token) return null;
    const fromToken = userFromToken(token);
    if (!fromToken) { clearStoredToken(); return null; }
    return fromToken;
  });
  const [isLoading, setIsLoading] = useState(user === null);
  const attemptedRefresh = useRef(false);

  useEffect(() => {
    if (user || attemptedRefresh.current) { setIsLoading(false); return; }
    attemptedRefresh.current = true;
    let cancelled = false;
    refreshAccessToken().then(token => {
      if (cancelled) return;
      const fromToken = token ? userFromToken(token) : null;
      if (fromToken) setUser(fromToken);
      setIsLoading(false);
    });
    return () => { cancelled = true; };
  }, [user]);

  function applySession(response: LoginResponse) {
    setStoredToken(response.token);
    setUser({ ...response.user });
  }

  const value = useMemo<AuthContextValue>(() => ({
    isAuthenticated: Boolean(user),
    isLoading,
    userName: user?.displayName ?? '',
    userEmail: user?.email ?? '',
    organizationName: user?.organizationName ?? '',
    roles: user?.roles ?? [],
    isEmailVerified: user?.isEmailVerified ?? true,
    isTwoFactorEnabled: user?.isTwoFactorEnabled ?? false,
    login: async (email, password) => {
      const response = await apiRequest<LoginStartResponse>('/api/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) });
      if ('requiresTwoFactor' in response && response.requiresTwoFactor) {
        return { requiresTwoFactor: true, challengeToken: response.challengeToken };
      }
      applySession(response as LoginResponse);
      return { requiresTwoFactor: false };
    },
    completeTwoFactorLogin: async (challengeToken, code) => {
      const response = await apiRequest<LoginResponse>('/api/auth/login/2fa', { method: 'POST', body: JSON.stringify({ challengeToken, code }) });
      applySession(response);
    },
    register: async (organizationName, displayName, email, password, currency, language) => {
      const response = await apiRequest<LoginResponse>('/api/auth/register', { method: 'POST', body: JSON.stringify({ organizationName, displayName, email, password, currency, language }) });
      applySession(response);
    },
    loginWithToken: (token: string) => {
      const fromToken = userFromToken(token);
      if (!fromToken) return false;
      setStoredToken(token);
      setUser(fromToken);
      return true;
    },
    logout: () => {
      apiRequest('/api/auth/logout', { method: 'POST' }).catch(() => {});
      clearStoredToken();
      setUser(null);
    }
  }), [user, isLoading]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth musi być użyty wewnątrz AuthProvider.');
  return context;
}
