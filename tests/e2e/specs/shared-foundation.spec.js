const { test, expect } = require('@playwright/test');
const { AxeBuilder } = require('@axe-core/playwright');
const path = require('path');
const { clinicalUrl, dashboardUrl, adminUrl } = require('../config/urls');
const { signInThroughIdentity } = require('../helpers/sso-login');
const { getE2eCredentials } = require('../config/credentials');

if (process.env.E2E_AUTH_REQUIRED === 'true') {
  test.use({ storageState: path.join(__dirname, '..', 'fixtures', 'shared-foundation-auth.generated.json') });
}

const targets = [
  { name: 'clinical', url: `${clinicalUrl}/`, dashboardPath: '/en/dashboard' },
  { name: 'dashboard', url: `${dashboardUrl}/`, dashboardPath: '/resources' },
  { name: 'admin', url: `${adminUrl}/`, dashboardPath: '/clients' },
];

const TEST_USER = getE2eCredentials();

async function ensureAuthenticatedPage(page, target) {
  if (process.env.E2E_AUTH_REQUIRED !== 'true') {
    throw new Error('Authenticated E2E checks require E2E_AUTH_REQUIRED=true.');
  }

  await signInThroughIdentity(page, target.url, {
    dashboardPath: target.dashboardPath,
    email: TEST_USER.email,
    password: TEST_USER.password,
  });
  // Angular may commit the document before the shared shell has rendered;
  // screenshot and interaction assertions must wait for a real shell anchor.
  await page.locator('mat-toolbar, hh-page-header, hh-brand, app-root').first().waitFor({
    state: 'visible',
    timeout: 15000,
  });
}

for (const target of targets) {
  test(`${target.name} shared shell has no critical axe violations @shared-foundation`, async ({ page }) => {
    await ensureAuthenticatedPage(page, target);
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations.filter(violation => ['critical', 'serious'].includes(violation.impact))).toEqual([]);
  });

  test(`${target.name} shared shell visual contract @shared-foundation`, async ({ page }, testInfo) => {
    await ensureAuthenticatedPage(page, target);
    const screenshotOptions = {
      fullPage: true,
      animations: 'disabled',
      // Dashboard health metrics and timestamps are live values and can
      // repaint between stable frames; keep the tolerance bounded to the
      // dashboard surface rather than making the entire visual test fuzzy.
      // Clinical dashboard also renders the current date; keep the bound
      // explicit so a day rollover does not turn a valid shell into a false
      // visual failure. Other app shells remain pixel exact.
      maxDiffPixels: target.name === 'dashboard' ? 5000 : target.name === 'clinical' ? 1000 : 0,
    };
    // Admin data tables are server-backed and can legitimately be in either
    // the loading skeleton or loaded/empty state when the shell screenshot is
    // captured. Mask that volatile region so this contract checks the shared
    // navigation, header, typography and surface tokens rather than timing.
    if (target.name === 'admin') {
      screenshotOptions.mask = [page.locator('main').last()];
    }
    const screenshotTarget = testInfo.project.name === 'mobile'
      ? target.name === 'clinical'
        ? page.locator('.app-sidenav-container')
        : target.name === 'dashboard'
          ? page.locator('.sidenav-container')
          : page.locator('hh-app-shell')
      : page;
    await expect(screenshotTarget).toHaveScreenshot(`${target.name}-${testInfo.project.name}.png`, screenshotOptions);
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
  const trigger = page.getByRole('button', { name: /command palette|bảng lệnh/i });
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
  const toggle = page.getByRole('button', { name: /toggle theme|đổi giao diện|đổi chế độ tối/i });
  await expect(toggle).toHaveCount(1);
  await toggle.click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect.poll(() => page.locator('body').evaluate(element => getComputedStyle(element).backgroundColor)).toBe('rgb(17, 24, 21)');
  await expect.poll(() => page.locator('.hh-shell__sidebar').evaluate(element => getComputedStyle(element).backgroundColor)).toBe('rgb(24, 33, 28)');
});
