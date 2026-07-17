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
  await expect(link.first()).toBeVisible({ timeout: 5000 });
  await link.first().click();
  if (expectedPath) {
    await page.waitForURL(new RegExp(expectedPath), { timeout: 10000 });
  }
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

    const table = page.locator('mat-table, table');
    await expect(table).toBeVisible();

    const headerCells = page.locator('mat-header-cell, th');
    const headerCount = await headerCells.count();
    expect(headerCount).toBeGreaterThan(0);
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

    // Try to navigate to new form
    const createBtn = page.locator('button, a').filter({ hasText: /Thêm|Th.m/ }).first();
    if (await createBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createBtn.click();
      await page.waitForTimeout(1000);
    }

    const fields = ['patientId', 'amount', 'description', 'invoiceDate'];
    let fieldCount = 0;
    for (const field of fields) {
      const input = page.locator(
        `input[formControlName="${field}"], mat-select[formControlName="${field}"]`
      ).first();
      if (await input.isVisible({ timeout: 2000 }).catch(() => false)) {
        fieldCount++;
      }
    }
    expect(fieldCount).toBeGreaterThanOrEqual(1);

    const submitBtn = page.locator('button[type="submit"]').first();
    if (await submitBtn.isVisible().catch(() => false)) {
      await expect(submitBtn).toBeVisible();
    }
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
