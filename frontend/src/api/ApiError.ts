export const GENERIC_ERROR_MESSAGE = 'Something went wrong. Please try again.';

/** An error from the API client that carries a message that is safe to show to the user. */
export class ApiError extends Error {
  /** The HTTP status code, or 0 when the server could not be reached. */
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

/** Turns anything that was thrown into a message for the user. */
export function toErrorMessage(error: unknown): string {
  return error instanceof ApiError ? error.message : GENERIC_ERROR_MESSAGE;
}
