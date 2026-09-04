const { expect } = require('@playwright/test');
const { getE2eCredentials } = require('../config/credentials');

/**
 * Logs into the current application through the Identity Service SSO flow.
 * The application owns only the SSO button; credentials are entered only on
 * the Identity Service page when the local test environment exposes it.
 */
async function signInThroughIdentity(page, baseUrl, options = {}) {
  const credentials = options.email && options.password
    ? { email: options.email, password: options.password }
    : getE2eCredentials();
  const dashboardPath = options.dashboardPath || '/en/dashboard';
  const loginPath = dashboardPath.replace(/\/[^/]+$/, '/auth/login');
  const routePattern = path => {
    const normalized = path.replace(/^\/en(?=\/)/, '');
    return new RegExp(`(?:\\/en)?${normalized.replace(/[.*+?^${}()|[\\]\\\\]/g, '\\$&')}(?:[\\/?]|$)`);
  };
  // Angular's bootstrap can keep DOMContentLoaded pending while it resolves
  // the OIDC/BFF session. Wait for the document commit, then assert the app
  // state explicitly below instead of treating navigation timing as auth.
  // Enter through the SPA login route so a stale/previously authenticated
  // dashboard URL cannot be mistaken for a completed sign-in. The returnUrl
  // is carried by the SPA and then by Identity's server form.
  await gotoCommittedDocument(page, `${baseUrl}${loginPath}?returnUrl=${encodeURIComponent(dashboardPath)}`);
  // Angular resolves the auth guard after the document commit; wait for the
  // resulting route before deciding whether to start Identity SSO.
  await page.waitForURL(
    url => routePattern(loginPath).test(url.pathname + url.search)
      || routePattern(dashboardPath).test(url.pathname + url.search),
    { timeout: 30000 },
  );
  // A proxy response can commit the Identity document before its HTML parser
  // has populated the login form. Synchronize on the document lifecycle once
  // the URL is known instead of racing the first DOM query.
  await page.waitForLoadState('domcontentloaded', { timeout: 10000 }).catch(() => {});

  const isAppLogin = () => /(?:^|\/)auth\/login$/.test(new URL(page.url()).pathname);
  const isTargetRoute = () => routePattern(dashboardPath).test(new URL(page.url()).pathname + new URL(page.url()).search)
    || (dashboardPath === '/clients' && routePattern('/dashboard').test(new URL(page.url()).pathname + new URL(page.url()).search));
  const appLoginButton = page.getByRole('button', { name: /sign in with his\.hope|đăng nhập bằng his\.hope/i });
  const authenticatedShell = page.locator('mat-nav-list a, nav[hhShellNavigation] a').first();
  if (isAppLogin()) {
    await Promise.any([
      authenticatedShell.waitFor({ state: 'visible', timeout: 10000 }),
      appLoginButton.waitFor({ state: 'visible', timeout: 10000 }),
      page.waitForURL(() => isTargetRoute(), { timeout: 10000 }),
    ]).catch(() => {});
    // A protected shell can briefly render its public Dashboard link while the
    // login component is still bootstrapping. Only treat it as authenticated
    // when the SSO action is no longer present and the shell has more than its
    // public fallback link.
    if (!await appLoginButton.isVisible().catch(() => false)
      && isTargetRoute()) {
      return page.url();
    }
    if (!await appLoginButton.isVisible().catch(() => false)
      && await authenticatedShell.isVisible().catch(() => false)
      && await page.locator('mat-nav-list a, nav[hhShellNavigation] a').count() > 1) {
      return page.url();
    }
  }
  if (isAppLogin() || await appLoginButton.isVisible().catch(() => false)) {
    const button = appLoginButton;
    await expect(button).toBeVisible({ timeout: 30000 });
    // The BFF action is a full-document redirect. Do not make Playwright
    // wait for the old document's load lifecycle; the URL assertion below is
    // the authoritative navigation gate.
    // Angular can replace the button during the final bootstrap tick. Force
    // the click through the current locator so a detached/stability race does
    // not turn a valid SSO flow into a flaky E2E failure.
    // The auth guard can replace the login component in the same tick that
    // exposes the button. Resolve the current locator immediately before the
    // click and retry a short, bounded number of times when Angular detaches
    // the old element. This keeps the test synchronized with the real SSO
    // redirect instead of turning a harmless DOM replacement into a failure.
    let clicked = false;
    for (let attempt = 0; attempt < 3 && !clicked; attempt += 1) {
      try {
        const currentButton = page.getByRole('button', { name: /sign in with his\.hope|đăng nhập bằng his\.hope/i });
        await currentButton.waitFor({ state: 'visible', timeout: 5000 });
        await currentButton.click({ noWaitAfter: true, force: true });
        clicked = true;
      } catch (error) {
        if (!isAppLogin()) break;
        if (attempt === 2) throw error;
        await page.waitForTimeout(250);
      }
    }

    // The BFF flow performs a full-document navigation to Identity. Docker
    // Desktop can acknowledge the click before it exposes the new document,
    // so wait for the server route explicitly and retry the idempotent click
    // once if the SPA is still on its login route.
    const reachedIdentity = async () => {
      try {
        await page.waitForURL(url => /\/Account\/Login(?:\?|$)/i.test(url.pathname + url.search), { timeout: 15000 });
        return true;
      } catch {
        return false;
      }
    };
    if (!await reachedIdentity() && isAppLogin()) {
      const retryButton = page.getByRole('button', { name: /sign in with his\.hope|đăng nhập bằng his\.hope/i });
      if (await retryButton.isVisible().catch(() => false)) {
        await retryButton.click({ force: true, noWaitAfter: true });
      }
      // A navigation can still be in flight even after the old SPA document
      // stopped exposing the button. In that case wait for the same identity
      // document instead of attempting a second click on a detached element.
      await page.waitForURL(url => /\/Account\/Login(?:\?|$)/i.test(url.pathname + url.search), { timeout: 15000 });
    }
  }

  const email = page.locator('input[type="email"], input#email').first();
  const continueWorkspace = page
    .getByRole('button', { name: /continue to workspace|tiếp tục.*workspace|tiếp tục/i })
    .or(page.getByRole('link', { name: /continue to workspace|tiếp tục.*workspace|tiếp tục/i }))
    .first();
  try {
    await Promise.any([
      email.waitFor({ state: 'visible', timeout: 30000 }),
      continueWorkspace.waitFor({ state: 'visible', timeout: 30000 }),
      page.waitForURL(
        url => url.origin === new URL(baseUrl).origin
          && new RegExp(`${dashboardPath.replace('/', '\\/')}(?:\\?|$)`).test(url.pathname + url.search),
        { timeout: 30000 },
      ),
    ]);
  } catch (error) {
    const diagnostics = await page.evaluate(() => ({
      url: window.location.href,
      title: document.title,
      inputs: Array.from(document.querySelectorAll('input')).map((input) => ({
        id: input.id,
        type: input.type,
        name: input.getAttribute('name'),
        visible: Boolean(input.offsetWidth || input.offsetHeight || input.getClientRects().length),
      })),
      buttons: Array.from(document.querySelectorAll('button')).map((button) => ({
        text: (button.textContent || '').trim().slice(0, 120),
        visible: Boolean(button.offsetWidth || button.offsetHeight || button.getClientRects().length),
      })).slice(0, 12),
    })).catch(() => ({ url: page.url(), title: 'unavailable' }));
    throw new Error(`SSO state was not reached: ${JSON.stringify(diagnostics)}`, { cause: error });
  }

  if (await email.isVisible().catch(() => false)) {
    await expect(email).toBeVisible({ timeout: 15000 });
    await email.fill(credentials.email);
    await page.locator('input[type="password"]').first().fill(credentials.password);
    // Identity's credential submit performs a full-document redirect. Avoid
    // waiting on the old document's load lifecycle; the URL gate below is
    // authoritative and is more stable in Docker Chromium.
    await page.locator('button[type="submit"]').first().click({ noWaitAfter: true });
  }

  // An already-authenticated Identity session may render its consent/continue
  // page instead of the credential form. Treat that action as a successful
  // SSO leg and wait for the application callback below.
  if (await continueWorkspace.isVisible().catch(() => false)) {
    await continueWorkspace.click({ noWaitAfter: true });
  }

  // If the SPA returned to its login route without exposing the Identity
  // document (a transient Docker/browser redirect race), restart the same
  // idempotent SSO action once before treating the flow as failed.
  if (isAppLogin() || await page.getByRole('button', { name: /sign in with his\.hope|đăng nhập bằng his\.hope/i }).isVisible().catch(() => false)) {
    const retryLogin = page.getByRole('button', { name: /sign in with his\.hope|đăng nhập bằng his\.hope/i });
    if (await retryLogin.isVisible().catch(() => false)) {
      await retryLogin.click({ force: true, noWaitAfter: true });
      await page.waitForURL(url => /\/Account\/Login(?:\?|$)/i.test(url.pathname + url.search), { timeout: 15000 });
      const retryEmail = page.locator('input[type="email"], input#email').first();
      const retryContinue = page
        .getByRole('button', { name: /continue to workspace|tiếp tục.*workspace|tiếp tục/i })
        .or(page.getByRole('link', { name: /continue to workspace|tiếp tục.*workspace|tiếp tục/i }))
        .first();
      await Promise.any([
        retryEmail.waitFor({ state: 'visible', timeout: 15000 }),
        retryContinue.waitFor({ state: 'visible', timeout: 15000 }),
      ]);
      if (await retryContinue.isVisible().catch(() => false)) {
        await retryContinue.click({ noWaitAfter: true });
      } else {
        await retryEmail.fill(credentials.email);
        await page.locator('input[type="password"]').first().fill(credentials.password);
        await page.locator('button[type="submit"]').first().click({ noWaitAfter: true });
      }
    }
  }

  const authenticatedRoute = url => url.origin === new URL(baseUrl).origin
    && routePattern(dashboardPath).test(url.pathname + url.search);
  const authenticatedDefaultRoute = url => dashboardPath === '/clients'
    && url.origin === new URL(baseUrl).origin
    && routePattern('/dashboard').test(url.pathname + url.search);
  try {
    await page.waitForURL(url => authenticatedRoute(url) || authenticatedDefaultRoute(url), { timeout: 30000 });
    if (authenticatedDefaultRoute(new URL(page.url()))) {
      await gotoCommittedDocument(page, `${baseUrl}${dashboardPath}`);
      await page.waitForURL(authenticatedRoute, { timeout: 15000 });
    }
  } catch (error) {
    // A callback can land on the localized SPA login route when the BFF
    // exchange completes just after the first guard evaluation. Re-enter the
    // target route once; this preserves the session without re-entering creds.
    if (!isAppLogin()) throw error;
    await page.waitForTimeout(500);
    const recoveryLogin = page.getByRole('button', { name: /sign in with his\.hope|đăng nhập bằng his\.hope/i });
    if (await recoveryLogin.isVisible().catch(() => false)) {
      await recoveryLogin.click({ force: true, noWaitAfter: true });
      await page.waitForURL(url => /\/Account\/Login(?:\?|$)/i.test(url.pathname + url.search), { timeout: 15000 });
      const recoveryEmail = page.locator('input[type="email"], input#email').first();
      const recoveryContinue = page
        .getByRole('button', { name: /continue to workspace|tiếp tục.*workspace|tiếp tục/i })
        .or(page.getByRole('link', { name: /continue to workspace|tiếp tục.*workspace|tiếp tục/i }))
        .first();
      await Promise.any([
        recoveryEmail.waitFor({ state: 'visible', timeout: 15000 }),
        recoveryContinue.waitFor({ state: 'visible', timeout: 15000 }),
      ]);
      if (await recoveryContinue.isVisible().catch(() => false)) {
        await recoveryContinue.click({ noWaitAfter: true });
      } else {
        await recoveryEmail.fill(credentials.email);
        await page.locator('input[type="password"]').first().fill(credentials.password);
        await page.locator('button[type="submit"]').first().click({ noWaitAfter: true });
      }
    } else {
      await gotoCommittedDocument(page, `${baseUrl}${dashboardPath}`);
    }
    await page.waitForURL(authenticatedRoute, { timeout: 30000 });
  }
  // The route can change before the OnPush shell receives /auth/me. Wait for
  // one real navigation item so module specs do not race the authenticated
  // shell bootstrap.
  await page.locator('mat-nav-list a').first().waitFor({ state: 'visible', timeout: 15000 }).catch(() => {});
  return page.url();
}

async function gotoCommittedDocument(page, url) {
  if (process.env.E2E_RUNTIME_ENV === 'development') {
    await page.addInitScript(() => {
      window.__HISHOPE_RUNTIME_CONFIG__ = {
        environment: 'development',
        contractVersion: '1',
        apiOrigin: window.location.origin,
        oidcAuthority: window.location.origin,
      };
      window.__HISHOPE_CONFIG__ = {
        apiOrigin: window.location.origin,
        oidcAuthority: window.location.origin,
        production: false,
      };
    });
  }
  const origin = new URL(url).origin;
  let lastError;
  for (let attempt = 0; attempt < 5; attempt += 1) {
    try {
      await page.goto(url, { waitUntil: 'commit', timeout: 10000 });
      return;
    } catch (error) {
      lastError = error;
      // Docker Desktop can deliver the response while Chromium's navigation
      // lifecycle acknowledgement times out. Continue only when the target
      // document is already committed; otherwise retry the transport.
      if (page.url().startsWith(origin)) return;
      await page.waitForTimeout(400);
    }
  }
  throw lastError;
}

module.exports = { signInThroughIdentity, gotoCommittedDocument };
