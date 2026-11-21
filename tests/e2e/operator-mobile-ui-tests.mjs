import { expect, test } from "@playwright/test";

const e2eEmail = process.env.E2E_EMAIL;
const e2ePassword = process.env.E2E_PASSWORD;
const roleMatrix = (() => {
  if (!process.env.E2E_ROLE_MATRIX_JSON) return [];
  try {
    const parsed = JSON.parse(process.env.E2E_ROLE_MATRIX_JSON);
    return Array.isArray(parsed)
      ? parsed.filter((entry) => entry?.name && entry?.email && entry?.password)
      : [];
  } catch {
    throw new Error("E2E_ROLE_MATRIX_JSON must be a JSON array of {name,email,password} entries.");
  }
})();

async function signIn(page, email, password) {
  await page.goto("/auth/login");
  await page
    .getByRole("button", { name: /sign in securely|đăng nhập an toàn/i })
    .click();
  await page.waitForURL(/\/Account\/Login|\/connect\/authorize/i, {
    timeout: 30_000,
  });
  const emailInput = page.locator('input[type="email"], input#email').first();
  if (await emailInput.isVisible().catch(() => false)) {
    await emailInput.fill(email);
    await page.locator('input[type="password"]').first().fill(password);
    await page.locator('button[type="submit"]').first().click();
  }
  await page.waitForURL(/\/operations\/production$/, { timeout: 60_000 });
}

function requireAuthenticatedOperator() {
  if (
    process.env.E2E_AUTH_REQUIRED === "true" &&
    (!e2eEmail || !e2ePassword)
  ) {
    throw new Error(
      "E2E_AUTH_REQUIRED=true requires E2E_EMAIL and E2E_PASSWORD from protected secret storage.",
    );
  }
  test.skip(
    !e2eEmail || !e2ePassword,
    "Authenticated operator E2E requires E2E_EMAIL and E2E_PASSWORD from protected secret storage.",
  );
}

test("unauthenticated operator is sent to secure login", async ({ page }) => {
  await page.goto("/operations/production");
  await expect(page).toHaveURL(/\/auth\/login$/);
  await expect(page.locator("#mobile-login-title")).toBeVisible();
  await expect(page.getByRole("button")).toBeVisible();
});

test("authenticated operator can reach production work", async ({ page }) => {
  requireAuthenticatedOperator();
  await page.goto("/auth/login");
  await page
    .getByRole("button", { name: /sign in securely|đăng nhập an toàn/i })
    .click();
  await page.waitForURL(/\/Account\/Login|\/connect\/authorize/i, {
    timeout: 30_000,
  });
  const email = page.locator('input[type="email"], input#email').first();
  if (await email.isVisible().catch(() => false)) {
    await email.fill(e2eEmail);
    await page
      .locator('input[type="password"]')
      .first()
      .fill(e2ePassword);
    await page.locator('button[type="submit"]').first().click();
  }
  await page.waitForURL(/\/operations\/production$/, { timeout: 60_000 });
  await page.setViewportSize({ width: 390, height: 844 });
  const batchesResponse = page.waitForResponse((response) =>
    response.url().includes("/api/v1/manufacturing/production-batches"),
  );
  const kpiResponse = page.waitForResponse((response) =>
    response.url().includes("/api/v1/manufacturing/dashboard/production-kpis"),
  );
  const oeeResponse = page.waitForResponse((response) =>
    response.url().includes("/api/v1/manufacturing/dashboard/oee"),
  );
  const exceptionsResponse = page.waitForResponse((response) =>
    response.url().includes("/api/v1/manufacturing/dashboard/exceptions"),
  );
  const costsResponse = page.waitForResponse((response) =>
    response.url().includes("/api/v1/manufacturing/dashboard/production-costs"),
  );
  await page.reload();
  await expect((await batchesResponse).status()).toBe(200);
  await expect((await kpiResponse).status()).toBe(200);
  await expect((await oeeResponse).status()).toBe(200);
  await expect((await exceptionsResponse).status()).toBe(200);
  await expect((await costsResponse).status()).toBe(200);
  await expect(
    page.getByRole("heading", { name: /Production work|Vận hành sản xuất/i }),
  ).toBeVisible();
  const batchSelect = page.locator("section.work-page hh-select").first();
  await batchSelect.click();
  await expect
    .poll(async () => page.locator(".hh-select__option").count())
    .toBeGreaterThan(1);
  await page.locator(".hh-select__option").nth(1).click();
  await page.getByRole("tab", { name: /Overview|Tổng quan/i }).click();
  await expect(page.locator(".work-page details")).toHaveCount(0);
  await expect(page.getByRole("tab")).toHaveCount(2);
  await expect(
    page.getByRole("heading", { name: /Batch workflow|Vòng đời lô sản xuất/i }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: /Production KPIs|KPI sản xuất/i }),
  ).toBeVisible();
  await expect(page.getByRole("heading", { name: /^OEE/ })).toBeVisible();
  await expect(page.locator("body")).not.toContainText("mobile.");
  await expect
    .poll(() =>
      page.evaluate(
        () => document.documentElement.scrollWidth - window.innerWidth,
      ),
    )
    .toBeLessThanOrEqual(1);
});

