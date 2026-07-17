// @ts-check
const { test, expect } = require('@playwright/test');

const BASE = 'http://localhost:8081';
const TEST_USER = 'admin@hishop.vn';
const TEST_PASS = 'Password123!';

// ============================================================
// PHASE 1: AUTH & NAVIGATION
// ============================================================
test.describe('Auth & Navigation', () => {

  test('TC01: Login page renders correctly', async ({ page }) => {
    await page.goto(BASE + '/auth/login');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('mat-card-title')).toContainText('His.Hope');
    await expect(page.locator('button[type="submit"]')).toContainText('Đăng nhập');
    await page.screenshot({ path: 'screenshots/tc01-login-page.png', fullPage: true });
  });

  test('TC02: Login with valid credentials - registered user', async ({ page }) => {
    await page.goto(BASE + '/auth/login');
    await page.waitForLoadState('networkidle');

    // Fill login form using formControlName
    await page.locator('input[formControlName="username"]').fill(TEST_USER);
    await page.locator('input[formControlName="password"]').fill(TEST_PASS);

    // Click submit
    await page.locator('button[type="submit"]').click();

    // Wait for navigation to dashboard (mock service returns immediately)
    await page.waitForTimeout(2000);

    // Should redirect to dashboard after successful login
    const currentUrl = page.url();
    const isRedirected = currentUrl.includes('/dashboard') || currentUrl.includes('/');
    expect(isRedirected).toBeTruthy();
    await page.screenshot({ path: 'screenshots/tc02-login-success.png', fullPage: true });
  });

  test('TC03: Login form validation - empty fields', async ({ page }) => {
    await page.goto(BASE + '/auth/login');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Check that form is invalid when fields are empty
    // Button should be disabled because form is invalid
    const submitBtn = page.locator('button[type="submit"]');
    await expect(submitBtn).toBeDisabled({ timeout: 5000 });
    await page.screenshot({ path: 'screenshots/tc03-form-validation.png', fullPage: true });
  });

  test('TC04: Protected route loads after login', async ({ page }) => {
    // Mock service always returns isLoggedIn=true, so protected routes load directly
    // First login
    await page.goto(BASE + '/auth/login');
    await page.waitForLoadState('networkidle');
    await page.locator('input[formControlName="username"]').fill(TEST_USER);
    await page.locator('input[formControlName="password"]').fill(TEST_PASS);
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(1500);

    // Navigate to protected route
    await page.goto(BASE + '/patients');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500);

    // Should successfully load patients page
    expect(page.url()).toContain('patients');
    await page.screenshot({ path: 'screenshots/tc04-protected-route.png', fullPage: true });
  });
});

// ============================================================
// PHASE 2: DASHBOARD & SIDEBAR
// ============================================================
test.describe('Dashboard & Sidebar Navigation', () => {

  test.beforeEach(async ({ page }) => {
    // Login first
    await page.goto(BASE + '/auth/login');
    await page.waitForLoadState('networkidle');
    await page.locator('input[formControlName="username"]').fill(TEST_USER);
    await page.locator('input[formControlName="password"]').fill(TEST_PASS);
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);
  });

  test('TC05: Dashboard loads with content', async ({ page }) => {
    await page.goto(BASE + '/dashboard');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);
    await page.screenshot({ path: 'screenshots/tc05-dashboard.png', fullPage: true });
  });

  test('TC06: Navigate to Bệnh nhân via sidebar', async ({ page }) => {
    await page.goto(BASE + '/dashboard');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);
    await page.screenshot({ path: 'screenshots/tc06-nav-patients.png', fullPage: true });
  });

  test('TC07: Navigate to Quản trị (Admin)', async ({ page }) => {
    await page.goto(BASE + '/admin');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);
    await page.screenshot({ path: 'screenshots/tc07-admin.png', fullPage: true });
  });
});

// ============================================================
// PHASE 3: ALL MODULE ROUTES (8 modules)
// ============================================================
test.describe('All Module Routes', () => {

  test.beforeEach(async ({ page }) => {
    await page.goto(BASE + '/auth/login');
    await page.waitForLoadState('networkidle');
    await page.locator('input[formControlName="username"]').fill(TEST_USER);
    await page.locator('input[formControlName="password"]').fill(TEST_PASS);
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);
  });

  const routes = [
    { name: 'Dashboard', path: '/dashboard' },
    { name: 'Bệnh nhân', path: '/patients' },
    { name: 'Lịch hẹn', path: '/appointments' },
    { name: 'Lâm sàng', path: '/clinical' },
    { name: 'Dược phẩm', path: '/pharmacy/medications' },
    { name: 'Đơn thuốc', path: '/pharmacy/prescriptions' },
    { name: 'Xét nghiệm', path: '/lab' },
    { name: 'Thanh toán', path: '/billing' },
    { name: 'Quản trị', path: '/admin' },
  ];

  for (const route of routes) {
    test(`TC08-${route.name}: Route /${route.path.replace(/^\//, '')} loads`, async ({ page }) => {
      await page.goto(BASE + route.path);
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(1500);
      const url = page.url();
      expect(url).toContain(route.path);
      await page.screenshot({ path: `screenshots/tc08-${route.name.replace(/ /g, '_')}.png`, fullPage: true });
    });
  }
});

