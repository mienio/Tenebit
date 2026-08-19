const read = (value: string | undefined) => value?.trim() ?? '';

export const legalConfig = {
  supportEmail: read(import.meta.env.VITE_LEGAL_SUPPORT_EMAIL),
  effectiveDate: read(import.meta.env.VITE_LEGAL_EFFECTIVE_DATE) || '2026-08-18',
  termsVersion: read(import.meta.env.VITE_LEGAL_TERMS_VERSION) || '1.0'
} as const;
