const { chromium } = require('@playwright/test');
const fs = require('fs');
const path = require('path');
const { clinicalUrl } = require('./config/urls');

const STORAGE_STATE_FILE = path.join(__dirname, 'fixtures', 'shared-foundation-auth.json');

module.exports = async () => {
  if (process.env.E2E_AUTH_REQUIRED !== 'true') {
    return;
  }

  fs.mkdirSync(path.dirname(STORAGE_STATE_FILE), { recursive: true });
  const browser = await chromium.launch({ headless: true, args: ['--no-sandbox', '--disable-setuid-sandbox'] });
  const context = await browser.newContext();
  const page = await context.newPage();

  try {
    await page.goto(`${clinicalUrl}/`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2000);
    if (/\/auth\/login(?:\?|$)/.test(page.url())) {
      await page.getByRole('button', { name: /Sign in with His\.Hope/i }).click();
    }
    await page.waitForURL(/\/Account\/Login(?:\?|$)/, { timeout: 30000 });
    await page.locator('#email').fill(process.env.E2E_EMAIL || 'admin@hishop.com');
    await page.locator('#password').fill(process.env.E2E_PASSWORD || 'Admin@123');
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    await page.waitForURL((url) => url.origin === new URL(clinicalUrl).origin && !/\/auth\/(?:login|callback)(?:\?|$)/.test(url.pathname), { timeout: 30000 });
    await page.waitForLoadState('networkidle');
    await context.storageState({ path: STORAGE_STATE_FILE });
  } finally {
    await browser.close();
  }
};
