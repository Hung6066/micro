import { defineConfig, devices } from "@playwright/test";
export default defineConfig({testDir:".",testMatch:"operator-mobile-ui-tests.mjs",workers:1,timeout:90000,use:{baseURL:"http://localhost:4310",bypassCSP:true,...devices["Desktop Chrome"]}});
