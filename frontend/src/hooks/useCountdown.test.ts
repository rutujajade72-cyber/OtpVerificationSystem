import { act, renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useCountdown } from './useCountdown';

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date('2026-10-17T10:00:00Z'));
});

describe('useCountdown', () => {
  it('returns 0 and starts no timer when there is no deadline', () => {
    const setIntervalSpy = vi.spyOn(globalThis, 'setInterval');

    const { result } = renderHook(() => useCountdown(null));

    expect(result.current).toBe(0);
    expect(setIntervalSpy).not.toHaveBeenCalled();
  });

  it('returns the full time immediately for a new deadline', () => {
    const { result } = renderHook(() => useCountdown(Date.now() + 30_000));

    expect(result.current).toBe(30_000);
  });

  it('counts down as time passes', () => {
    const deadline = Date.now() + 30_000;
    const { result } = renderHook(() => useCountdown(deadline));

    act(() => {
      vi.advanceTimersByTime(10_000);
    });

    expect(result.current).toBe(20_000);
  });

  it('reaches exactly zero at the deadline and stays there', () => {
    const deadline = Date.now() + 30_000;
    const { result } = renderHook(() => useCountdown(deadline));

    act(() => {
      vi.advanceTimersByTime(30_000);
    });
    expect(result.current).toBe(0);

    act(() => {
      vi.advanceTimersByTime(15_000);
    });
    expect(result.current).toBe(0);
  });

  it('stops its timer once the deadline has passed', () => {
    const clearIntervalSpy = vi.spyOn(globalThis, 'clearInterval');
    const deadline = Date.now() + 5_000;
    renderHook(() => useCountdown(deadline));

    act(() => {
      vi.advanceTimersByTime(5_000);
    });

    expect(clearIntervalSpy).toHaveBeenCalled();
  });

  it('returns 0 and starts no timer for a deadline in the past', () => {
    const setIntervalSpy = vi.spyOn(globalThis, 'setInterval');

    const { result } = renderHook(() => useCountdown(Date.now() - 1_000));

    expect(result.current).toBe(0);
    expect(setIntervalSpy).not.toHaveBeenCalled();
  });

  it('restarts when the deadline changes', () => {
    const { result, rerender } = renderHook(({ deadline }) => useCountdown(deadline), {
      initialProps: { deadline: Date.now() + 5_000 },
    });

    act(() => {
      vi.advanceTimersByTime(5_000);
    });
    expect(result.current).toBe(0);

    rerender({ deadline: Date.now() + 30_000 });
    expect(result.current).toBe(30_000);

    act(() => {
      vi.advanceTimersByTime(4_000);
    });
    expect(result.current).toBe(26_000);
  });

  it('cancels its timer when the component unmounts', () => {
    const clearIntervalSpy = vi.spyOn(globalThis, 'clearInterval');
    const { unmount } = renderHook(() => useCountdown(Date.now() + 30_000));

    unmount();

    expect(clearIntervalSpy).toHaveBeenCalled();
  });

  it('honours a custom tick length', () => {
    const deadline = Date.now() + 30_000;
    const { result } = renderHook(() => useCountdown(deadline, 1_000));

    act(() => {
      vi.advanceTimersByTime(1_000);
    });

    expect(result.current).toBe(29_000);
  });
});
