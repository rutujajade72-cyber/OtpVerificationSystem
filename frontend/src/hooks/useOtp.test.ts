import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, GENERIC_ERROR_MESSAGE } from '../api/ApiError';
import { createFakeApi, createGenerateResponse, createValidateResponse } from '../test/fixtures';
import type { GenerateOtpResponse, ValidateOtpResponse } from '../types/otp';
import { useOtp } from './useOtp';

const generateRequest = { userId: 'john.doe', dateTime: '2026-10-16T12:30:00.000Z' };
const validateRequest = { userId: 'john.doe', code: '482913' };

beforeEach(() => {
  // Only Date is faked: promises and React behave normally, while Date.now() stays fixed.
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date('2026-10-17T10:00:00Z'));
});

afterEach(() => {
  vi.useRealTimers();
});

describe('useOtp', () => {
  it('starts idle', () => {
    const { api } = createFakeApi();

    const { result } = renderHook(() => useOtp(api));

    expect(result.current.otp).toBeNull();
    expect(result.current.expiresAtMs).toBeNull();
    expect(result.current.isGenerating).toBe(false);
    expect(result.current.generateError).toBeNull();
    expect(result.current.validation).toBeNull();
    expect(result.current.isValidating).toBe(false);
    expect(result.current.validateError).toBeNull();
  });

  describe('generate', () => {
    it('stores the password and anchors the countdown on remainingSeconds', async () => {
      const { api, generate } = createFakeApi();
      generate.mockResolvedValue(createGenerateResponse({ remainingSeconds: 28 }));
      const { result } = renderHook(() => useOtp(api));

      await act(async () => {
        await result.current.generate(generateRequest);
      });

      expect(generate).toHaveBeenCalledWith(generateRequest);
      expect(result.current.otp?.code).toBe('482913');
      expect(result.current.expiresAtMs).toBe(Date.now() + 28_000);
      expect(result.current.isGenerating).toBe(false);
      expect(result.current.generateError).toBeNull();
    });

    it('is busy while the request is pending', async () => {
      const { api, generate } = createFakeApi();
      let resolveRequest: (value: GenerateOtpResponse) => void = () => undefined;
      generate.mockReturnValue(
        new Promise<GenerateOtpResponse>((resolve) => {
          resolveRequest = resolve;
        }),
      );
      const { result } = renderHook(() => useOtp(api));
      let pending: Promise<void> = Promise.resolve();

      act(() => {
        pending = result.current.generate(generateRequest);
      });
      expect(result.current.isGenerating).toBe(true);

      await act(async () => {
        resolveRequest(createGenerateResponse());
        await pending;
      });
      expect(result.current.isGenerating).toBe(false);
    });

    it('shows the message of an ApiError', async () => {
      const { api, generate } = createFakeApi();
      generate.mockRejectedValue(new ApiError('Too many incorrect attempts.', 429));
      const { result } = renderHook(() => useOtp(api));

      await act(async () => {
        await result.current.generate(generateRequest);
      });

      expect(result.current.generateError).toBe('Too many incorrect attempts.');
      expect(result.current.otp).toBeNull();
      expect(result.current.isGenerating).toBe(false);
    });

    it('hides the details of an unexpected error', async () => {
      const { api, generate } = createFakeApi();
      generate.mockRejectedValue(new Error('internal detail'));
      const { result } = renderHook(() => useOtp(api));

      await act(async () => {
        await result.current.generate(generateRequest);
      });

      expect(result.current.generateError).toBe(GENERIC_ERROR_MESSAGE);
    });

    it('keeps the previous password when a later request fails', async () => {
      const { api, generate } = createFakeApi();
      generate.mockResolvedValueOnce(createGenerateResponse({ code: '111111' }));
      generate.mockRejectedValueOnce(new ApiError('Nope.', 500));
      const { result } = renderHook(() => useOtp(api));

      await act(async () => {
        await result.current.generate(generateRequest);
      });
      await act(async () => {
        await result.current.generate(generateRequest);
      });

      expect(result.current.otp?.code).toBe('111111');
      expect(result.current.generateError).toBe('Nope.');
    });

    it('clears an earlier error when the next request succeeds', async () => {
      const { api, generate } = createFakeApi();
      generate.mockRejectedValueOnce(new ApiError('Nope.', 500));
      generate.mockResolvedValueOnce(createGenerateResponse());
      const { result } = renderHook(() => useOtp(api));

      await act(async () => {
        await result.current.generate(generateRequest);
      });
      await act(async () => {
        await result.current.generate(generateRequest);
      });

      expect(result.current.generateError).toBeNull();
      expect(result.current.otp).not.toBeNull();
    });

    it('discards the previous validation result', async () => {
      const { api, generate, validate } = createFakeApi();
      generate.mockResolvedValue(createGenerateResponse());
      validate.mockResolvedValue(createValidateResponse());
      const { result } = renderHook(() => useOtp(api));
      await act(async () => {
        await result.current.generate(generateRequest);
      });
      await act(async () => {
        await result.current.validate(validateRequest);
      });
      expect(result.current.validation).not.toBeNull();

      await act(async () => {
        await result.current.generate(generateRequest);
      });

      expect(result.current.validation).toBeNull();
      expect(result.current.validateError).toBeNull();
    });
  });

  describe('validate', () => {
    it('stores the validation result', async () => {
      const { api, validate } = createFakeApi();
      const response = createValidateResponse({ isValid: false, status: 'Expired' });
      validate.mockResolvedValue(response);
      const { result } = renderHook(() => useOtp(api));

      await act(async () => {
        await result.current.validate(validateRequest);
      });

      expect(validate).toHaveBeenCalledWith(validateRequest);
      expect(result.current.validation).toEqual(response);
      expect(result.current.isValidating).toBe(false);
      expect(result.current.validateError).toBeNull();
    });

    it('is busy while the request is pending', async () => {
      const { api, validate } = createFakeApi();
      let resolveRequest: (value: ValidateOtpResponse) => void = () => undefined;
      validate.mockReturnValue(
        new Promise<ValidateOtpResponse>((resolve) => {
          resolveRequest = resolve;
        }),
      );
      const { result } = renderHook(() => useOtp(api));
      let pending: Promise<void> = Promise.resolve();

      act(() => {
        pending = result.current.validate(validateRequest);
      });
      expect(result.current.isValidating).toBe(true);

      await act(async () => {
        resolveRequest(createValidateResponse());
        await pending;
      });
      expect(result.current.isValidating).toBe(false);
    });

    it('shows the message of an ApiError and clears the stale result', async () => {
      const { api, validate } = createFakeApi();
      validate.mockResolvedValueOnce(createValidateResponse());
      validate.mockRejectedValueOnce(new ApiError('Unable to reach the OTP service.', 0));
      const { result } = renderHook(() => useOtp(api));
      await act(async () => {
        await result.current.validate(validateRequest);
      });

      await act(async () => {
        await result.current.validate(validateRequest);
      });

      expect(result.current.validateError).toBe('Unable to reach the OTP service.');
      expect(result.current.validation).toBeNull();
    });

    it('hides the details of an unexpected error', async () => {
      const { api, validate } = createFakeApi();
      validate.mockRejectedValue(new Error('internal detail'));
      const { result } = renderHook(() => useOtp(api));

      await act(async () => {
        await result.current.validate(validateRequest);
      });

      expect(result.current.validateError).toBe(GENERIC_ERROR_MESSAGE);
    });
  });
});
