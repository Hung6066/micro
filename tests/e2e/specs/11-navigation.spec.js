const { test, expect } = require('@playwright/test');

const BASE = 'http://localhost:8081';

/**
 * Robust login helper: clears state, navigates fresh, fills form, waits for redirect.
 */
async function doLogin(page, attempt = 1) {
  try {
    await page.goto(BASE + '/auth/login', { waitUntil: 'domcontentloaded', timeout: 30000 });
    // Wait for the login form to be ready
    await page.locator('input[formControlName="username"]').waitFor({ state: 'visible', timeout: 30000 });
    // Clear stale session state after page is fully loaded on the correct origin
    await page.evaluate(() => sessionStorage.clear());
    await page.locator('input[formControlName="username"]').fill('admin');
    await page.locator('input[formControlName="password"]').fill('Admin@123');
    await page.waitForTimeout(500); // Let Angular settle before submit
    await page.locator('button[type="submit"]').click();
    await page.waitForURL(/\/dashboard/, { timeout: 60000 });
  } catch (e) {
    if (attempt < 2) {
      // Retry once with a fresh page load
      console.log('Login timeout, retrying...');
      await doLogin(page, 2);
    } else {
      throw e;
    }
  }
}

test.describe('Sidebar Navigation', () => {
  test.beforeEach(async ({ page }) => {
    await doLogin(page);
  });

  async function clickSidebarLink(page, text, expectedPath) {
    const link = page.locator('mat-nav-list a').filter({ hasText: text });
    await expect(link.first()).toBeVisible({ timeout: 10000 });
    await link.first().click();
    if (expectedPath) {
      await page.waitForURL(new RegExp(expectedPath), { timeout: 15000 });
    }
    expect(page.url()).toMatch(new RegExp(expectedPath));
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
    await clickSidebarLink(page, 'Quản trị', '\\/admin');
  });
});
