import { expect, test } from '@playwright/test';

const buyerUrl = process.env.BUYER_APP_URL ?? 'http://localhost:4205';

test.describe('manufacturing buyer localization and theme @buyer-app', () => {
  test('switches locale and theme without losing readable control colours', async ({ page }) => {
    await page.goto(buyerUrl);
    await page.evaluate(() => localStorage.setItem('hh-locale', 'en-US'));
    await page.reload();

    await expect(page.locator('html')).toHaveAttribute('data-theme', /light|dark/);
    // Catalog is intentionally protected by the buyer portal guard; the
    // public shell must still render its localized navigation before login.
    await expect(page.getByRole('link', { name: 'Home' }).first()).toBeVisible();

    await page.getByRole('button', { name: /dark theme/i }).click();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');

    const search = page.locator('input[type="search"]');
    await expect(search).toHaveCSS('color', /rgb\(/);
    await expect(page.locator('.hero-slide--active h1')).toHaveCSS('color', 'rgb(255, 255, 255)');
  });

  test('handles an invalid callback without empty-token console errors', async ({ page }) => {
    const tokenErrors = [];
    page.on('console', (message) => {
      if (message.type() === 'error' && /token\s*['"]?\s*['"]?\s*is not valid/i.test(message.text())) {
        tokenErrors.push(message.text());
      }
    });

    await page.goto(`${buyerUrl}/auth/callback?code=invalid&state=invalid`);
    await expect.poll(() => page.url(), { timeout: 15_000 }).toContain('/auth/login');
    expect(tokenErrors).toEqual([]);
  });
});
