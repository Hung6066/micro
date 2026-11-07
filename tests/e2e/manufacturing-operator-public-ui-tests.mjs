import { expect, test } from '@playwright/test';

const operatorUrl = process.env.OPERATOR_APP_URL ?? 'http://localhost:4300';

test('operator public login keeps the authenticated shell hidden and uses readable fonts', async ({ page }) => {
  await page.goto(`${operatorUrl}/auth/login`);
  await page.evaluate(() => localStorage.setItem('hh-locale', 'en'));
  await page.reload();

  await expect(page.locator('h2')).toBeVisible();
  await expect(page.locator('mat-toolbar')).toHaveCount(0);
  await expect(page.locator('mat-sidenav')).toHaveCount(0);
  await expect
    .poll(() =>
      page.evaluate(() => getComputedStyle(document.body).fontFamily),
    )
    .toMatch(/HisHope Inter|Inter/);
  const shellContract = await page.evaluate(() => {
    const root = getComputedStyle(document.documentElement);
    const body = getComputedStyle(document.body);
    return {
      htmlFont: root.fontFamily,
      bodyFont: body.fontFamily,
      surface: root.getPropertyValue('--surface').trim(),
      borderSubtle: root.getPropertyValue('--border-subtle').trim(),
      horizontalOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth,
    };
  });
  expect(shellContract.bodyFont).toMatch(/HisHope Inter|Inter/);
  expect(shellContract.surface).not.toBe('');
  expect(shellContract.borderSubtle).not.toBe('');
  expect(shellContract.horizontalOverflow).toBeFalsy();
});
