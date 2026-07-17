const { chromium } = require('playwright');

(async () => {
    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
    const page = await context.newPage();

    // Log API calls
    page.on('response', resp => {
        const url = resp.url();
        if (url.includes('/api/') || url.includes('localhost:5000')) {
            console.log('API:', resp.status(), resp.request().method(), url.substring(0, 100));
        }
    });

    // Login
    await page.goto('http://localhost:8081/auth/login');
    await page.waitForTimeout(1000);
    const inputs = page.locator('input');
    const allInputs = await inputs.all();
    if (allInputs.length >= 2) {
        await allInputs[0].fill('admin@hishop.vn');
        await allInputs[1].fill('Password123!');
    }
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(3000);
    console.log('--- After login URL:', page.url());
    
    // Check token
    const token = await page.evaluate(() => sessionStorage.getItem('hishope_access_token'));
    console.log('Token prefix:', token ? token.substring(0, 30) + '...' : 'NONE');
    
    // Navigate to patients via location.href (which triggers Angular re-init)
    console.log('\n--- Navigating to /patients via location.href ---');
    await page.evaluate(() => window.location.href = '/patients');
    await page.waitForTimeout(4000);
    console.log('URL after navigation:', page.url());
    
    await browser.close();
})().catch(e => { console.error('ERROR:', e.message); process.exit(1); });
