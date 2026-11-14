import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  testMatch: 'manufacturing-buyer-auth-ui-tests.mjs',
  timeout: 120_000,
  use: {
    baseURL: process.env.BUYER_APP_URL ?? 'http://localhost:4205',
    headless: true,
    viewport: { width: 1440, height: 900 },
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
});
