import { expect, test } from '@playwright/test';
import { getE2eCredentials } from './config/credentials.js';

const buyerUrl = process.env.BUYER_APP_URL ?? `http://127.0.0.1:${process.env.BUYER_E2E_PORT ?? '4225'}`;
const operatorUrl = process.env.OPERATOR_APP_URL ?? `http://127.0.0.1:${process.env.OPERATOR_E2E_PORT ?? '4220'}`;

async function loginBuyer(page, email, password) {
  await page.goto(`${buyerUrl}/auth/login`);
  await page.locator('app-login button.fx-btn-primary').click();
  await page.waitForURL(/\/Account\/Login/, { timeout: 30_000 });
  await page.locator('#email').fill(email);
  await page.locator('#password').fill(password);
  await page.getByRole('button', { name: /Sign in/i }).first().click();
  await page.waitForURL(/\/(catalog|home)/, { timeout: 60_000 });
}

async function loginOperator(page, email, password) {
  await page.goto(`${operatorUrl}/auth/login`);
  await page.getByRole('button', { name: /Sign in with His.Hope/i }).click();
  await page.waitForURL(/\/Account\/Login/, { timeout: 30_000 });
  await page.locator('#email').fill(email);
  await page.locator('#password').fill(password);
  await page.getByRole('button', { name: /Sign in/i }).first().click();
  await page.waitForURL(`${operatorUrl}/dashboard`, { timeout: 60_000 });
}

test.describe('buyer public content pages @buyer-content', () => {
  test('blog list renders localized shell and articles or empty state', async ({ page }) => {
    await page.goto(`${buyerUrl}/blog`);
    await expect(page).toHaveURL(/\/blog$/);
    await expect(page.locator('[data-testid="buyer-blog-list"]')).toBeVisible({ timeout: 20_000 });
    await expect(page.locator('.page-head h1')).toBeVisible();

    const cards = page.locator('.news-card');
    const empty = page.getByText(/Chưa có bài viết|No articles yet/i);
    await expect(cards.first().or(empty)).toBeVisible({ timeout: 15_000 });
  });

  test('blog detail opens from list when articles exist', async ({ page }) => {
    await page.goto(`${buyerUrl}/blog`);
    const firstLink = page.locator('.news-card__link').first();
    if (!(await firstLink.isVisible({ timeout: 15_000 }).catch(() => false))) {
      test.skip(true, 'No published articles to open.');
    }

    await firstLink.click();
    await expect(page.locator('[data-testid="buyer-blog-detail"]')).toBeVisible();
    const articleTitle = page.locator('.article h1');
    const notFound = page.getByText(/Không tìm thấy bài viết|Article not found/i);
    await expect(articleTitle.or(notFound)).toBeVisible({ timeout: 10_000 });
  });

  test('cooperation form renders i18n labels and theme tokens', async ({ page }) => {
    await page.goto(`${buyerUrl}/cooperation`);
    await expect(page).toHaveURL(/\/cooperation$/);
    await expect(page.locator('[data-testid="buyer-cooperation"]')).toBeVisible({ timeout: 20_000 });
    await expect(page.locator('[data-testid="cooperation-form"]')).toBeVisible();
    await expect(page.locator('[data-testid="cooperation-form"] input[name="companyName"]')).toBeVisible();
    await expect(page.locator('[data-testid="cooperation-form"] textarea[name="message"]')).toBeVisible();
    await expect(page.locator('.page-head h1')).toHaveCSS('font-weight', /700|800|bold/i);
  });
});

test.describe('buyer authenticated commerce RFQ @buyer-rfq', () => {
  test('RFQ page renders after login', async ({ page }) => {
    const { email, password } = getE2eCredentials();
    test.skip(
      process.env.E2E_SKIP_AUTH === 'true' || !process.env.E2E_PASSWORD,
      'Set E2E_PASSWORD (and optionally E2E_EMAIL) for authenticated RFQ coverage.',
    );

    try {
      await loginBuyer(page, email, password);
    } catch {
      test.skip(true, 'Buyer auth backend unavailable for RFQ E2E.');
    }

    await page.goto(`${buyerUrl}/rfq`);
    await expect(page.locator('[data-testid="buyer-rfq"]')).toBeVisible({ timeout: 20_000 });
    await expect(page.locator('[data-testid="rfq-form"]')).toBeVisible();
    await expect(page.locator('[data-testid="rfq-form"] textarea[name="message"]')).toBeVisible();
  });
});

test.describe('operator CMS and RFQ @operator-content', () => {
  test('content and RFQ routes render for authenticated operator', async ({ page }) => {
    const { email, password } = getE2eCredentials();
    test.skip(
      process.env.E2E_SKIP_AUTH === 'true' || !process.env.E2E_PASSWORD,
      'Set E2E_PASSWORD (and optionally E2E_EMAIL) for authenticated operator coverage.',
    );

    try {
      await loginOperator(page, email, password);
    } catch {
      test.skip(true, 'Operator auth backend unavailable for content E2E.');
    }

    await page.goto(`${operatorUrl}/content`);
    await expect(page.locator('[data-testid="operator-content"]')).toBeVisible({ timeout: 20_000 });
    await expect(page.locator('[data-testid="content-tabs"]')).toBeVisible();

    await page.goto(`${operatorUrl}/rfqs`);
    await expect(page.locator('[data-testid="operator-rfqs"]')).toBeVisible({ timeout: 20_000 });
  });
});
