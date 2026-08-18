// @ts-check
const { test, expect } = require('@playwright/test');
const { adminUrl } = require('../config/urls');
const { signInThroughIdentity } = require('../helpers/sso-login');
const { getE2eCredentials } = require('../config/credentials');

test('audit IAM menu backend-to-render timing', async ({ page }) => {
  await signInThroughIdentity(page, adminUrl, { ...getE2eCredentials(), dashboardPath: '/dashboard' });
  const routes = [
    '/roles', '/consents', '/clients', '/users', '/iam/scopes', '/iam/users', '/iam/groups',
    '/iam/external-identities', '/iam/service-principals', '/iam/workload-roles',
    '/iam/clients', '/iam/api-audiences', '/iam/trusted-issuers', '/iam/services',
    '/iam/permission-sets', '/iam/policies', '/iam/boundaries', '/iam/resource-policies',
    '/iam/assignments', '/iam/sessions', '/iam/workload-sessions',
    '/iam/analyzer/unused-permissions',
    '/iam/access-requests', '/iam/jit-access', '/iam/break-glass',
    '/access-management', '/identity-operations', '/identity-capabilities',
  ];
  for (const route of routes) {
    const started = Date.now();
    const apiTimings = [];
    const onResponse = async response => {
      if (response.url().includes('/api/v1/admin/')) {
        const entry = { url: response.url(), status: response.status(), ms: Date.now() - started };
        apiTimings.push(entry);
      }
    };
    page.on('response', onResponse);
    const menuLink = page.locator(`mat-nav-list a[href="${route}"]`);
    if (await menuLink.count()) { await menuLink.click(); await page.waitForURL(url => url.pathname === route); }
    else await page.goto(`${adminUrl}${route}`, { waitUntil: 'domcontentloaded' });
    const routeMs = Date.now() - started;
    const table = page.locator('hh-data-table section').first();
    if (await table.count()) {
      await table.waitFor({ state: 'visible', timeout: 10000 });
      await table.locator('..').waitFor({ state: 'visible' });
      try {
        await page.waitForFunction(() => {
          const node = document.querySelector('hh-data-table section');
          return !node || node.getAttribute('aria-busy') !== 'true';
        }, { timeout: 10000 });
      } catch (error) {
        console.log(JSON.stringify({ route, apiTimings, busy: await page.locator('hh-data-table section').getAttribute('aria-busy'), rows: await page.locator('hh-data-table tbody tr').count(), url: page.url(), text: (await page.locator('body').innerText()).slice(0, 500) }));
        throw error;
      }
    }
    expect(Date.now() - started, `${route} render exceeded 1s`).toBeLessThan(1000);
    console.log(JSON.stringify({ route, routeMs, renderMs: Date.now() - started, apiTimings }));
    page.off('response', onResponse);
  }
});
