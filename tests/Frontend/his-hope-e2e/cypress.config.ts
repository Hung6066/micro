import { defineConfig } from 'cypress';

export default defineConfig({
  e2e: {
    baseUrl: 'http://localhost:4200',
    supportFile: 'cypress/support/e2e.ts',
    specPattern: 'cypress/e2e/**/*.cy.ts',
    video: true,
    screenshotOnRunFailure: true,
    viewportWidth: 1280,
    viewportHeight: 720,
    defaultCommandTimeout: 10000,
    requestTimeout: 10000,
    responseTimeout: 30000,
    retries: {
      runMode: 1,
      openMode: 0,
    },
    env: {
      // The local gateway is the supported browser/API entry point. Port 5011
      // was the retired standalone API mapping and makes token setup fail before
      // any E2E assertion runs.
      apiUrl: 'http://localhost:5000/api/v1',
      // Keep the local E2E fixture aligned with Identity's seeded bootstrap
      // account (the old testadmin/Test@12345 pair no longer exists).
      testUsername: 'admin',
      testPassword: 'Test@123456',
    },
  },
});
