import { defineConfig } from '@playwright/test';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.dirname(fileURLToPath(import.meta.url));
const operatorDist = path.resolve(root, '../../internal-operator-app/dist/internal-operator-app/browser');
const operatorPort = process.env.OPERATOR_E2E_PORT ?? '4220';
const operatorUrl =
  process.env.OPERATOR_APP_URL ??
  (process.env.E2E_USE_STATIC_OPERATOR === 'true'
    ? `http://127.0.0.1:${operatorPort}`
    : 'http://127.0.0.1:4300');
const useStaticOperator = operatorUrl.includes(`:${operatorPort}`);

export default defineConfig({
  testDir: '.',
  testMatch: 'manufacturing-workflow-e2e.mjs',
  timeout: 120_000,
  workers: 1,
  use: {
    headless: true,
    viewport: { width: 1440, height: 900 },
    baseURL: operatorUrl,
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
  webServer: useStaticOperator
    ? {
        command: `npx --yes http-server "${operatorDist}" -p ${operatorPort} -P http://127.0.0.1:${operatorPort}?`,
        url: `http://127.0.0.1:${operatorPort}/index.html`,
        reuseExistingServer: false,
        timeout: 60_000,
      }
    : undefined,
});
