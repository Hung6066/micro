import { expect, test } from "@playwright/test";

test("unauthenticated operator is sent to secure login", async ({ page }) => {
  await page.goto("/operations/production");
  await expect(page).toHaveURL(/\/auth\/login$/);
  await expect(page.locator("#mobile-login-title")).toBeVisible();
  await expect(page.getByRole("button")).toBeVisible();
});

test("authenticated operator can reach production work", async ({ page }) => {
  await page.goto("/auth/login");
  await page.getByRole("button", { name: /sign in securely|đăng nhập an toàn/i }).click();
  await page.waitForURL(/\/Account\/Login|\/connect\/authorize/i, { timeout: 30_000 });
  const email = page.locator('input[type="email"], input#email').first();
  if (await email.isVisible().catch(() => false)) {
    await email.fill(process.env.E2E_EMAIL ?? "admin@hishop.com");
    await page.locator('input[type="password"]').first().fill(process.env.E2E_PASSWORD ?? "Test@123456");
    await page.locator('button[type="submit"]').first().click();
  }
  await page.waitForURL(/\/operations\/production$/, { timeout: 60_000 });
  await expect(page.getByRole("heading", { name: "Production work" })).toBeVisible();
});

test("login page exposes the operator mobile entry point", async ({ page }) => {
  await page.goto("/auth/login");
  await expect(page.locator(".mobile-auth__eyebrow")).toHaveText("His.Hope Mobile");
  await expect(page.locator("button")).toHaveCount(1);
});
