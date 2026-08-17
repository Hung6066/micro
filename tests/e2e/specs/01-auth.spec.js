const { test, expect } = require('@playwright/test');

const { clinicalUrl: BASE_URL } = require('../config/urls');
const { signInThroughIdentity, gotoCommittedDocument } = require('../helpers/sso-login');

test.describe('Authentication', () => {
  test('TC-AUTH-01: login page exposes the canonical SSO action', async ({ page }) => {
    await gotoCommittedDocument(page, `${BASE_URL}/auth/login`);
    await expect(page.getByRole('button', { name: /sign in with his\.hope/i })).toBeVisible();
    await expect(page.locator('input[formControlName="username"]')).toHaveCount(0);
    await expect(page.locator('input[formControlName="password"]')).toHaveCount(0);
  });

  test('TC-AUTH-02: protected route redirects to login when unauthenticated', async ({ page }) => {
    await gotoCommittedDocument(page, `${BASE_URL}/dashboard`);
    await page.waitForURL(/\/auth\/login/, { timeout: 10000 });
    expect(page.url()).toMatch(/\/auth\/login/);
  });

  test('TC-AUTH-03: SSO returns to the dashboard', async ({ page }) => {
    await signInThroughIdentity(page, BASE_URL);
    expect(page.url()).toMatch(/\/dashboard(?:\?|$)/);
  });

  test('TC-AUTH-04: logout returns to login', async ({ page }) => {
    await signInThroughIdentity(page, BASE_URL);
    const logout = page.getByRole('button', { name: /logout|đăng xuất/i }).first();
    await expect(logout).toBeVisible({ timeout: 30000 });
    await logout.click();
    await page.waitForURL(/\/auth\/login/, { timeout: 15000 });
  });

  test('TC-AUTH-05: deep link is preserved through the SSO flow', async ({ page }) => {
    await gotoCommittedDocument(page, `${BASE_URL}/patients`);
    await page.waitForURL(/\/auth\/login/, { timeout: 10000 });
    await signInThroughIdentity(page, BASE_URL, { dashboardPath: '/patients' });
    expect(page.url()).toMatch(/\/patients(?:\?|$)/);
  });
});
