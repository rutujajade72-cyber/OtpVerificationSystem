function pad(value: number): string {
  return String(value).padStart(2, '0');
}

/**
 * Formats a date for an `<input type="datetime-local">`: local time, with seconds,
 * for example `2026-10-17T03:56:44`.
 */
export function toDateTimeLocalValue(date: Date): string {
  const day = `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
  const time = `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
  return `${day}T${time}`;
}

/**
 * Converts the value of a datetime-local input (local time) to an ISO 8601 string in UTC,
 * which is what the API expects. Returns null when the value is empty or not a date.
 */
export function dateTimeLocalToIso(value: string): string | null {
  if (value.trim() === '') {
    return null;
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

/** Converts milliseconds to whole seconds, rounding up and never going below zero. */
export function msToWholeSeconds(milliseconds: number): number {
  return Math.max(0, Math.ceil(milliseconds / 1000));
}
