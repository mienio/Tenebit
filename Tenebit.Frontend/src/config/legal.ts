const read = (value: string | undefined) => value?.trim() ?? '';

export const legalConfig = {
  operatorName: read(import.meta.env.VITE_LEGAL_OPERATOR_NAME) || 'Tenebit',
  operatorAddress: read(import.meta.env.VITE_LEGAL_OPERATOR_ADDRESS),
  operatorRegistration: read(import.meta.env.VITE_LEGAL_OPERATOR_REGISTRATION),
  operatorTaxId: read(import.meta.env.VITE_LEGAL_OPERATOR_TAX_ID),
  privacyEmail: read(import.meta.env.VITE_LEGAL_PRIVACY_EMAIL),
  supportEmail: read(import.meta.env.VITE_LEGAL_SUPPORT_EMAIL),
  effectiveDate: read(import.meta.env.VITE_LEGAL_EFFECTIVE_DATE) || '2026-08-18',
  termsVersion: read(import.meta.env.VITE_LEGAL_TERMS_VERSION) || '1.0'
} as const;

export const hasCompleteLegalOperator = Boolean(
  legalConfig.operatorName &&
  legalConfig.operatorAddress &&
  legalConfig.operatorRegistration &&
  legalConfig.operatorTaxId &&
  legalConfig.privacyEmail &&
  legalConfig.supportEmail
);
