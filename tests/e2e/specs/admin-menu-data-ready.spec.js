// @ts-check
const { test, expect } = require('@playwright/test');
const { adminUrl } = require('../config/urls');
const { signInThroughIdentity } = require('../helpers/sso-login');
const { getE2eCredentials } = require('../config/credentials');

test('IAM menu renders data after a fast backend response', async ({ page }) => {
  await signInThroughIdentity(page, adminUrl, {
    ...getE2eCredentials(),
    dashboardPath: '/dashboard',
  });

  await page.locator('mat-nav-list a[href="/iam/groups"]').click();
  await page.waitForURL(url => url.pathname === '/iam/groups');

  const groupsResponse = page.waitForResponse(response =>
    response.url().includes('/api/v1/admin/iam/groups') && response.status() === 200,
  );
  await groupsResponse;
  await expect(page.locator('hh-data-table section')).not.toHaveAttribute('aria-busy', 'true', { timeout: 1000 });
  await expect(page.getByText('Loading data...', { exact: true })).toHaveCount(0);
  await expect(page.locator('hh-data-table tbody tr')).toHaveCount(1);
});
