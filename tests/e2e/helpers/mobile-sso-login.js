const { expect } = require('@playwright/test');
const { getE2eCredentials } = require('../config/credentials');
const { gotoCommittedDocument } = require('./sso-login');

const { email: DEFAULT_EMAIL, password: DEFAULT_PASSWORD } = getE2eCredentials();

/**
 * Signs into the mobile app through direct OIDC (Authorization Code + PKCE).
 * Unlike the clinical BFF flow, mobile exposes a single "Sign in securely"
 * button that redirects to Identity Service.
 */
async function signInThroughMobileIdentity(page, baseUrl, options = {}) {
  const dashboardPath = options.dashboardPath || '/admin/dashboard';
  const loginPath = '/auth/login';
  const authenticatedRoute = (url) =>
    url.origin === new URL(baseUrl).origin
    && new RegExp(`${dashboardPath.replace('/', '\\/')}(?:\\?|$)`).test(
      url.pathname + url.search,
    );

  await gotoCommittedDocument(page, `${baseUrl}${loginPath}`);
  await page.waitForURL(
    (url) => /\/auth\/login(?:\?|$)/.test(url.pathname + url.search)
      || authenticatedRoute(url),
    { timeout: 30000 },
  );

  if (!authenticatedRoute(new URL(page.url()))) {
    const loginButton = page.getByRole('button', {
      name: /sign in securely|đăng nhập an toàn/i,
    });
    await expect(loginButton).toBeVisible({ timeout: 30000 });
    await loginButton.click({ noWaitAfter: true });
    await page.waitForURL(
      (url) => /\/Account\/Login|\/connect\/authorize/i.test(url.pathname + url.search),
      { timeout: 30000 },
    );
  }

  const email = page.locator('input[type="email"], input#email').first();
  const continueWorkspace = page
    .getByRole('button', { name: /continue to workspace|tiếp tục.*workspace|tiếp tục/i })
    .or(page.getByRole('link', { name: /continue to workspace|tiếp tục.*workspace|tiếp tục/i }))
    .first();

  await Promise.race([
    email.waitFor({ state: 'visible', timeout: 30000 }),
    continueWorkspace.waitFor({ state: 'visible', timeout: 30000 }),
    page.waitForURL(authenticatedRoute, { timeout: 30000 }),
  ]);

  if (await email.isVisible().catch(() => false)) {
    await email.fill(options.email || DEFAULT_EMAIL);
    await page.locator('input[type="password"]').first().fill(options.password || DEFAULT_PASSWORD);
    await page.locator('button[type="submit"]').first().click({ noWaitAfter: true });
  } else if (await continueWorkspace.isVisible().catch(() => false)) {
    await continueWorkspace.click({ noWaitAfter: true });
  }

  await page.waitForURL(authenticatedRoute, { timeout: 60000 });
  await page.locator('.mobile-shell').waitFor({ state: 'visible', timeout: 15000 });
  return page.url();
}

module.exports = { signInThroughMobileIdentity };
