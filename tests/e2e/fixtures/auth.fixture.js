// @ts-check
/**
 * Auth test fixture: extends Playwright's base test with a logged-in context.
 * All spec files import { test } from this file to get pre-authenticated pages.
 */
const { test: base, expect } = require('@playwright/test');
const path = require('path');

const STORAGE_STATE_FILE = path.join(__dirname, 'auth-storage.json');

const test = base.extend({
  // Override context with saved auth state
  context: async ({ browser }, use) => {
    const context = await browser.newContext({
      baseURL: 'http://localhost:8081',
      storageState: STORAGE_STATE_FILE,
    });
    await use(context);
    await context.close();
  },

  // Convenience: a page that's already logged in
  authedPage: async ({ context }, use) => {
    const page = await context.newPage();
    await page.goto('/dashboard', { waitUntil: 'networkidle', timeout: 30000 });
    // Verify authenticated
    await page.waitForURL('**/dashboard', { timeout: 15000 });
    await use(page);
    await page.close();
  },
});

module.exports = { test, expect };
