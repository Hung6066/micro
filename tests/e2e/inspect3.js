const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch();
  const context = await browser.newContext({ baseURL: 'http://localhost:8081' });
  const page = await context.newPage();

  // Login via fetch
  const loginResp = await fetch('http://localhost:8081/api/v1/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: 'admin', password: 'Admin@123' }),
  });
  const loginData = await loginResp.json();
  console.log('Login API success:', !!loginData.accessToken);
  
  // Set token in sessionStorage before navigating
  await page.goto('/en/auth/login');
  await page.evaluate((token) => {
    sessionStorage.setItem('hishope_access_token', token);
  }, loginData.accessToken);
  
  // Monitor requests
  const apiCalls = [];
  page.on('request', req => {
    if (req.url().includes('/api/')) {
      apiCalls.push({ url: req.url().split('/api')[1], method: req.method() });
    }
  });
  
  // Go to dashboard
  await page.goto('/en/dashboard');
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(2000);
  console.log('Dashboard URL:', page.url());
  const dashContent = await page.locator('body').textContent();
  console.log('Has login form:', dashContent.includes('Đăng nhập'));
  
  // Check verify call result
  console.log('API calls:');
  apiCalls.forEach(c => console.log(`  ${c.method} /api${c.url}`));
  
  // Check if auth guard passed
  const hasPatientsLink = dashContent.includes('Bệnh nhân');
  console.log('Has sidebar (Bệnh nhân):', hasPatientsLink);
  
  await browser.close();
})();
