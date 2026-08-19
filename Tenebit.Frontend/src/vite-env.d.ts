/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
  readonly VITE_AUTH_ENABLED?: string;
  readonly VITE_AUTH_AUTHORITY?: string;
  readonly VITE_AUTH_CLIENT_ID?: string;
  readonly VITE_AUTH_REDIRECT_URI?: string;
  readonly VITE_AUTH_POST_LOGOUT_REDIRECT_URI?: string;
  readonly VITE_AUTH_SCOPE?: string;
  readonly VITE_LEGAL_OPERATOR_NAME?: string;
  readonly VITE_LEGAL_OPERATOR_ADDRESS?: string;
  readonly VITE_LEGAL_OPERATOR_REGISTRATION?: string;
  readonly VITE_LEGAL_OPERATOR_TAX_ID?: string;
  readonly VITE_LEGAL_PRIVACY_EMAIL?: string;
  readonly VITE_LEGAL_SUPPORT_EMAIL?: string;
  readonly VITE_LEGAL_EFFECTIVE_DATE?: string;
  readonly VITE_LEGAL_TERMS_VERSION?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
