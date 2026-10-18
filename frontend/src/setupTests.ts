import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';

// Globals are disabled in the Vitest config, so Testing Library's automatic cleanup is not
// registered. Teardown order matters: unmount first, while fake timers are still installed, so each
// component's interval is cleared by the same clock that created it. Only then restore real timers.
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});
