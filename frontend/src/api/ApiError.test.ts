import { describe, expect, it } from 'vitest';
import { ApiError, GENERIC_ERROR_MESSAGE, toErrorMessage } from './ApiError';

describe('ApiError', () => {
  it('is an Error that carries a message and a status', () => {
    const error = new ApiError('Nope.', 429);

    expect(error).toBeInstanceOf(Error);
    expect(error).toBeInstanceOf(ApiError);
    expect(error.name).toBe('ApiError');
    expect(error.message).toBe('Nope.');
    expect(error.status).toBe(429);
  });
});

describe('toErrorMessage', () => {
  it('returns the message of an ApiError', () => {
    expect(toErrorMessage(new ApiError('Too many requests.', 429))).toBe('Too many requests.');
  });

  it.each([new Error('internal detail'), 'a string', null, undefined, 42])(
    'hides the details of anything else (%s)',
    (thrown) => {
      expect(toErrorMessage(thrown)).toBe(GENERIC_ERROR_MESSAGE);
    },
  );
});
