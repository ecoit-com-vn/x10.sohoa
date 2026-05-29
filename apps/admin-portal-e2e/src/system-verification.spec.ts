import { test, expect } from '@playwright/test';

test.describe('EVNHANOI System E2E Automated Verification', () => {

  test('Should login successfully via Mock SSO Ticket and navigate through all main functions', async ({ page }) => {
    // 0. Intercept the SSO Auth verification endpoint to return a mock JWT token instantly
    console.log('Setting up API route interception...');
    await page.route('**/api/v1/auth/sso', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          access_token: 'mock-jwt-token-value-for-testing'
        })
      });
    });

    // 1. Visit Login page with mock ticket to trigger authentication
    console.log('Navigating to Login page with Mock SSO Ticket...');
    await page.goto('/login?ticket=mock-sso-ticket-123456');

    // Wait for redirection to dashboard (default is user-management)
    console.log('Verifying redirection to dashboard...');
    await expect(page).toHaveURL(/.*administration\/user-management/);

    // Verify Title Header is present
    const headerTitle = page.locator('h3:has-text("Hệ thống Số hóa EVNHANOI")');
    await expect(headerTitle).toBeVisible();
    console.log('Dashboard loaded successfully.');

    // 2. Navigate to Audit Log
    console.log('Navigating to Audit Log...');
    await page.goto('/administration/audit-log');
    await expect(page).toHaveURL(/.*administration\/audit-log/);
    await expect(page.getByText('Nhật ký thao tác (Audit Log)', { exact: true })).toBeVisible();
    console.log('Audit Log page verified.');

    // 3. Navigate to Sync Config
    console.log('Navigating to Sync Config...');
    await page.goto('/administration/sync-config');
    await expect(page).toHaveURL(/.*administration\/sync-config/);
    await expect(page.getByText('Cấu hình Đồng bộ PMIS', { exact: true })).toBeVisible();
    console.log('Sync Config page verified.');

    // 4. Navigate to OCR Correction page
    console.log('Navigating to OCR Correction...');
    await page.goto('/ocr-correction');
    await expect(page).toHaveURL(/.*ocr-correction/);
    await expect(page.getByText('Hiệu đính AI-OCR', { exact: true })).toBeVisible();

    // Test OCR Noise Reduction Toggle
    const noiseReductionToggle = page.getByText('Bật Giảm bóng mờ & Nhiễu', { exact: true });
    await expect(noiseReductionToggle).toBeVisible();
    
    console.log('Toggling Noise Reduction...');
    await noiseReductionToggle.click();
    
    // Check if the toggle state changes to active/on
    const activeToggle = page.getByText('Đã bật Giảm bóng mờ & Nhiễu', { exact: true });
    await expect(activeToggle).toBeVisible();
    console.log('Noise Reduction verified.');

    // 5. Navigate to Physical Storage page
    console.log('Navigating to Physical Storage...');
    await page.goto('/physical-storage');
    await expect(page).toHaveURL(/.*physical-storage/);
    await expect(page.getByText('Kệ (Shelf)', { exact: true })).toBeVisible();
    console.log('Physical Storage page verified.');

    // 6. Navigate to Reports page
    console.log('Navigating to Reports...');
    await page.goto('/reports');
    await expect(page).toHaveURL(/.*reports/);
    // Target the header title inside the page content card specifically
    await expect(page.locator('p-card').getByText('Báo cáo thống kê', { exact: true })).toBeVisible();
    console.log('Reports page verified.');
    
    console.log('All functions navigated and verified successfully!');
  });
});
