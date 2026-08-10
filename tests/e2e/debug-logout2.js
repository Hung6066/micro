const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext();
  const page = await context.newPage();
  
  // Monitor network requests
  page.on('request', req => {
    if (req.url().includes('/api/')) console.log('REQ:', req.method(), req.url().split('/api')[1]);
  });
  page.on('response', res => {
    if (res.url().includes('/api/')) console.log('RES:', res.status(), res.url().split('/api')[1]);
  });
  
  await page.goto('http://localhost:8081/auth/login');
  await page.waitForLoadState('networkidle');
  await page.locator('input[formControlName="username"]').fill('admin');
  await page.locator('input[formControlName="password"]').fill('Admin@123');
  await page.locator('button[type="submit"]').click();
  await page.waitForURL(/\/dashboard/, { timeout: 15000 });
  
  console.log('URL after login:', page.url());
  
  // Click logout button
  const logoutBtn = page.locator('.sidebar-footer button[aria-label="Đăng xuất"]').first();
  const isVisible = await logoutBtn.isVisible();
  console.log('Logout button visible:', isVisible);
  
  if (isVisible) {
    await logoutBtn.click();
    console.log('Clicked logout, waiting for redirect...');
    await page.waitForTimeout(3000);
    console.log('URL after logout:', page.url());
    const token = await page.evaluate(() => sessionStorage.getItem('hishope_access_token'));
    console.log('Token after logout:', token ? 'EXISTS (not cleared)' : 'null (cleared)');
  }
  
  await browser.close();
})();
