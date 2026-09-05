const read = (value: string | undefined) => value?.trim() ?? '';

// Split so the address never sits as a single scannable "x@y.z" token in source or the built JS bundle -
// raises the bar against basic scrapers that regex-scan static text, though anything rendering the DOM still sees it.
const DEFAULT_SUPPORT_EMAIL = ['hello', 'teneb.it'].join('@');

export const legalConfig = {
  supportEmail: read(import.meta.env.VITE_LEGAL_SUPPORT_EMAIL) || DEFAULT_SUPPORT_EMAIL,
  effectiveDate: read(import.meta.env.VITE_LEGAL_EFFECTIVE_DATE) || '2026-08-18',
  // 1.1: doprecyzowany wspólny limit planu (licencje, zespoły, zestawy stanowiskowe, kategorie),
  // dodany pułap fair-use 200 pól własnych na kategorię oraz wersje włoska i francuska dokumentów.
  termsVersion: read(import.meta.env.VITE_LEGAL_TERMS_VERSION) || '1.1'
} as const;
