// @ts-check
const { test, expect } = require('@playwright/test');
const { adminUrl } = require('../config/urls');
const { signInThroughIdentity } = require('../helpers/sso-login');
const { getE2eCredentials } = require('../config/credentials');

test('authenticated admin exposes manufacturing IAM graph', async ({ page }) => {
  await signInThroughIdentity(page, adminUrl, { ...getE2eCredentials(), dashboardPath: '/dashboard' });
  const payloads = await page.evaluate(async () => {
    const paths = [
      '/api/v1/admin/clients',
      '/api/v1/admin/iam/services',
      '/api/v1/admin/iam/service-principals',
      '/api/v1/admin/iam/workload-roles',
    ];
    const responses = await Promise.all(paths.map(async path => {
      const response = await fetch(path, { credentials: 'include' });
      return { path, status: response.status, body: await response.json() };
    }));
    return responses;
  });

  for (const payload of payloads) expect(payload.status, payload.path).toBe(200);
  const serialized = JSON.stringify(payloads);
  expect(serialized).toContain('manufacturing-app');
  expect(serialized).toContain('manufacturing');
  expect(serialized).toContain('manufacturing-api');
});
