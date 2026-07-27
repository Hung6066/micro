const { test, expect } = require('@playwright/test');
const { AxeBuilder } = require('@axe-core/playwright');

const targets = [
  { name: 'clinical', url: 'http://localhost:8081/' },
  { name: 'dashboard', url: 'http://localhost:8082/' },
  { name: 'admin', url: 'http://localhost:8083/' },
];

async function requireAuthenticatedPage(page, targetName) {
  if (process.env.E2E_AUTH_REQUIRED !== 'true') {
    throw new Error('Authenticated E2E checks require E2E_AUTH_REQUIRED=true.');
  }
  if (/\/auth\/login(?:\?|$)/.test(page.url())) {
    throw new Error(`${targetName} E2E prerequisite failed: browser landed on the login page.`);
  }
}

for (const target of targets) {
  test(`${target.name} shared shell has no critical axe violations @shared-foundation`, async ({ page }) => {
    await page.goto(target.url, { waitUntil: 'networkidle' });
    await requireAuthenticatedPage(page, target.name);
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations.filter(violation => ['critical', 'serious'].includes(violation.impact))).toEqual([]);
  });

  test(`${target.name} shared shell visual contract @shared-foundation`, async ({ page }, testInfo) => {
    await page.goto(target.url, { waitUntil: 'networkidle' });
    await requireAuthenticatedPage(page, target.name);
    await expect(page).toHaveScreenshot(`${target.name}-${testInfo.project.name}.png`, { fullPage: true, animations: 'disabled' });
  });

  test(`${target.name} shared shell fits responsive viewport @shared-foundation`, async ({ page }) => {
    await page.goto(target.url, { waitUntil: 'networkidle' });
    await requireAuthenticatedPage(page, target.name);
    const overflow = await page.evaluate(() => ({
      documentWidth: document.documentElement.scrollWidth,
      viewportWidth: window.innerWidth,
      bodyWidth: document.body.scrollWidth,
    }));
    expect(overflow.documentWidth, `${target.name} document overflows viewport`).toBeLessThanOrEqual(overflow.viewportWidth + 1);
    expect(overflow.bodyWidth, `${target.name} body overflows viewport`).toBeLessThanOrEqual(overflow.viewportWidth + 1);
  });

  test(`${target.name} shared controls expose keyboard names @shared-foundation`, async ({ page }) => {
    await page.goto(target.url, { waitUntil: 'networkidle' });
    await requireAuthenticatedPage(page, target.name);
    const unnamed = await page.locator('button, [role="button"], a').evaluateAll(elements => elements
      .filter(element => {
        const style = getComputedStyle(element);
        const rect = element.getBoundingClientRect();
        return style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
      })
      .filter(element => !((element.getAttribute('aria-label') || element.textContent || '').trim()))
      .map(element => element.outerHTML.slice(0, 180)));
    expect(unnamed, `${target.name} has unnamed interactive controls`).toEqual([]);
  });
}

test('dashboard command palette supports Escape and focus entry @shared-foundation', async ({ page }) => {
  await page.goto('http://localhost:8082/', { waitUntil: 'networkidle' });
  await requireAuthenticatedPage(page, 'dashboard');
  const trigger = page.getByRole('button', { name: /command palette/i });
  await expect(trigger).toHaveCount(1);
  await trigger.click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('searchbox')).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
  await expect(trigger).toBeFocused();
});

test('admin theme toggle updates the full document surface @shared-foundation', async ({ page }) => {
  await page.goto('http://localhost:8083/', { waitUntil: 'networkidle' });
  await requireAuthenticatedPage(page, 'admin');
  const toggle = page.getByRole('button', { name: 'Toggle theme' });
  await expect(toggle).toHaveCount(1);
  await toggle.click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect.poll(() => page.locator('body').evaluate(element => getComputedStyle(element).backgroundColor)).toBe('rgb(17, 24, 21)');
  await expect.poll(() => page.locator('.app-sidenav').evaluate(element => getComputedStyle(element).backgroundColor)).toBe('rgb(24, 33, 28)');
});
