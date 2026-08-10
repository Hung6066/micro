const { chromium } = require('playwright');

(async () => {
    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
    const page = await context.newPage();

    // Navigate to login
    await page.goto('http://localhost:8081/auth/login');
    await page.waitForTimeout(1000);
    
    // Fill login form
    const inputs = page.locator('input');
    const allInputs = await inputs.all();
    if (allInputs.length >= 2) {
        await allInputs[0].fill('admin@hishop.vn');
        await allInputs[1].fill('Password123!');
    }
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(3000);
    
    console.log('URL after login:', page.url());
    
    // Check sessionStorage
    const token = await page.evaluate(() => sessionStorage.getItem('hishope_access_token'));
    console.log('Token exists:', !!token);
    if (token) console.log('Token length:', token.length);
    
    // Now navigate to dashboard
    await page.goto('http://localhost:8081/dashboard');
    await page.waitForTimeout(3000);
    
    console.log('URL after goto dashboard:', page.url());
    
    // Try a different approach - navigate without goto
    await page.evaluate(() => window.location.href = '/patients');
    await page.waitForTimeout(3000);
    console.log('URL after location.href patients:', page.url());
    
    await browser.close();
})().catch(e => { console.error('ERROR:', e.message); process.exit(1); });
