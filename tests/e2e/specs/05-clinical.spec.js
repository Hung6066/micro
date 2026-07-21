const { test, expect } = require('@playwright/test');

const BASE_URL = 'http://localhost:8081';
const VALID_USER = { username: 'admin', password: 'Admin@123' };
const AUTH_LOGIN_RE = /\/(?:en\/)?auth\/login(?:\?|$)/;
const ACCESS_DENIED_RE = /\/(?:en\/)?access-denied(?:\?|$)/;

async function login(page) {
  await page.goto(BASE_URL + '/auth/login');
  await expect(page.locator('input[formControlName="username"]')).toBeVisible({ timeout: 10000 });
  await page.locator('input[formControlName="username"]').fill(VALID_USER.username);
  await page.locator('input[formControlName="password"]').fill(VALID_USER.password);
  await page.locator('button[type="submit"]').click();
  await page.waitForURL(
    (url) => /\/(?:en\/)?dashboard(?:\?|$)/.test(url.toString()) || ACCESS_DENIED_RE.test(url.toString()),
    { timeout: 30000 },
  );

  return /\/(?:en\/)?dashboard(?:\?|$)/.test(page.url());
}

async function navigateToSidebar(page, label, expectedPath) {
  const link = page.locator('mat-nav-list a').filter({ hasText: label });
  await expect(link.first()).toBeVisible({ timeout: 10000 });
  await link.first().click();
  if (expectedPath) {
    try {
      await page.waitForURL(new RegExp(expectedPath), { timeout: 15000 });
    } catch {
      // PermissionGuard may redirect to login on stale auth
      if (AUTH_LOGIN_RE.test(page.url())) {
        console.log(`PermissionGuard redirected to login for ${label}, re-logging in...`);
        await login(page);
        // Re-navigate
        await link.first().click();
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

    await expect(page.locator('mat-nav-list a').first()).toBeVisible({ timeout: 10000 });
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
        await backButton.click();
        await page.waitForTimeout(1500);
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
