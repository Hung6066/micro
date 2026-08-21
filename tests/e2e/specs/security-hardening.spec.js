// @ts-check
const { test, expect } = require('@playwright/test');
const { adminUrl, mobileUrl } = require('../config/urls');
const { signInThroughIdentity } = require('../helpers/sso-login');
const { getE2eCredentials } = require('../config/credentials');

const AUTH_LOGIN_RE = /\/auth\/login(?:\?|$)/;
const FORBIDDEN_RE = /\/forbidden(?:\?|$)/;

function parseCookieHeader(cookies, names) {
  return names
    .map(name => {
      const cookie = cookies.find(item => item.name === name);
      return cookie ? `${name}=${cookie.value}` : null;
    })
    .filter(Boolean)
    .join('; ');
}

test.describe('@security Admin SPA hardening', () => {
  test('unauthenticated IAM route does not render protected workspace', async ({ page }) => {
    await page.goto(`${adminUrl}/iam/users`, { waitUntil: 'domcontentloaded' });
    await page.waitForURL(url => AUTH_LOGIN_RE.test(url.pathname + url.search) || FORBIDDEN_RE.test(url.pathname + url.search), {
      timeout: 30000,
    });
    expect(page.url()).toMatch(AUTH_LOGIN_RE);
    await expect(page.getByRole('button', { name: /sign in with his\.hope|đăng nhập bằng his\.hope/i })).toBeVisible({
      timeout: 15000,
    });
  });

  test('authenticated admin can open a guarded IAM route', async ({ page }) => {
    const credentials = getE2eCredentials();
    await signInThroughIdentity(page, adminUrl, {
      email: credentials.email,
      password: credentials.password,
      dashboardPath: '/dashboard',
    });

    await page.goto(`${adminUrl}/users`, { waitUntil: 'domcontentloaded' });
    await page.waitForURL(url => /\/users(?:\?|$)/.test(url.pathname + url.search), { timeout: 30000 });
    await expect(page.locator('hh-page-layout, hh-page-header, .main-content').first()).toBeVisible({
      timeout: 15000,
    });
  });
});

test.describe('@security Admin API CSRF gate', () => {
  test('mutating admin API without CSRF token is rejected', async ({ page, request }) => {
    const credentials = getE2eCredentials();
    await signInThroughIdentity(page, adminUrl, {
      email: credentials.email,
      password: credentials.password,
      dashboardPath: '/dashboard',
    });

    const cookies = await page.context().cookies();
    const sid = cookies.find(item => item.name === 'hishop_sid');
    if (!sid) {
      test.skip(true, 'Admin cookie session is unavailable in this environment.');
    }

    const response = await request.post(`${adminUrl}/api/v1/admin/ldap/sync`, {
      headers: {
        Cookie: parseCookieHeader(cookies, ['hishop_sid']),
      },
    });

    expect(response.status()).toBe(403);
  });

  test('mutating admin API with CSRF token is not rejected for missing CSRF', async ({ page, request }) => {
    const credentials = getE2eCredentials();
    await signInThroughIdentity(page, adminUrl, {
      email: credentials.email,
      password: credentials.password,
      dashboardPath: '/dashboard',
    });

    const cookies = await page.context().cookies();
    const sid = cookies.find(item => item.name === 'hishop_sid');
    const csrf = cookies.find(item => item.name === 'hishop_csrf');
    if (!sid || !csrf) {
      test.skip(true, 'Admin cookie session or CSRF cookie is unavailable in this environment.');
    }

    const response = await request.post(`${adminUrl}/api/v1/admin/ldap/sync`, {
      headers: {
        Cookie: parseCookieHeader(cookies, ['hishop_sid', 'hishop_csrf']),
        'X-CSRF-Token': csrf.value,
      },
    });

    expect(response.status()).not.toBe(403);
  });
});

test.describe('@security Mobile SPA hardening', () => {
  test('unauthenticated mobile admin route redirects to login', async ({ page, request }) => {
    const probe = await request.get(`${mobileUrl}/auth/login`).catch(() => null);
    if (!probe || !probe.ok()) {
      test.skip(true, 'Mobile dev server is not reachable on E2E_MOBILE_URL.');
    }

    await page.goto(`${mobileUrl}/admin/users`, { waitUntil: 'domcontentloaded' });
    await page.waitForURL(url => /\/auth\/login(?:\?|$)/.test(url.pathname + url.search), { timeout: 30000 });
    await expect(
      page.getByRole('button', { name: /sign in securely|đăng nhập an toàn/i }),
    ).toBeVisible({ timeout: 15000 });
  });
});
