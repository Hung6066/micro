// @ts-check
/**
 * Playwright global setup: Login once and save storage state for all tests.
 * Runs before any test file.
 */
const { chromium } = require('@playwright/test');
const path = require('path');

const STORAGE_STATE_FILE = path.join(__dirname, 'fixtures', 'auth-storage.json');
const BASE_URL = 'http://localhost:8081';
const API_BASE = `${BASE_URL}/api/v1`;
const TEST_USER = 'admin';
const TEST_PASS = 'Admin@123';

async function globalSetup() {
  console.log('\n[AuthSetup] Logging in via API...');

  // Attempt API login
  let token = null;
  try {
    const response = await fetch(`${API_BASE}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: TEST_USER, password: TEST_PASS }),
    });

    if (!response.ok) {
      throw new Error(`API login failed: ${response.status} ${response.statusText}`);
    }

    const data = await response.json();
    token = data.accessToken || data.token || null;
    console.log(`[AuthSetup] API login success, token length: ${token?.length ?? 0}`);
  } catch (err) {
    console.warn(`[AuthSetup] API login failed: ${err.message}`);
    console.log('[AuthSetup] Falling back to UI login...');
  }

  // Launch browser for UI login (if API didn't work) or to set sessionStorage
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ baseURL: BASE_URL });
  const page = await context.newPage();

  if (token) {
    // Set token via sessionStorage
    await page.goto('/auth/login', { waitUntil: 'networkidle', timeout: 30000 });
    await page.evaluate((t) => {
      sessionStorage.setItem('hishope_access_token', t);
    }, token);
    // Navigate to dashboard to confirm
    await page.goto('/dashboard', { waitUntil: 'networkidle', timeout: 30000 });
    const url = page.url();
    if (url.includes('/dashboard')) {
      console.log('[AuthSetup] Token verified - dashboard loaded');
    } else {
      console.warn(`[AuthSetup] Token set but landed on: ${url}`);
      // Fall back to UI login
      token = null;
    }
  }

  if (!token) {
    // UI login fallback
    await page.goto('/auth/login', { waitUntil: 'networkidle', timeout: 30000 });
    await page.locator('input[formControlName="username"]').fill(TEST_USER);
    await page.locator('input[formControlName="password"]').fill(TEST_PASS);
    await page.locator('button[type="submit"]').click();
    // Wait for navigation to dashboard
    await page.waitForURL('**/dashboard', { timeout: 30000 });
    console.log('[AuthSetup] UI login success - redirected to dashboard');
  }

  // Save storage state for reuse in tests
  await context.storageState({ path: STORAGE_STATE_FILE });
  console.log(`[AuthSetup] Storage state saved to ${STORAGE_STATE_FILE}`);

  await browser.close();
  console.log('[AuthSetup] Setup complete\n');
}

module.exports = globalSetup;
