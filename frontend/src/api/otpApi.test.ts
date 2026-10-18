import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { GenerateOtpResponse, ValidateOtpResponse } from '../types/otp';
import { ApiError } from './ApiError';
import { otpApi } from './otpApi';

const generateResponse: GenerateOtpResponse = {
  userId: 'john.doe',
  code: '482913',
  issuedAt: '2026-10-17T10:00:00+00:00',
  expiresAt: '2026-10-17T10:00:30+00:00',
  validitySeconds: 30,
  remainingSeconds: 30,
};

const validateResponse: ValidateOtpResponse = {
  isValid: true,
  status: 'Valid',
  message: 'The one-time password is valid.',
  remainingAttempts: null,
  retryAfterSeconds: null,
};

const fetchMock = vi.fn<typeof fetch>();

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

async function captureError(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise;
  } catch (error) {
    if (error instanceof ApiError) {
      return error;
    }
    throw error;
  }

  throw new Error('Expected the promise to reject.');
}

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal('fetch', fetchMock);
});

describe('otpApi.generate', () => {
  it('posts the request as JSON to the generate endpoint', async () => {
    fetchMock.mockResolvedValue(jsonResponse(generateResponse));
    const request = { userId: 'john.doe', dateTime: '2026-10-16T12:30:00.000Z' };

    await otpApi.generate(request);

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/otp/generate',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify(request),
        headers: expect.objectContaining({ 'Content-Type': 'application/json' }),
      }),
    );
  });

  it('returns the parsed response', async () => {
    fetchMock.mockResolvedValue(jsonResponse(generateResponse));

    const result = await otpApi.generate({ userId: 'john.doe', dateTime: '2026-10-16T12:30:00Z' });

    expect(result).toEqual(generateResponse);
  });

  it('joins field errors of a validation problem into one message', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(
        {
          title: 'One or more validation errors occurred.',
          errors: {
            UserId: ['The UserId field is required.'],
            DateTime: ['The DateTime field is required.'],
          },
        },
        400,
      ),
    );

    const error = await captureError(otpApi.generate({ userId: '', dateTime: '' }));

    expect(error.status).toBe(400);
    expect(error.message).toBe('The UserId field is required. The DateTime field is required.');
  });

  it('prefers the detail of a problem response over its title', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(
        {
          title: 'Too many incorrect attempts.',
          detail: 'Too many incorrect attempts. Please try again in 42 second(s).',
        },
        429,
      ),
    );

    const error = await captureError(otpApi.generate({ userId: 'a', dateTime: 'b' }));

    expect(error.status).toBe(429);
    expect(error.message).toBe('Too many incorrect attempts. Please try again in 42 second(s).');
  });

  it('falls back to the title when there is no detail', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ title: 'An unexpected error occurred.' }, 500));

    const error = await captureError(otpApi.generate({ userId: 'a', dateTime: 'b' }));

    expect(error.message).toBe('An unexpected error occurred.');
  });

  it('uses a generic message that names the status when the body is not JSON', async () => {
    fetchMock.mockResolvedValue(new Response('Bad gateway', { status: 502 }));

    const error = await captureError(otpApi.generate({ userId: 'a', dateTime: 'b' }));

    expect(error.status).toBe(502);
    expect(error.message).toBe('Request failed with status 502.');
  });

  it('reports an unreachable server with status 0', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));

    const error = await captureError(otpApi.generate({ userId: 'a', dateTime: 'b' }));

    expect(error.status).toBe(0);
    expect(error.message).toContain('Unable to reach the OTP service');
  });

  it('rejects a successful response whose body is not JSON', async () => {
    fetchMock.mockResolvedValue(new Response('<html></html>', { status: 200 }));

    const error = await captureError(otpApi.generate({ userId: 'a', dateTime: 'b' }));

    expect(error.status).toBe(200);
    expect(error.message).toBe('The service returned an unexpected response.');
  });
});

describe('otpApi.validate', () => {
  it('posts the request as JSON to the validate endpoint', async () => {
    fetchMock.mockResolvedValue(jsonResponse(validateResponse));
    const request = { userId: 'john.doe', code: '482913' };

    await otpApi.validate(request);

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/otp/validate',
      expect.objectContaining({ method: 'POST', body: JSON.stringify(request) }),
    );
  });

  it('returns business outcomes such as Expired as a normal result', async () => {
    const expired: ValidateOtpResponse = {
      isValid: false,
      status: 'Expired',
      message: 'The one-time password has expired. Please generate a new one.',
    };
    fetchMock.mockResolvedValue(jsonResponse(expired));

    const result = await otpApi.validate({ userId: 'john.doe', code: '482913' });

    expect(result).toEqual(expired);
  });

  it('rejects with the validation message for a malformed code', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse({ errors: { Code: ['The code must be 4 to 9 digits.'] } }, 400),
    );

    const error = await captureError(otpApi.validate({ userId: 'john.doe', code: 'abc' }));

    expect(error.status).toBe(400);
    expect(error.message).toBe('The code must be 4 to 9 digits.');
  });

  it('reports an unreachable server with status 0', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));

    const error = await captureError(otpApi.validate({ userId: 'john.doe', code: '482913' }));

    expect(error.status).toBe(0);
  });
});
