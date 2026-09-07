import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import {
  getMyProfile, partnerLogin, partnerLogout, partnerTryRestoreSession, type AffiliateProfile,
} from './partnerApi';

type PartnerAuthContextValue = {
  isLoading: boolean;
  isAuthenticated: boolean;
  affiliate: AffiliateProfile | null;
  login: (email: string, password: string) => Promise<AffiliateProfile>;
  logout: () => Promise<void>;
  refreshProfile: () => Promise<void>;
};

const PartnerAuthContext = createContext<PartnerAuthContextValue | null>(null);

export function PartnerAuthProvider({ children }: { children: ReactNode }) {
  const [affiliate, setAffiliate] = useState<AffiliateProfile | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    partnerTryRestoreSession()
      .then(profile => { if (!cancelled) setAffiliate(profile); })
      .finally(() => { if (!cancelled) setIsLoading(false); });
    return () => { cancelled = true; };
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const profile = await partnerLogin(email, password);
    setAffiliate(profile);
    return profile;
  }, []);

  const logout = useCallback(async () => {
    await partnerLogout();
    setAffiliate(null);
  }, []);

  const refreshProfile = useCallback(async () => {
    setAffiliate(await getMyProfile());
  }, []);

  const value = useMemo<PartnerAuthContextValue>(() => ({
    isLoading, isAuthenticated: affiliate !== null, affiliate, login, logout, refreshProfile,
  }), [isLoading, affiliate, login, logout, refreshProfile]);

  return <PartnerAuthContext.Provider value={value}>{children}</PartnerAuthContext.Provider>;
}

export function usePartnerAuth(): PartnerAuthContextValue {
  const context = useContext(PartnerAuthContext);
  if (!context) throw new Error('usePartnerAuth must be used within PartnerAuthProvider');
  return context;
}
