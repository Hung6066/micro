// @ts-check
const { defineConfig, devices } = require('@playwright/test');
const { clinicalUrl } = require('./config/urls');
const retainFailureArtifacts = process.env.E2E_RETAIN_ARTIFACTS !== 'false';

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
    baseURL: clinicalUrl,
    headless: true,
    viewport: { width: 1280, height: 720 },
    screenshot: retainFailureArtifacts ? 'only-on-failure' : 'off',
    video: retainFailureArtifacts ? 'retain-on-failure' : 'off',
    trace: retainFailureArtifacts ? 'retain-on-failure' : 'off',
    actionTimeout: 15000,
    navigationTimeout: 20000,
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        launchOptions: {
          args: ['--no-sandbox', '--disable-setuid-sandbox', '--proxy-server=direct://', '--proxy-bypass-list=*'],
        },
      },
    },
    {
      name: 'mobile',
      use: {
        ...devices['iPhone 12'],
        browserName: 'chromium',
        launchOptions: { args: ['--no-sandbox', '--disable-setuid-sandbox', '--proxy-server=direct://', '--proxy-bypass-list=*'] },
      },
    },
    {
      name: 'tablet',
      use: {
        ...devices['iPad Mini'],
        browserName: 'chromium',
        launchOptions: { args: ['--no-sandbox', '--disable-setuid-sandbox', '--proxy-server=direct://', '--proxy-bypass-list=*'] },
      },
    },
  ],
});
