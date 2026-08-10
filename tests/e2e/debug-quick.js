const { chromium } = require('playwright');
(async () => {
    const browser = await chromium.launch({ headless: true });
    const page = await browser.newPage();

    // Navigate to login
    await page.goto('http://localhost:8081/auth/login');
    await page.waitForTimeout(2000);
    console.log('URL:', page.url());

    // Login
    const inputs = await page.locator('input').all();
    if (inputs.length >= 2) {
        await inputs[0].fill('admin@hishop.vn');
        await inputs[1].fill('Password123!');
    }
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(5000);
    console.log('After login URL:', page.url());

    // Navigate to patients
    await page.goto('http://localhost:8081/patients');
    await page.waitForTimeout(5000);
    console.log('After patients goto URL:', page.url());

    await browser.close();
})().catch(e => { console.error('ERROR:', e.message); process.exit(1); });
