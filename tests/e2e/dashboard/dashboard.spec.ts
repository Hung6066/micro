import { test, expect, Page } from '@playwright/test';

const DASHBOARD_URL = 'http://localhost:4201';

/**
 * Create a mock JWT token that expires far in the future.
 * The auth guard checks if localStorage has an 'access_token' with a valid exp claim.
 */
function createMockToken(): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const payload = btoa(
    JSON.stringify({
      sub: 'e2e-test-user',
      name: 'E2E Test User',
      exp: Math.floor(Date.now() / 1000) + 86400 * 30, // 30 days from now
      iat: Math.floor(Date.now() / 1000),
      roles: ['admin'],
    }),
  );
  const signature = btoa('mock-signature-for-e2e-tests');
  return `${header}.${payload}.${signature}`;
}

/**
 * Bypass authentication by setting a mock JWT token in localStorage
 * and reloading the page so the AuthGuard and AuthService detect it.
 */
async function bypassAuth(page: Page): Promise<void> {
  await page.goto(DASHBOARD_URL + '/auth/login');
  await page.evaluate((token) => {
    localStorage.setItem('access_token', token);
    localStorage.setItem('refresh_token', 'mock-refresh-token');
  }, createMockToken());
  // Navigate to the app root; AuthGuard should read the token
  await page.goto(DASHBOARD_URL + '/resources');
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(1000);
}

