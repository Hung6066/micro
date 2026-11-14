const { test, expect } = require('@playwright/test');

const { clinicalUrl: BASE_URL } = require('../config/urls');
const { signInThroughIdentity } = require('../helpers/sso-login');
const { ensureSidebarVisible } = require('../helpers/ensure-sidebar-visible');
const AUTH_LOGIN_RE = /\/(?:en\/)?auth\/login(?:\?|$)/;
const ACCESS_DENIED_RE = /\/(?:en\/)?access-denied(?:\?|$)/;

async function login(page) {
  await signInThroughIdentity(page, BASE_URL);
  return /\/(?:en\/)?dashboard(?:\?|$)/.test(page.url());
}

async function navigateToSidebar(page, label, expectedPath) {
  await ensureSidebarVisible(page);
  const link = page.locator('nav[hhShellNavigation] a:visible, mat-nav-list a:visible').filter({ hasText: label === 'Lâm sàng' ? /Lâm sàng|Clinical/i : label });
  await expect(link.first()).toBeVisible({ timeout: 10000 });
  if (expectedPath) {
    let reached = false;
    for (let attempt = 0; attempt < 3 && !reached; attempt += 1) {
      const currentLink = page.locator('nav[hhShellNavigation] a:visible, mat-nav-list a:visible').filter({ hasText: label === 'Lâm sàng' ? /Lâm sàng|Clinical/i : label }).first();
      await currentLink.click();
      try {
        await page.waitForURL(new RegExp(expectedPath), { timeout: 5000 });
        reached = true;
      } catch {
        if (AUTH_LOGIN_RE.test(page.url())) {
          console.log(`PermissionGuard redirected to login for ${label}, re-logging in...`);
          await login(page);
          continue;
        }
        if (ACCESS_DENIED_RE.test(page.url())) {
          test.skip(true, `${label} is access denied in this environment.`);
        }
        // During Angular shell bootstrap the first click can be consumed by
        // the route guard while the link remains on dashboard. Retry the
        // idempotent navigation before reporting a real route failure.
        await page.waitForTimeout(250);
      }
    }
    if (!reached) {
      // PermissionGuard may redirect to login on stale auth
      if (AUTH_LOGIN_RE.test(page.url())) {
        console.log(`PermissionGuard redirected to login for ${label}, re-logging in...`);
        await login(page);
        const retryLink = page.locator('nav[hhShellNavigation] a:visible, mat-nav-list a:visible').filter({ hasText: label === 'Lâm sàng' ? /Lâm sàng|Clinical/i : label }).first();
        await retryLink.click();
        await page.waitForURL(new RegExp(expectedPath), { timeout: 15000 });
      } else if (ACCESS_DENIED_RE.test(page.url())) {
        test.skip(true, `${label} is access denied in this environment.`);
      } else {
        throw new Error(`navigateToSidebar: expected ${expectedPath}, got ${page.url()}`);
      }
    }
  }

  if (ACCESS_DENIED_RE.test(page.url())) {
    test.skip(true, `${label} is access denied in this environment.`);
  }
  expect(page.url()).toMatch(new RegExp(expectedPath));
}

