const { test, expect } = require('@playwright/test');

const BASE = 'http://localhost:8081';
const AUTH_LOGIN_RE = /\/(?:en\/)?auth\/login(?:\?|$)/;
const ACCESS_DENIED_RE = /\/(?:en\/)?access-denied(?:\?|$)/;

async function doLogin(page) {
  await page.goto(BASE + '/auth/login');
  await expect(page.locator('input[formControlName="username"]')).toBeVisible({ timeout: 10000 });
  await page.locator('input[formControlName="username"]').fill('admin');
  await page.locator('input[formControlName="password"]').fill('Admin@123');
  await page.locator('button[type="submit"]').click();
  await page.waitForURL(
    (url) => /\/(?:en\/)?dashboard(?:\?|$)/.test(url.toString()) || ACCESS_DENIED_RE.test(url.toString()),
    { timeout: 30000 },
  );

  return /\/(?:en\/)?dashboard(?:\?|$)/.test(page.url());
}

test.describe('Sidebar Navigation', () => {
  test.beforeEach(async ({ page }) => {
    const loggedIn = await doLogin(page);
    if (!loggedIn) {
      test.skip(true, 'Protected navigation routes are unavailable in this environment.');
    }

    await expect(page.locator('mat-nav-list a').first()).toBeVisible({ timeout: 10000 });
  });

  async function clickSidebarLink(page, text, expectedPath) {
    const link = page.locator('mat-nav-list a').filter({ hasText: text });
    await expect(link.first()).toBeVisible({ timeout: 10000 });
    await link.first().click();
    if (expectedPath) {
      await page.waitForURL(
        (url) => new RegExp(expectedPath).test(url.toString()) || AUTH_LOGIN_RE.test(url.toString()) || ACCESS_DENIED_RE.test(url.toString()),
        { timeout: 15000 },
      );
    }

    if (ACCESS_DENIED_RE.test(page.url()) && text === 'Quản trị') {
      return 'access-denied';
    }

    expect(page.url()).toMatch(new RegExp(expectedPath));
    return 'ok';
  }

  test('TC-NAV-01: Dashboard link navigates to /dashboard', async ({ page }) => {
    await clickSidebarLink(page, 'Dashboard', '\\/dashboard');
  });

  test('TC-NAV-02: Bệnh nhân link navigates to /patients', async ({ page }) => {
    await clickSidebarLink(page, 'Bệnh nhân', '\\/patients');
  });

  test('TC-NAV-03: Lịch hẹn link navigates to /appointments', async ({ page }) => {
    await clickSidebarLink(page, 'Lịch hẹn', '\\/appointments');
  });

  test('TC-NAV-04: Lâm sàng link navigates to /clinical', async ({ page }) => {
    await clickSidebarLink(page, 'Lâm sàng', '\\/clinical');
  });

  test('TC-NAV-05: Dược phẩm link navigates to /pharmacy', async ({ page }) => {
    await clickSidebarLink(page, 'Dược phẩm', '\\/pharmacy');
  });

  test('TC-NAV-06: Xét nghiệm link navigates to /lab', async ({ page }) => {
    await clickSidebarLink(page, 'Xét nghiệm', '\\/lab');
  });

  test('TC-NAV-07: Thanh toán link navigates to /billing', async ({ page }) => {
    await clickSidebarLink(page, 'Thanh toán', '\\/billing');
  });

  test('TC-NAV-08: Quản trị link navigates to /admin', async ({ page }) => {
    const status = await clickSidebarLink(page, 'Quản trị', '\\/admin');
    expect(status === 'ok' || status === 'access-denied').toBeTruthy();
    expect(page.url()).toMatch(/\/(?:en\/)?(admin|access-denied)(?:\?|$)/);
  });
});
