const { test, expect } = require('@playwright/test');

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
  const email = page.locator('input[type="email"]');
  if (await email.count()) {
    await expect(email).toBeVisible({ timeout: 15000 });
    await email.fill(USER.email);
    await page.locator('input[type="password"]').fill(USER.password);
    await page.locator('button[type="submit"]').click();
    await page.waitForURL(/localhost:8081\/en\/dashboard/, { timeout: 30000 });
  }
  await expect(page).toHaveURL(/localhost:8081\/en\/dashboard/, { timeout: 30000 });
}

async function openAuthenticatedApp(context, url, marker) {
  const page = await context.newPage();
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await expect(page).not.toHaveURL(/Account\/Login/, { timeout: 15000 });
  await expect(page.getByText(marker, { exact: false }).first()).toBeVisible({ timeout: 15000 });
  return page;
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
      const metrics = await page.evaluate(() => ({
        scrollWidth: document.documentElement.scrollWidth,
        clientWidth: document.documentElement.clientWidth,
      }));
      expect(metrics.scrollWidth).toBe(metrics.clientWidth);
    }
    await context.close();
  });

  test('dashboard technical routes are lazy-loaded and navigable', async ({ browser }) => {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const clinical = await context.newPage();
    await signInThroughIdentity(clinical);
    const dashboard = await openAuthenticatedApp(context, `${APPS.dashboard}/resources`, 'Resources');

    for (const [path, marker] of [['/logs', 'Logs'], ['/traces', 'Traces'], ['/metrics', 'Metrics'], ['/slo', 'SLO']]) {
      await dashboard.goto(`${APPS.dashboard}${path}`, { waitUntil: 'domcontentloaded' });
      await expect(dashboard).toHaveURL(new RegExp(`${path}$`));
      await expect(dashboard.getByText(marker, { exact: false }).first()).toBeVisible({ timeout: 15000 });
    }
    await context.close();
  });

  test('admin tables fill desktop shell and scroll on mobile', async ({ browser }) => {
    const context = await browser.newContext({ viewport: { width: 1920, height: 900 } });
    const clinical = await context.newPage();
    await signInThroughIdentity(clinical);
    const admin = await openAuthenticatedApp(context, `${APPS.admin}/clients`, 'Clients');

    const desktop = await admin.evaluate(() => {
      const shell = document.querySelector('.hh-table-shell').getBoundingClientRect();
      const table = document.querySelector('table').getBoundingClientRect();
      return { shellWidth: shell.width, tableWidth: table.width, viewport: innerWidth };
    });
    expect(desktop.tableWidth).toBeGreaterThan(desktop.shellWidth - 4);

    await admin.setViewportSize({ width: 390, height: 844 });
    const mobile = await admin.evaluate(() => ({
      scrollWidth: document.documentElement.scrollWidth,
      clientWidth: document.documentElement.clientWidth,
      tableWidth: document.querySelector('table').getBoundingClientRect().width,
    }));
    expect(mobile.scrollWidth).toBe(mobile.clientWidth);
    expect(mobile.tableWidth).toBeGreaterThan(mobile.clientWidth);
    await context.close();
  });
});
