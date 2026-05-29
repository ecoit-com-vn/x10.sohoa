import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { NotificationBellComponent } from './notification-bell.component';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, NotificationBellComponent],
  template: `
    <div class="layout-wrapper">
      <div class="layout-header">
        <div class="header-container">
          <div class="header-left">
            <img src="/logo-white.svg" alt="EVNHANOI" class="header-logo" />
            <span class="header-divider">|</span>
            <h1 class="header-title">HỆ THỐNG SỐ HÓA HỒ SƠ KỸ THUẬT ĐƯỜNG DÂY VÀ TRẠM EVNHANOI</h1>
          </div>
          <div class="header-right">
            <button class="theme-toggle-btn" (click)="toggleTheme()" [title]="isDarkMode ? 'Bật chế độ sáng' : 'Bật chế độ tối'">
              <i [class]="isDarkMode ? 'pi pi-sun' : 'pi pi-moon'"></i>
            </button>
            <div class="header-bell-wrapper">
              <app-notification-bell></app-notification-bell>
            </div>
            <div class="user-profile">
              <div class="avatar-circle">
                <i class="pi pi-user"></i>
              </div>
              <span class="username-text">admin</span>
              <i class="pi pi-chevron-down profile-arrow"></i>
            </div>
            <button class="logout-btn" (click)="logout()" title="Đăng xuất">
              <i class="pi pi-sign-out"></i>
            </button>
          </div>
        </div>
      </div>
      <div class="layout-main">
        <!-- Sidebar Menu CSKH EVNHANOI style -->
        <div class="layout-sidebar">


          <div class="sidebar-menu-list">
            <div *ngFor="let item of items" class="menu-group-item" [class.group-active]="isGroupActive(item)">
              <!-- Dòng nhóm menu -->
              <div class="menu-row" (click)="toggleGroup(item)">
                <span class="menu-left">
                  <i [class]="'menu-icon ' + item.icon"></i>
                  <span class="menu-label">{{ item.label }}</span>
                </span>
                <i class="pi pi-chevron-right menu-chevron" *ngIf="!item.items"></i>
                <i class="pi menu-chevron" [class.pi-chevron-down]="item.expanded" [class.pi-chevron-right]="!item.expanded" *ngIf="item.items"></i>
              </div>

              <!-- Menu con (Submenu) -->
              <div class="submenu-list" *ngIf="item.items && item.expanded">
                <a *ngFor="let sub of item.items"
                   [routerLink]="sub.routerLink"
                   routerLinkActive="sub-active"
                   class="submenu-item">
                  <i [class]="'sub-icon ' + sub.icon"></i>
                  <span class="sub-label">{{ sub.label }}</span>
                </a>
              </div>
            </div>
          </div>
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
    .header-container {
      display: flex;
      justify-content: space-between;
      align-items: center;
      height: 56px;
      background-color: #002D72; /* EVN Deep Blue */
      color: #ffffff;
      padding: 0 1.25rem;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
      border-bottom: 2px solid #FF6B00; /* Orange border */
    }
    .header-left {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .header-logo {
      height: 28px;
      object-fit: contain;
    }
    .header-divider {
      color: rgba(255, 255, 255, 0.3);
      font-size: 1.2rem;
      font-weight: 300;
      margin: 0 0.5rem;
      display: inline-block;
    }
    .header-title {
      font-size: 0.95rem;
      font-weight: 600;
      color: #ffffff;
      margin: 0;
      letter-spacing: 0.5px;
      font-family: 'Inter', sans-serif;
    }
    .header-right {
      display: flex;
      align-items: center;
      gap: 1rem;
    }
    .theme-toggle-btn {
      background: transparent;
      border: none;
      color: rgba(255, 255, 255, 0.85);
      font-size: 1.15rem;
      cursor: pointer;
      padding: 0.4rem;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: background-color 0.2s, color 0.2s;
    }
    .theme-toggle-btn:hover {
      background-color: rgba(255, 255, 255, 0.1);
      color: #ffffff;
    }
    .header-bell-wrapper {
      color: #ffffff !important;
    }
    .user-profile {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.25rem 0.5rem;
      border-radius: 20px;
      cursor: pointer;
      transition: background-color 0.2s;
    }
    .user-profile:hover {
      background-color: rgba(255, 255, 255, 0.08);
    }
    .avatar-circle {
      width: 28px;
      height: 28px;
      border-radius: 50%;
      background-color: rgba(255, 255, 255, 0.2);
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 0.85rem;
      color: #ffffff;
    }
    .username-text {
      font-size: 0.875rem;
      font-weight: 500;
    }
    .profile-arrow {
      font-size: 0.75rem;
      opacity: 0.8;
    }
    .logout-btn {
      background: transparent;
      border: none;
      color: rgba(255, 255, 255, 0.85);
      font-size: 1.15rem;
      cursor: pointer;
      padding: 0.4rem;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: background-color 0.2s, color 0.2s;
    }
    .logout-btn:hover {
      background-color: rgba(255, 255, 255, 0.1);
      color: #ff4d4d;
    }
    .layout-main {
      display: flex;
      flex: 1 1 auto;
      overflow: hidden;
    }
    
    /* Sidebar CSKH EVNHANOI styling */
    .layout-sidebar {
      flex: 0 0 280px;
      overflow-y: auto;
      border-right: 1px solid var(--p-content-border-color);
      background-color: #ffffff;
      display: flex;
      flex-direction: column;
      box-shadow: 2px 0 8px rgba(0,0,0,0.02);
      user-select: none;
    }
    
    .sidebar-header {
      height: 56px;
      background-color: #002D72; /* EVN Deep Blue */
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0 16px;
      flex-shrink: 0;
    }
    
    .sidebar-logo {
      height: 28px;
      object-fit: contain;
    }
    
    .sidebar-kebab-btn {
      background: transparent;
      border: none;
      color: #ffffff;
      font-size: 1.1rem;
      cursor: pointer;
      padding: 4px;
      display: flex;
      align-items: center;
      justify-content: center;
      opacity: 0.85;
      transition: opacity 0.2s;
    }
    
    .sidebar-kebab-btn:hover {
      opacity: 1;
    }
    
    .sidebar-menu-list {
      padding: 8px 0;
      display: flex;
      flex-direction: column;
    }
    
    .menu-group-item {
      display: flex;
      flex-direction: column;
    }
    
    .menu-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 12px 18px;
      cursor: pointer;
      transition: all 0.2s;
      border-bottom: 1px solid #f9fafb;
    }
    
    .menu-row:hover {
      background-color: #f8fafc;
    }
    
    .menu-left {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    
    .menu-icon {
      color: #002D72;
      font-size: 1.1rem;
      width: 20px;
      text-align: center;
    }
    
    .menu-label {
      font-size: 0.88rem;
      font-weight: 500;
      color: #374151;
      font-family: 'Inter', sans-serif;
    }
    
    .menu-chevron {
      color: #002D72;
      font-size: 0.75rem;
    }
    
    /* Active State - CSKH Brand Red color */
    .group-active .menu-row {
      background-color: #fef2f2;
    }
    .group-active .menu-icon {
      color: #ED1C24 !important;
    }
    .group-active .menu-label {
      color: #ED1C24 !important;
      font-weight: 600;
    }
    .group-active .menu-chevron {
      color: #ED1C24 !important;
    }
    
    /* Submenu styling */
    .submenu-list {
      display: flex;
      flex-direction: column;
      padding: 6px 0 10px 32px;
      background-color: #fafbfc;
      border-left: 1px dashed #d1d5db;
      margin-left: 28px;
      margin-top: 2px;
      margin-bottom: 4px;
    }
    
    .submenu-item {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 8px 16px;
      color: #4b5563;
      font-size: 0.83rem;
      font-weight: 500;
      text-decoration: none;
      border-radius: 4px;
      transition: all 0.15s;
    }
    
    .submenu-item:hover {
      background-color: #eff6ff;
      color: #002D72;
    }
    
    .submenu-item .sub-icon {
      font-size: 0.78rem;
      color: #9ca3af;
      width: 14px;
      text-align: center;
    }
    
    .submenu-item:hover .sub-icon {
      color: #002D72;
    }
    
    /* Submenu Active */
    .submenu-item.sub-active {
      color: #ED1C24 !important;
      font-weight: 600;
      background-color: #fee2e2;
    }
    
    .submenu-item.sub-active .sub-icon {
      color: #ED1C24 !important;
    }
    
    .layout-content {
      flex: 1 1 auto;
      overflow-y: auto;
      padding: 0;
      background-color: #f5f7fb;
    }
  `,
})
export class AdminLayout implements OnInit {
  private router = inject(Router);
  isDarkMode = true;

