import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  testMatch: 'manufacturing-operator-ui-tests.mjs',
  timeout: 120_000,
  use: {
    baseURL: process.env.OPERATOR_APP_URL ?? 'http://localhost:4200',
    headless: true,
    viewport: { width: 1440, height: 900 },
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
});
