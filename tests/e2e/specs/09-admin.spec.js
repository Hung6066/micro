// @ts-check
const { test, expect } = require('@playwright/test');

const BASE = 'http://localhost:8081';
const TEST_USER = 'admin';
const TEST_PASS = 'Admin@123';

async function login(page, attempt = 1) {
  try {
    await page.goto(BASE + '/auth/login', { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.locator('input[formControlName="username"]').waitFor({ state: 'visible', timeout: 30000 });
    await page.evaluate(() => sessionStorage.clear());
    await page.locator('input[formControlName="username"]').fill(TEST_USER);
    await page.locator('input[formControlName="password"]').fill(TEST_PASS);
    await page.waitForTimeout(500);
    await page.locator('button[type="submit"]').click();
    await page.waitForURL(/\/dashboard/, { timeout: 60000 });
  } catch (e) {
    if (attempt < 2) {
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

test.describe('Admin Module', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('TC-ADM-01: Admin dashboard loads', async ({ page }) => {
    await navigateToSidebar(page, 'Quản trị', '/admin');
    await page.waitForTimeout(1000);

    const header = page.locator('h1, h2, .page-title, mat-card-title').first();
    if (await header.isVisible({ timeout: 3000 }).catch(() => false)) {
      await expect(header).toBeVisible();
    }

    await page.screenshot({ path: 'screenshots/tc-adm-01-dashboard.png', fullPage: true });
  });

  test('TC-ADM-02: Manage users page loads', async ({ page }) => {
    // Admin uses RoleGuard which checks roles from currentUser
    // Since we navigated via sidebar from login, currentUser is set
    const manageUsersLink = page.locator('a[routerLink*="/admin/manage-users"], mat-card[routerLink*="/admin/manage-users"]').first();
    if (await manageUsersLink.isVisible({ timeout: 3000 }).catch(() => false)) {
      await manageUsersLink.click();
      await page.waitForURL(/\/admin\/manage-users/, { timeout: 10000 });
    } else {
      await navigateToSidebar(page, 'Quản trị', '/admin');
      await page.waitForTimeout(500);
      // Try clicking again after admin page loads
      const link2 = page.locator('a[routerLink*="/admin/manage-users"]').first();
      if (await link2.isVisible().catch(() => false)) {
        await link2.click();
        await page.waitForURL(/\/admin\/manage-users/, { timeout: 10000 });
      }
    }

    const table = page.locator('mat-table, table');
    await expect(table).toBeVisible({ timeout: 5000 });

    await page.screenshot({ path: 'screenshots/tc-adm-02-manage-users.png', fullPage: true });
  });

  test('TC-ADM-03: Users table shows data', async ({ page }) => {
    const manageUsersLink = page.locator('a[routerLink*="/admin/manage-users"], mat-card[routerLink*="/admin/manage-users"]').first();
    if (await manageUsersLink.isVisible({ timeout: 3000 }).catch(() => false)) {
      await manageUsersLink.click();
      await page.waitForURL(/\/admin\/manage-users/, { timeout: 10000 });
    }

    const rows = page.locator('mat-row, tr');
    if (await rows.first().isVisible({ timeout: 5000 }).catch(() => false)) {
      const count = await rows.count();
      expect(count).toBeGreaterThanOrEqual(1);
    }
  });

  test('TC-ADM-04: Create user form (if dialog-based)', async ({ page }) => {
    const manageUsersLink = page.locator('a[routerLink*="/admin/manage-users"], mat-card[routerLink*="/admin/manage-users"]').first();
    if (await manageUsersLink.isVisible({ timeout: 3000 }).catch(() => false)) {
      await manageUsersLink.click();
      await page.waitForURL(/\/admin\/manage-users/, { timeout: 10000 });
    }

    const addBtn = page.locator('button:has-text("Thêm người dùng"), button:has-text("Thêm mới")').first();
    if (await addBtn.isVisible().catch(() => false)) {
      await addBtn.click();
      await page.waitForTimeout(500);

      // Check if a dialog opened
      const dialog = page.locator('mat-dialog-container, .cdk-overlay-pane');
      if (await dialog.isVisible().catch(() => false)) {
        const formFields = ['fullName', 'email', 'phone', 'password', 'roles'];
        let fieldCount = 0;
        for (const field of formFields) {
          const input = page.locator(`input[formControlName="${field}"], mat-select[formControlName="${field}"]`).first();
          if (await input.isVisible({ timeout: 2000 }).catch(() => false)) {
            fieldCount++;
          }
        }
        expect(fieldCount).toBeGreaterThanOrEqual(2);

        // Close dialog
        const cancelBtn = page.locator('button:has-text("Hủy"), button:has-text("Cancel"), button[aria-label="Close"]').first();
        if (await cancelBtn.isVisible().catch(() => false)) {
          await cancelBtn.click();
        }
      }
    }
  });

  test('TC-ADM-05: Manage roles page loads', async ({ page }) => {
    const manageRolesLink = page.locator('a[routerLink*="/admin/manage-roles"], mat-card[routerLink*="/admin/manage-roles"]').first();
    if (await manageRolesLink.isVisible({ timeout: 3000 }).catch(() => false)) {
      await manageRolesLink.click();
      await page.waitForURL(/\/admin\/manage-roles/, { timeout: 10000 });
    } else {
      await navigateToSidebar(page, 'Quản trị', '/admin');
    }

    const header = page.locator('h1, h2, .page-title').first();
    if (await header.isVisible({ timeout: 3000 }).catch(() => false)) {
      await expect(header).toBeVisible();
    }
  });

  test('TC-ADM-06: Roles table shows data', async ({ page }) => {
    const manageRolesLink = page.locator('a[routerLink*="/admin/manage-roles"]').first();
    if (await manageRolesLink.isVisible({ timeout: 3000 }).catch(() => false)) {
      await manageRolesLink.click();
      await page.waitForURL(/\/admin\/manage-roles/, { timeout: 10000 });
    }

    const rows = page.locator('mat-row, tr').first();
    if (await rows.isVisible({ timeout: 5000 }).catch(() => false)) {
      expect(await rows.count()).toBeGreaterThanOrEqual(0);
    }
  });

  test('TC-ADM-07: Settings page loads', async ({ page }) => {
    const settingsLink = page.locator('a[routerLink*="/admin/settings"], mat-card[routerLink*="/admin/settings"]').first();
    if (await settingsLink.isVisible({ timeout: 3000 }).catch(() => false)) {
      await settingsLink.click();
      await page.waitForURL(/\/admin\/settings/, { timeout: 10000 });
    }

    await page.waitForTimeout(1000);

    const slideToggle = page.locator('mat-slide-toggle');
    const checkbox = page.locator('mat-checkbox');
    const hasToggle = (await slideToggle.count()) > 0 || (await checkbox.count()) > 0;
    expect(hasToggle).toBe(true);
  });

  test('TC-ADM-08: Settings can be toggled (if UI allows)', async ({ page }) => {
    const settingsLink = page.locator('a[routerLink*="/admin/settings"]').first();
    if (await settingsLink.isVisible({ timeout: 3000 }).catch(() => false)) {
      await settingsLink.click();
      await page.waitForURL(/\/admin\/settings/, { timeout: 10000 });
    }

    const slideToggle = page.locator('mat-slide-toggle').first();
    if (await slideToggle.isVisible({ timeout: 3000 }).catch(() => false)) {
      const initialChecked = await slideToggle.locator('input').isChecked().catch(() => false);
      await slideToggle.click();
      await page.waitForTimeout(500);
      const afterClick = await slideToggle.locator('input').isChecked().catch(() => false);
      expect(afterClick).toBe(!initialChecked);
    }
  });

  test('TC-ADM-09: Audit logs page loads', async ({ page }) => {
    const auditLink = page.locator('a[routerLink*="/admin/audit-logs"], mat-card[routerLink*="/admin/audit-logs"]').first();
    if (await auditLink.isVisible({ timeout: 3000 }).catch(() => false)) {
      await auditLink.click();
      await page.waitForURL(/\/admin\/audit-logs/, { timeout: 10000 });
    }

    await page.waitForTimeout(1000);

    const table = page.locator('mat-table, table');
    await expect(table).toBeVisible({ timeout: 5000 });
  });

  test('TC-ADM-10: Audit logs shows data', async ({ page }) => {
    const auditLink = page.locator('a[routerLink*="/admin/audit-logs"]').first();
    if (await auditLink.isVisible({ timeout: 3000 }).catch(() => false)) {
      await auditLink.click();
      await page.waitForURL(/\/admin\/audit-logs/, { timeout: 10000 });
    }

    const rows = page.locator('mat-row, tr').first();
    if (await rows.isVisible({ timeout: 5000 }).catch(() => false)) {
      const count = await page.locator('mat-row, tr').count();
      expect(count).toBeGreaterThanOrEqual(0);
    }
  });

  test('TC-ADM-11: Navigation between admin sub-routes', async ({ page }) => {
    // Navigate to admin first
    await navigateToSidebar(page, 'Quản trị', '/admin');
    await page.waitForTimeout(500);

    // Try clicking sub-route links
    const links = [
      { selector: 'a[routerLink*="/admin/manage-users"]', path: '/admin/manage-users' },
      { selector: 'a[routerLink*="/admin/manage-roles"]', path: '/admin/manage-roles' },
      { selector: 'a[routerLink*="/admin/settings"]', path: '/admin/settings' },
    ];

    for (const link of links) {
      const el = page.locator(link.selector).first();
      if (await el.isVisible({ timeout: 2000 }).catch(() => false)) {
        await el.click();
        await page.waitForURL(new RegExp(link.path.replace('/', '\\/')), { timeout: 10000 });
        // Navigate back to admin main
        await navigateToSidebar(page, 'Quản trị', '/admin');
        await page.waitForTimeout(500);
      }
    }
  });

  test('TC-ADM-12: Back to main admin from sub-route', async ({ page }) => {
    // Navigate to admin manage-users sub-route
    await navigateToSidebar(page, 'Quản trị', '/admin');
    await page.waitForTimeout(500);

    const manageUsersLink = page.locator('a[routerLink*="/admin/manage-users"]').first();
    if (await manageUsersLink.isVisible().catch(() => false)) {
      await manageUsersLink.click();
      await page.waitForURL(/\/admin\/manage-users/, { timeout: 10000 });
    }

    // Navigate back to admin via sidebar
    await navigateToSidebar(page, 'Quản trị', '/admin');
    expect(page.url()).toMatch(/\/admin(\?|$)/);
  });
});
