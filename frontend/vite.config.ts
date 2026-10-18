import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// During development the browser talks to Vite, and Vite forwards /api to the .NET API.
// That keeps the app same-origin, so no CORS setup is needed while developing.
const apiTarget = process.env.API_PROXY_TARGET ?? 'http://localhost:5080';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': { target: apiTarget, changeOrigin: true },
    },
  },
  preview: {
    port: 4173,
    proxy: {
      '/api': { target: apiTarget, changeOrigin: true },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/setupTests.ts'],
    css: false,
    restoreMocks: true,
    unstubGlobals: true,
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html'],
      include: ['src/**/*.{ts,tsx}'],
      exclude: [
        'src/**/*.test.{ts,tsx}',
        'src/main.tsx',
        'src/setupTests.ts',
        'src/test/**',
        'src/vite-env.d.ts',
        'src/types/**',
      ],
    },
  },
});
