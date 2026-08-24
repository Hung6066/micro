import { defineConfig } from '@playwright/test';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.dirname(fileURLToPath(import.meta.url));
const buyerDist = path.resolve(root, '../../manufacturing-buyer-app/dist/manufacturing-buyer-app/browser');
const operatorDist = path.resolve(root, '../../internal-operator-app/dist/internal-operator-app/browser');
const buyerPort = process.env.BUYER_E2E_PORT ?? '4225';
const operatorPort = process.env.OPERATOR_E2E_PORT ?? '4220';

export default defineConfig({
  testDir: '.',
  testMatch: 'manufacturing-content-commerce-e2e.mjs',
  timeout: 120_000,
  workers: 1,
  use: {
    headless: true,
    viewport: { width: 1440, height: 900 },
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
  webServer: [
    {
      command: `npx --yes http-server "${buyerDist}" -p ${buyerPort} -P http://127.0.0.1:${buyerPort}?`,
      url: `http://127.0.0.1:${buyerPort}/index.html`,
      reuseExistingServer: false,
      timeout: 60_000,
    },
    {
      command: `npx --yes http-server "${operatorDist}" -p ${operatorPort} -P http://127.0.0.1:${operatorPort}?`,
      url: `http://127.0.0.1:${operatorPort}/index.html`,
      reuseExistingServer: false,
      timeout: 60_000,
    },
  ],
});
