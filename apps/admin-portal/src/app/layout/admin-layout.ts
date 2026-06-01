import { Component, inject, OnInit, ChangeDetectorRef, NgZone } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { NotificationBellComponent } from './notification-bell.component';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, NotificationBellComponent],
  template: `
    <div class="layout-wrapper">
      <div class="layout-header">
        <div class="header-container">
          <div class="header-left">
            <button class="hamburger-btn" (click)="toggleSidebar()" title="Thu gọn / Mở rộng Menu">
              <i class="pi pi-bars"></i>
            </button>
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
              <span class="username-text">{{ username }}</span>
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
        <div class="layout-sidebar" [class.collapsed]="isSidebarCollapsed" [class.mobile-open]="isMobileSidebarOpen">
          <div class="sidebar-menu-list">
            <div *ngFor="let item of items" class="menu-group-item" [class.group-active]="isGroupActive(item)">
              <!-- Dòng nhóm menu -->
              <div class="menu-row" (click)="toggleGroup(item)" [title]="isSidebarCollapsed ? item.label : ''">
                <span class="menu-left">
                  <i [class]="'menu-icon ' + item.icon"></i>
                  <span class="menu-label">{{ item.label }}</span>
                </span>
                <i class="pi pi-chevron-right menu-chevron" *ngIf="!item.items && !isSidebarCollapsed"></i>
                <i class="pi menu-chevron" [class.pi-chevron-down]="item.expanded" [class.pi-chevron-right]="!item.expanded" *ngIf="item.items && !isSidebarCollapsed"></i>
              </div>

              <!-- Menu con (Submenu) -->
              <div class="submenu-list" *ngIf="item.items && item.expanded && !isSidebarCollapsed">
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
      <!-- Backdrop cho mobile khi sidebar mở -->
      <div class="sidebar-backdrop" *ngIf="isMobileSidebarOpen" (click)="closeMobileSidebar()"></div>
    </div>
  `,
  styles: `
    :host {
      --sidebar-bg: #ffffff;
      --sidebar-color: #374151;
      --sidebar-border: #e5e7eb;
      --menu-row-hover: #f8fafc;
      --menu-label-color: #374151;
      --submenu-bg: #fafbfc;
      --layout-bg: #f5f7fb;
    }
    :global(html.dark-mode) {
      --sidebar-bg: #1e293b;
      --sidebar-color: #f1f5f9;
      --sidebar-border: #334155;
      --menu-row-hover: #334155;
      --menu-label-color: #e2e8f0;
      --submenu-bg: #111827;
      --layout-bg: #0f172a;
    }
    
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
    .hamburger-btn {
      background: transparent;
      border: none;
      color: #ffffff;
      font-size: 1.2rem;
      cursor: pointer;
      padding: 0.4rem;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: background-color 0.2s;
    }
    .hamburger-btn:hover {
      background-color: rgba(255, 255, 255, 0.1);
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
      border-right: 1px solid var(--sidebar-border);
      background-color: var(--sidebar-bg);
      display: flex;
      flex-direction: column;
      box-shadow: 2px 0 8px rgba(0,0,0,0.02);
      user-select: none;
      transition: flex-basis 0.25s ease-in-out;
    }
    
    .layout-sidebar.collapsed {
      flex: 0 0 64px;
    }
    
    .layout-sidebar.collapsed .menu-label,
    .layout-sidebar.collapsed .menu-chevron,
    .layout-sidebar.collapsed .submenu-list {
      display: none !important;
    }
    
    .layout-sidebar.collapsed .menu-row {
      justify-content: center;
      padding: 12px 0;
    }
    
    .layout-sidebar.collapsed .menu-left {
      justify-content: center;
      gap: 0;
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
      border-bottom: 1px solid var(--sidebar-border);
    }
    
    .menu-row:hover {
      background-color: var(--menu-row-hover);
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
      color: var(--menu-label-color);
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
    :global(html.dark-mode) .group-active .menu-row {
      background-color: rgba(237, 28, 36, 0.15);
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
      background-color: var(--submenu-bg);
      border-left: 1px dashed var(--sidebar-border);
      margin-left: 28px;
      margin-top: 2px;
      margin-bottom: 4px;
    }
    
    .submenu-item {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 8px 16px;
      color: var(--sidebar-color);
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
    
    :global(html.dark-mode) .submenu-item:hover {
      background-color: #1e293b;
      color: #38bdf8;
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
    :global(html.dark-mode) .submenu-item.sub-active {
      background-color: rgba(237, 28, 36, 0.2);
    }
    
    .submenu-item.sub-active .sub-icon {
      color: #ED1C24 !important;
    }
    
    .layout-content {
      flex: 1 1 auto;
      overflow-y: auto;
      padding: 0;
      background-color: var(--layout-bg);
    }
    
    .sidebar-backdrop {
      display: none;
    }
    
    @media (max-width: 768px) {
      .header-title {
        font-size: 0.75rem;
        display: -webkit-box;
        -webkit-line-clamp: 2;
        -webkit-box-orient: vertical;
        overflow: hidden;
      }
      .header-logo {
        height: 22px;
      }
      .header-divider, .user-profile .username-text, .profile-arrow {
        display: none !important;
      }
      .layout-sidebar {
        position: fixed;
        top: 56px;
        left: 0;
        bottom: 0;
        width: 260px;
        z-index: 999;
        transform: translateX(-100%);
        transition: transform 0.25s ease-in-out;
      }
      .layout-sidebar.mobile-open {
        transform: translateX(0);
      }
      .sidebar-backdrop {
        display: block;
        position: fixed;
        top: 56px;
        left: 0;
        width: 100vw;
        height: calc(100vh - 56px);
        background-color: rgba(0, 0, 0, 0.4);
        z-index: 998;
      }
    }
  `,
})
export class AdminLayout implements OnInit {
  private router = inject(Router);
  private http = inject(HttpClient);
  private cdr = inject(ChangeDetectorRef);
  private ngZone = inject(NgZone);
  isDarkMode = true;
  username = 'Người dùng';
  isSidebarCollapsed = false;
  isMobileSidebarOpen = false;

