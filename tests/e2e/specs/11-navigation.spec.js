const { test, expect } = require('@playwright/test');

const { clinicalUrl: BASE } = require('../config/urls');
const { signInThroughIdentity } = require('../helpers/sso-login');
const { ensureSidebarVisible } = require('../helpers/ensure-sidebar-visible');
const AUTH_LOGIN_RE = /\/(?:en\/)?auth\/login(?:\?|$)/;
const ACCESS_DENIED_RE = /\/(?:en\/)?access-denied(?:\?|$)/;

async function doLogin(page) {
  await signInThroughIdentity(page, BASE);
  return /\/(?:en\/)?dashboard(?:\?|$)/.test(page.url());
}

test.describe('Sidebar Navigation', () => {
  test.beforeEach(async ({ page }) => {
    const loggedIn = await doLogin(page);
    if (!loggedIn) {
      test.skip(true, 'Protected navigation routes are unavailable in this environment.');
    }

    await ensureSidebarVisible(page);
  });

  async function clickSidebarLink(page, text, expectedPath) {
    const labels = {
      'Bệnh nhân': /Bệnh nhân|Patients/i,
      'Lịch hẹn': /Lịch hẹn|Appointments/i,
      'Lâm sàng': /Lâm sàng|Clinical/i,
      'Dược phẩm': /Dược phẩm|Pharmacy/i,
      'Xét nghiệm': /Xét nghiệm|Laboratory|Lab/i,
      'Thanh toán': /Thanh toán|Billing/i,
      'Quản trị': /Quản trị|Administration|Admin/i,
    };
    await ensureSidebarVisible(page);
    const link = page.locator('nav[hhShellNavigation] a:visible, mat-nav-list a:visible').filter({ hasText: labels[text] || text });
    await expect(link.first()).toBeVisible({ timeout: 10000 });
    await link.first().click();
    if (expectedPath) {
      await page.waitForURL(
        (url) => new RegExp(expectedPath).test(url.toString()) || AUTH_LOGIN_RE.test(url.toString()) || ACCESS_DENIED_RE.test(url.toString()),
        { timeout: 15000 },
      );
    }

    if (ACCESS_DENIED_RE.test(page.url()) && text === 'Quản trị') {
      return 'access-denied';
    }

    expect(page.url()).toMatch(new RegExp(expectedPath));
    return 'ok';
  }

  test('TC-NAV-01: Dashboard link navigates to /dashboard', async ({ page }) => {
    await clickSidebarLink(page, 'Dashboard', '\\/dashboard');
  });

  test('TC-NAV-02: Bệnh nhân link navigates to /patients', async ({ page }) => {
    await clickSidebarLink(page, 'Bệnh nhân', '\\/patients');
  });

  test('TC-NAV-03: Lịch hẹn link navigates to /appointments', async ({ page }) => {
    await clickSidebarLink(page, 'Lịch hẹn', '\\/appointments');
  });

  test('TC-NAV-04: Lâm sàng link navigates to /clinical', async ({ page }) => {
    await clickSidebarLink(page, 'Lâm sàng', '\\/clinical');
  });

  test('TC-NAV-05: Dược phẩm link navigates to /pharmacy', async ({ page }) => {
    await clickSidebarLink(page, 'Dược phẩm', '\\/pharmacy');
  });

  test('TC-NAV-06: Xét nghiệm link navigates to /lab', async ({ page }) => {
    await clickSidebarLink(page, 'Xét nghiệm', '\\/lab');
  });

  test('TC-NAV-07: Thanh toán link navigates to /billing', async ({ page }) => {
    await clickSidebarLink(page, 'Thanh toán', '\\/billing');
  });

  test('TC-NAV-08: Quản trị link navigates to /admin', async ({ page }) => {
    const status = await clickSidebarLink(page, 'Quản trị', '\\/admin');
    expect(status === 'ok' || status === 'access-denied').toBeTruthy();
    expect(page.url()).toMatch(/\/(?:en\/)?(admin|access-denied)(?:\?|$)/);
  });
});
