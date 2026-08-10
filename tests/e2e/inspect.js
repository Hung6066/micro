const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch();
  const context = await browser.newContext({ baseURL: 'http://localhost:8081' });
  const page = await context.newPage();

  // Login
  await page.goto('/en/auth/login');
  await page.locator('input[formControlName="username"]').fill('admin');
  await page.locator('input[formControlName="password"]').fill('Admin@123');
  await page.locator('button[type="submit"]').click();
  await page.waitForURL('**/en/dashboard', { timeout: 15000 });

  // Check patient list
  await page.goto('/en/patients');
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(2000);
  const snap = await page.locator('body').innerHTML();
  console.log('Has mat-table:', snap.includes('mat-table'));
  console.log('Has table:', snap.includes('<table'));
  console.log('Has mat-row:', snap.includes('mat-row'));
  console.log('Has tr:', snap.includes('<tr'));
  const searchCount = await page.locator('input[placeholder*="tìm" i], input[formControlName="search"]').count();
  console.log('Search inputs:', searchCount);
  console.log('Title:', await page.title());
  console.log('URL:', page.url());
  const h1Text = await page.locator('h1').textContent().catch(() => 'N/A');
  console.log('H1:', h1Text);

  // Check patient form
  await page.goto('/en/patients/new');
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(1000);
  const formControls = await page.evaluate(() => {
    const inputs = document.querySelectorAll('[formControlName]');
    return Array.from(inputs).map(el => el.getAttribute('formControlName'));
  });
  console.log('Form controls:', formControls);

  // Check patient detail page - click first patient
  await page.goto('/en/patients');
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(1500);
  const links = await page.evaluate(() => {
    const allLinks = document.querySelectorAll('a');
    return Array.from(allLinks).slice(0, 10).map(a => ({ href: a.getAttribute('href'), text: a.textContent.trim() }));
  });
  console.log('First 10 links:', JSON.stringify(links, null, 2));

  // Check buttons with aria-labels
  const btns = await page.evaluate(() => {
    const allBtns = document.querySelectorAll('button');
    return Array.from(allBtns).slice(0, 20).map(b => ({
      text: b.textContent.trim(),
      ariaLabel: b.getAttribute('aria-label'),
      class: b.className.slice(0, 60)
    }));
  });
  console.log('Buttons:', JSON.stringify(btns, null, 2));

  await browser.close();
})();
