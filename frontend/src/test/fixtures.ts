import { vi } from 'vitest';
import type { OtpApi } from '../api/otpApi';
import type { GenerateOtpResponse, ValidateOtpResponse } from '../types/otp';

export function createGenerateResponse(
  overrides: Partial<GenerateOtpResponse> = {},
): GenerateOtpResponse {
  return {
    userId: 'john.doe',
    code: '482913',
    issuedAt: '2026-10-17T10:00:00+00:00',
    expiresAt: '2026-10-17T10:00:30+00:00',
    validitySeconds: 30,
    remainingSeconds: 30,
    ...overrides,
  };
}

export function createValidateResponse(
  overrides: Partial<ValidateOtpResponse> = {},
): ValidateOtpResponse {
  return {
    isValid: true,
    status: 'Valid',
    message: 'The one-time password is valid.',
    remainingAttempts: null,
    retryAfterSeconds: null,
    ...overrides,
  };
}

/** A fake API whose two methods are mock functions the test can program and inspect. */
export function createFakeApi() {
  const generate = vi.fn<OtpApi['generate']>();
  const validate = vi.fn<OtpApi['validate']>();
  const api: OtpApi = { generate, validate };

  return { api, generate, validate };
}
