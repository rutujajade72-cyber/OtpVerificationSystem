const rawBaseUrl = import.meta.env.VITE_API_BASE_URL ?? '';

/** Base URL of the API without a trailing slash. Empty means "same origin". */
export const API_BASE_URL: string = rawBaseUrl.replace(/\/+$/, '');

const rawCodeLength = Number(import.meta.env.VITE_OTP_CODE_LENGTH);

/**
 * Digits in a one-time password. Keep in sync with Otp:CodeLength in the backend (default 6).
 * The validator uses it until a password has been generated; after that it uses the real length.
 */
export const OTP_CODE_LENGTH: number =
  Number.isInteger(rawCodeLength) && rawCodeLength >= 4 && rawCodeLength <= 9 ? rawCodeLength : 6;

/** Keep in sync with OtpConstraints.MaxUserIdLength in the backend. */
export const MAX_USER_ID_LENGTH = 30;

/** The validity the backend is configured with by default. Used only for the page text. */
export const DEFAULT_VALIDITY_SECONDS = 30;

/** How often the countdown redraws. A quarter second keeps the displayed second accurate. */
export const COUNTDOWN_TICK_MS = 250;

/** The countdown switches to its warning style when this many seconds or fewer remain. */
export const ENDING_SOON_SECONDS = 10;
