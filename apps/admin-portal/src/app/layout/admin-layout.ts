import { Component, inject, OnInit } from '@angular/core';
import { RouterModule, Router } from '@angular/router';
import { Toolbar } from 'primeng/toolbar';
import { Button } from 'primeng/button';
import { PanelMenu } from 'primeng/panelmenu';
import { MenuItem } from 'primeng/api';
import { NotificationBellComponent } from './notification-bell.component';

@Component({
  selector: 'app-admin-layout',
  imports: [RouterModule, Toolbar, Button, PanelMenu, NotificationBellComponent],
  template: `
    <div class="layout-wrapper">
      <div class="layout-header">
        <p-toolbar styleClass="border-none border-bottom-1 border-surface-200 shadow-1 px-4 py-3" [style]="{'background-color': 'var(--p-surface-0)'}">
          <ng-template #start>
            <div class="flex align-items-center gap-2">
              <i class="pi pi-bolt text-primary text-2xl"></i>
              <h3 class="m-0 text-xl font-semibold text-color">Hệ thống Số hóa EVNHANOI</h3>
            </div>
          </ng-template>
          <ng-template #end>
            <p-button [icon]="isDarkMode ? 'pi pi-sun' : 'pi pi-moon'" (onClick)="toggleTheme()" [rounded]="true" [text]="true" severity="secondary" styleClass="mr-2"></p-button>
            <app-notification-bell></app-notification-bell>
            <p-button icon="pi pi-sign-out" label="Đăng xuất" [rounded]="true" [text]="true" severity="secondary" styleClass="ml-3 font-semibold" (onClick)="logout()" />
          </ng-template>
        </p-toolbar>
      </div>
      <div class="layout-main">
        <div class="layout-sidebar">
          <p-panelMenu [model]="items" [style]="{'width':'100%'}" />
        </div>
        <div class="layout-content">
          <router-outlet></router-outlet>
        </div>
      </div>
    </div>
  `,
  styles: `
    .layout-wrapper {
      display: flex;
      flex-direction: column;
      height: 100vh;
    }
    .layout-header {
      flex: 0 0 auto;
    }
    .layout-main {
      display: flex;
      flex: 1 1 auto;
      overflow: hidden;
    }
    .layout-sidebar {
      flex: 0 0 280px;
      overflow-y: auto;
      border-right: 1px solid var(--p-content-border-color);
      background-color: var(--p-surface-0);
      padding: 1.5rem 1rem;
      box-shadow: 2px 0 8px rgba(0,0,0,0.02);
    }
    .layout-content {
      flex: 1 1 auto;
      overflow-y: auto;
      padding: 2.5rem;
      background-color: var(--p-surface-50);
    }
    
    ::ng-deep .p-panelmenu .p-panelmenu-header-content {
      border: none !important;
      background: transparent !important;
    }
    ::ng-deep .p-panelmenu .p-panelmenu-header-action {
      padding: 1rem 1.25rem !important;
      border-radius: 8px !important;
      font-weight: 600 !important;
      color: var(--p-surface-700) !important;
    }
    ::ng-deep .p-panelmenu .p-panelmenu-header-action:hover {
      background-color: var(--p-primary-50) !important;
      color: var(--p-primary-600) !important;
    }
  `,
})
export class AdminLayout implements OnInit {
  private router = inject(Router);
  isDarkMode = true;

  ngOnInit() {
    // Check saved theme
    const savedTheme = localStorage.getItem('theme');
    if (savedTheme === 'light') {
      this.isDarkMode = false;
      document.documentElement.classList.remove('dark-mode');
    } else {
      this.isDarkMode = true;
      document.documentElement.classList.add('dark-mode');
    }
  }

  toggleTheme() {
    this.isDarkMode = !this.isDarkMode;
    if (this.isDarkMode) {
      document.documentElement.classList.add('dark-mode');
      localStorage.setItem('theme', 'dark');
    } else {
      document.documentElement.classList.remove('dark-mode');
      localStorage.setItem('theme', 'light');
    }
  }

  items: MenuItem[] = [
    {
      label: 'Quản trị hệ thống',
      icon: 'pi pi-fw pi-cog',
      items: [
        {
          label: 'Quản lý người dùng',
          icon: 'pi pi-fw pi-users',
          routerLink: ['/administration/user-management']
        }
      ]
    },
    {
      label: 'Quản lý thiết bị',
      icon: 'pi pi-fw pi-box',
      items: [
        {
          label: 'Tìm kiếm thiết bị',
          icon: 'pi pi-fw pi-search',
          routerLink: ['/search']
        },
        {
          label: 'Tạo biểu mẫu động',
          icon: 'pi pi-fw pi-pencil',
          routerLink: ['/equipment/form-builder']
        },
        {
          label: 'Hiển thị biểu mẫu',
          icon: 'pi pi-fw pi-list',
          routerLink: ['/equipment/form-renderer']
        }
      ]
    },
    {
      label: 'Số hóa dữ liệu',
      icon: 'pi pi-fw pi-cloud-upload',
      items: [
        {
          label: 'Tải lên tài liệu OCR',
          icon: 'pi pi-fw pi-file-arrow-up',
          routerLink: ['/digitization/ocr-upload']
        },
        {
          label: 'Quản lý mượn/trả hồ sơ',
          icon: 'pi pi-fw pi-folder-open',
          routerLink: ['/workflow/borrow-return']
        }
      ]
    },
    {
      label: 'Báo cáo thống kê',
      icon: 'pi pi-fw pi-chart-bar',
      items: [
        {
          label: 'Xuất báo cáo',
          icon: 'pi pi-fw pi-file-excel',
          routerLink: ['/reports']
        }
      ]
    }
  ];

  logout() {
    localStorage.removeItem('token');
    this.router.navigate(['/login']);
  }
}
