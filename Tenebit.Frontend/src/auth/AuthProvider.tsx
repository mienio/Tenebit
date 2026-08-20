import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
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
export type RegisterOutcome = { requiresEmailVerification: boolean };

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
  completeTwoFactorLogin: (challengeToken: string, code: string, rememberDevice: boolean) => Promise<void>;
  register: (organizationName: string, displayName: string, email: string, password: string, currency: string, language: string, acceptTerms: boolean) => Promise<RegisterOutcome>;
  loginWithToken: (token: string) => boolean;
  completeExternalLogin: () => Promise<boolean>;
  logout: () => Promise<boolean>;
  updateDisplayName: (displayName: string) => Promise<void>;
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
  const initialUser = useRef(user).current;
  const [isLoading, setIsLoading] = useState(initialUser === null);
  const bootstrapRefresh = useRef<Promise<void> | null>(null);

  useEffect(() => {
    const onSessionExpired = () => {
      clearStoredToken();
      setUser(null);
      setIsLoading(false);
    };
    window.addEventListener('tenebit:session-expired', onSessionExpired);
    return () => window.removeEventListener('tenebit:session-expired', onSessionExpired);
  }, []);

  useEffect(() => {
    if (initialUser) {
      setIsLoading(false);
      return;
    }

    // Bootstrap exactly once. A later explicit logout/session-expiry must not trigger an automatic
    // refresh that could silently log the user back in while the server-side revoke is in flight.
    if (!bootstrapRefresh.current) {
      bootstrapRefresh.current = refreshAccessToken().then(token => {
        const fromToken = token ? userFromToken(token) : null;
        if (fromToken) setUser(fromToken);
        setIsLoading(false);
      });
    }
  }, [initialUser]);

  const applySession = useCallback((response: LoginResponse) => {
    setStoredToken(response.token);
    setUser({ ...response.user });
    setIsLoading(false);
  }, []);

  const completeExternalLogin = useCallback(async () => {
    // The refresh cookie established by the OAuth callback is the only credential accepted here.
    // Discard any access token from a previously signed-in account before exchanging that cookie.
    clearStoredToken();
    setUser(null);
    setIsLoading(true);

    const token = await refreshAccessToken();
    const fromToken = token ? userFromToken(token) : null;
    if (!fromToken) {
      clearStoredToken();
      setUser(null);
      setIsLoading(false);
      return false;
    }

    setUser(fromToken);
    setIsLoading(false);
    return true;
  }, []);

  const logout = useCallback(async () => {
    // Start the revoke while the current bearer token and refresh cookie are still available, but
    // clear local state immediately. The one-shot bootstrap effect above will not re-login the user.
    const revoke = apiRequest('/api/auth/logout', { method: 'POST' });
    clearStoredToken();
    setUser(null);
    setIsLoading(false);

    try {
      await revoke;
      return true;
    } catch {
      return false;
    }
  }, []);

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
    completeTwoFactorLogin: async (challengeToken, code, rememberDevice) => {
      const response = await apiRequest<LoginResponse>('/api/auth/login/2fa', { method: 'POST', body: JSON.stringify({ challengeToken, code, rememberDevice }) });
      applySession(response);
    },
    register: async (organizationName, displayName, email, password, currency, language, acceptTerms) =>
      apiRequest<RegisterOutcome>('/api/auth/register', {
        method: 'POST',
        body: JSON.stringify({ organizationName, displayName, email, password, currency, language, acceptTerms })
      }),
    loginWithToken: (token: string) => {
      const fromToken = userFromToken(token);
      if (!fromToken) return false;
      setStoredToken(token);
      setUser(fromToken);
      setIsLoading(false);
      return true;
    },
    updateDisplayName: async (displayName: string) => {
      const response = await apiRequest<{ token: string }>('/api/auth/display-name', { method: 'PUT', body: JSON.stringify({ displayName }) });
      const fromToken = userFromToken(response.token);
      if (fromToken) {
        setStoredToken(response.token);
        setUser(fromToken);
      }
    },
    completeExternalLogin,
    logout
  }), [applySession, completeExternalLogin, isLoading, logout, user]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth musi być użyty wewnątrz AuthProvider.');
  return context;
}
