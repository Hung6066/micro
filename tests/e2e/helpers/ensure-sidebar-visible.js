const { expect } = require('@playwright/test');

async function ensureSidebarVisible(page) {
  const firstLink = page.locator('nav[hhShellNavigation] a:visible, mat-nav-list a:visible').first();
  const isInViewport = async () => {
    if (await firstLink.count() === 0) {
      return false;
    }
    const box = await firstLink.boundingBox();
    return box !== null && box.x >= 0 && box.width > 0 && box.height > 0;
  };

  if (await isInViewport()) {
    return;
  }

  const menuButton = page.getByRole('button', { name: /open navigation menu|mở menu điều hướng|toggle navigation|admin navigation/i }).first();
  if (await menuButton.isVisible().catch(() => false)) {
    await menuButton.click();
  }

  const openedLink = page.locator('nav[hhShellNavigation] a:visible, mat-nav-list a:visible').first();
  await expect(openedLink).toBeVisible({ timeout: 10000 });
  await expect.poll(async () => {
    if (await openedLink.count() === 0) {
      return false;
    }
    const box = await openedLink.boundingBox();
    return box !== null && box.x >= 0 && box.width > 0 && box.height > 0;
  }, { timeout: 10000, message: 'Navigation drawer link should settle in the viewport before interaction' }).toBe(true);
}

module.exports = { ensureSidebarVisible };
