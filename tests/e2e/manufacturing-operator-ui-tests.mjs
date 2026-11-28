import { expect, test } from '@playwright/test';
import { assertE2eCredentials } from './config/credentials.js';

const operatorUrl = process.env.OPERATOR_APP_URL ?? 'http://localhost:4300';
const email = process.env.E2E_EMAIL;
const password = process.env.E2E_PASSWORD;
const routes = ['dashboard', 'inventory/lots', 'production', 'recipes', 'product-specifications', 'quality-inspections', 'deviations', 'forecast', 'sales-allocation', 'procurement', 'maintenance', 'orders', 'users', 'content', 'rfqs'];

test.describe('manufacturing operator localization, theme and route contract @operator-app', () => {
  test('handles an invalid callback without empty-token console errors', async ({ page }) => {
    const tokenErrors = [];
    page.on('console', (message) => {
      if (message.type() === 'error' && /token\s*['"]?\s*['"]?\s*is not valid/i.test(message.text())) {
        tokenErrors.push(message.text());
      }
    });

    await page.goto(`${operatorUrl}/auth/callback?code=invalid&state=invalid`);
    await expect.poll(() => page.url(), { timeout: 15_000 }).toContain('/auth/login');
    expect(tokenErrors).toEqual([]);
  });

  test('authenticated routes render without console errors or API 5xx', async ({ page }) => {
    if (!assertE2eCredentials(email, password)) {
      test.skip(true, 'Set E2E_EMAIL and E2E_PASSWORD for authenticated operator coverage.');
    }

    const consoleErrors = [];
    const serverErrors = [];
    page.on('console', message => {
      if (message.type() === 'error') consoleErrors.push({ url: page.url(), text: message.text() });
    });
    page.on('response', response => {
      if (response.status() >= 500) serverErrors.push({ status: response.status(), url: response.url() });
    });

    await page.goto(`${operatorUrl}/auth/login`);
    await page.getByRole('button', { name: /Sign in with His.Hope/i }).click();
    await page.waitForURL(/\/Account\/Login/);
    await page.locator('#email').fill(email);
    await page.locator('#password').fill(password);
    await page.getByRole('button', { name: /Sign in/i }).first().click();
    await page.waitForURL(`${operatorUrl}/dashboard`);

    for (const route of routes) {
      await page.goto(`${operatorUrl}/${route}`);
      // The operator app now uses the shared shell toolbar slot instead of
      // Angular Material's legacy mat-toolbar element.
      await expect(page.locator('.hh-shell-toolbar-slot')).toBeVisible();
    }

    const themeToggle = page.getByRole('button', { name: /toggle theme|đổi giao diện|đổi chế độ tối/i });
    await themeToggle.click();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');

    const languageTrigger = page.locator('hh-language-switcher .hh-language-trigger');
    await languageTrigger.focus();
    await page.keyboard.press('Enter');
    await page.getByRole('option', { name: /English/i }).click();
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');

    expect(consoleErrors).toEqual([]);
    expect(serverErrors).toEqual([]);
  });
});
