import { useCallback, useState } from 'react';
import { toErrorMessage } from '../api/ApiError';
import { otpApi, type OtpApi } from '../api/otpApi';
import type {
  GenerateOtpRequest,
  GenerateOtpResponse,
  ValidateOtpRequest,
  ValidateOtpResponse,
} from '../types/otp';

export interface UseOtpResult {
  otp: GenerateOtpResponse | null;
  /** Client-clock timestamp at which the current password expires, or null when there is none. */
  expiresAtMs: number | null;
  isGenerating: boolean;
  generateError: string | null;
  validation: ValidateOtpResponse | null;
  isValidating: boolean;
  validateError: string | null;
  generate: (request: GenerateOtpRequest) => Promise<void>;
  validate: (request: ValidateOtpRequest) => Promise<void>;
}

/** Holds the state of the generate and validate flows and talks to the API. */
export function useOtp(api: OtpApi = otpApi): UseOtpResult {
  const [otp, setOtp] = useState<GenerateOtpResponse | null>(null);
  const [expiresAtMs, setExpiresAtMs] = useState<number | null>(null);
  const [isGenerating, setIsGenerating] = useState(false);
  const [generateError, setGenerateError] = useState<string | null>(null);
  const [validation, setValidation] = useState<ValidateOtpResponse | null>(null);
  const [isValidating, setIsValidating] = useState(false);
  const [validateError, setValidateError] = useState<string | null>(null);

  const generate = useCallback(
    async (request: GenerateOtpRequest) => {
      setIsGenerating(true);
      setGenerateError(null);

      try {
        const response = await api.generate(request);

        // The countdown is anchored on the server's remainingSeconds, not on the server's
        // timestamps, so a wrong clock on this computer cannot shorten or extend the password.
        setOtp(response);
        setExpiresAtMs(Date.now() + response.remainingSeconds * 1000);

        // A new password makes the previous validation result meaningless.
        setValidation(null);
        setValidateError(null);
      } catch (error) {
        setGenerateError(toErrorMessage(error));
      } finally {
        setIsGenerating(false);
      }
    },
    [api],
  );

  const validate = useCallback(
    async (request: ValidateOtpRequest) => {
      setIsValidating(true);
      setValidateError(null);
      setValidation(null);

      try {
        setValidation(await api.validate(request));
      } catch (error) {
        setValidateError(toErrorMessage(error));
      } finally {
        setIsValidating(false);
      }
    },
    [api],
  );

  return {
    otp,
    expiresAtMs,
    isGenerating,
    generateError,
    validation,
    isValidating,
    validateError,
    generate,
    validate,
  };
}
