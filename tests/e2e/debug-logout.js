const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  
  await page.goto('http://localhost:8081/auth/login');
  await page.waitForLoadState('networkidle');
  await page.locator('input[formControlName="username"]').fill('admin');
  await page.locator('input[formControlName="password"]').fill('Admin@123');
  await page.locator('button[type="submit"]').click();
  await page.waitForURL(/\/dashboard/, { timeout: 15000 });
  
  // Check page content
  const bodyText = await page.locator('body').textContent();
  console.log('Body text contains sidebar-footer:', bodyText.includes('sidebar-footer'));
  console.log('Body text contains Đăng xuất:', bodyText.includes('Đăng xuất'));
  
  // Find logout button
  const selectors = [
    '.sidebar-footer button',
    '.sidebar-footer button[aria-label="Đăng xuất"]',
    '.sidebar-footer button mat-icon',
    'button[aria-label="Đăng xuất"]',
    '.sidebar-footer',
    'app-sidebar',
  ];
  
  for (const sel of selectors) {
    const count = await page.locator(sel).count();
    const visible = count > 0 ? await page.locator(sel).first().isVisible().catch(() => false) : false;
    console.log(`${sel}: count=${count}, visible=${visible}`);
  }
  
  // Check if app-sidebar exists
  const sidebarCount = await page.locator('app-sidebar').count();
  console.log('app-sidebar count:', sidebarCount);
  
  if (sidebarCount > 0) {
    const sidebarHtml = await page.locator('app-sidebar').innerHTML();
    console.log('Sidebar HTML (first 500 chars):', sidebarHtml.substring(0, 500));
  }
  
  await page.screenshot({ path: 'screenshots/debug-logout.png', fullPage: true });
  
  await browser.close();
})();