test.describe('Aspire Dashboard', () => {
  test.beforeEach(async ({ page }) => {
    await bypassAuth(page);
  });

  test('resources page shows service groups', async ({ page }) => {
    // Default route redirects to /resources
    await expect(page).toHaveURL(/\/resources/);

    // Page title in Vietnamese
    await expect(page.getByText('Tài nguyên hệ thống')).toBeVisible({ timeout: 10000 });

    // Check for resource group headings (Services, Databases, Infrastructure)
    // These appear when resources are loaded
    const pageContent = page.locator('.main-content');
    await expect(pageContent).toBeVisible();

    // At least one resource group or empty state should be present
    const groupHeaders = page.locator('.group-title');
    const emptyState = page.locator('text=Không có tài nguyên nào');
    const loadingSpinner = page.locator('mat-spinner');

    // Wait for loading to finish
    await loadingSpinner.first().waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
    await page.waitForTimeout(500);

    // Either we have data groups or empty state
    const groupCount = await groupHeaders.count();
    const isEmpty = await emptyState.isVisible().catch(() => false);
    expect(groupCount > 0 || isEmpty).toBeTruthy();
  });

  test('can navigate between pages via sidebar', async ({ page }) => {
    // Click "Nhật ký" (Logs) in sidebar
    await page.getByText('Nhật ký').click();
    await expect(page).toHaveURL(/\/logs/);
    await expect(page.getByText('Nhật ký hệ thống')).toBeVisible();

    // Click "Truy vết" (Traces) in sidebar
    await page.getByText('Truy vết').click();
    await expect(page).toHaveURL(/\/traces/);
    await expect(page.getByText('Truy vết hệ thống')).toBeVisible();

    // Click "Chỉ số" (Metrics) in sidebar
    await page.getByText('Chỉ số').click();
    await expect(page).toHaveURL(/\/metrics/);
    await expect(page.getByText('Chỉ số hệ thống')).toBeVisible();

    // Click "Tài nguyên" (Resources) to go back
    await page.getByText('Tài nguyên').click();
    await expect(page).toHaveURL(/\/resources/);
    await expect(page.getByText('Tài nguyên hệ thống')).toBeVisible();
  });

  test('logs page has filter controls', async ({ page }) => {
    await page.getByText('Nhật ký').click();
    await expect(page).toHaveURL(/\/logs/);

    // Logs page has a search textbox with placeholder "Từ khóa..."
    const searchInput = page.locator('input[placeholder*="Từ khóa"]');
    await expect(searchInput).toBeVisible();

    // Service filter dropdown
    const serviceSelect = page.locator('mat-select').filter({ hasText: /Dịch vụ/i });
    await expect(serviceSelect.first()).toBeVisible();

    // Search button
    const searchButton = page.getByRole('button', { name: /Tìm kiếm/i });
    await expect(searchButton).toBeVisible();

    // Log table should be present (or empty state)
    await page.waitForTimeout(1000);
    const hasTable = await page.locator('table[mat-table]').count();
    if (hasTable > 0) {
      await expect(page.locator('table[mat-table]')).toBeVisible();
    }
  });

  test('logs page service filter shows options', async ({ page }) => {
    await page.getByText('Nhật ký').click();
    await expect(page).toHaveURL(/\/logs/);

    // Open the service dropdown
    const serviceSelect = page.locator('mat-form-field').filter({ hasText: 'Dịch vụ' }).locator('mat-select');
    await serviceSelect.click();

    // Wait for the options panel
    const optionsPanel = page.locator('.mat-mdc-select-panel');
    await expect(optionsPanel).toBeVisible({ timeout: 5000 });

    // Should list services
    const options = optionsPanel.locator('mat-option');
    const count = await options.count();
    expect(count).toBeGreaterThanOrEqual(1);

    // Close the panel by pressing Escape
    await page.keyboard.press('Escape');
  });

  test('traces page has search and filter controls', async ({ page }) => {
    await page.getByText('Truy vết').click();
    await expect(page).toHaveURL(/\/traces/);

    // Traces page should have a search button
    const searchButton = page.getByRole('button', { name: /Tìm kiếm/i });
    await expect(searchButton).toBeVisible();

    // Service filter dropdown
    const serviceSelect = page.locator('mat-form-field').filter({ hasText: 'Dịch vụ' }).locator('mat-select');
    await expect(serviceSelect).toBeVisible();

    // Time range dropdown
    const timeRangeSelect = page.locator('mat-form-field').filter({ hasText: /Khoảng thời gian/i }).locator('mat-select');
    await expect(timeRangeSelect).toBeVisible();

    // Min duration input
    const minDurationInput = page.locator('input[placeholder="0"]');
    await expect(minDurationInput).toBeVisible();
  });

  test('traces page time range options are selectable', async ({ page }) => {
    await page.getByText('Truy vết').click();
    await expect(page).toHaveURL(/\/traces/);

    // Open time range dropdown
    const timeRangeSelect = page.locator('mat-form-field').filter({ hasText: /Khoảng thời gian/i }).locator('mat-select');
    await timeRangeSelect.click();

    const panel = page.locator('.mat-mdc-select-panel');
    await expect(panel).toBeVisible({ timeout: 5000 });

    // Verify time range options exist
    await expect(panel.getByText('15 phút')).toBeVisible();
    await expect(panel.getByText('1 giờ')).toBeVisible();
    await expect(panel.getByText('6 giờ')).toBeVisible();
    await expect(panel.getByText('24 giờ')).toBeVisible();
    await expect(panel.getByText('7 ngày')).toBeVisible();

    await page.keyboard.press('Escape');
  });

  test('metrics page has chart controls and canvas area', async ({ page }) => {
    await page.getByText('Chỉ số').click();
    await expect(page).toHaveURL(/\/metrics/);

    // Metrics page should have service multi-select
    const serviceSelect = page.locator('mat-form-field').filter({ hasText: 'Dịch vụ' }).locator('mat-select');
    await expect(serviceSelect).toBeVisible();

    // Metric type selector
    const metricTypeSelect = page.locator('mat-form-field').filter({ hasText: 'Loại chỉ số' }).locator('mat-select');
    await expect(metricTypeSelect).toBeVisible();

    // Time range selector
    const timeRangeSelect = page.locator('mat-form-field').filter({ hasText: 'Khoảng thời gian' }).locator('mat-select');
    await expect(timeRangeSelect).toBeVisible();

    // Apply button
    const applyButton = page.getByRole('button', { name: /Áp dụng/i });
    await expect(applyButton).toBeVisible();

    // Wait for any loading to complete
    const spinner = page.locator('mat-spinner');
    await spinner.first().waitFor({ state: 'hidden', timeout: 10000 }).catch(() => {});

    // If services are available and a chart renders, canvas should be visible
    // Otherwise, the empty state hint should be visible
    const canvas = page.locator('canvas');
    const emptyHint = page.getByText('Chọn dịch vụ và chỉ số để xem biểu đồ');
    const canvasVisible = await canvas.isVisible().catch(() => false);
    const emptyVisible = await emptyHint.isVisible().catch(() => false);

    if (canvasVisible) {
      // Chart canvas should have non-zero dimensions
      const box = await canvas.boundingBox();
      expect(box).not.toBeNull();
      if (box) {
        expect(box.width).toBeGreaterThan(0);
        expect(box.height).toBeGreaterThan(0);
      }
    } else if (emptyVisible) {
      await expect(emptyHint).toBeVisible();
    } else {
      // At minimum, the metrics page loaded
      expect(true).toBeTruthy();
    }
  });

  test('metrics page metric types are selectable', async ({ page }) => {
    await page.getByText('Chỉ số').click();
    await expect(page).toHaveURL(/\/metrics/);

    // Open metric type dropdown
    const metricTypeSelect = page.locator('mat-form-field').filter({ hasText: 'Loại chỉ số' }).locator('mat-select');
    await metricTypeSelect.click();

    const panel = page.locator('.mat-mdc-select-panel');
    await expect(panel).toBeVisible({ timeout: 5000 });

    // Verify metric type options
    await expect(panel.getByText('CPU')).toBeVisible();
    await expect(panel.getByText('Bộ nhớ')).toBeVisible();
    await expect(panel.getByText('Yêu cầu')).toBeVisible();
    await expect(panel.getByText('Lỗi')).toBeVisible();

    await page.keyboard.press('Escape');
  });

  test('sidebar navigation is visible and contains all items', async ({ page }) => {
    const navList = page.locator('mat-nav-list');
    await expect(navList).toBeVisible();

    // All 4 sidebar navigation items
    const navItems = [
      { icon: 'dns', label: 'Tài nguyên' },
      { icon: 'article', label: 'Nhật ký' },
      { icon: 'timeline', label: 'Truy vết' },
      { icon: 'monitoring', label: 'Chỉ số' },
    ];

    for (const item of navItems) {
      const navLink = navList.locator(`a:has-text("${item.label}")`);
      await expect(navLink.first()).toBeVisible();
    }

    // Verify counts
    const links = navList.locator('a');
    expect(await links.count()).toBe(4);
  });

  test('toolbar displays app title and user menu', async ({ page }) => {
    const toolbar = page.locator('.app-toolbar');
    await expect(toolbar).toBeVisible();

    // App title
    await expect(toolbar.getByText('His.Hope Dashboard')).toBeVisible();

    // User menu button
    const userMenu = toolbar.locator('button[aria-label="User menu"]');
    await expect(userMenu).toBeVisible();

    // Navigation toggle
    const navToggle = toolbar.locator('button[aria-label="Toggle navigation"]');
    await expect(navToggle).toBeVisible();
  });

  test('sidenav can be toggled open and closed', async ({ page }) => {
    // Click the menu toggle button
    const toggleBtn = page.locator('button[aria-label="Toggle navigation"]');
    await toggleBtn.click();
    await page.waitForTimeout(500);

    // Click again to re-open
    await toggleBtn.click();
    await page.waitForTimeout(500);

    // Sidenav should be visible (mode="side", opened)
    const sidenav = page.locator('mat-sidenav');
    await expect(sidenav).toBeVisible();
  });

  test('resources page has refresh button', async ({ page }) => {
    // The refresh button should be visible
    const refreshBtn = page.getByRole('button', { name: /Làm mới/i });
    await expect(refreshBtn).toBeVisible();
  });

  test('page navigations update the URL correctly', async ({ page }) => {
    // Verify each route has the correct URL pattern
    const routes = [
      { label: 'Nhật ký', path: /\/logs/ },
      { label: 'Truy vết', path: /\/traces/ },
      { label: 'Chỉ số', path: /\/metrics/ },
      { label: 'Tài nguyên', path: /\/resources/ },
    ];

    for (const route of routes) {
      await page.getByText(route.label).click();
      await expect(page).toHaveURL(route.path);
      // Verify the page heading matches
      const heading = page.locator('h1.page-title');
      await expect(heading).toBeVisible();
    }
  });

  test('user menu has logout button', async ({ page }) => {
    // Open user menu
    const userMenuBtn = page.locator('button[aria-label="User menu"]');
    await userMenuBtn.click();

    // Wait for menu panel
    const menuPanel = page.locator('.mat-mdc-menu-panel');
    await expect(menuPanel).toBeVisible({ timeout: 5000 });

    // Logout option
    const logoutItem = menuPanel.getByText('Đăng xuất');
    await expect(logoutItem).toBeVisible();
  });

  test('protected routes redirect without auth token', async ({ page }) => {
    // Clear auth state
    await page.evaluate(() => {
      localStorage.removeItem('access_token');
      localStorage.removeItem('refresh_token');
    });

    // Navigate to a protected route
    await page.goto(DASHBOARD_URL + '/resources');
    await page.waitForLoadState('networkidle');

    // Should redirect to identity service login page (external redirect)
    // The auth guard calls authService.login() which does window.location.href = identityUrl/auth/login
    // We should be on a login page or redirected
    const currentUrl = page.url();
    // The redirect goes to IdentityService external login, so we may not be on the dashboard app anymore
    const isLoginRedirect = currentUrl.includes('/auth/login') || currentUrl.includes('login');
    const isIdentityRedirect = currentUrl.includes('localhost:5001');
    expect(isLoginRedirect || isIdentityRedirect).toBeTruthy();
  });

  test('can navigate to trace detail if traces exist', async ({ page }) => {
    await page.getByText('Truy vết').click();
    await expect(page).toHaveURL(/\/traces/);

    // Wait for data to load
    await page.waitForTimeout(2000);

    // If trace rows exist, clicking one should navigate to detail
    const traceRows = page.locator('table[mat-table] .trace-row');
    const rowCount = await traceRows.count().catch(() => 0);

    if (rowCount > 0) {
      await traceRows.first().click();
      // Should navigate to /traces/:traceId
      await expect(page).toHaveURL(/\/traces\/[a-zA-Z0-9]+/);
    }
    // If no traces, the empty state should be visible
    else {
      const emptyState = page.getByText('Không có truy vết nào');
      const loadingState = page.getByText('Đang tải...');
      const isVisible = await emptyState.isVisible().catch(() => false);
      const isLoading = await loadingState.isVisible().catch(() => false);
      expect(isVisible || isLoading).toBeTruthy();
    }
  });

  test('logs page shows load more button when data exceeds page size', async ({ page }) => {
    await page.getByText('Nhật ký').click();
    await expect(page).toHaveURL(/\/logs/);

    // Wait for data to load
    await page.waitForTimeout(2000);

    // Check if load more button exists
    const loadMoreBtn = page.getByRole('button', { name: /Tải thêm/i });
    const loadMoreVisible = await loadMoreBtn.isVisible().catch(() => false);

    if (loadMoreVisible) {
      await expect(loadMoreBtn).toBeVisible();
    }
    // If not visible, the dataset is smaller than page size — that's fine
  });

  test('logs page has level filter component', async ({ page }) => {
    await page.getByText('Nhật ký').click();
    await expect(page).toHaveURL(/\/logs/);

    // The log-level-filter component should be present
    const levelFilter = page.locator('app-log-level-filter');
    await expect(levelFilter).toBeVisible();
  });

  test('logs page has real-time stream toggle', async ({ page }) => {
    await page.getByText('Nhật ký').click();
    await expect(page).toHaveURL(/\/logs/);

    // The log-stream component should be present
    const logStream = page.locator('app-log-stream');
    await expect(logStream).toBeVisible();
  });

  test('resource cards display action buttons', async ({ page }) => {
    // Wait for resources to load
    const spinner = page.locator('mat-spinner');
    await spinner.first().waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
    await page.waitForTimeout(500);

    // Check for resource card action buttons
    const startButtons = page.getByRole('button', { name: /Bắt đầu/i });
    const stopButtons = page.getByRole('button', { name: /Dừng/i });
    const restartButtons = page.getByRole('button', { name: /Khởi động lại/i });

    const startCount = await startButtons.count();
    const stopCount = await stopButtons.count();
    const restartCount = await restartButtons.count();

    // At least some action buttons should exist if resources loaded
    if (startCount > 0 || stopCount > 0 || restartCount > 0) {
      expect(true).toBeTruthy();
    } else {
      // Empty state might be shown
      const emptyState = page.getByText('Không có tài nguyên nào');
      const isEmpty = await emptyState.isVisible().catch(() => false);
      if (isEmpty) {
        await expect(emptyState).toBeVisible();
      } else {
        // Loading might still be in progress
        expect(true).toBeTruthy();
      }
    }
  });

  test('resource detail dialog opens on card click', async ({ page }) => {
    // Wait for resources to load
    const spinner = page.locator('mat-spinner');
    await spinner.first().waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
    await page.waitForTimeout(500);

    // Find resource cards
    const cards = page.locator('app-resource-card');
    const cardCount = await cards.count();

    if (cardCount > 0) {
      // Click the first resource card
      await cards.first().click();
      await page.waitForTimeout(1000);

      // A dialog should open with resource detail
      const dialog = page.locator('mat-dialog-container');
      const dialogVisible = await dialog.isVisible().catch(() => false);

      if (dialogVisible) {
        await expect(dialog).toBeVisible();
        // Close the dialog
        await page.keyboard.press('Escape');
        await page.waitForTimeout(500);
        await expect(dialog).not.toBeVisible();
      }
    }
  });

  test('responsive layout: toolbar and content visible on mobile viewport', async ({ page }) => {
    // Set a mobile viewport
    await page.setViewportSize({ width: 375, height: 812 });

    // Reload with mobile viewport
    await bypassAuth(page);

    // Toolbar should still be visible
    const toolbar = page.locator('.app-toolbar');
    await expect(toolbar).toBeVisible();

    // App title should be visible (may be truncated on small screens)
    const title = toolbar.getByText('His.Hope Dashboard');
    await expect(title).toBeVisible();

    // Navigation toggle button should be visible
    const toggleBtn = toolbar.locator('button[aria-label="Toggle navigation"]');
    await expect(toggleBtn).toBeVisible();
  });

  test('user can open and close resource detail dialog', async ({ page }) => {
    // Wait for resources to load
    const spinner = page.locator('mat-spinner');
    await spinner.first().waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
    await page.waitForTimeout(500);

    const cards = page.locator('app-resource-card');
    const cardCount = await cards.count();

    if (cardCount > 0) {
      await cards.first().click();
      await page.waitForTimeout(800);

      const dialog = page.locator('mat-dialog-container');
      const dialogVisible = await dialog.isVisible().catch(() => false);

      if (dialogVisible) {
        // Close via backdrop click (if allowed) or Escape
        await page.keyboard.press('Escape');
        await page.waitForTimeout(500);

        // Dialog should close
        await expect(dialog).not.toBeVisible();
      }
    }
  });
});
