import { chromium } from '@playwright/test';

(async () => {
  const browser = await chromium.launch({ headless: true, args: ['--no-sandbox'] });
  const context = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await context.newPage();

  // Listen to console messages from browser
  page.on('console', msg => console.log(`[BROWSER ${msg.type()}] ${msg.text()}`));
  page.on('pageerror', err => console.log(`[PAGE ERROR] ${err.message}`));

  try {
    await page.goto('http://127.0.0.1:4500/auth/login', { waitUntil: 'networkidle', timeout: 15000 });
    console.log('URL:', page.url());
    
    // Check Angular zone - use dispatchEvent for better Angular compatibility
    console.log('Filling form with dispatchEvent approach...');
    
    await page.evaluate(() => {
      const usernameInput = document.querySelector('input[formControlName="username"]') as HTMLInputElement;
      const passwordInput = document.querySelector('input[formControlName="password"]') as HTMLInputElement;
      if (usernameInput) {
        usernameInput.value = 'admin';
        usernameInput.dispatchEvent(new Event('input', { bubbles: true }));
        usernameInput.dispatchEvent(new Event('change', { bubbles: true }));
      }
      if (passwordInput) {
        passwordInput.value = 'Admin@123';
        passwordInput.dispatchEvent(new Event('input', { bubbles: true }));
        passwordInput.dispatchEvent(new Event('change', { bubbles: true }));
      }
    });
    
    await page.waitForTimeout(500);
    
    // Now click submit
    const submitBtn = page.locator('button[type="submit"]');
    console.log('Submit enabled:', await submitBtn.isEnabled());
    console.log('Submit text:', await submitBtn.textContent());
    
    await submitBtn.click();
    
    // Wait and check
    await page.waitForTimeout(5000);
    console.log('After click URL:', page.url());
    
    const bodyText = await page.locator('body').innerText();
    console.log('Body length:', bodyText.length);
    console.log('Body:', bodyText.substring(0, 500));
    
    // Check network requests
    console.log('\nNetwork requests:');
    const requests = page.locator('*');
    
  } catch (err) {
    console.error('ERROR:', err.message);
  } finally {
    await page.screenshot({ path: 'debug-login2.png' }).catch(() => {});
    await browser.close();
  }
})();
