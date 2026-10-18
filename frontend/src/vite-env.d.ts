/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Base URL of the API. Empty means "same origin" (the Vite dev proxy forwards /api). */
  readonly VITE_API_BASE_URL?: string;
  /** Digits per code. Must match Otp:CodeLength in the backend. Defaults to 6. */
  readonly VITE_OTP_CODE_LENGTH?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