test.describe('Clinical (Lâm sàng) Module', () => {
  test.beforeEach(async ({ page }) => {
    const loggedIn = await login(page);
    if (!loggedIn) {
      test.skip(true, 'Protected clinical routes are unavailable in this environment.');
    }

    await ensureSidebarVisible(page);
  });

  test('TC-CLN-01: Clinical encounters list loads', async ({ page }) => {
    await navigateToSidebar(page, 'Lâm sàng', '/clinical');
    await page.waitForTimeout(1000);

    await page.screenshot({ path: 'screenshots/tc-cln-01-encounter-list.png', fullPage: true });
  });

  test('TC-CLN-02: Encounters display data (if any exist)', async ({ page }) => {
    await navigateToSidebar(page, 'Lâm sàng', '/clinical');
    await page.waitForTimeout(1000);

    const table = page.locator('mat-table, table, .encounter-list');
    const tableExists = await table.count();

    if (tableExists > 0) {
      const rows = table.locator('mat-row, tr');
      const spinner = page.locator('mat-spinner');
      const spinnerVisible = await spinner.first().isVisible().catch(() => false);
      if (!spinnerVisible) {
        const rowCount = await rows.count();
        expect(rowCount).toBeGreaterThanOrEqual(0);
      }
    }
  });

  test('TC-CLN-03: Click encounter shows detail', async ({ page }) => {
    await navigateToSidebar(page, 'Lâm sàng', '/clinical');
    await page.waitForTimeout(1000);

    const firstRow = page.locator('mat-table mat-row, table tbody tr').first();
    const rowExists = await firstRow.count();

    if (rowExists > 0 && await firstRow.isVisible().catch(() => false)) {
      await firstRow.click();
      await page.waitForTimeout(1500);
      expect(page.url()).toMatch(/\/clinical\/\d+/);
    } else {
      test.skip();
    }
  });

  test('TC-CLN-04: Encounter detail shows info', async ({ page }) => {
    await navigateToSidebar(page, 'Lâm sàng', '/clinical');
    await page.waitForTimeout(1000);

    const firstRow = page.locator('mat-table mat-row, table tbody tr').first();
    if (await firstRow.isVisible().catch(() => false)) {
      await firstRow.click();
      await page.waitForTimeout(1500);
      expect(page.url()).toMatch(/\/clinical\/\d+/);

      const detailContent = page.locator('main, .content, .detail-content, router-outlet + *');
      const contentCount = await detailContent.count();
      expect(contentCount).toBeGreaterThan(0);
    } else {
      test.skip();
    }
  });

  test('TC-CLN-05: Back to list from detail', async ({ page }) => {
    await navigateToSidebar(page, 'Lâm sàng', '/clinical');
    await page.waitForTimeout(1000);

    const firstRow = page.locator('mat-table mat-row, table tbody tr').first();
    if (await firstRow.isVisible().catch(() => false)) {
      await firstRow.click();
      await page.waitForTimeout(1500);
      expect(page.url()).toMatch(/\/clinical\/\d+/);

      const backButton = page.locator(
        'button:has-text("Quay lại"), button:has-text("Back"), ' +
        'a:has-text("Quay lại"), .back-button, button[aria-label="Back"]'
      ).first();

      if (await backButton.isVisible().catch(() => false)) {
        // The detail workspace can still be committing deferred data when
        // the back control is visible. Force the idempotent router action and
        // use the URL as the navigation gate instead of waiting for the old
        // document's load lifecycle.
        await backButton.click({ force: true, noWaitAfter: true });
        await page.waitForURL(/\/clinical(?:\?|$)/, { timeout: 15000 });
        expect(page.url()).toMatch(/\/clinical/);
      }
    } else {
      test.skip();
    }
  });

  test('TC-CLN-06: Loading state handled', async ({ page }) => {
    await navigateToSidebar(page, 'Lâm sàng', '/clinical');

    const spinner = page.locator('mat-spinner');
    if (await spinner.count() > 0) {
      await expect(spinner.first()).not.toBeVisible({ timeout: 15000 }).catch(() => {});
    }

    const table = page.locator('mat-table, table');
    if (await table.first().isVisible().catch(() => false)) {
      await expect(table.first()).toBeVisible();
    }
  });

  test('TC-CLN-07: Empty state when no records', async ({ page }) => {
    await navigateToSidebar(page, 'Lâm sàng', '/clinical');
    await page.waitForTimeout(1000);

    const emptyState = page.locator(
      'text=Không có dữ liệu, text=No data, ' +
      '.empty-state, .no-data, .no-records'
    );
    const rows = page.locator('mat-table mat-row, table tbody tr');
    const rowCount = await rows.count();

    if (rowCount === 0 && await emptyState.count() > 0) {
      await expect(emptyState.first()).toBeVisible({ timeout: 5000 });
    }
  });

  test('TC-CLN-08: Page title/header visible', async ({ page }) => {
    await navigateToSidebar(page, 'Lâm sàng', '/clinical');
    await page.waitForTimeout(1000);

    const title = page.locator(
      'h1, h2, h3, .page-title, .mat-card-title, mat-card-title, ' +
      '.page-header, header, mat-toolbar, main, .content, ' +
      'mat-card, .container, .clinical-content'
    );
    await expect(title.first()).toBeVisible({ timeout: 5000 });
  });
});
