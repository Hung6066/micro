import { expect, test } from "@playwright/test";

test("unauthenticated operator is sent to secure login", async ({ page }) => {
  test.skip(true, "Requires a reachable Identity Service OIDC authority for the guard to complete.");
  await page.goto("/operations/production");
  await expect(page).toHaveURL(/\/auth\/login$/);
  await expect(page.locator("#mobile-login-title")).toBeVisible();
  await expect(page.getByRole("button")).toBeVisible();
});

test("login page exposes the operator mobile entry point", async ({ page }) => {
  await page.goto("/auth/login");
  await expect(page.locator(".mobile-auth__eyebrow")).toHaveText("His.Hope Mobile");
  await expect(page.locator("button")).toHaveCount(1);
});
