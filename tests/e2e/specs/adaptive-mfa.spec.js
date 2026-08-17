const { test, expect } = require('@playwright/test');
const fs = require('fs');
const path = require('path');
const { IdentityRoutes } = require('../support/identity-routes');

const MFA_PAGE_URL = 'http://localhost:5000/Account/Mfa';
const identityScript = path.join(__dirname, '..', '..', '..', 'src', 'Services', 'IdentityService', 'IdentityService.Api', 'wwwroot', 'js', 'identity-login.js');
const endpointExtensions = path.join(__dirname, '..', '..', '..', 'src', 'Services', 'IdentityService', 'IdentityService.Api', 'Composition', 'IdentityServiceEndpointExtensions.cs');
const mfaEndpoints = path.join(__dirname, '..', '..', '..', 'src', 'Services', 'IdentityService', 'IdentityService.Api', 'Endpoints', 'MfaEndpoints.cs');

async function installAdaptiveMfaHarness(page, options = {}) {
  const {
    mfaMethods = {
      preferredMethod: 'passkey',
      availableMethods: ['passkey', 'mobileApproval', 'totp'],
      isUnfamiliarDevice: false,
      redirectHandle: '/',
    },
    pollStatuses = [202, 200],
    pollRedirectUrl = '/connect/authorize/callback?code=mobile-code&state=pkce-state',
    nativeStartStatus = 200,
    passkeyOptionsStatus = 200,
    passkeyCompleteStatus = 200,
    passkeyRedirectUrl = '/connect/authorize/callback?code=passkey-code&state=pkce-state',
    credentialResult = 'success',
  } = options;

  await page.addInitScript(({ credentialResult }) => {
    const encoder = new TextEncoder();
    let fakeClockOffsetMs = 0;
    const realNow = Date.now.bind(Date);
    const recordOpenedLink = url => {
      if (!url) return;
      window.__adaptiveMfa.openedLinks.push(url);
      const openedLinks = JSON.parse(localStorage.getItem('adaptiveMfaOpenedLinks') || '[]');
      openedLinks.push(url);
      localStorage.setItem('adaptiveMfaOpenedLinks', JSON.stringify(openedLinks));
    };

    if (!localStorage.getItem('adaptiveMfaOpenedLinks')) {
      localStorage.setItem('adaptiveMfaOpenedLinks', JSON.stringify([]));
    }
    window.__adaptiveMfa = {
      openedLinks: [],
      credentialCalls: 0,
      pollCount: 0,
    };
    Date.now = () => realNow() + fakeClockOffsetMs;
    window.open = () => {
      const openedWindow = {
        closed: false,
        opener: window,
        close() {
          this.closed = true;
        },
        location: {
          replace: recordOpenedLink,
        },
      };
      Object.defineProperty(openedWindow.location, 'href', {
        configurable: true,
        set: recordOpenedLink,
      });
      return openedWindow;
    };
    Object.defineProperty(window, 'PublicKeyCredential', {
      configurable: true,
      value: function PublicKeyCredential() {},
    });
    Object.defineProperty(navigator, 'credentials', {
      configurable: true,
      value: {
      get: async () => {
        window.__adaptiveMfa.credentialCalls += 1;
        if (credentialResult === 'cancel') return null;
        return {
          id: 'credential-id',
          rawId: encoder.encode('raw-id').buffer,
          type: 'public-key',
          response: {
            clientDataJSON: encoder.encode('client-data').buffer,
            authenticatorData: encoder.encode('authenticator-data').buffer,
            signature: encoder.encode('signature').buffer,
            userHandle: encoder.encode('user-123').buffer,
          },
        };
      },
      },
    });
    window.setTimeout = (callback, timeoutMs = 0) => {
      fakeClockOffsetMs += Math.max(Number(timeoutMs) || 0, 1);
      callback();
      return 0;
    };
  }, { credentialResult });

  await page.route(`**${IdentityRoutes.PasskeyMfaOptions}`, async route => {
    await route.fulfill({
      status: passkeyOptionsStatus,
      contentType: 'application/json',
      body: JSON.stringify({
        userId: '11111111-1111-1111-1111-111111111111',
        options: {
          challenge: 'Y2hhbGxlbmdl',
          allowCredentials: [{ type: 'public-key', id: 'Y3JlZGVudGlhbA' }],
          userVerification: 'required',
        },
      }),
    });
  });

  // The synthetic page does not have a real browser session cookie. Stub the
  // post-MFA BFF exchange so the test verifies callback selection rather than
  // depending on a live Identity session.
  await page.route(`**${IdentityRoutes.SessionExchange}`, async route => {
    await route.fulfill({ status: 204, body: '' });
  });

  await page.route(`**${IdentityRoutes.MfaMethods}`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mfaMethods),
    });
  });

  await page.route(`**${IdentityRoutes.PasskeyMfaComplete}`, async route => {
    await route.fulfill({
      status: passkeyCompleteStatus,
      contentType: 'application/json',
      body: JSON.stringify({ redirectUrl: passkeyRedirectUrl }),
    });
  });

  await page.route(`**${IdentityRoutes.NativeMfaStart}`, async route => {
    await route.fulfill({
      status: nativeStartStatus,
      contentType: 'application/json',
      body: JSON.stringify({
        ticket: 'ticket-123',
        deepLink: 'hishope://auth/mfa?ticket=ticket-123',
        expiresInMs: 120000,
      }),
    });
  });

  await page.route(`**${IdentityRoutes.NativeMfaPoll}?**`, async route => {
    const next = pollStatuses[Math.min(await page.evaluate(() => window.__adaptiveMfa.pollCount++), pollStatuses.length - 1)];
    const body = next === 200
      ? { redirectUrl: pollRedirectUrl }
      : next === 409
        ? { detail: 'Mobile approval was rejected in the His.Hope mobile app.' }
        : next === 410
          ? { detail: 'Mobile approval expired. Retry the request from this page.' }
          : { status: 'pending' };

    await route.fulfill({
      status: next,
      contentType: 'application/json',
      body: JSON.stringify(body),
    });
  });

  await page.route('**/connect/authorize/callback?**', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'text/html',
      body: '<!doctype html><title>OIDC callback preserved</title><main>OIDC callback preserved</main>',
    });
  });
}