test("authenticated operator can switch locale and theme", async ({ page }) => {
  requireAuthenticatedOperator();
  await page.goto("/auth/login");
  await page
    .getByRole("button", { name: /sign in securely|đăng nhập an toàn/i })
    .click();
  await page.waitForURL(/\/Account\/Login|\/connect\/authorize/i, {
    timeout: 30_000,
  });
  const email = page.locator('input[type="email"], input#email').first();
  if (await email.isVisible().catch(() => false)) {
    await email.fill(e2eEmail);
    await page
      .locator('input[type="password"]')
      .first()
      .fill(e2ePassword);
    await page.locator('button[type="submit"]').first().click();
  }
  await page.waitForURL(/\/operations\/production$/, { timeout: 60_000 });
  await page.setViewportSize({ width: 390, height: 844 });
  const language = page.locator("hh-language-switcher .hh-language-trigger");
  await language.click();
  await page.getByRole("option", { name: /English/i }).click();
  await expect(page.locator("html")).toHaveAttribute("lang", "en");
  await expect(
    page.getByRole("heading", { name: "Production work" }),
  ).toBeVisible();

  const menu = page.locator(".operator-shell__icon-button");
  await menu.click();
  await page.getByRole("menuitem", { name: /Switch theme/i }).click();
  const theme = await page.locator("html").getAttribute("data-theme");
  expect(theme).toMatch(/dark|light/);
  await page.reload();
  await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
  const healthResponse = page.waitForResponse((response) =>
    response.url().includes("/api/v1/manufacturing/dashboard/machine-health"),
  );
  const maintenancePlansResponse = page.waitForResponse((response) =>
    response.url().includes("/api/v1/manufacturing/maintenance-plans"),
  );
  await page.goto("/operations/maintenance");
  await expect((await healthResponse).status()).toBe(200);
  await expect((await maintenancePlansResponse).status()).toBe(200);
  await page.getByRole("tab", { name: /Machine|Thông tin máy/i }).click();
  await expect(page.locator(".maintenance-page details")).toHaveCount(0);
  await expect(page.getByRole("tab")).toHaveCount(3);
  await expect(
    page.getByRole("heading", { name: /Machine health|Tình trạng máy/i }),
  ).toBeVisible();
  await expect(page.locator("body")).not.toContainText("mobile.");
  await expect
    .poll(() =>
      page.evaluate(
        () => document.documentElement.scrollWidth - window.innerWidth,
      ),
    )
    .toBeLessThanOrEqual(1);
  const lotsResponse = page.waitForResponse((response) =>
    response.url().includes("/api/v1/manufacturing/lots"),
  );
  await page.goto("/operations/traceability");
  await expect((await lotsResponse).status()).toBe(200);
  await expect(
    page.getByRole("heading", { name: /Scan a lot|Quét lô/i }),
  ).toBeVisible();
  await page.getByRole("tab", { name: /History|Lịch sử/i }).click();
  await expect(page.locator(".scan-page details")).toHaveCount(0);
  await expect(page.getByRole("tab")).toHaveCount(2);
  await expect(page.locator(".scan-page button").first()).toBeVisible();
  await page.getByRole("tab", { name: /Lot action|Thao tác lô/i }).click();
  await expect
    .poll(() =>
      page.evaluate(
        () => document.documentElement.scrollWidth - window.innerWidth,
      ),
    )
    .toBeLessThanOrEqual(1);
  await expect(page.locator(".scan-page button").first()).toBeVisible();
  expect(
    await page
      .locator(".scan-page button")
      .first()
      .evaluate(
        (el) =>
          el.getBoundingClientRect().right <=
          document.documentElement.clientWidth + 1,
      ),
  ).toBeTruthy();
  const lotSelect = page.locator(".scan-page hh-select");
  await lotSelect.click();
  await expect
    .poll(async () => page.locator(".hh-select__option").count())
    .toBeGreaterThan(1);
  const recallResponse = page.waitForResponse(
    (response) =>
      response.url().includes("/api/v1/manufacturing/lots/") &&
      response.url().includes("/recall-impact"),
  );
  await page.locator(".hh-select__option").nth(1).click();
  await page.getByRole("button", { name: /Open lot/i }).click();
  await expect((await recallResponse).status()).toBe(200);
  const plansResponse = page.waitForResponse((response) =>
    response.url().includes("/api/v1/manufacturing/inspection-plan-versions"),
  );
  await page.goto("/operations/quality");
  await expect((await plansResponse).status()).toBe(200);
  await expect(
    page.getByRole("heading", { name: /Record inspection|Ghi nhận kiểm tra/i }),
  ).toBeVisible();
  await expect(page.locator(".quality-page details")).toHaveCount(0);
  await page.getByRole("tab", { name: /Samples|Mẫu/i }).click();
  await expect(
    page.getByRole("heading", { name: /Quality sample|Mẫu chất lượng/i }),
  ).toBeVisible();
  await page.getByRole("tab", { name: /Deviations|Sai lệch/i }).click();
  await expect(
    page.getByRole("heading", {
      name: /Production deviation|Sai lệch sản xuất/i,
    }),
  ).toBeVisible();
  await page.getByRole("tab", { name: /Inspection|Kiểm tra/i }).click();
  await expect(page.locator("body")).not.toContainText("mobile.");
  await expect
    .poll(() =>
      page.evaluate(
        () => document.documentElement.scrollWidth - window.innerWidth,
      ),
    )
    .toBeLessThanOrEqual(1);
  expect(
    await page
      .locator(".quality-page fieldset")
      .evaluateAll((els) =>
        els.every(
          (el) =>
            el.getBoundingClientRect().right <=
            document.documentElement.clientWidth + 1,
        ),
      ),
  ).toBeTruthy();
  await page.goto("/operations/sync");
  await expect(
    page.getByRole("heading", { name: /Sync status|Trạng thái đồng bộ/i }),
  ).toBeVisible();
  await expect(page.locator("body")).not.toContainText("mobile.");
  await expect
    .poll(() =>
      page.evaluate(
        () => document.documentElement.scrollWidth - window.innerWidth,
      ),
    )
    .toBeLessThanOrEqual(1);
  await page.goto("/operations/handover");
  await expect(
    page.getByRole("heading", { name: /Shift handover|Bàn giao ca/i }),
  ).toBeVisible();
  await expect(page.locator("body")).not.toContainText("mobile.");
});

test("login page exposes the operator mobile entry point", async ({ page }) => {
  await page.goto("/auth/login");
  await expect(page.locator(".mobile-auth__eyebrow")).toHaveText(
    /His\.Hope Mobile|Operator access|Truy cập operator/i,
  );
  await expect(page.locator("button")).toHaveCount(1);
});

for (const role of roleMatrix) {
  test(`authenticated operator role ${role.name} reaches production dashboard`, async ({ page }) => {
    await signIn(page, role.email, role.password);
    await expect(
      page.getByRole("heading", { name: /Production work|Vận hành sản xuất/i }),
    ).toBeVisible();
    await expect(page.locator("body")).not.toContainText("mobile.");
  });
}
