import { expect, test } from '@playwright/test';

const operatorUrl = process.env.OPERATOR_APP_URL ?? 'http://localhost:4300';
const email = process.env.E2E_EMAIL;
const password = process.env.E2E_PASSWORD;
const routes = ['dashboard', 'master-data', 'traceability', 'procurement', 'production', 'quality-inspections'];

async function login(page) {
  await page.goto(`${operatorUrl}/auth/login`);
  await page.locator('hh-action-button button').click();
  await page.waitForURL(u => u.toString().includes('/Account/Login'));
  await page.locator('#email').fill(email);
  await page.locator('#password').fill(password);
  await page.getByRole('button', { name: /Sign in/i }).first().click();
  await page.waitForURL(u => u.toString().includes('/dashboard'));
}

test.describe('manufacturing operator completeness @operator-app', () => {
  test('protected feature routes redirect unauthenticated users to login', async ({ page }) => {
    await page.goto(`${operatorUrl}/master-data`);
    await expect(page).toHaveURL(/\/auth\/login/);
  });

  test('authenticated operator can navigate completed feature groups', async ({ page }) => {
    test.skip(!email || !password, 'Set E2E_EMAIL and E2E_PASSWORD for authenticated operator coverage.');
    const consoleErrors = [];
    const serverErrors = [];
    page.on('console', message => { if (message.type() === 'error') consoleErrors.push(message.text()); });
    page.on('response', response => { if (response.status() >= 500) serverErrors.push(`${response.status()} ${response.url()}`); });

    await login(page);

    for (const route of routes) {
      await page.goto(`${operatorUrl}/${route}`);
      await expect(page).toHaveURL(new RegExp(`/${route}`));
      await expect(page.locator('hh-page-header')).toBeVisible();

      if (route === 'dashboard') {
        await expect(page.getByRole('heading', { name: /Cost projection|Dự phóng chi phí/i })).toBeVisible();
      }
      if (route === 'master-data') {
        await expect(page.getByRole('heading', { name: /Products|Sản phẩm/i })).toBeVisible();
      }
      if (route === 'traceability') {
        await expect(page.getByRole('heading', { name: /Reservations|Phiếu giữ hàng/i })).toBeVisible();
      }
      if (route === 'procurement') {
        await expect(page.locator('.procurement-nav')).toBeVisible();
        for (const anchor of ['requirements', 'master-data', 'facilities', 'suppliers', 'rfqs', 'purchase-orders', 'inbound-receipts']) {
          await expect(page.locator(`a[href="#${anchor}"]`)).toBeVisible();
        }
      }
      if (route === 'production') {
        await expect(page.getByRole('heading', { name: /Production orders|Đơn sản xuất/i })).toBeVisible();
      }
      if (route === 'quality-inspections') {
        await expect(page.getByText(/Inspection history|Lịch sử kiểm tra/i, { exact: true })).toBeVisible();
      }
    }

    expect(consoleErrors).toEqual([]);
    expect(serverErrors).toEqual([]);
  });

  test('authenticated operator can submit a master-data write', async ({ page }) => {
    test.skip(!email || !password, 'Set E2E_EMAIL and E2E_PASSWORD for authenticated operator coverage.');
    await login(page);
    await page.goto(`${operatorUrl}/master-data`);
    await expect(page.getByRole('heading', { name: /Products|Sản phẩm/i })).toBeVisible();
    await page.getByRole('button', { name: /Add product|Thêm sản phẩm/i }).click();
    const suffix = Date.now().toString(36).toUpperCase();
    const productSku = `E2E-${suffix}`;
    await page.locator('input[name="sku"]').fill(productSku);
    await page.locator('input[name="name"]').fill(`E2E product ${suffix}`);
    const uom = await page.locator('select[name="productUom"] option').nth(1).getAttribute('value');
    expect(uom).toBeTruthy();
    await page.locator('select[name="productUom"]').selectOption(uom);
    await page.getByRole('button', { name: /Save|Lưu/i }).last().click();
    await expect(page.getByText(productSku, { exact: true })).toBeVisible();
  });

  test('authenticated operator can reserve a released lot', async ({ page }) => {
    test.skip(!email || !password, 'Set E2E_EMAIL and E2E_PASSWORD for authenticated operator coverage.');
    await login(page);
    await page.goto(`${operatorUrl}/traceability`);
    await expect(page.getByRole('heading', { name: /Reservations|Phiếu giữ hàng/i })).toBeVisible();
    const lotsResponse = await page.request.get(`${operatorUrl}/api/v1/manufacturing/lots?disposition=Released&limit=1`);
    expect(lotsResponse.ok()).toBeTruthy();
    const lots = await lotsResponse.json();
    expect(lots.length).toBeGreaterThan(0);
    const referenceId = crypto.randomUUID();
    await page.locator('input[name="reservationLotId"]').fill(lots[0].id);
    await page.locator('input[name="reservationReferenceType"]').fill('E2E');
    await page.locator('input[name="reservationReferenceId"]').fill(referenceId);
    await page.locator('input[name="reservationQuantity"]').fill('0.001');
    await page.getByRole('button', { name: /Reserve|Giữ/i }).click();
    await expect(page.getByText('E2E', { exact: true })).toBeVisible();
  });

  test('authenticated operator can create a supplier', async ({ page }) => {
    test.skip(!email || !password, 'Set E2E_EMAIL and E2E_PASSWORD for authenticated operator coverage.');
    await login(page);
    await page.goto(`${operatorUrl}/procurement`);
    await expect(page.locator('.procurement-nav')).toBeVisible();
    await page.getByRole('button', { name: /Add supplier|Thêm nhà cung cấp/i }).click();
    const suffix = Date.now().toString(36).toUpperCase();
    const supplierCode = `E2E-${suffix}`;
    await page.locator('input[name="supplierCode"]').fill(supplierCode);
    await page.locator('input[name="supplierName"]').fill(`E2E supplier ${suffix}`);
    const supplierForm = page.locator('form').filter({ has: page.locator('input[name="supplierCode"]') });
    await supplierForm.getByRole('button', { name: /Save|Lưu/i }).click();
    await expect(page.getByText(supplierCode, { exact: true })).toBeVisible();
  });

  test('authenticated operator can create a production order', async ({ page }) => {
    test.skip(!email || !password, 'Set E2E_EMAIL and E2E_PASSWORD for authenticated operator coverage.');
    await login(page);
    await page.goto(`${operatorUrl}/production`);
    await expect(page.getByRole('heading', { name: /Production orders|Đơn sản xuất/i })).toBeVisible();
    const recipeOption = page.locator('select[name="recipeId"] option').nth(1);
    const recipeId = await recipeOption.getAttribute('value');
    const recipeLabel = (await recipeOption.textContent()) ?? '';
    expect(recipeId).toBeTruthy();
    expect(recipeId).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
    const suffix = Date.now().toString(36).toUpperCase();
    const orderNumber = `E2E-${suffix}`;
    await page.locator('input[name="orderNumber"]').fill(orderNumber);
    await page.locator('input[name="productSku"]').fill(recipeLabel.split(' · ')[0]);
    await page.locator('select[name="recipeId"]').selectOption({ value: recipeId });
    await expect(page.locator('select[name="recipeId"]')).toHaveValue(recipeId);
    await page.locator('input[name="targetQuantity"]').fill('1');
    await page.locator('input[name="outputUom"]').fill('kg');
    await page.getByRole('button', { name: /Create production order|Tạo lệnh sản xuất/i }).first().click();
    await expect(page.getByText(orderNumber, { exact: true })).toBeVisible();
  });

  test('authenticated operator can record a quality inspection', async ({ page }) => {
    test.skip(!email || !password, 'Set E2E_EMAIL and E2E_PASSWORD for authenticated operator coverage.');
    await login(page);
    await page.goto(`${operatorUrl}/quality-inspections`);
    await expect(page.getByText(/Inspection history|Lịch sử kiểm tra/i, { exact: true })).toBeVisible();
    const inspector = `e2e-${Date.now().toString(36)}`;
    await page.getByRole('textbox', { name: /Inspector|Người kiểm tra/i }).fill(inspector);
    await page.getByRole('button', { name: /Record inspection|Ghi nhận kiểm tra/i }).click();
    await expect(page.getByText(new RegExp(inspector))).toBeVisible();
  });
});
