import { useEffect, useState } from 'react';
import { COUNTDOWN_TICK_MS } from '../config';

/**
 * Returns the milliseconds left until `deadlineMs` (a `Date.now()` timestamp) and re-renders the
 * component while time is running. It returns 0 for a null or past deadline.
 *
 * The remaining time is computed while rendering, not stored, so the very first render after a
 * new deadline is already correct. There is no frame that briefly shows "expired".
 */
export function useCountdown(
  deadlineMs: number | null,
  tickMs: number = COUNTDOWN_TICK_MS,
): number {
  const [, setTick] = useState(0);

  useEffect(() => {
    if (deadlineMs === null || deadlineMs <= Date.now()) {
      return undefined;
    }

    const intervalId = setInterval(() => {
      setTick((tick) => tick + 1);

      // The final tick renders the zero state, then the timer stops itself.
      if (Date.now() >= deadlineMs) {
        clearInterval(intervalId);
      }
    }, tickMs);

    return () => clearInterval(intervalId);
  }, [deadlineMs, tickMs]);

  return deadlineMs === null ? 0 : Math.max(0, deadlineMs - Date.now());
}
