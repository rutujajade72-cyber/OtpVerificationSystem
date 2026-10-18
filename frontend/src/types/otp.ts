/** The outcomes the API reports when a code is validated. */
export type OtpValidationStatus = 'Valid' | 'Invalid' | 'Expired' | 'NotFound' | 'AttemptsExceeded';

export interface GenerateOtpRequest {
  userId: string;
  /** ISO 8601 date and time. */
  dateTime: string;
}

export interface GenerateOtpResponse {
  userId: string;
  code: string;
  issuedAt: string;
  expiresAt: string;
  validitySeconds: number;
  /** Whole seconds left when the response was created. The countdown is anchored on this value. */
  remainingSeconds: number;
}

export interface ValidateOtpRequest {
  userId: string;
  code: string;
}

export interface ValidateOtpResponse {
  isValid: boolean;
  status: OtpValidationStatus;
  message: string;
  remainingAttempts?: number | null;
  retryAfterSeconds?: number | null;
}
