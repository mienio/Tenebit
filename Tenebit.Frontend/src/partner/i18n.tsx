import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { setPartnerLanguage } from './partnerApi';

export type PartnerLocale = 'en' | 'pl';

const bases: Record<PartnerLocale, string> = { en: '/partner', pl: '/partner/pl' };

export function partnerRoutePath(locale: PartnerLocale, subpath: string): string {
  return subpath ? `${bases[locale]}/${subpath}` : bases[locale];
}

interface PartnerLocaleContextValue {
  locale: PartnerLocale;
  /** Builds an absolute route path under the current locale, e.g. path('login') -> '/partner/login' or '/partner/pl/login'. */
  path: (subpath?: string) => string;
}

const PartnerLocaleContext = createContext<PartnerLocaleContextValue>({
  locale: 'en',
  path: subpath => partnerRoutePath('en', subpath ?? ''),
});

export function PartnerLocaleProvider({ locale, children }: { locale: PartnerLocale; children: ReactNode }) {
  // Set synchronously during render, not in an effect: child components read this on mount (their
  // own useEffect data fetches), which React runs before a parent's useEffect ever fires.
  setPartnerLanguage(locale);
  const value = useMemo<PartnerLocaleContextValue>(() => ({
    locale,
    path: subpath => partnerRoutePath(locale, subpath ?? ''),
  }), [locale]);
  return <PartnerLocaleContext.Provider value={value}>{children}</PartnerLocaleContext.Provider>;
}

export function usePartnerLocale(): PartnerLocaleContextValue {
  return useContext(PartnerLocaleContext);
}

/** Link that switches to the equivalent page in the other language, preserving the current subpath. */
export function PartnerLanguageSwitch({ className }: { className?: string }) {
  const { locale } = usePartnerLocale();
  const location = useLocation();
  const other: PartnerLocale = locale === 'en' ? 'pl' : 'en';
  const currentBase = bases[locale];
  const rest = location.pathname === currentBase ? '' : location.pathname.slice(currentBase.length);
  const target = `${bases[other]}${rest}${location.search}`;
  return (
    <Link to={target} className={className} lang={other}>
      {other === 'en' ? 'English' : 'Polski'}
    </Link>
  );
}
