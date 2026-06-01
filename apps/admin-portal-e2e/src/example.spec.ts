import { test, expect } from '@playwright/test';

test('has title', async ({ page }) => {
  await page.goto('/');

  // Expect h1 to contain the EVNHANOI branding name
  expect(await page.locator('h1').innerText()).toContain('HỆ THỐNG SỐ HÓA');
});
