import { expect, test } from '@playwright/test';
import { assertE2eCredentials } from './config/credentials.js';

const buyerUrl = process.env.BUYER_APP_URL ?? 'http://localhost:4205';
const email = process.env.E2E_EMAIL;
const password = process.env.E2E_PASSWORD;
const routes = ['catalog', 'cart', 'orders', 'profile', 'notifications', 'rfq'];

test.describe('manufacturing buyer authenticated route contract @buyer-app', () => {
  test('renders protected routes without console errors or API 5xx', async ({ page }) => {
    if (!assertE2eCredentials(email, password)) {
      test.skip(true, 'Set E2E_EMAIL and E2E_PASSWORD for authenticated buyer coverage.');
    }

    const consoleErrors = [];
    const serverErrors = [];
    page.on('console', message => {
      if (message.type() === 'error') consoleErrors.push({ url: page.url(), text: message.text() });
    });
    page.on('response', response => {
      if (response.status() >= 500) serverErrors.push({ status: response.status(), url: response.url() });
    });

    await page.goto(`${buyerUrl}/auth/login`);
    await page.locator('app-login button.fx-btn-primary').click();
    await page.waitForURL(/\/Account\/Login/);
    await page.locator('#email').fill(email);
    await page.locator('#password').fill(password);
    await page.getByRole('button', { name: /Sign in/i }).first().click();
    await page.waitForURL(`${buyerUrl}/catalog`);

    for (const route of routes) {
      await page.goto(`${buyerUrl}/${route}`);
      await expect(page.locator('.site-header')).toBeVisible();
    }

    await page.getByRole('button', { name: /dark theme|giao diện tối/i }).click();
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
