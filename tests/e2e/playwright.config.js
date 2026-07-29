// @ts-check
const { defineConfig, devices } = require('@playwright/test');

module.exports = defineConfig({
  testDir: './specs',
  globalSetup: process.env.E2E_AUTH_REQUIRED === 'true' ? './shared-foundation.setup.js' : undefined,
  testMatch: '*.spec.js',
  timeout: 120000,
  expect: {
    timeout: 15000,
  },
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 1,
  workers: process.env.CI ? 2 : 4,
  reporter: [
    ['list'],
    ['html', { outputFolder: 'html-report' }],
  ],
  use: {
    baseURL: 'http://localhost:8081',
    headless: true,
    viewport: { width: 1280, height: 720 },
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    trace: 'retain-on-failure',
    actionTimeout: 15000,
    navigationTimeout: 20000,
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        launchOptions: {
          args: ['--no-sandbox', '--disable-setuid-sandbox'],
        },
      },
    },
    {
      name: 'mobile',
      use: {
        ...devices['iPhone 12'],
        browserName: 'chromium',
        launchOptions: { args: ['--no-sandbox', '--disable-setuid-sandbox'] },
      },
    },
    {
      name: 'tablet',
      use: {
        ...devices['iPad Mini'],
        browserName: 'chromium',
        launchOptions: { args: ['--no-sandbox', '--disable-setuid-sandbox'] },
      },
    },
  ],
});