async function openMfaPage(page, { nativePrimary = false } = {}) {
  const preferredMethod = nativePrimary ? 'mobileApproval' : 'passkey';
  await page.route(MFA_PAGE_URL, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'text/html',
      body: `
    <!doctype html>
    <html lang="en">
      <body>
        <main id="adaptive-mfa">
        <section class="card"
          data-mfa-methods-endpoint="${IdentityRoutes.MfaMethods}"
          data-preferred-method="${preferredMethod}"
          data-available-methods="passkey,mobileApproval,totp"
          data-native-hardware-unverified="true">
          <h1>Verify your identity</h1>
          <p id="mfa-status" role="status" aria-live="polite"></p>
          <p id="mfa-error" role="alert" aria-live="assertive" hidden></p>
          <div id="primary-actions">
            ${nativePrimary ? '<button id="native-passkey-mfa" type="button">Approve in His.Hope mobile app</button>' : ''}
            <button id="passkey-mfa" type="button">Continue with device passkey</button>
            <button id="alternate-methods" type="button" aria-controls="alternate-method-panel" aria-expanded="false">Use another method</button>
          </div>
          <section id="alternate-method-panel" hidden aria-label="Alternate verification methods">
            <div id="alternate-actions">
              ${nativePrimary ? '' : '<button id="native-passkey-mfa" type="button">Approve in His.Hope mobile app</button>'}
            </div>
            <form id="totp-form" method="post" action="/Account/Mfa">
              <label for="totp-code">Authenticator code</label>
              <input id="totp-code" name="code" inputmode="numeric" autocomplete="one-time-code" maxlength="6" pattern="[0-9]{6}">
              <button type="submit">Verify with TOTP</button>
            </form>
          </section>
        </section>
        </main>
      </body>
    </html>
  `,
    });
  });
  await page.goto(MFA_PAGE_URL);
  await page.addScriptTag({ path: identityScript });
}

test.describe('Adaptive passkey-first MFA source integration contract', () => {
  test('binds the real server page to server-derived MFA methods', () => {
    const pageSource = fs.readFileSync(endpointExtensions, 'utf8');
    const apiSource = fs.readFileSync(mfaEndpoints, 'utf8');
    const scriptSource = fs.readFileSync(identityScript, 'utf8');

    expect(pageSource).toContain('app.MapGet("/Account/Mfa", async (HttpContext httpContext, string? error, OidcLoginCompletionService completion');
    expect(pageSource).toContain('completion.GetPendingMfaMethodsAsync(httpContext, ct)');
    expect(pageSource).toContain(`data-mfa-methods-endpoint="${IdentityRoutes.MfaMethods}"`);
    expect(pageSource).toContain('id="primary-actions"');
    expect(pageSource).toContain('id="alternate-method-panel"');
    expect(apiSource).toContain('group.MapGet(IdentityApiRoutes.MfaMethodsSegment');
    expect(scriptSource).toContain("fetch(methodsEndpoint");
  });
});

