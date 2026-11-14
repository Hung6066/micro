const crypto = require('crypto');
const { spawnSync } = require('child_process');
const { chromium } = require('../tests/e2e/node_modules/playwright');

const baseUrl = process.env.BASE_URL || 'http://localhost:5000';
const email = process.env.E2E_EMAIL;
const password = process.env.E2E_PASSWORD;
const runK6 = process.argv.includes('--run-k6');

if (!email || !password) {
  throw new Error('E2E_EMAIL and E2E_PASSWORD are required; refusing to use placeholder credentials.');
}

function base64Url(value) {
  return Buffer.from(value).toString('base64url');
}

async function acquireToken() {
  const verifier = base64Url(crypto.randomBytes(32));
  const challenge = base64Url(crypto.createHash('sha256').update(verifier).digest());
  const state = base64Url(crypto.randomBytes(24));
  const authorizeUrl = new URL('/connect/authorize', baseUrl);
  authorizeUrl.search = new URLSearchParams({
    client_id: 'manufacturing-app',
    redirect_uri: 'http://localhost:4200/auth/callback',
    response_type: 'code',
    scope: 'openid profile email roles hishop:permissions offline_access',
    nonce: base64Url(crypto.randomBytes(24)),
    state,
    code_challenge: challenge,
    code_challenge_method: 'S256',
  });

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext();
  const page = await context.newPage();
  let callbackUrl;
  page.on('request', request => {
    if (request.url().includes('localhost:4200/auth/callback?')) callbackUrl = request.url();
  });

  try {
    await page.goto(authorizeUrl.toString(), { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.locator('input#email').fill(email);
    await page.locator('input#password').fill(password);
    await page.locator('form[action="/Account/Login"] button[type=submit]').click({ noWaitAfter: true }).catch(() => {});
    await page.waitForTimeout(1000);
    if (/\/Account\/Consent(?:\?|$)/.test(page.url())) {
      await page.locator('button[name=decision][value=allow]').click({ noWaitAfter: true }).catch(() => {});
      await page.waitForTimeout(1000);
    }
    // The callback application is intentionally not required for this local
    // validation. Re-issue the authorize request through the shared context
    // so Playwright returns the callback Location even when port 4200 is down.
    const authorizeResponse = await context.request.get(authorizeUrl.toString(), { maxRedirects: 0 });
    callbackUrl = callbackUrl || authorizeResponse.headers().location;
    if (callbackUrl && callbackUrl.startsWith('/')) callbackUrl = new URL(callbackUrl, baseUrl).toString();
  } finally {
    await browser.close();
  }

  if (!callbackUrl) throw new Error('OIDC callback request was not observed.');
  const callback = new URL(callbackUrl);
  if (callback.searchParams.get('state') !== state) throw new Error('OIDC state validation failed.');
  const code = callback.searchParams.get('code');
  if (!code) throw new Error('OIDC authorization code was not returned.');

  const response = await fetch(new URL('/connect/token', baseUrl), {
    method: 'POST',
    headers: { 'content-type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'authorization_code',
      client_id: 'manufacturing-app',
      redirect_uri: 'http://localhost:4200/auth/callback',
      code,
      code_verifier: verifier,
    }),
  });
  const payload = await response.json();
  if (!response.ok || !payload.access_token) {
    throw new Error(`OIDC token exchange failed with HTTP ${response.status}.`);
  }
  return payload.access_token;
}

acquireToken().then(token => {
  if (!runK6) {
    console.log('OIDC token exchange passed.');
    return;
  }

  const result = spawnSync('k6', [
    'run',
    '--summary-export',
    'tests/load/results/baseline-summary-live.json',
    'tests/Load/baseline-load-test.js',
  ], {
    stdio: 'inherit',
    env: { ...process.env, AUTH_TOKEN: token },
  });
  process.exit(result.status ?? 1);
}).catch(error => {
  console.error(error.message);
  process.exit(1);
});
