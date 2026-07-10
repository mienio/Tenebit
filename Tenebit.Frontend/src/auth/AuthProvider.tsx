import { createContext, useContext, useMemo, useState, type ReactNode } from 'react';
import { apiRequest, setAccessTokenProvider } from '../api/apiClient';
import { clearStoredToken, decodeToken, getStoredToken, isTokenExpired, setStoredToken } from './authConfig';

type AuthUser = {
  id: string;
  organizationId: string;
  organizationName: string;
  email: string;
  displayName: string;
  roles: string[];
};

type LoginResponse = { token: string; user: AuthUser };

type AuthContextValue = {
  isAuthenticated: boolean;
  isLoading: boolean;
  userName: string;
  userEmail: string;
  organizationName: string;
  roles: string[];
  login: (email: string, password: string) => Promise<void>;
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
  return { id: payload.sub, organizationId: payload.organization_id, organizationName: payload.organization_name, email: payload.email, displayName: payload.name, roles };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => {
    const token = getStoredToken();
    if (!token) return null;
    const fromToken = userFromToken(token);
    if (!fromToken) { clearStoredToken(); return null; }
    return fromToken;
  });

  function applySession(response: LoginResponse) {
    setStoredToken(response.token);
    setUser({ ...response.user });
  }

  const value = useMemo<AuthContextValue>(() => ({
    isAuthenticated: Boolean(user),
    isLoading: false,
    userName: user?.displayName ?? '',
    userEmail: user?.email ?? '',
    organizationName: user?.organizationName ?? '',
    roles: user?.roles ?? [],
    login: async (email, password) => {
      const response = await apiRequest<LoginResponse>('/api/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) });
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
      clearStoredToken();
      setUser(null);
    }
  }), [user]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth musi być użyty wewnątrz AuthProvider.');
  return context;
}
