import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: ".",
  testMatch: "operator-mobile-ui-tests.mjs",
  workers: 1,
  timeout: 30_000,
  use: { baseURL: "http://127.0.0.1:4310", ...devices["Desktop Chrome"] },
  webServer: {
    command: "npm start -- --host 127.0.0.1",
    cwd: "../../operator-mobile",
    url: "http://127.0.0.1:4310/auth/login",
    reuseExistingServer: false,
    timeout: 120_000,
  },
});
