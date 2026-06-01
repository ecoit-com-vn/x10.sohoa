# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: apps\admin-portal-e2e\src\test-sidebar-click.spec.ts >> Kiểm tra lỗi click sidebar menu
- Location: apps\admin-portal-e2e\src\test-sidebar-click.spec.ts:5:5

# Error details

```
Error: page.goto: Protocol error (Page.navigate): Cannot navigate to invalid URL
Call log:
  - navigating to "/login?ticket=mock-sso-ticket-123456", waiting until "load"

```

# Test source

```ts
  1  | import { test, expect } from '@playwright/test';
  2  | import * as fs from 'fs';
  3  | import * as path from 'path';
  4  | 
  5  | test('Kiểm tra lỗi click sidebar menu', async ({ page }) => {
  6  |   const consoleLogs: string[] = [];
  7  |   const errors: string[] = [];
  8  | 
  9  |   page.on('console', msg => {
  10 |     consoleLogs.push(`[${msg.type()}] ${msg.text()}`);
  11 |   });
  12 | 
  13 |   page.on('pageerror', err => {
  14 |     errors.push(err.stack || err.message);
  15 |   });
  16 | 
  17 |   // 1. Đăng nhập
> 18 |   await page.goto('/login?ticket=mock-sso-ticket-123456');
     |              ^ Error: page.goto: Protocol error (Page.navigate): Cannot navigate to invalid URL
  19 |   await expect(page).toHaveURL(/.*dashboard/);
  20 |   await page.waitForTimeout(1000);
  21 | 
  22 |   // 2. Click vào nhóm "Quản trị hệ thống"
  23 |   const quanTriNhom = page.locator('.menu-row:has-text("Quản trị hệ thống")');
  24 |   await expect(quanTriNhom).toBeVisible();
  25 |   await quanTriNhom.click();
  26 |   await page.waitForTimeout(500);
  27 | 
  28 |   // 3. Click vào "Cấu hình Menu"
  29 |   const cauHinhMenu = page.locator('.submenu-item:has-text("Cấu hình Menu")');
  30 |   await expect(cauHinhMenu).toBeVisible();
  31 |   await cauHinhMenu.click();
  32 |   
  33 |   // Chờ 3 giây để dữ liệu load (hoặc bị nghẽn)
  34 |   await page.waitForTimeout(3000);
  35 | 
  36 |   // Ghi log ra file
  37 |   const artifactDir = 'C:\\Users\\tanha\\.gemini\\antigravity\\brain\\69c71269-35c0-4a7a-aa6b-9ff44b35312a';
  38 |   const logContent = `=== CONSOLE LOGS ===\n${consoleLogs.join('\n')}\n\n=== JAVASCRIPT ERRORS ===\n${errors.join('\n')}`;
  39 |   fs.writeFileSync(path.join(artifactDir, 'sidebar_click_log.txt'), logContent);
  40 | 
  41 |   console.log('Đã ghi log thành công vào sidebar_click_log.txt');
  42 | });
  43 | 
```