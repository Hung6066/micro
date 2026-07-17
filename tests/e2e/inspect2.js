const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch();
  const context = await browser.newContext({ baseURL: 'http://localhost:8081' });
  const page = await context.newPage();

  // Login with logging
  try {
    await page.goto('/en/auth/login');
    console.log('1. Login page loaded:', page.url());
    await page.waitForLoadState('networkidle');
    await page.locator('input[formControlName="username"]').waitFor({ state: 'visible', timeout: 5000 });
    console.log('2. Form visible');
    await page.locator('input[formControlName="username"]').fill('admin');
    await page.locator('input[formControlName="password"]').fill('Admin@123');
    console.log('3. Fields filled');
    await page.locator('button[type="submit"]').click();
    console.log('4. Submit clicked');
    await page.waitForURL('**/dashboard', { timeout: 15000 }).catch(() => 
      console.log('   Wait for dashboard failed, URL:', page.url())
    );
    console.log('5. After login URL:', page.url());
    
    // Check token
    const token = await page.evaluate(() => sessionStorage.getItem('hishope_access_token'));
    console.log('6. Token exists:', !!token);
    
    // Try patients
    await page.goto('/en/patients');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500);
    console.log('7. Patients URL:', page.url());
    
    // What's on the page?
    const bodyHTML = await page.evaluate(() => {
      const main = document.querySelector('main, router-outlet, .app-content, .content');
      if (main) return main.innerHTML.substring(0, 2000);
      return document.body.innerHTML.substring(0, 1000);
    });
    console.log('8. Body content (first 1000 chars):');
    console.log(bodyHTML.substring(0, 800));
    
  } catch(e) {
    console.error('Error:', e.message);
  }

  await browser.close();
})();
