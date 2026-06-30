import {
  Component,
  inject,
  OnInit,
  signal,
  DestroyRef,
  afterNextRender,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { retry, timer } from 'rxjs';
import { NotificationBellComponent } from '../notification-bell/notification-bell.component';
import { AuthService, MenuService, LoadingService } from '@sohoa.frontend/shared/core';
import { LoadingComponent } from '../common/loading/loading.component';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, NotificationBellComponent, LoadingComponent],
  templateUrl: './admin-layout.component.html',
  styleUrl: './admin-layout.component.scss',
})
export class AdminLayout implements OnInit {
  public loadingService = inject(LoadingService);
  private router = inject(Router);
  private authService = inject(AuthService);
  private menuService = inject(MenuService);
  private destroyRef = inject(DestroyRef);

  isDarkMode = false;
  username = 'Người dùng';
  isSidebarCollapsed = false;
  isMobileSidebarOpen = false;

  /** Signal tránh NG0100 khi menu API trả về sau vòng CD đầu. */
  items = signal<MenuItem[]>([]);
  menuLoaded = signal(false);

  constructor() {
    afterNextRender(() => {
      this.loadSidebarMenu();
    });
  }

  ngOnInit() {
    this.router.events.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.isMobileSidebarOpen = false;
    });

    if (typeof window === 'undefined') {
      return;
    }

    const token = this.authService.getToken();
    if (token) {
      try {
        const payloadBase64 = token.split('.')[1];
        const payloadJson = atob(payloadBase64.replace(/-/g, '+').replace(/_/g, '/'));
        const payload = JSON.parse(payloadJson);
        this.username = payload.name || payload.unique_name || payload.username || payload.sub || 'Người dùng';
      } catch {
        this.username = 'Người dùng';
      }
    }

    const savedTheme = localStorage.getItem('theme');
    if (savedTheme === 'dark') {
      this.isDarkMode = true;
      document.documentElement.classList.add('dark-mode');
    } else {
      this.isDarkMode = false;
      document.documentElement.classList.remove('dark-mode');
    }
  }

  trackByMenuId(_index: number, item: MenuItem): string {
    return String(item.id ?? item.label ?? _index);
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
      const targetId = String(item.id ?? '');
      this.items.update((list) =>
        list.map((group) =>
          String(group.id ?? '') === targetId
            ? { ...group, expanded: !group.expanded }
            : group
        )
      );
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
      return group.items.some((sub) => {
        if (sub.routerLink) {
          const subLink = sub.routerLink.join('/');
          return currentUrl === subLink || currentUrl.startsWith(subLink + '/');
        }
        return false;
      });
    }
    return false;
  }

  loadSidebarMenu() {
    this.menuService
      .getSidebarMenu()
      .pipe(
        retry({ count: 2, delay: (_err, retryCount) => timer(300 * retryCount) }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (res) => {
          const menus = Array.isArray(res)
            ? res
            : res && Array.isArray(res.items)
              ? res.items
              : res && Array.isArray(res.value)
                ? res.value
                : [];
          this.items.set(this.buildMenuTree(menus, this.router.url || ''));
          this.menuLoaded.set(true);
        },
        error: (err) => {
          console.error('Không thể load sidebar menu động', err);
          this.menuLoaded.set(true);
        },
      });
  }

  buildMenuTree(flatMenus: any[], currentUrl: string): MenuItem[] {
    const menuMap = new Map<number, MenuItem>();
    const rootItems: MenuItem[] = [];
    const menusCopy = [...flatMenus];

    const hasApprovalMenu = menusCopy.some((m) => m.url === '/equipment/form-approval');
    if (!hasApprovalMenu) {
      const formMgmtMenu = menusCopy.find((m) => m.url === '/equipment/form-management');
      if (formMgmtMenu) {
        menusCopy.push({
          id: 999999,
          name: 'Phê duyệt biểu mẫu',
          icon: 'pi pi-check-square',
          url: '/equipment/form-approval',
          parentId: formMgmtMenu.parentId,
        });
      }
    }

    const hasTemplateMenu = menusCopy.some((m) => m.url === '/equipment/form-template');
    if (!hasTemplateMenu) {
      const formMgmtMenu = menusCopy.find((m) => m.url === '/equipment/form-management');
      if (formMgmtMenu) {
        menusCopy.push({
          id: 999998,
          name: 'Quản lý biểu mẫu',
          icon: 'pi pi-file',
          url: '/equipment/form-template',
          parentId: formMgmtMenu.parentId,
        });
      }
    }

    const hasPublishMenu = menusCopy.some((m) => m.url === '/dossier-management/publish');
    if (!hasPublishMenu && (this.authService.hasPermission('SUPER_ADMIN') || this.authService.hasPermission('DOSSIER_PUBLISH_VIEW') || this.authService.hasPermission('DOSSIER_PUBLISH_RELEASE'))) {
      const dossierMgmtMenu = menusCopy.find((m) => m.url === '/dossier-management/my-dossiers' || m.url === '/dossier-management/approve');
      if (dossierMgmtMenu) {
        menusCopy.push({
          id: 999997,
          name: 'Xuất bản hồ sơ',
          icon: 'pi pi-cloud-upload',
          url: '/dossier-management/publish',
          parentId: dossierMgmtMenu.parentId,
        });
      }
    }

    menusCopy.forEach((m) => {
      const item: MenuItem = {
        id: m.id.toString(),
        label: m.name,
        icon: m.icon || undefined,
        routerLink: m.url ? [m.url] : undefined,
        items: undefined,
        expanded: false,
      };
      menuMap.set(m.id, item);
    });

    menusCopy.forEach((m) => {
      const item = menuMap.get(m.id);
      if (!item) return;

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
    });

    for (const group of rootItems) {
      if (!group.items?.length) continue;
      const isActive = group.items.some((sub) => {
        if (!sub.routerLink) return false;
        const subLink = sub.routerLink.join('/');
        return currentUrl === subLink || currentUrl.startsWith(subLink + '/');
      });
      if (isActive) {
        group.expanded = true;
      }
    }

    return rootItems;
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