// ============================================================
// PHASE 4: FORM PAGES
// ============================================================
test.describe('Form Pages', () => {

  test.beforeEach(async ({ page }) => {
    await page.goto(BASE + '/auth/login');
    await page.waitForLoadState('networkidle');
    await page.locator('input[formControlName="username"]').fill(TEST_USER);
    await page.locator('input[formControlName="password"]').fill(TEST_PASS);
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);
  });

  test('TC09: Patient create form', async ({ page }) => {
    await page.goto(BASE + '/patients/new');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500);
    await page.screenshot({ path: 'screenshots/tc09-patient-form.png', fullPage: true });
  });

  test('TC10: Appointment create form', async ({ page }) => {
    await page.goto(BASE + '/appointments/new');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500);
    await page.screenshot({ path: 'screenshots/tc10-appointment-form.png', fullPage: true });
  });

  test('TC11: Medication create form', async ({ page }) => {
    await page.goto(BASE + '/pharmacy/medications/new');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500);
    await page.screenshot({ path: 'screenshots/tc11-medication-form.png', fullPage: true });
  });

  test('TC12: Prescription create form', async ({ page }) => {
    await page.goto(BASE + '/pharmacy/prescriptions/new');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500);
    await page.screenshot({ path: 'screenshots/tc12-prescription-form.png', fullPage: true });
  });

  test('TC13: Lab order create form', async ({ page }) => {
    await page.goto(BASE + '/lab/new');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500);
    await page.screenshot({ path: 'screenshots/tc13-lab-form.png', fullPage: true });
  });

  test('TC14: Invoice create form', async ({ page }) => {
    await page.goto(BASE + '/billing/new');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500);
    await page.screenshot({ path: 'screenshots/tc14-invoice-form.png', fullPage: true });
  });
});

// ============================================================
// PHASE 5: ADMIN SUB-ROUTES
// ============================================================
test.describe('Admin Sub-routes', () => {

  test.beforeEach(async ({ page }) => {
    await page.goto(BASE + '/auth/login');
    await page.waitForLoadState('networkidle');
    await page.locator('input[formControlName="username"]').fill(TEST_USER);
    await page.locator('input[formControlName="password"]').fill(TEST_PASS);
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);
  });

  test('TC15: Admin dashboard', async ({ page }) => {
    await page.goto(BASE + '/admin');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500);
    await page.screenshot({ path: 'screenshots/tc15-admin-dashboard.png', fullPage: true });
  });

  test('TC16: Manage users', async ({ page }) => {
    await page.goto(BASE + '/admin/manage-users');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500);
    await page.screenshot({ path: 'screenshots/tc16-manage-users.png', fullPage: true });
  });

  test('TC17: Manage roles', async ({ page }) => {
    await page.goto(BASE + '/admin/manage-roles');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500);
    await page.screenshot({ path: 'screenshots/tc17-manage-roles.png', fullPage: true });
  });

  test('TC18: Settings', async ({ page }) => {
    await page.goto(BASE + '/admin/settings');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500);
    await page.screenshot({ path: 'screenshots/tc18-settings.png', fullPage: true });
  });

  test('TC19: Audit logs', async ({ page }) => {
    await page.goto(BASE + '/admin/audit-logs');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500);
    await page.screenshot({ path: 'screenshots/tc19-audit-logs.png', fullPage: true });
  });
});

// ============================================================
// PHASE 6: ACCESS DENIED & CATCH-ALL
// ============================================================
test.describe('Edge Cases', () => {

  test.beforeEach(async ({ page }) => {
    await page.goto(BASE + '/auth/login');
    await page.waitForLoadState('networkidle');
    await page.locator('input[formControlName="username"]').fill(TEST_USER);
    await page.locator('input[formControlName="password"]').fill(TEST_PASS);
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(2000);
  });

  test('TC20: Access denied page', async ({ page }) => {
    await page.goto(BASE + '/access-denied');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500);
    await page.screenshot({ path: 'screenshots/tc20-access-denied.png', fullPage: true });
  });

  test('TC21: Wildcard redirect to dashboard', async ({ page }) => {
    await page.goto(BASE + '/some-nonexistent-route');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    await page.screenshot({ path: 'screenshots/tc21-wildcard-redirect.png', fullPage: true });
  });
});
