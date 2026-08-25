import { expect, test } from '@playwright/test';

test.describe('manufacturing buyer localization and theme @buyer-app', () => {
  test('switches locale and theme without losing readable control colours', async ({ page }) => {
    await page.goto('/');
    // The shared foundation supports `en` (not the browser's `en-US` tag).
    await page.evaluate(() => localStorage.setItem('hh-locale', 'en'));
    await page.reload();

    await expect(page.locator('html')).toHaveAttribute('data-theme', /light|dark/);
    await expect(page.getByRole('link', { name: 'Products' }).first()).toBeVisible();

    await page.getByRole('button', { name: /dark theme/i }).click();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');

    const search = page.locator('input[type="search"]');
    await expect(search).toHaveCSS('color', /rgb\(/);
  });

  test('changes locale through the language switcher', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('button', { name: 'Tiếng Việt' }).click();
    await page.getByRole('option', { name: /English/ }).click();

    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(page.getByRole('link', { name: 'Products' }).first()).toBeVisible();

    await page.getByRole('button', { name: 'English' }).click();
    await page.getByRole('option', { name: /Tiếng Việt/ }).click();
    await expect(page.locator('html')).toHaveAttribute('lang', 'vi-VN');
  });

  test('protects the catalog route before requesting manufacturing data', async ({ page }) => {
    const manufacturingRequests = [];
    page.on('request', (request) => {
      if (request.url().includes('/api/v1/manufacturing')) manufacturingRequests.push(request.url());
    });

    await page.goto('/catalog');
    await expect(page).toHaveURL(/\/auth\/login/);
    expect(manufacturingRequests).toHaveLength(0);
  });
});