  ngOnInit() {
    if (typeof window !== 'undefined') {
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
    // Expand active group on load automatically
    this.expandActiveGroupOnLoad();
  }

  toggleTheme() {
    this.isDarkMode = !this.isDarkMode;
    if (typeof window !== 'undefined') {
      if (this.isDarkMode) {
        document.documentElement.classList.add('dark-mode');
        localStorage.setItem('theme', 'dark');
      } else {
        document.documentElement.classList.remove('dark-mode');
        localStorage.setItem('theme', 'light');
      }
    }
  }

  toggleGroup(item: MenuItem) {
    if (item.items) {
      item.expanded = !item.expanded;
    } else if (item.routerLink) {
      this.router.navigate(item.routerLink);
    }
  }

  isGroupActive(group: MenuItem): boolean {
    const currentUrl = this.router.url || '';
    if (group.routerLink) {
      const link = group.routerLink.join('/');
      return currentUrl === link || currentUrl.startsWith(link + '/');
    }
    if (group.items) {
      return group.items.some(sub => {
        if (sub.routerLink) {
          const subLink = sub.routerLink.join('/');
          return currentUrl === subLink || currentUrl.startsWith(subLink + '/');
        }
        return false;
      });
    }
    return false;
  }

  expandActiveGroupOnLoad() {
    setTimeout(() => {
      const currentUrl = this.router.url || '';
      this.items.forEach(group => {
        if (group.items) {
          const isActive = group.items.some(sub => {
            if (sub.routerLink) {
              const subLink = sub.routerLink.join('/');
              return currentUrl === subLink || currentUrl.startsWith(subLink + '/');
            }
            return false;
          });
          if (isActive) {
            group.expanded = true;
          }
        }
      });
    }, 100);
  }

  items: MenuItem[] = [
    {
      label: 'Trang chủ',
      icon: 'pi pi-home',
      routerLink: ['/dashboard']
    },
    {
      label: 'Quản lý thiết bị',
      icon: 'pi pi-box',
      items: [
        {
          label: 'Tìm kiếm thiết bị',
          icon: 'pi pi-search',
          routerLink: ['/search']
        },
        {
          label: 'Quản lý biểu mẫu',
          icon: 'pi pi-file',
          routerLink: ['/equipment/form-management']
        },
        {
          label: 'Tạo biểu mẫu động',
          icon: 'pi pi-pencil',
          routerLink: ['/equipment/form-builder']
        },
        {
          label: 'Hiển thị biểu mẫu',
          icon: 'pi pi-list',
          routerLink: ['/equipment/form-renderer']
        }
      ]
    },
    {
      label: 'Số hóa dữ liệu',
      icon: 'pi pi-cloud-upload',
      items: [
        {
          label: 'Tải lên tài liệu OCR',
          icon: 'pi pi-file-import',
          routerLink: ['/digitization/ocr-upload']
        },
        {
          label: 'Phân bổ nhập liệu OCR',
          icon: 'pi pi-sitemap',
          routerLink: ['/digitization/ocr-allocation']
        },
        {
          label: 'Dữ liệu Huấn luyện OCR',
          icon: 'pi pi-brain',
          routerLink: ['/digitization/ocr-training']
        },
        {
          label: 'Quản lý mượn/trả hồ sơ',
          icon: 'pi pi-folder-open',
          routerLink: ['/workflow/borrow-return']
        },
        {
          label: 'Quy trình duyệt (Builder)',
          icon: 'pi pi-sliders-h',
          routerLink: ['/workflow/builder']
        }
      ]
    },
    {
      label: 'Lưu trữ Vật lý & OCR',
      icon: 'pi pi-server',
      items: [
        {
          label: 'Quản lý Lưu trữ',
          icon: 'pi pi-table',
          routerLink: ['/physical-storage']
        },
        {
          label: 'Hiệu đính OCR',
          icon: 'pi pi-file-edit',
          routerLink: ['/ocr-correction']
        }
      ]
    },
    {
      label: 'Báo cáo thống kê',
      icon: 'pi pi-chart-bar',
      items: [
        {
          label: 'Xuất báo cáo',
          icon: 'pi pi-file-excel',
          routerLink: ['/reports']
        }
      ]
    },
    {
      label: 'Quản trị hệ thống',
      icon: 'pi pi-cog',
      items: [
        {
          label: 'Quản lý người dùng',
          icon: 'pi pi-users',
          routerLink: ['/administration/user-management']
        },
        {
          label: 'Nhật ký thao tác',
          icon: 'pi pi-history',
          routerLink: ['/administration/audit-log']
        },
        {
          label: 'Cấu hình đồng bộ PMIS',
          icon: 'pi pi-sync',
          routerLink: ['/administration/sync-config']
        }
      ]
    }
  ];

  logout() {
    localStorage.removeItem('token');
    this.router.navigate(['/login']);
  }
}
