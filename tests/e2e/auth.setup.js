// @ts-check
/**
 * Playwright global setup: Login once and save storage state for all tests.
 * Runs before any test file.
 */
const { chromium } = require('@playwright/test');
const path = require('path');
const { clinicalUrl } = require('./config/urls');
const { signInThroughIdentity } = require('./helpers/sso-login');

const STORAGE_STATE_FILE = path.join(__dirname, 'fixtures', 'auth-storage.json');
const BASE_URL = clinicalUrl;

async function globalSetup() {
  console.log('\n[AuthSetup] Logging in through Identity Service SSO...');
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ baseURL: BASE_URL });
  const page = await context.newPage();
  await signInThroughIdentity(page, BASE_URL);
  console.log('[AuthSetup] SSO login success - redirected to dashboard');

  // Save storage state for reuse in tests
  await context.storageState({ path: STORAGE_STATE_FILE });
  console.log(`[AuthSetup] Storage state saved to ${STORAGE_STATE_FILE}`);

  await browser.close();
  console.log('[AuthSetup] Setup complete\n');
}

module.exports = globalSetup;
