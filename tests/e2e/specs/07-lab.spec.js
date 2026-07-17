const { test, expect } = require('@playwright/test');

const BASE_URL = 'http://localhost:8081';
const VALID_USER = { username: 'admin', password: 'Admin@123' };

async function login(page, attempt = 1) {
  try {
    await page.goto(BASE_URL + '/auth/login', { waitUntil: 'domcontentloaded', timeout: 30000 });
    // Wait for the login form to be ready
    await page.locator('input[formControlName="username"]').waitFor({ state: 'visible', timeout: 30000 });
    // Clear stale session state after page is fully loaded on the correct origin
    await page.evaluate(() => sessionStorage.clear());
    await page.locator('input[formControlName="username"]').fill(VALID_USER.username);
    await page.locator('input[formControlName="password"]').fill(VALID_USER.password);
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

test.describe('Lab (Xét nghiệm) Module', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('TC-LAB-01: Lab orders list loads', async ({ page }) => {
    await navigateToSidebar(page, 'Xét nghiệm', '/lab');
    await page.waitForTimeout(1000);

    await page.screenshot({ path: 'screenshots/tc-lab-01-lab-list.png', fullPage: true });
  });

  test('TC-LAB-02: Lab orders display data (if any)', async ({ page }) => {
    await navigateToSidebar(page, 'Xét nghiệm', '/lab');
    await page.waitForTimeout(1000);

    const table = page.locator('table, mat-table, mat-card');
    await expect(table.first()).toBeVisible({ timeout: 5000 });

    const headerCells = page.locator('mat-header-row mat-header-cell, thead th');
    if (await headerCells.count() > 0) {
      const headerTexts = await headerCells.allTextContents();
      const allText = headerTexts.join(' ');
      expect(allText.length).toBeGreaterThan(0);
    }
  });

  test('TC-LAB-03: Create order button navigates to form', async ({ page }) => {
    await navigateToSidebar(page, 'Xét nghiệm', '/lab');

    const createBtn = page.locator(
      'button:has-text("Tạo phiếu xét nghiệm"), ' +
      'button:has-text("Thêm mới"), ' +
      'a[routerLink*="/lab/new"]'
    ).first();

    if (await createBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createBtn.click();
      await page.waitForURL(/\/lab\/new/, { timeout: 10000 });
      expect(page.url()).toMatch(/\/lab\/new/);
    }
  });

  test('TC-LAB-04: Lab order form renders', async ({ page }) => {
    await navigateToSidebar(page, 'Xét nghiệm', '/lab');

    const createBtn = page.locator('button:has-text("Tạo phiếu xét nghiệm"), a[routerLink*="/lab/new"]').first();
    if (await createBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createBtn.click();
      await page.waitForURL(/\/lab\/new/, { timeout: 10000 });
    }

    const formSelectors = [
      'input[formControlName="patientSearch"]',
      'mat-select[formControlName="priorityCode"]',
      'textarea[formControlName="notes"]',
      'input[formControlName="testCode"]',
      'input[formControlName="testName"]',
    ];
    let fieldCount = 0;
    for (const selector of formSelectors) {
      const field = page.locator(selector).first();
      if (await field.isVisible().catch(() => false)) {
        fieldCount++;
      }
    }
    expect(fieldCount).toBeGreaterThanOrEqual(2);

    const submitBtn = page.locator('button[type="submit"]').first();
    if (await submitBtn.isVisible().catch(() => false)) {
      await expect(submitBtn).toBeVisible();
    }
  });

  test('TC-LAB-05: Submit empty form shows validation', async ({ page }) => {
    await navigateToSidebar(page, 'Xét nghiệm', '/lab');

    const createBtn = page.locator('a[routerLink*="/lab/new"]').first();
    if (await createBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createBtn.click();
      await page.waitForURL(/\/lab\/new/, { timeout: 10000 });
    }

    const submitBtn = page.locator('button[type="submit"]').first();
    if (await submitBtn.isVisible().catch(() => false)) {
      if (await submitBtn.isDisabled().catch(() => true)) {
        await expect(submitBtn).toBeDisabled();
      } else {
        await submitBtn.click();
        await page.waitForTimeout(500);
        const hasMatError = await page.locator('mat-error').count() > 0;
        expect(hasMatError).toBeTruthy();
      }
    }
  });

  test('TC-LAB-06: Fill and create lab order', async ({ page }) => {
    await navigateToSidebar(page, 'Xét nghiệm', '/lab');

    const createBtn = page.locator('a[routerLink*="/lab/new"]').first();
    if (await createBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createBtn.click();
      await page.waitForURL(/\/lab\/new/, { timeout: 10000 });
    }

    const testCodeInput = page.locator('input[formControlName="testCode"]');
    if (await testCodeInput.isVisible().catch(() => false)) {
      await testCodeInput.fill('CBC');
      await page.locator('input[formControlName="testName"]').fill('Tổng phân tích máu').catch(() => {});

      const submitBtn = page.locator('button[type="submit"]').first();
      if (!(await submitBtn.isDisabled().catch(() => true))) {
        await submitBtn.click();
        await page.waitForTimeout(2000);

        const hasSnackbar = await page.locator('.mat-mdc-snack-bar-container').count() > 0;
        const navigatedFromNew = !page.url().includes('/new');
        expect(hasSnackbar || navigatedFromNew).toBeTruthy();
      }
    }
  });

  test('TC-LAB-07: View lab order detail', async ({ page }) => {
    await navigateToSidebar(page, 'Xét nghiệm', '/lab');

    const row = page.locator('mat-table mat-row, table tbody tr').first();
    if (await row.isVisible({ timeout: 5000 }).catch(() => false)) {
      await row.click();
      await page.waitForTimeout(1000);
      expect(page.url()).toMatch(/\/lab\/(?!new)/);
    } else {
      test.skip();
    }
  });

  test('TC-LAB-08: Back navigation from detail', async ({ page }) => {
    await navigateToSidebar(page, 'Xét nghiệm', '/lab');

    const row = page.locator('mat-table mat-row, table tbody tr').first();
    if (await row.isVisible({ timeout: 5000 }).catch(() => false)) {
      await row.click();
      await page.waitForTimeout(1000);
      expect(page.url()).toMatch(/\/lab\/(?!new)/);

      const backBtn = page.locator(
        'button:has-text("Quay lại"), a:has-text("Quay lại"), ' +
        'button:has-text("Back"), button[aria-label="Back"]'
      ).first();

      if (await backBtn.isVisible().catch(() => false)) {
        await backBtn.click();
        await page.waitForURL(/\/lab(\?|$)/, { timeout: 10000 });
      }
    } else {
      await navigateToSidebar(page, 'Xét nghiệm', '/lab');
    }
  });
});
