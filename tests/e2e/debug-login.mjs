import { chromium } from '@playwright/test';

(async () => {
  const browser = await chromium.launch({ headless: true, args: ['--no-sandbox'] });
  const context = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await context.newPage();

  try {
    // Step 1: Navigate to login
    console.log('1. Navigate to /auth/login');
    await page.goto('http://127.0.0.1:4500/auth/login', { waitUntil: 'networkidle', timeout: 15000 });
    console.log(`   URL: ${page.url()}`);
    
    // Step 2: Take screenshot
    await page.screenshot({ path: 'debug-01-login-page.png' });
    
    // Step 3: Fill form
    console.log('2. Fill login form');
    const usernameInput = page.locator('input[formControlName="username"]');
    const passwordInput = page.locator('input[formControlName="password"]');
    console.log(`   Username visible: ${await usernameInput.isVisible()}`);
    console.log(`   Password visible: ${await passwordInput.isVisible()}`);
    
    await usernameInput.fill('admin');
    await passwordInput.fill('Admin@123');
    
    // Step 4: Click submit
    console.log('3. Click submit button');
    const submitBtn = page.locator('button[type="submit"]');
    console.log(`   Submit visible: ${await submitBtn.isVisible()}`);
    console.log(`   Submit enabled: ${await submitBtn.isEnabled()}`);
    console.log(`   Submit text: ${await submitBtn.textContent()}`);
    
    await submitBtn.click();
    
    // Step 5: Wait for navigation
    console.log('4. Wait for navigation...');
    await page.waitForTimeout(3000);
    console.log(`   Current URL: ${page.url()}`);
    
    // Step 6: Check for dashboard
    const bodyText = await page.locator('body').innerText();
    console.log(`   Body text length: ${bodyText.length}`);
    console.log(`   Contains "Xin chào": ${bodyText.includes('Xin chào')}`);
    console.log(`   Contains "Dashboard": ${bodyText.includes('Dashboard')}`);
    
    await page.screenshot({ path: 'debug-02-after-login.png' });
    
    console.log('5. SUCCESS - Login works!');
  } catch (err) {
    console.error('ERROR:', err.message);
    await page.screenshot({ path: 'debug-error.png' }).catch(() => {});
  } finally {
    await browser.close();
  }
})();