  ngOnInit() {
    // Tự động đóng mobile sidebar khi chuyển trang
    this.router.events.subscribe(() => {
      this.isMobileSidebarOpen = false;
    });

    if (typeof window !== 'undefined') {
      // Decode username từ JWT Token
      const token = localStorage.getItem('token');
      if (token) {
        try {
          const payloadBase64 = token.split('.')[1];
          const payloadJson = atob(payloadBase64.replace(/-/g, '+').replace(/_/g, '/'));
          const payload = JSON.parse(payloadJson);
          this.username = payload.name || payload.unique_name || payload.username || payload.sub || 'Người dùng';
        } catch (e) {
          this.username = 'Người dùng';
        }
      }

      // Check saved theme
      const savedTheme = localStorage.getItem('theme');
      if (savedTheme === 'light') {
        this.isDarkMode = false;
        document.documentElement.classList.remove('dark-mode');
      } else {
        this.isDarkMode = true;
        document.documentElement.classList.add('dark-mode');
      }

      // Chỉ tải sidebar menu động ở môi trường client (tránh 401 trên server SSR)
      this.ngZone.run(() => {
        this.loadSidebarMenu();
      });
    }
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

  toggleSidebar() {
    if (typeof window !== 'undefined' && window.innerWidth <= 768) {
      this.isMobileSidebarOpen = !this.isMobileSidebarOpen;
    } else {
      this.isSidebarCollapsed = !this.isSidebarCollapsed;
    }
  }

  closeMobileSidebar() {
    this.isMobileSidebarOpen = false;
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

  loadSidebarMenu() {
    this.http.get<any>(`${environment.apiGatewayUrl}/api/v1/menus/sidebar`).subscribe({
      next: (res) => {
        this.ngZone.run(() => {
          // Hỗ trợ cả 2 trường hợp: trả về array trực tiếp hoặc trả về object { value: [...] }
          const menus = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []);
          this.items = this.buildMenuTree(menus);
          this.expandActiveGroupOnLoad();
          this.cdr.detectChanges(); // Ép buộc Angular cập nhật lại View ngay lập tức để tránh lỗi ExpressionChangedAfterItHasBeenCheckedError
        });
      },
      error: (err) => {
        console.error('Không thể load sidebar menu động', err);
      }
    });
  }

  buildMenuTree(flatMenus: any[]): MenuItem[] {
    const menuMap = new Map<number, MenuItem>();
    const rootItems: MenuItem[] = [];

    flatMenus.forEach(m => {
      const item: MenuItem = {
        id: m.id.toString(),
        label: m.name,
        icon: m.icon || undefined,
        routerLink: m.url ? [m.url] : undefined,
        items: undefined,
        expanded: false
      };
      menuMap.set(m.id, item);
    });

    flatMenus.forEach(m => {
      const item = menuMap.get(m.id);
      if (item) {
        if (m.parentId) {
          const parentItem = menuMap.get(m.parentId);
          if (parentItem) {
            if (!parentItem.items) {
              parentItem.items = [];
            }
            parentItem.items.push(item);
          }
        } else {
          rootItems.push(item);
        }
      }
    });

    return rootItems;
  }

  items: MenuItem[] = [];

  logout() {
    localStorage.removeItem('token');
    this.router.navigate(['/login']);
  }
}
