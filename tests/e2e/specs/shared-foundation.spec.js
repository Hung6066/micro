const { test, expect } = require('@playwright/test');
const { AxeBuilder } = require('@axe-core/playwright');
const path = require('path');
const { clinicalUrl, dashboardUrl, adminUrl } = require('../config/urls');

if (process.env.E2E_AUTH_REQUIRED === 'true') {
  test.use({ storageState: path.join(__dirname, '..', 'fixtures', 'shared-foundation-auth.json') });
}

const targets = [
  { name: 'clinical', url: `${clinicalUrl}/` },
  { name: 'dashboard', url: `${dashboardUrl}/` },
  { name: 'admin', url: `${adminUrl}/` },
];

const TEST_USER = {
  email: process.env.E2E_EMAIL || 'admin@hishop.com',
  password: process.env.E2E_PASSWORD || 'Admin@123',
};

async function ensureAuthenticatedPage(page, target) {
  if (process.env.E2E_AUTH_REQUIRED !== 'true') {
    throw new Error('Authenticated E2E checks require E2E_AUTH_REQUIRED=true.');
  }

  await page.goto(target.url, { waitUntil: 'domcontentloaded' });
  // Angular bootstraps OIDC asynchronously; the first navigation can resolve
  // before the guard has redirected to the app or Identity login page.
  await page.waitForTimeout(2000);

  if (/\/auth\/login(?:\?|$)/.test(page.url())) {
    const signIn = page.getByRole('button', { name: /Sign in with His\.Hope/i });
    await signIn.click();
  }

  if (/\/Account\/Login(?:\?|$)/.test(page.url())) {
    await page.locator('#email').fill(TEST_USER.email);
    await page.locator('#password').fill(TEST_USER.password);
    await page.locator('form[action="/Account/Login"] button[type="submit"]').click();
    const targetOrigin = new URL(target.url).origin;
    await page.waitForURL((url) =>
      url.origin === targetOrigin &&
      !/\/auth\/(?:login|callback)(?:\?|$)/.test(url.pathname) &&
      !/\/Account\/Login(?:\?|$)/.test(url.pathname),
      { timeout: 30000 },
    );
  }

  // The dashboard keeps telemetry/polling requests open, so networkidle is
  // not a reliable readiness signal for an authenticated shell.
  await page.waitForTimeout(1000);
  if (/\/auth\/login(?:\?|$)|\/Account\/Login(?:\?|$)/.test(page.url())) {
    throw new Error(`${target.name} E2E authentication failed: browser remained on a login page.`);
  }
}

for (const target of targets) {
  test(`${target.name} shared shell has no critical axe violations @shared-foundation`, async ({ page }) => {
    await ensureAuthenticatedPage(page, target);
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations.filter(violation => ['critical', 'serious'].includes(violation.impact))).toEqual([]);
  });

  test(`${target.name} shared shell visual contract @shared-foundation`, async ({ page }, testInfo) => {
    await ensureAuthenticatedPage(page, target);
    await expect(page).toHaveScreenshot(`${target.name}-${testInfo.project.name}.png`, {
      fullPage: true,
      animations: 'disabled',
      // Dashboard health metrics are live values and can repaint between frames.
      maxDiffPixels: target.name === 'dashboard' ? 1000 : 0,
    });
  });

  test(`${target.name} shared shell fits responsive viewport @shared-foundation`, async ({ page }) => {
    await ensureAuthenticatedPage(page, target);
    const overflow = await page.evaluate(() => ({
      documentWidth: document.documentElement.scrollWidth,
      viewportWidth: window.innerWidth,
      bodyWidth: document.body.scrollWidth,
    }));
    expect(overflow.documentWidth, `${target.name} document overflows viewport`).toBeLessThanOrEqual(overflow.viewportWidth + 1);
    expect(overflow.bodyWidth, `${target.name} body overflows viewport`).toBeLessThanOrEqual(overflow.viewportWidth + 1);
  });

  test(`${target.name} shared controls expose keyboard names @shared-foundation`, async ({ page }) => {
    await ensureAuthenticatedPage(page, target);
    const unnamed = await page.locator('button, [role="button"], a').evaluateAll(elements => elements
      .filter(element => {
        const style = getComputedStyle(element);
        const rect = element.getBoundingClientRect();
        return style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
      })
      .filter(element => !((element.getAttribute('aria-label') || element.textContent || '').trim()))
      .map(element => element.outerHTML.slice(0, 180)));
    expect(unnamed, `${target.name} has unnamed interactive controls`).toEqual([]);
  });
}

test('dashboard command palette supports Escape and focus entry @shared-foundation', async ({ page }) => {
  await ensureAuthenticatedPage(page, targets[1]);
  const trigger = page.getByRole('button', { name: /command palette/i });
  await expect(trigger).toHaveCount(1);
  await trigger.click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('searchbox')).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
  await expect(trigger).toBeFocused();
});

test('admin theme toggle updates the full document surface @shared-foundation', async ({ page }) => {
  await ensureAuthenticatedPage(page, targets[2]);
  const toggle = page.getByRole('button', { name: 'Toggle theme' });
  await expect(toggle).toHaveCount(1);
  await toggle.click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect.poll(() => page.locator('body').evaluate(element => getComputedStyle(element).backgroundColor)).toBe('rgb(17, 24, 21)');
  await expect.poll(() => page.locator('.app-sidenav').evaluate(element => getComputedStyle(element).backgroundColor)).toBe('rgb(24, 33, 28)');
});
