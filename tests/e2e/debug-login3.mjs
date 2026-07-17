import { chromium } from '@playwright/test';

(async () => {
  const browser = await chromium.launch({ headless: true, args: ['--no-sandbox'] });
  const context = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await context.newPage();

  // Track network requests
  const apiCalls = [];
  page.on('request', req => {
    if (req.url().includes('api') || req.url().includes('auth')) {
      apiCalls.push({ url: req.url(), method: req.method(), type: 'request' });
    }
  });
  page.on('response', res => {
    if (res.url().includes('api') || res.url().includes('auth')) {
      apiCalls.push({ url: res.url(), status: res.status(), type: 'response' });
    }
  });
  page.on('pageerror', err => console.log('[PAGE_ERROR]', err.message));

  try {
    await page.goto('http://127.0.0.1:4500/auth/login', { waitUntil: 'networkidle', timeout: 15000 });
    console.log('1. URL:', page.url());

    // Check form controls
    const formState = await page.evaluate(() => {
      const username = document.querySelector('input[formControlName="username"]');
      const password = document.querySelector('input[formControlName="password"]');
      const btn = document.querySelector('button[type="submit"]');
      return {
        hasUsername: !!username,
        hasPassword: !!password,
        hasSubmit: !!btn,
        btnDisabled: btn ? btn.hasAttribute('disabled') : 'N/A',
        btnText: btn ? btn.textContent : 'N/A',
      };
    });
    console.log('2. Form state:', JSON.stringify(formState));

    // Fill the form using page.fill
    console.log('3. Filling form...');
    await page.fill('input[formControlName="username"]', 'admin');
    await page.fill('input[formControlName="password"]', 'Admin@123');
    
    // Verify values
    const valuesAfter = await page.evaluate(() => {
      const u = document.querySelector('input[formControlName="username"]');
      const p = document.querySelector('input[formControlName="password"]');
      return {
        usernameVal: u ? u.value : 'no-field',
        passwordVal: p ? p.value : 'no-field',
      };
    });
    console.log('4. Values:', JSON.stringify(valuesAfter));
    
    // Check button state after fill
    const btnState = await page.evaluate(() => {
      const btn = document.querySelector('button[type="submit"]');
      return {
        disabled: btn ? btn.disabled : 'N/A',
        text: btn ? btn.textContent.trim() : 'N/A',
      };
    });
    console.log('5. Button state:', JSON.stringify(btnState));
    
    // Click submit
    console.log('6. Clicking submit...');
    await page.click('button[type="submit"]');
    
    // Wait
    await page.waitForTimeout(5000);
    console.log('7. URL after submit:', page.url());
    
    const bodyText = await page.locator('body').innerText();
    console.log('8. Body length:', bodyText.length);
    console.log('9. Body preview:', bodyText.substring(0, 500));
    
    console.log('\n10. API calls:');
    apiCalls.forEach(c => console.log('   ', JSON.stringify(c)));
    
    await page.screenshot({ path: 'debug-login3.png' });
    
  } catch (err) {
    console.error('ERROR:', err.message);
    await page.screenshot({ path: 'debug-login-error.png' }).catch(() => {});
  } finally {
    await browser.close();
  }
})();
