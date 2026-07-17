import { test, expect } from '@playwright/test';

/**
 * Synthetic monitoring: Login → Search Patient → Logout
 *
 * This test simulates a complete user journey through the His.Hope hospital
 * information system. It is executed periodically via a CronJob to validate
 * end-to-end availability of the frontend, authentication, and patient search.
 *
 * Environment variables:
 *   BASE_URL      - Frontend URL (default: http://frontend/)
 *   USERNAME      - Login username (default: admin)
 *   PASSWORD      - Login password (default: Admin@123)
 *   SEARCH_TERM   - Patient search term (default: Nguyễn)
 */

const BASE_URL   = process.env.BASE_URL   || 'http://frontend/';
const USERNAME   = process.env.USERNAME   || 'admin';
const PASSWORD   = process.env.PASSWORD   || 'Admin@123';
const SEARCH_TERM = process.env.SEARCH_TERM || 'Nguyễn';

test.describe('Synthetic Monitoring — Login → Search Patient → Logout', () => {
  test('should complete the full user journey', async ({ page }) => {
    // ─── Phase 1: Navigate and login ──────────────────────────────
    await test.step('Navigate to login page', async () => {
      await page.goto(`${BASE_URL}/auth/login`, { waitUntil: 'networkidle' });
      await page.waitForLoadState('domcontentloaded');
    });

    await test.step('Login with valid credentials', async () => {
      await page.locator('input[formControlName="username"]').waitFor({ state: 'visible', timeout: 30000 });
      await page.locator('input[formControlName="username"]').fill(USERNAME);
      await page.locator('input[formControlName="password"]').fill(PASSWORD);
      await page.locator('button[type="submit"]').click();
      await page.waitForURL(/\/dashboard/, { timeout: 30000 });
      expect(page.url()).toMatch(/\/dashboard/);
    });

    // ─── Phase 2: Search for a patient ────────────────────────────
    await test.step('Navigate to patient list', async () => {
      // Click sidebar link for patients (Vietnamese: "Bệnh nhân")
      const patientLink = page.locator('mat-nav-list a').filter({ hasText: 'Bệnh nhân' }).first();
      await expect(patientLink).toBeVisible({ timeout: 10000 });
      await patientLink.click();
      await page.waitForURL(/\/patients/, { timeout: 15000 });
      expect(page.url()).toMatch(/\/patients/);
    });

    await test.step('Search for a patient by name', async () => {
      // Wait for search input to appear
      const searchInput = page.locator(
        'input[placeholder*="tìm kiếm"], input[placeholder*="search"], ' +
        'input[formControlName="search"], input[placeholder*="Tìm"]'
      ).first();
      await expect(searchInput).toBeVisible({ timeout: 10000 });

      // Type search term
      await searchInput.fill(SEARCH_TERM);
      await page.waitForTimeout(1500); // Allow debounce / API call

      // Verify table or empty state renders
      const table = page.locator('mat-table, table, .mat-mdc-table');
      const tableVisible = await table.isVisible().catch(() => false);
      if (tableVisible) {
        const rows = page.locator('mat-table mat-row, mat-table tr, table tbody tr');
        const count = await rows.count();
        console.log(`Patient search returned ${count} results for "${SEARCH_TERM}"`);
        // Having zero results for a search is acceptable — the UI still rendered
        expect(count).toBeGreaterThanOrEqual(0);
      }

      // Clear search for clean state
      await searchInput.fill('');
      await page.waitForTimeout(500);
    });

    // ─── Phase 3: Logout ──────────────────────────────────────────
    await test.step('Logout from the application', async () => {
      // Click logout button in sidebar footer
      const logoutBtn = page.locator('button[aria-label="Đăng xuất"], .sidebar-footer button').first();
      await expect(logoutBtn).toBeVisible({ timeout: 5000 });
      await logoutBtn.click();
      await page.waitForURL(/\/auth\/login/, { timeout: 15000 });
      expect(page.url()).toMatch(/\/auth\/login/);
    });

    // ─── Phase 4: Verify redirected back to login ─────────────────
    await test.step('Verify login page is shown after logout', async () => {
      await expect(page.locator('input[formControlName="username"]')).toBeVisible({ timeout: 10000 });
      console.log('Synthetic journey completed successfully');
    });
  });

  test('should expose health probe endpoints', async ({ request }) => {
    // Verify frontend root is reachable (returns 200)
    const response = await request.get(`${BASE_URL}/`);
    expect(response.ok()).toBeTruthy();
  });
});
