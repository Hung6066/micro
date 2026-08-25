import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  testMatch: 'manufacturing-buyer-ui-tests.mjs',
  timeout: 60_000,
  use: {
    // Prefer IPv4 so the test cannot accidentally hit a separately running
    // Angular dev server bound to ::1 instead of the Docker nginx container.
    baseURL: process.env.BUYER_APP_URL ?? 'http://127.0.0.1:4205',
    headless: true,
    viewport: { width: 1440, height: 900 },
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
});
