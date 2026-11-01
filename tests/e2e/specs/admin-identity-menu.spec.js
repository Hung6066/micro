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
    const mobileMenu = page.getByRole('button', { name: /^(Admin navigation|Mở menu điều hướng)$/i }).first();
    const menuLinks = page.locator('nav[hhShellNavigation] a');
    await expect(menuLinks.first()).toBeVisible({ timeout: 15000 });
    await expect.poll(() => menuLinks.count(), {
      timeout: 15000,
      message: 'Identity admin menu should finish hydrating before traversal',
    }).toBeGreaterThan(10);

    const links = await menuLinks.evaluateAll(elements => elements.map(element => ({
      label: element.textContent?.trim() ?? '',
      href: element.getAttribute('href') ?? '',
    })));
    expect(links.length).toBeGreaterThan(10);

    const ensureAdminMenuVisible = async () => {
      const firstLink = menuLinks.first();
      const box = await firstLink.boundingBox().catch(() => null);
      if (box && box.x >= 0 && box.width > 0 && box.height > 0) {
        return;
      }

      await expect(mobileMenu).toBeVisible({ timeout: 10000 });
      await mobileMenu.click();
      await expect.poll(async () => {
        const openedBox = await firstLink.boundingBox().catch(() => null);
        return openedBox !== null && openedBox.x >= 0 && openedBox.width > 0 && openedBox.height > 0;
      }, { timeout: 10000, message: 'Admin mobile navigation should settle before menu traversal' }).toBe(true);
    };

    for (let index = 0; index < links.length; index += 1) {
      const link = links[index];
      const expectedPath = new URL(link.href, adminUrl).pathname;
      await test.step(`${link.label} -> ${link.href}`, async () => {
        await ensureAdminMenuVisible();
        await page.locator('nav[hhShellNavigation] a').nth(index).click();
        await page.waitForURL(url => url.pathname === expectedPath, { timeout: 15000 });
        await expect(page.locator('hh-page-layout, hh-page-header, .main-content').first()).toBeVisible({ timeout: 10000 });
        await page.waitForTimeout(250);
      });
    }

    expect(pageErrors, `browser page errors: ${JSON.stringify(pageErrors)}`).toEqual([]);
    expect(httpErrors, `HTTP errors during authenticated menu traversal: ${httpErrors.join(' | ')}`).toEqual([]);
  });
});
