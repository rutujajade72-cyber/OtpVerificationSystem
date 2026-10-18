import { API_BASE_URL } from '../config';
import type {
  GenerateOtpRequest,
  GenerateOtpResponse,
  ValidateOtpRequest,
  ValidateOtpResponse,
} from '../types/otp';
import { ApiError } from './ApiError';

/** The contract the UI depends on. Tests pass a fake implementation. */
export interface OtpApi {
  generate(request: GenerateOtpRequest): Promise<GenerateOtpResponse>;
  validate(request: ValidateOtpRequest): Promise<ValidateOtpResponse>;
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

const NETWORK_ERROR_MESSAGE =
  'Unable to reach the OTP service. Check that the API is running and try again.';
const UNEXPECTED_RESPONSE_MESSAGE = 'The service returned an unexpected response.';

/**
 * Picks the most helpful text out of an RFC 7807 problem response: field errors first,
 * then the detail, then the title, then a generic message that names the status code.
 */
async function readErrorMessage(response: Response): Promise<string> {
  const fallback = `Request failed with status ${response.status}.`;

  try {
    const problem = (await response.json()) as ProblemDetails;
    const fieldErrors = problem.errors ? Object.values(problem.errors).flat() : [];
    if (fieldErrors.length > 0) {
      return fieldErrors.join(' ');
    }

    return problem.detail ?? problem.title ?? fallback;
  } catch {
    return fallback;
  }
}

async function postJson<TResponse>(path: string, body: unknown): Promise<TResponse> {
  let response: Response;

  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify(body),
    });
  } catch {
    throw new ApiError(NETWORK_ERROR_MESSAGE, 0);
  }

  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response), response.status);
  }

  try {
    return (await response.json()) as TResponse;
  } catch {
    throw new ApiError(UNEXPECTED_RESPONSE_MESSAGE, response.status);
  }
}

export const otpApi: OtpApi = {
  generate: (request) => postJson<GenerateOtpResponse>('/api/otp/generate', request),
  validate: (request) => postJson<ValidateOtpResponse>('/api/otp/validate', request),
};
