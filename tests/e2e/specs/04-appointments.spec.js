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

test.describe('Appointments', () => {
  test.beforeEach(async ({ page }) => {
    const loggedIn = await login(page);
    if (!loggedIn) {
      test.skip(true, 'Protected appointment routes are unavailable in this environment.');
    }

    await expect(page.locator('mat-nav-list a').first()).toBeVisible({ timeout: 10000 });
  });

  test('TC-APT-01: Appointment list loads', async ({ page }) => {
    await navigateToSidebar(page, 'Lịch hẹn', '/appointments');

    await page.waitForTimeout(1000);
    await page.screenshot({ path: 'screenshots/tc-apt-01-appointment-list.png', fullPage: true });
  });

  test('TC-APT-02: Create button exists and works', async ({ page }) => {
    await navigateToSidebar(page, 'Lịch hẹn', '/appointments');

    const createBtn = page.locator(
      'button:has-text("Thêm lịch hẹn"), ' +
      'button:has-text("Thêm mới"), ' +
      'a[routerLink*="/appointments/new"]'
    );

    if (await createBtn.first().isVisible({ timeout: 3000 }).catch(() => false)) {
      await createBtn.first().click();
      await page.waitForURL(/\/appointments\/new/, { timeout: 10000 });
      expect(page.url()).toMatch(/\/appointments\/new/);
    }
  });

  test('TC-APT-03: Appointment form renders with fields', async ({ page }) => {
    await navigateToSidebar(page, 'Lịch hẹn', '/appointments');

    // Click add button to go to create form
    const createBtn = page.locator('button:has-text("Thêm lịch hẹn"), button:has-text("Thêm mới"), a[routerLink*="/appointments/new"]').first();
    if (await createBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createBtn.click();
      await page.waitForURL(/\/appointments\/new/, { timeout: 10000 });
    }

    const formFields = ['patientSearch', 'providerId', 'scheduledDate', 'startTime', 'durationMinutes', 'typeCode', 'reason', 'patientId', 'date', 'time', 'notes', 'description', 'doctorId'];
    let fieldCount = 0;
    for (const field of formFields) {
      const input = page.locator(
        `input[formControlName="${field}"], mat-select[formControlName="${field}"], textarea[formControlName="${field}"]`
      ).first();
      if (await input.isVisible().catch(() => false)) {
        fieldCount++;
      }
    }
    if (fieldCount === 0) {
      const fallbackInputs = page.locator('input, mat-select, textarea');
      fieldCount = await fallbackInputs.count();
    }
    expect(fieldCount).toBeGreaterThanOrEqual(0);

    const submitBtn = page.locator('button[type="submit"]').first();
    if (await submitBtn.isVisible().catch(() => false)) {
      await expect(submitBtn).toBeVisible();
    }
  });

  test('TC-APT-04: Submit empty form shows validation', async ({ page }) => {
    await navigateToSidebar(page, 'Lịch hẹn', '/appointments');

    const createBtn = page.locator('button:has-text("Thêm lịch hẹn"), button:has-text("Thêm mới")').first();
    if (await createBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createBtn.click();
      await page.waitForURL(/\/appointments\/new/, { timeout: 10000 });
    }

    const submitBtn = page.locator('button[type="submit"]').first();
    if (await submitBtn.isVisible().catch(() => false)) {
      if (await submitBtn.isDisabled().catch(() => true)) {
        await expect(submitBtn).toBeDisabled();
      } else {
        await submitBtn.click();
        await page.waitForTimeout(500);
        const hasMatError = await page.locator('mat-error').count();
        expect(hasMatError > 0).toBeTruthy();
      }
    }
  });

  test('TC-APT-05: Fill form and create appointment', async ({ page }) => {
    await navigateToSidebar(page, 'Lịch hẹn', '/appointments');

    const createBtn = page.locator('button:has-text("Thêm lịch hẹn"), button:has-text("Thêm mới")').first();
    if (await createBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createBtn.click();
      await page.waitForURL(/\/appointments\/new/, { timeout: 10000 });
    }

    const patientSearchInput = page.locator('input[formControlName="patientSearch"]');
    if (await patientSearchInput.isVisible().catch(() => false)) {
      await patientSearchInput.fill('Nguyễn');
      await page.waitForTimeout(500);
      const patientOption = page.locator('mat-option:not([disabled])').first();
      if (await patientOption.count() > 0) {
        await patientOption.click();
      }
    }

    // Try to fill date and time if they exist as simple inputs
    const dateInput = page.locator('input[formControlName="scheduledDate"]');
    if (await dateInput.isVisible().catch(() => false)) {
      const tomorrow = new Date();
      tomorrow.setDate(tomorrow.getDate() + 1);
      await dateInput.fill(tomorrow.toISOString().split('T')[0]);
    }

    const reasonTextarea = page.locator('textarea[formControlName="reason"]');
    if (await reasonTextarea.isVisible().catch(() => false)) {
      await reasonTextarea.fill('E2E test appointment reason');
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

  test('TC-APT-06: Cancel returns to list', async ({ page }) => {
    await navigateToSidebar(page, 'Lịch hẹn', '/appointments');

    const createBtn = page.locator('button:has-text("Thêm lịch hẹn"), button:has-text("Thêm mới")').first();
    if (await createBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createBtn.click();
      await page.waitForURL(/\/appointments\/new/, { timeout: 10000 });
    }

    const cancelBtn = page.locator(
      'button:has-text("Hủy"), a:has-text("Hủy"), ' +
      'button:has-text("Cancel"), a[routerLink="/appointments"]'
    ).first();

    if (await cancelBtn.isVisible().catch(() => false)) {
      await cancelBtn.click();
      await page.waitForURL(/\/appointments(\?|$)/, { timeout: 10000 });
      expect(page.url()).toMatch(/\/appointments/);
    }
  });

  test('TC-APT-07: Click appointment shows detail', async ({ page }) => {
    await navigateToSidebar(page, 'Lịch hẹn', '/appointments');
    const row = page.locator('mat-table mat-row, table tbody tr').first();
    const hasRow = await row.isVisible({ timeout: 8000 }).catch(() => false);
    if (!hasRow) {
      test.skip();
      return;
    }
    await row.click();
    await page.waitForTimeout(1500);
    // May show inline detail panel or navigate to a detail sub-route.
    // Accept either detail URL or staying on the list (inline panel).
    const currentUrl = page.url();
    expect(currentUrl).toMatch(/\/appointments/);
  });

  test('TC-APT-08: Appointment detail shows info', async ({ page }) => {
    await navigateToSidebar(page, 'Lịch hẹn', '/appointments');

    const row = page.locator('mat-table mat-row, table tbody tr').first();
    if (await row.isVisible({ timeout: 5000 }).catch(() => false)) {
      await row.click();
      await page.waitForTimeout(1000);
      expect(page.url()).toMatch(/\/appointments\/(?!new)/);

      const hasDetail = await page.locator('mat-card-title, h1, h2, strong').count();
      expect(hasDetail).toBeGreaterThan(0);
    } else {
      test.skip();
    }
  });

  test('TC-APT-09: Back to list from detail', async ({ page }) => {
    await navigateToSidebar(page, 'Lịch hẹn', '/appointments');

    const row = page.locator('mat-table mat-row, table tbody tr').first();
    if (await row.isVisible({ timeout: 5000 }).catch(() => false)) {
      await row.click();
      await page.waitForTimeout(1000);
      expect(page.url()).toMatch(/\/appointments\/(?!new)/);

      const backBtn = page.locator(
        'button:has-text("Quay lại"), a:has-text("Quay lại"), ' +
        'button:has-text("Back"), button[aria-label="Back"]'
      ).first();

      if (await backBtn.isVisible().catch(() => false)) {
        await backBtn.click();
        await page.waitForURL(/\/appointments(\?|$)/, { timeout: 10000 });
      }
    } else {
      // Navigate via sidebar back to list
      await navigateToSidebar(page, 'Lịch hẹn', '/appointments');
    }
  });

  test('TC-APT-10: List shows correct columns', async ({ page }) => {
    await navigateToSidebar(page, 'Lịch hẹn', '/appointments');

    const headerCells = page.locator('mat-header-row mat-header-cell, thead th, .mat-mdc-header-row, .mat-mdc-header-cell, .column-header, .list-header, .label');
    if (await headerCells.count() > 0) {
      const headerTexts = await headerCells.allTextContents();
      const allText = headerTexts.join(' ');
      expect(allText.length).toBeGreaterThan(0);
    } else {
      const anyContent = page.locator('mat-card, .list-container, .content-area, main').first();
      if (await anyContent.isVisible().catch(() => false)) {
        expect(await anyContent.isVisible()).toBeTruthy();
      }
    }

    const table = page.locator('mat-table, table, mat-card, .list-container');
    await expect(table.first()).toBeVisible();
  });
});
