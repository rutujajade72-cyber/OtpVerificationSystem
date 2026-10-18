import { describe, expect, it } from 'vitest';
import { dateTimeLocalToIso, msToWholeSeconds, toDateTimeLocalValue } from './dateTime';

describe('toDateTimeLocalValue', () => {
  it('formats local time with zero padding and seconds', () => {
    expect(toDateTimeLocalValue(new Date(2026, 0, 5, 3, 4, 9))).toBe('2026-01-05T03:04:09');
  });

  it('formats a late evening time on a two-digit month and day', () => {
    expect(toDateTimeLocalValue(new Date(2026, 9, 17, 23, 59, 59))).toBe('2026-10-17T23:59:59');
  });
});

describe('dateTimeLocalToIso', () => {
  it('round-trips a value produced by toDateTimeLocalValue', () => {
    const date = new Date(2026, 9, 17, 3, 56, 44);

    expect(dateTimeLocalToIso(toDateTimeLocalValue(date))).toBe(date.toISOString());
  });

  it('returns an ISO 8601 string in UTC', () => {
    expect(dateTimeLocalToIso('2026-10-17T03:56:44')).toMatch(
      /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/,
    );
  });

  it('accepts a value without seconds', () => {
    expect(dateTimeLocalToIso('2026-10-17T03:56')).toBe(new Date(2026, 9, 17, 3, 56).toISOString());
  });

  it.each(['', '   ', 'not a date', '2026-13-45T99:99'])('returns null for "%s"', (value) => {
    expect(dateTimeLocalToIso(value)).toBeNull();
  });
});

describe('msToWholeSeconds', () => {
  it.each([
    [0, 0],
    [1, 1],
    [999, 1],
    [1000, 1],
    [1001, 2],
    [29_001, 30],
    [30_000, 30],
    [-500, 0],
  ])('converts %i ms to %i s, rounding up and never below zero', (milliseconds, expected) => {
    expect(msToWholeSeconds(milliseconds)).toBe(expected);
  });
});
