const { test, expect } = require('@playwright/test');
const path = require('path');

if (process.env.E2E_AUTH_REQUIRED === 'true') {
  test.use({ storageState: path.join(__dirname, '..', 'fixtures', 'shared-foundation-auth.json') });
}

const APPS = {
  clinical: 'http://localhost:8081',
  dashboard: 'http://localhost:8082',
  admin: 'http://localhost:8083',
};

const USER = {
  email: process.env.E2E_EMAIL || 'admin@hishop.com',
  password: process.env.E2E_PASSWORD || 'Admin@123',
};

async function signInThroughIdentity(page) {
  await page.goto(`${APPS.clinical}/en/dashboard`, { waitUntil: 'domcontentloaded' });
  if (/\/auth\/login(?:\?|$)/.test(page.url())) {
    await page.getByRole('button', { name: /Sign in with His\.Hope/i }).click();
  }
  const email = page.locator('input[type="email"]');
  // The Angular auth guard redirects asynchronously after the initial document
  // load. Wait for either the Identity form or the authenticated dashboard
  // component; app-root alone also exists during the unauthenticated bootstrap.
  await Promise.race([
    email.waitFor({ state: 'visible', timeout: 30000 }),
    page.locator('app-root app-dashboard').waitFor({ state: 'attached', timeout: 30000 }),
  ]);
  if (await email.count()) {
    await expect(email).toBeVisible({ timeout: 15000 });
    await email.fill(USER.email);
    await page.locator('input[type="password"]').fill(USER.password);
    await page.locator('button[type="submit"]').click();
    await page.waitForURL(/localhost:8081\/en\/dashboard/, { timeout: 30000 });
  }
  await expect(page).toHaveURL(/localhost:8081\/en\/dashboard/, { timeout: 30000 });
  await page.waitForLoadState('load');
}

async function openAuthenticatedApp(context, url, marker) {
  const page = await context.newPage();
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await expect(page).not.toHaveURL(/Account\/Login/, { timeout: 15000 });
  await expect(page.locator('body')).toContainText(marker, { timeout: 15000 });
  await page.waitForLoadState('load');
  return page;
}

async function readViewportMetrics(page) {
  for (let attempt = 0; attempt < 5; attempt += 1) {
    try {
      return await page.evaluate(() => ({
        scrollWidth: document.documentElement.scrollWidth,
        clientWidth: document.documentElement.clientWidth,
      }));
    } catch (error) {
      if (!String(error).includes('Execution context was destroyed') || attempt === 4) throw error;
      await page.waitForTimeout(250);
    }
  }
  throw new Error('Unable to read viewport metrics');
}

test.describe('His.Hope current SSO and responsive smoke', () => {
  test('one Identity login opens all three applications', async ({ browser }) => {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const clinical = await context.newPage();
    await signInThroughIdentity(clinical);

    const dashboard = await openAuthenticatedApp(context, `${APPS.dashboard}/resources`, 'Resources');
    const admin = await openAuthenticatedApp(context, `${APPS.admin}/clients`, 'Clients');

    await expect(clinical).toHaveURL(/\/en\/dashboard/);
    await expect(dashboard).toHaveURL(/\/resources/);
    await expect(admin).toHaveURL(/\/clients/);
    await expect(dashboard.locator('body')).not.toContainText('Clinical access, protected.');
    await expect(admin.locator('body')).not.toContainText('Clinical access, protected.');
    await context.close();
  });

  test('all app shells remain inside the viewport on mobile', async ({ browser }) => {
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const clinical = await context.newPage();
    await signInThroughIdentity(clinical);

    const pages = [
      clinical,
      await openAuthenticatedApp(context, `${APPS.dashboard}/resources`, 'Resources'),
      await openAuthenticatedApp(context, `${APPS.admin}/clients`, 'Clients'),
    ];

    for (const page of pages) {
      const metrics = await readViewportMetrics(page);
      expect(metrics.scrollWidth).toBe(metrics.clientWidth);
    }
    await context.close();
  });

  test('dashboard technical routes are lazy-loaded and navigable', async ({ browser }) => {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const clinical = await context.newPage();
    await signInThroughIdentity(clinical);
    const dashboard = await openAuthenticatedApp(context, `${APPS.dashboard}/resources`, 'Resources');
    await dashboard.waitForTimeout(2500);

    for (const [path, marker] of [['/logs', 'Logs'], ['/traces', 'Traces'], ['/metrics', 'Metrics'], ['/slo', 'SLO']]) {
      await dashboard.locator(`a[href="${path}"]`).first().click();
      await expect(dashboard).toHaveURL(new RegExp(`${path}$`), { timeout: 30000 });
      await expect(dashboard.locator('body')).toContainText(marker, { timeout: 15000 });
    }
    await context.close();
  });

  test('admin tables fill desktop shell and scroll on mobile', async ({ browser }) => {
    const context = await browser.newContext({ viewport: { width: 1920, height: 900 } });
    const clinical = await context.newPage();
    await signInThroughIdentity(clinical);
    const admin = await openAuthenticatedApp(context, `${APPS.admin}/clients`, 'Clients');

    await expect(admin.locator('hh-data-table')).toBeVisible({ timeout: 15000 });
    const desktop = await admin.evaluate(() => ({
      scrollWidth: document.documentElement.scrollWidth,
      clientWidth: document.documentElement.clientWidth,
    }));
    expect(desktop.scrollWidth).toBe(desktop.clientWidth);

    await admin.setViewportSize({ width: 390, height: 844 });
    const mobile = await admin.evaluate(() => ({
      scrollWidth: document.documentElement.scrollWidth,
      clientWidth: document.documentElement.clientWidth,
    }));
    expect(mobile.scrollWidth).toBe(mobile.clientWidth);
    await context.close();
  });
});
