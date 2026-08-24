import { expect, test } from '@playwright/test';

const buyerUrl = process.env.BUYER_APP_URL ?? 'http://localhost:4205';

test.describe('manufacturing buyer localization and theme @buyer-app', () => {
  test('switches locale and theme without losing readable control colours', async ({ page }) => {
    await page.goto(buyerUrl);
    await page.evaluate(() => localStorage.setItem('hh-locale', 'en-US'));
    await page.reload();

    await expect(page.locator('html')).toHaveAttribute('data-theme', /light|dark/);
    await expect(page.getByRole('link', { name: 'Products' }).first()).toBeVisible();

    await page.getByRole('button', { name: /dark theme/i }).click();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');

    const search = page.locator('input[type="search"]');
    await expect(search).toHaveCSS('color', /rgb\(/);
  });
});
