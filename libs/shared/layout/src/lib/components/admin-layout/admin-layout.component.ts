import { Component, inject, OnInit, ChangeDetectorRef, NgZone } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { NotificationBellComponent } from '../notification-bell/notification-bell.component';
import { AuthService, MenuService } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, NotificationBellComponent],
  templateUrl: './admin-layout.component.html',
  styleUrl: './admin-layout.component.scss'
})
export class AdminLayout implements OnInit {
  private router = inject(Router);
  private authService = inject(AuthService);
  private menuService = inject(MenuService);
  private cdr = inject(ChangeDetectorRef);
  private ngZone = inject(NgZone);

  isDarkMode = true;
  username = 'Người dùng';
  isSidebarCollapsed = false;
  isMobileSidebarOpen = false;
  items: MenuItem[] = [];

  ngOnInit() {
    // Tự động đóng mobile sidebar khi chuyển trang
    this.router.events.subscribe(() => {
      this.isMobileSidebarOpen = false;
    });

    if (typeof window !== 'undefined') {
      // Decode username từ JWT Token
      const token = this.authService.getToken();
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
    this.menuService.getSidebarMenu().subscribe({
      next: (res) => {
        this.ngZone.run(() => {
          const menus = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []);
          this.items = this.buildMenuTree(menus);
          this.expandActiveGroupOnLoad();
          this.cdr.detectChanges();
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

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
