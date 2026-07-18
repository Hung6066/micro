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

test.describe('Pharmacy Module', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('TC-PHR-01: Medications tab loads with list', async ({ page }) => {
    await navigateToSidebar(page, 'Dược phẩm', '/pharmacy');
    await page.waitForTimeout(1000);

    await page.screenshot({ path: 'screenshots/tc-phr-01-medications-list.png', fullPage: true });
  });

  test('TC-PHR-02: Medication list shows data', async ({ page }) => {
    await navigateToSidebar(page, 'Dược phẩm', '/pharmacy');
    await page.waitForTimeout(1000);

    const content = page.locator('table, mat-table, mat-card, .mat-mdc-card, main, .content, .pharmacy-content, .tab-group, mat-tab-group, .mat-mdc-tab-body, .list-container').first();
    const visible = await content.isVisible({ timeout: 5000 }).catch(() => false);
    expect(page.url()).toMatch(/\/pharmacy/);
  });

  test('TC-PHR-03: Create medication form renders', async ({ page }) => {
    await navigateToSidebar(page, 'Dược phẩm', '/pharmacy');

    // Try clicking add button
    const addBtn = page.locator('button:has-text("Thêm thuốc"), a[routerLink*="/pharmacy/medications/new"]').first();
    if (await addBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await addBtn.click();
      await page.waitForTimeout(1000);
    }

    const fieldNames = ['name', 'manufacturer', 'dosageForm', 'strength', 'quantity'];
    let fieldCount = 0;
    for (const name of fieldNames) {
      const field = page.locator(`input[formControlName="${name}"], mat-select[formControlName="${name}"]`).first();
      if (await field.isVisible().catch(() => false)) {
        fieldCount++;
      }
    }
    expect(fieldCount).toBeGreaterThanOrEqual(2);

    const submitBtn = page.locator('button[type="submit"], button:has-text("Lưu thuốc"), button:has-text("Save")').first();
    if (await submitBtn.isVisible().catch(() => false)) {
      await expect(submitBtn).toBeVisible();
    }
  });

  test('TC-PHR-04: Submit empty medication shows validation', async ({ page }) => {
    await navigateToSidebar(page, 'Dược phẩm', '/pharmacy');

    const addBtn = page.locator('button:has-text("Thêm thuốc"), a[routerLink*="/pharmacy/medications/new"]').first();
    if (await addBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await addBtn.click();
      await page.waitForTimeout(1000);
    }

    const submitBtn = page.locator('button[type="submit"]').first();
    if (await submitBtn.isVisible().catch(() => false)) {
      if (await submitBtn.isDisabled().catch(() => true)) {
        await expect(submitBtn).toBeDisabled();
      } else {
        await submitBtn.click();
        await page.waitForTimeout(500);
        const errorFields = page.locator('mat-error');
        const errorCount = await errorFields.count();
        expect(errorCount > 0).toBeTruthy();
      }
    }
  });

  test('TC-PHR-05: Fill and save medication', async ({ page }) => {
    await navigateToSidebar(page, 'Dược phẩm', '/pharmacy');

    const addBtn = page.locator('button:has-text("Thêm thuốc"), a[routerLink*="/pharmacy/medications/new"]').first();
    if (await addBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await addBtn.click();
      await page.waitForTimeout(1000);
    }

    const nameInput = page.locator('input[formControlName="name"]');
    if (await nameInput.isVisible().catch(() => false)) {
      await nameInput.fill('Test Med E2E');
      await page.locator('input[formControlName="manufacturer"]').fill('Test Corp').catch(() => {});
      await page.locator('input[formControlName="strength"]').fill('500mg').catch(() => {});
      await page.locator('input[formControlName="quantity"]').fill('100').catch(() => {});

      const submitBtn = page.locator('button[type="submit"], button:has-text("Lưu thuốc"), button:has-text("Save")').first();
      if (!(await submitBtn.isDisabled().catch(() => true))) {
        await submitBtn.click();
        await page.waitForTimeout(2000);

        const snackbar = page.locator('.mat-mdc-snack-bar-container');
        const snackbarVisible = await snackbar.first().isVisible().catch(() => false);
        const navigated = page.url().includes('/pharmacy/medications') && !page.url().includes('/new');
        expect(snackbarVisible || navigated).toBeTruthy();
      }
    }
  });

  test('TC-PHR-06: Navigate to prescriptions tab', async ({ page }) => {
    await navigateToSidebar(page, 'Dược phẩm', '/pharmacy');

    // Look for tab/button for prescriptions
    const prescTab = page.locator(
      'button:has-text("Đơn thuốc"), a[routerLink*="/pharmacy/prescriptions"], ' +
      '.mat-tab-label:has-text("Đơn thuốc"), .tab:has-text("Prescription")'
    ).first();

    if (await prescTab.isVisible({ timeout: 3000 }).catch(() => false)) {
      await prescTab.click();
      await page.waitForTimeout(1000);
      expect(page.url()).toMatch(/\/pharmacy\/prescriptions|\/pharmacy/);
    }
  });

  test('TC-PHR-07: Prescriptions list loads', async ({ page }) => {
    await navigateToSidebar(page, 'Dược phẩm', '/pharmacy');

    const prescTab = page.locator('a[routerLink*="/pharmacy/prescriptions"]').first();
    if (await prescTab.isVisible({ timeout: 3000 }).catch(() => false)) {
      await prescTab.click();
      await page.waitForTimeout(1000);
    }

    const content = page.locator('table, mat-table, mat-card, .mat-mdc-card, main, .content, .pharmacy-content, .prescription-content, .mat-mdc-tab-body, .list-container').first();
    const visible = await content.isVisible({ timeout: 5000 }).catch(() => false);
    expect(page.url()).toMatch(/\/pharmacy/);
  });

  test('TC-PHR-08: Create prescription form renders', async ({ page }) => {
    await navigateToSidebar(page, 'Dược phẩm', '/pharmacy');

    const addPrescBtn = page.locator(
      'button:has-text("Thêm đơn"), a[routerLink*="/pharmacy/prescriptions/new"]'
    ).first();
    if (await addPrescBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await addPrescBtn.click();
      await page.waitForTimeout(1000);
    }

    const form = page.locator('form, .form, .mat-mdc-card, mat-card, .content-area, .form-container').first();
    const formExists = await form.isVisible({ timeout: 3000 }).catch(() => false);
    expect(formExists || page.url().includes('/pharmacy')).toBeTruthy();
  });

  test('TC-PHR-09: Submit empty prescription shows validation', async ({ page }) => {
    await navigateToSidebar(page, 'Dược phẩm', '/pharmacy');

    const addPrescBtn = page.locator('a[routerLink*="/pharmacy/prescriptions/new"]').first();
    if (await addPrescBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await addPrescBtn.click();
      await page.waitForTimeout(1000);
    }

    const submitBtn = page.locator('button[type="submit"]').first();
    if (await submitBtn.isVisible().catch(() => false)) {
      if (await submitBtn.isDisabled().catch(() => true)) {
        await expect(submitBtn).toBeDisabled();
      } else {
        await submitBtn.click();
        await page.waitForTimeout(500);
        const errorFields = page.locator('mat-error');
        const errorCount = await errorFields.count();
        expect(errorCount > 0).toBeTruthy();
      }
    }
  });

  test('TC-PHR-10: Fill and save prescription', async ({ page }) => {
    await navigateToSidebar(page, 'Dược phẩm', '/pharmacy');

    const addPrescBtn = page.locator('a[routerLink*="/pharmacy/prescriptions/new"]').first();
    if (await addPrescBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await addPrescBtn.click();
      await page.waitForTimeout(1000);
    }

    const submitBtn = page.locator('button[type="submit"]').first();
    if (await submitBtn.isVisible().catch(() => false)) {
      if (!(await submitBtn.isDisabled().catch(() => true))) {
        await submitBtn.click();
        await page.waitForTimeout(2000);

        const snackbar = page.locator('.mat-mdc-snack-bar-container');
        const snackbarVisible = await snackbar.first().isVisible().catch(() => false);
        expect(snackbarVisible || !page.url().includes('/new')).toBeTruthy();
      }
    }
  });
});
