const { test, expect } = require('@playwright/test');
const { mobileUrl } = require('../config/urls');
const { signInThroughMobileIdentity } = require('../helpers/mobile-sso-login');

const MOBILE_URL = mobileUrl;
const MOBILE_AUTH_AVAILABLE =
  process.env.E2E_AUTH_PROBE_URL && process.env.E2E_AUTH_TOKEN;

test.describe('@mobile-smoke His.Hope Mobile', () => {
  test('login screen renders the secure sign-in affordance', async ({ page }) => {
    await page.goto(`${MOBILE_URL}/auth/login`);
    await expect(page.getByRole('heading')).toContainText(/clinical access|truy cập lâm sàng/i);
    await expect(
      page.getByRole('button', { name: /sign in securely|đăng nhập an toàn/i }),
    ).toBeVisible();
  });

  test('unauthenticated admin routes redirect to login', async ({ page }) => {
    await page.goto(`${MOBILE_URL}/admin/dashboard`);
    await page.waitForLoadState('networkidle');
    const url = page.url();
    expect(
      url.includes('/auth/login') ||
        url.includes('/connect/authorize') ||
        url.includes('login'),
    ).toBeTruthy();
  });
});

test.describe('@mobile-smoke authenticated mobile workspace', () => {
  test.skip(
    !MOBILE_AUTH_AVAILABLE,
    'Requires E2E auth probe credentials and a running mobile server',
  );

  test('dashboard loads after OIDC sign-in', async ({ page }) => {
    await signInThroughMobileIdentity(page, MOBILE_URL, {
      dashboardPath: '/admin/dashboard',
    });
    await expect(page).toHaveURL(/\/admin\/dashboard/);
    await expect(page.locator('.mobile-shell')).toBeVisible();
    await expect(
      page.getByText(/overview|tổng quan|good to see you|chào mừng bạn/i),
    ).toBeVisible({ timeout: 15000 });
  });

  test('bottom navigation reaches the users list', async ({ page }) => {
    await signInThroughMobileIdentity(page, MOBILE_URL, {
      dashboardPath: '/admin/dashboard',
    });
    const usersNav = page.locator('hh-mobile-config-nav a').filter({
      hasText: /users|người dùng/i,
    });
    await expect(usersNav.first()).toBeVisible({ timeout: 15000 });
    await usersNav.first().click();
    await expect(page).toHaveURL(/\/admin\/users/);
  });
});
