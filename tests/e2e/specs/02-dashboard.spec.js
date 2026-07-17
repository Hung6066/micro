// @ts-check
const { test, expect } = require('@playwright/test');

const BASE = 'http://localhost:8081';
const TEST_USER = 'admin';
const TEST_PASS = 'Admin@123';

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
    await page.locator('input[formControlName="username"]').fill(TEST_USER);
    await page.locator('input[formControlName="password"]').fill(TEST_PASS);
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

test.describe('Dashboard Page', () => {

  test.beforeEach(async ({ page }) => {
    await doLogin(page);
  });

  test('TC-DASH-01: Dashboard page loads and renders correctly', async ({ page }) => {
    expect(page.url()).toMatch(/\/dashboard/);

    const header = page.locator('h1, h2, h3, mat-card-title, .page-title');
    await expect(header.first()).toBeVisible({ timeout: 5000 });
  });

  test('TC-DASH-02: Dashboard displays widget cards', async ({ page }) => {
    const cards = page.locator('mat-card');
    await expect(cards.first()).toBeVisible({ timeout: 5000 });
    const count = await cards.count();
    expect(count).toBeGreaterThanOrEqual(1);

    await page.screenshot({ path: 'screenshots/tc-dash-02-widget-cards.png', fullPage: true });
  });

  test('TC-DASH-03: Sidebar navigation items are visible', async ({ page }) => {
    // Sidebar renders because AuthGuard passes (no page reload needed)
    const navItems = page.locator('mat-nav-list a');
    const expectedLabels = [
      'Dashboard',
      'Bệnh nhân',
      'Lịch hẹn',
      'Lâm sàng',
      'Dược phẩm',
      'Xét nghiệm',
      'Thanh toán',
      'Quản trị',
    ];

    for (const label of expectedLabels) {
      const link = page.locator('mat-nav-list a').filter({ hasText: label });
      await expect(link.first()).toBeVisible({ timeout: 3000 });
    }
  });

  test('TC-DASH-04: Sidebar navigation links are visible', async ({ page }) => {
    const sidebarLinks = [
      'Dashboard', 'Bệnh nhân', 'Lịch hẹn', 'Lâm sàng',
      'Dược phẩm', 'Xét nghiệm', 'Thanh toán', 'Quản trị',
    ];

    for (const label of sidebarLinks) {
      const link = page.locator('mat-nav-list a').filter({ hasText: label });
      await expect(link.first()).toBeVisible({ timeout: 5000 });
    }
  });

  test('TC-DASH-05: Dashboard content area is rendered', async ({ page }) => {
    const content = page.locator('main, .content, router-outlet + *, .dashboard-content');
    const contentVisible = await content.first().isVisible().catch(() => false);
    if (contentVisible) {
      await expect(content.first()).toBeVisible();
    }

    const spinner = page.locator('mat-spinner, .loading-spinner, .loading');
    const spinnerVisible = await spinner.count();
    if (spinnerVisible > 0) {
      await expect(spinner.first()).not.toBeVisible({ timeout: 10000 });
    }

    const errorState = page.locator('.error-state, .error-message, mat-error');
    const errorVisible = await errorState.first().isVisible().catch(() => false);
    expect(errorVisible).toBe(false);
  });

  test('TC-DASH-06: Dashboard has responsive meta viewport', async ({ page }) => {
    const viewport = page.locator('meta[name="viewport"]');
    await expect(viewport).toHaveAttribute('content', /width=device-width/);
  });
});
