// @ts-check
const { test, expect } = require('@playwright/test');
const { adminUrl } = require('../config/urls');
const { signInThroughIdentity } = require('../helpers/sso-login');
const { getE2eCredentials } = require('../config/credentials');

test.describe('Admin Identity menu coverage', () => {
  test('every visible menu item navigates to a rendered route without server errors', async ({ page }) => {
    const httpErrors = [];
    const pageErrors = [];
    page.on('response', response => {
      if (response.status() >= 400) httpErrors.push(`${response.status()} ${response.url()}`);
    });
    page.on('pageerror', error => pageErrors.push({
      name: error.name,
      message: error.message,
      stack: error.stack,
      string: String(error),
      ownKeys: Object.getOwnPropertyNames(error),
    }));

    const credentials = getE2eCredentials();
    await signInThroughIdentity(page, adminUrl, {
      email: credentials.email,
      password: credentials.password,
      dashboardPath: '/dashboard',
    });
    // Responsive shell keeps the sidenav closed on mobile/tablet. Open it
    // before collecting links so the same contract is exercised at every
    // viewport without changing the production navigation behavior.
    const mobileMenu = page.locator('.mobile-menu-button, mat-toolbar button[aria-label*="Navigation" i], mat-toolbar button[aria-label*="Điều hướng" i]').first();
    if (!(await page.locator('mat-nav-list a').first().isVisible().catch(() => false)) && await mobileMenu.isVisible().catch(() => false)) {
      await mobileMenu.click();
    }
    await expect(page.locator('mat-nav-list a').first()).toBeVisible({ timeout: 15000 });

    const links = await page.locator('mat-nav-list a').evaluateAll(elements => elements.map(element => ({
      label: element.textContent?.trim() ?? '',
      href: element.getAttribute('href') ?? '',
    })));
    expect(links.length).toBeGreaterThan(10);

    for (let index = 0; index < links.length; index += 1) {
      const link = links[index];
      const expectedPath = new URL(link.href, adminUrl).pathname;
      await test.step(`${link.label} -> ${link.href}`, async () => {
        await page.locator('mat-nav-list a').nth(index).click();
        await page.waitForURL(url => url.pathname === expectedPath, { timeout: 15000 });
        await expect(page.locator('hh-page-layout, hh-page-header, .main-content').first()).toBeVisible({ timeout: 10000 });
        await page.waitForTimeout(250);
      });
    }

    expect(pageErrors, `browser page errors: ${JSON.stringify(pageErrors)}`).toEqual([]);
    expect(httpErrors, `HTTP errors during authenticated menu traversal: ${httpErrors.join(' | ')}`).toEqual([]);
  });
});
