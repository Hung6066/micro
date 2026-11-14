import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: ".",
  testMatch: "operator-mobile-ui-tests.mjs",
  workers: 1,
  timeout: 30_000,
  use: { baseURL: "http://localhost:4300", bypassCSP: true, ...devices["Desktop Chrome"] },
  webServer: {
    command: "npm start -- --host localhost --port 4300",
    cwd: "../../operator-mobile",
    url: "http://localhost:4300/auth/login",
    reuseExistingServer: false,
    timeout: 120_000,
  },
});
