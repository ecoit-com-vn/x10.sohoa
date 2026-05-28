import { Component, inject } from '@angular/core';
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
        <p-toolbar>
          <ng-template #start>
            <h3>Hệ thống Số hóa EVNHANOI</h3>
          </ng-template>
          <ng-template #end>
            <app-notification-bell></app-notification-bell>
            <p-button icon="pi pi-sign-out" label="Đăng xuất" [rounded]="true" [text]="true" severity="secondary" styleClass="ml-2" (onClick)="logout()" />
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
      flex: 0 0 250px;
      overflow-y: auto;
      border-right: 1px solid var(--p-content-border-color, #dee2e6);
      padding: 1rem;
    }
    .layout-content {
      flex: 1 1 auto;
      overflow-y: auto;
      padding: 2rem;
    }
  `,
})
export class AdminLayout {
  private router = inject(Router);

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
        }
      ]
    }
  ];

  logout() {
    localStorage.removeItem('token');
    this.router.navigate(['/login']);
  }
}
