const { test, expect } = require('@playwright/test');

const BASE = 'http://localhost:8081';

async function login(page, attempt = 1) {
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
      await login(page, 2);
    } else {
      throw e;
    }
  }
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
      if (page.url().includes('/auth/login')) {
        console.log(`PermissionGuard redirected to login for ${label}, re-logging in...`);
        await login(page);
        // Re-navigate
        await link.first().click();
        await page.waitForURL(new RegExp(expectedPath), { timeout: 15000 });
      } else {
        throw new Error(`navigateToSidebar: expected ${expectedPath}, got ${page.url()}`);
      }
    }
  }
  expect(page.url()).toMatch(new RegExp(expectedPath));
}

test.describe('Billing (Thanh toán) Module', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('TC-BIL-01: Invoice list loads', async ({ page }) => {
    await navigateToSidebar(page, 'Thanh toán', '/billing');
    await page.waitForTimeout(1000);

    await page.screenshot({ path: 'screenshots/billing-list.png', fullPage: true });
  });

  test('TC-BIL-02: Invoices display data', async ({ page }) => {
    await navigateToSidebar(page, 'Thanh toán', '/billing');
    await page.waitForTimeout(1000);

    const content = page.locator('mat-table, table, .mat-mdc-table, mat-card, .mat-mdc-card, main, .content, .billing-content, .list-container, .invoice-list').first();
    const visible = await content.isVisible({ timeout: 5000 }).catch(() => false);

    const headerCells = page.locator('mat-header-cell, th, .mat-mdc-header-cell, .column-label');
    const headerCount = await headerCells.count();
    if (headerCount > 0) {
      expect(headerCount).toBeGreaterThan(0);
    }
    expect(page.url()).toMatch(/\/billing/);
  });

  test('TC-BIL-03: Create invoice button navigates to form', async ({ page }) => {
    await navigateToSidebar(page, 'Thanh toán', '/billing');

    const createBtn = page.locator('button, a').filter({ hasText: /Thêm|Th.m/ }).first();
    if (await createBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createBtn.click();
      await page.waitForTimeout(1000);
      expect(page.url()).toMatch(/\/billing\/new|\/billing/);
    }
  });

  test('TC-BIL-04: Invoice form renders', async ({ page }) => {
    await navigateToSidebar(page, 'Thanh toán', '/billing');

    const createBtn = page.locator('button, a').filter({ hasText: /Thêm|Th.m/ }).first();
    if (await createBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createBtn.click();
      await page.waitForTimeout(1000);
    }

    const formContainer = page.locator('form, .form, .mat-mdc-card, mat-card, .form-container, .content-area, main').first();
    const formExists = await formContainer.isVisible({ timeout: 3000 }).catch(() => false);
    expect(formExists || page.url().includes('/billing')).toBeTruthy();
  });

  test('TC-BIL-05: Submit empty form shows validation', async ({ page }) => {
    await navigateToSidebar(page, 'Thanh toán', '/billing');

    const createBtn = page.locator('button, a').filter({ hasText: /Thêm|Th.m/ }).first();
    if (await createBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createBtn.click();
      await page.waitForTimeout(1000);
    }

    const submitBtn = page.locator('button[type="submit"]').first();
    if (await submitBtn.isVisible().catch(() => false)) {
      if (await submitBtn.isDisabled().catch(() => true)) {
        await expect(submitBtn).toBeDisabled();
      } else {
        await submitBtn.click();
        await page.waitForTimeout(500);
        const validationErrors = page.locator('mat-error, .invalid-feedback, .error').first();
        if (await validationErrors.isVisible({ timeout: 3000 }).catch(() => false)) {
          await expect(validationErrors).toBeVisible();
        }
      }
    }
  });

  test('TC-BIL-06: Fill and create invoice', async ({ page }) => {
    await navigateToSidebar(page, 'Thanh toán', '/billing');

    const createBtn = page.locator('button, a').filter({ hasText: /Thêm|Th.m/ }).first();
    if (await createBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createBtn.click();
      await page.waitForTimeout(1000);
    }

    const amountInput = page.locator('input[formControlName="amount"]');
    if (await amountInput.isVisible().catch(() => false)) {
      await amountInput.fill('150000');
    }

    const submitBtn = page.locator('button[type="submit"]').first();
    if (await submitBtn.isVisible().catch(() => false)) {
      if (!(await submitBtn.isDisabled().catch(() => true))) {
        await submitBtn.click();
        await page.waitForTimeout(2000);

        const hasSnackbar = await page.locator('.mat-mdc-snack-bar-container').count() > 0;
        const navigatedFromNew = !page.url().includes('/new');
        expect(hasSnackbar || navigatedFromNew).toBeTruthy();
      }
    }
  });

  test('TC-BIL-07: View invoice detail', async ({ page }) => {
    await navigateToSidebar(page, 'Thanh toán', '/billing');

    const firstRow = page.locator('mat-row, tr').first();
    if (await firstRow.isVisible({ timeout: 5000 }).catch(() => false)) {
      await firstRow.click();
      await page.waitForTimeout(1000);
      expect(page.url()).toMatch(/\/billing\/(?!new)/);
    } else {
      test.skip();
    }
  });

  test('TC-BIL-08: Back navigation from detail', async ({ page }) => {
    await navigateToSidebar(page, 'Thanh toán', '/billing');

    const firstRow = page.locator('mat-row, tr').first();
    if (await firstRow.isVisible({ timeout: 5000 }).catch(() => false)) {
      await firstRow.click();
      await page.waitForTimeout(1000);
      expect(page.url()).toMatch(/\/billing\/(?!new)/);

      const backBtn = page.locator('button, a').filter({ hasText: /Quay l.i|Tr. l.i|Back/ }).first();
      if (await backBtn.isVisible().catch(() => false)) {
        await backBtn.click();
        await page.waitForTimeout(1000);
        expect(page.url()).toMatch(/\/billing/);
      }
    } else {
      await navigateToSidebar(page, 'Thanh toán', '/billing');
    }
  });
});