test.describe('Adaptive passkey-first MFA synthetic browser contract', () => {
  test('starts passkey only after a user gesture and preserves the server callback redirect', async ({ page }) => {
    await installAdaptiveMfaHarness(page);
    await openMfaPage(page);

    expect(await page.evaluate(() => window.__adaptiveMfa.credentialCalls)).toBe(0);
    await expect(page.locator('#passkey-mfa')).toBeVisible();
    await page.locator('#passkey-mfa').click();

    await expect(page).toHaveURL('http://localhost:5000/connect/authorize/callback?code=passkey-code&state=pkce-state');
  });

  test('discloses alternate methods and keeps TOTP as a fallback without restarting OIDC', async ({ page }) => {
    await installAdaptiveMfaHarness(page);
    await openMfaPage(page);

    await expect(page.locator('#alternate-method-panel')).toBeHidden();
    await page.locator('#alternate-methods').click();

    await expect(page.locator('#alternate-method-panel')).toBeVisible();
    await expect(page.locator('#alternate-methods')).toHaveAttribute('aria-expanded', 'true');
    await expect(page.locator('#totp-form')).toHaveAttribute('action', '/Account/Mfa');
    await expect(page.locator('#totp-code')).toHaveAttribute('autocomplete', 'one-time-code');
  });

  test('uses mobile approval first for unfamiliar-device flows and consumes the approved ticket once', async ({ page }) => {
    await installAdaptiveMfaHarness(page, {
      mfaMethods: {
        preferredMethod: 'mobileApproval',
        availableMethods: ['passkey', 'mobileApproval', 'totp'],
        isUnfamiliarDevice: true,
        redirectHandle: '/',
      },
      pollStatuses: [202, 200],
    });
    await openMfaPage(page, { nativePrimary: true });

    await expect(page.locator('#native-passkey-mfa')).toBeVisible();
    await page.locator('#native-passkey-mfa').click();

    await expect.poll(() => page.evaluate(() => JSON.parse(localStorage.getItem('adaptiveMfaOpenedLinks') || '[]'))).toEqual([
      'hishope://auth/mfa?ticket=ticket-123',
    ]);
    await expect(page).toHaveURL('http://localhost:5000/connect/authorize/callback?code=mobile-code&state=pkce-state');
  });

  test('re-enables passkey after user cancel without leaking a credential response', async ({ page }) => {
    await installAdaptiveMfaHarness(page, { credentialResult: 'cancel' });
    await openMfaPage(page);

    await page.locator('#passkey-mfa').click();

    await expect(page.locator('#mfa-error')).toContainText('Passkey verification was cancelled.');
    await expect(page.locator('#passkey-mfa')).toBeEnabled();
  });

  test('reports replay expiry or session mismatch from the native approval poll', async ({ page }) => {
    await installAdaptiveMfaHarness(page, {
      mfaMethods: {
        preferredMethod: 'mobileApproval',
        availableMethods: ['passkey', 'mobileApproval', 'totp'],
        isUnfamiliarDevice: true,
        redirectHandle: '/',
      },
      pollStatuses: [409],
    });
    await openMfaPage(page, { nativePrimary: true });

    await page.locator('#native-passkey-mfa').click();

    await expect(page.locator('#mfa-error')).toContainText('Mobile approval was rejected');
    await expect(page.locator('#native-passkey-mfa')).toBeEnabled();
    await expect(page).toHaveURL(MFA_PAGE_URL);
  });

  test('times out native approval without assuming mobile hardware is present', async ({ page }) => {
    await installAdaptiveMfaHarness(page, {
      mfaMethods: {
        preferredMethod: 'mobileApproval',
        availableMethods: ['passkey', 'mobileApproval', 'totp'],
        isUnfamiliarDevice: true,
        redirectHandle: '/',
      },
      pollStatuses: [202],
    });
    await openMfaPage(page, { nativePrimary: true });

    await page.locator('#native-passkey-mfa').click();

    await expect(page.locator('#mfa-error')).toContainText('Mobile approval timed out');
    await expect(page.locator('#native-passkey-mfa')).toBeEnabled();
    await expect(page).toHaveURL(MFA_PAGE_URL);
  });
});
