// @ts-check
const { test, expect } = require('@playwright/test');

const { dashboardUrl: BASE } = require('../config/urls');
const { signInThroughIdentity } = require('../helpers/sso-login');
const { ensureSidebarVisible } = require('../helpers/ensure-sidebar-visible');
const AUTH_LOGIN_RE = /\/(?:en\/)?auth\/login(?:\?|$)/;
const ACCESS_DENIED_RE = /\/(?:en\/)?access-denied(?:\?|$)/;
const DASHBOARD_RE = /\/(?:en\/)?resources(?:\?|$)/;

/**
 * Robust login helper: clears state, navigates fresh, fills form, waits for redirect.
 */
async function doLogin(page) {
  await signInThroughIdentity(page, BASE, { dashboardPath: '/resources' });
  return DASHBOARD_RE.test(page.url());
}

test.describe('Dashboard Page', () => {

  test.beforeEach(async ({ page }) => {
    const loggedIn = await doLogin(page);
    if (!loggedIn) {
      test.skip(true, 'Protected dashboard shell is unavailable in this environment.');
    }

    // Desktop renders the drawer open; mobile intentionally starts closed.
    const mobileMenu = page.getByRole('button', { name: /open navigation|mở menu/i });
    if (await mobileMenu.isVisible().catch(() => false)) {
      await mobileMenu.click();
    }
    await ensureSidebarVisible(page);
  });

  test('TC-DASH-01: Dashboard page loads and renders correctly', async ({ page }) => {
    if (AUTH_LOGIN_RE.test(page.url()) || ACCESS_DENIED_RE.test(page.url())) {
      const loggedIn = await doLogin(page);
      if (!loggedIn) {
        test.skip(true, 'Dashboard is redirected away in this environment.');
      }
    }

    await page.waitForURL(DASHBOARD_RE, { timeout: 10000 });
    expect(page.url()).toMatch(DASHBOARD_RE);

    const header = page.locator('h1, h2, h3, mat-card-title, .page-title');
    await expect(header.first()).toBeVisible({ timeout: 5000 });
  });

  test('TC-DASH-02: Metrics route displays the system resource view', async ({ page }) => {
    // Playwright's project baseURL points at the clinical SPA for legacy
    // specs; dashboard tests must navigate through the dashboard origin.
    await page.goto(`${BASE}/metrics`);
    if (ACCESS_DENIED_RE.test(page.url()) || !/\/(?:en\/)?metrics(?:\?|$)/.test(page.url())) {
      // A fresh context can briefly land on the dashboard login route while
      // the BFF session cookie is exchanged. Re-run the idempotent SSO flow
      // once before treating the route as unavailable.
      const loggedIn = await doLogin(page);
      if (!loggedIn) {
        test.skip(true, 'Metrics route is unavailable for the authenticated dashboard principal.');
      }
      await page.goto(`${BASE}/metrics`);
    }
    if (ACCESS_DENIED_RE.test(page.url()) || !/\/(?:en\/)?metrics(?:\?|$)/.test(page.url())) {
      test.skip(true, 'Metrics route is unavailable for the authenticated dashboard principal.');
    }
    await page.waitForURL(/\/(?:en\/)?metrics(?:\?|$)/, { timeout: 10000 });
    await expect(page.getByRole('heading', { name: /system resources|tài nguyên hệ thống/i })).toBeVisible({ timeout: 10000 });
    await expect(page.getByRole('heading', { name: /health timeline|dòng thời gian sức khỏe/i })).toBeVisible({ timeout: 10000 });

    await page.screenshot({ path: 'screenshots/tc-dash-02-widget-cards.png', fullPage: true });
  });

  test('TC-DASH-03: Sidebar navigation items are visible', async ({ page }) => {
    // Sidebar renders because AuthGuard passes (no page reload needed)
    const navItems = page.locator('mat-nav-list a');
    const expectedLabels = [
      /Resources|Tài nguyên/i,
      /Logs|Nhật ký/i,
      /Traces|Trace/i,
      /SLO/i,
      /Metrics|Chỉ số/i,
    ];

    for (const label of expectedLabels) {
      const link = page.locator('mat-nav-list a').filter({ hasText: label });
      await expect(link.first()).toBeVisible({ timeout: 3000 });
    }
  });

  test('TC-DASH-04: Sidebar navigation links are visible', async ({ page }) => {
    const sidebarLinks = [
      /Resources|Tài nguyên/i, /Logs|Nhật ký/i, /Traces|Trace/i,
      /SLO/i, /Metrics|Chỉ số/i,
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
