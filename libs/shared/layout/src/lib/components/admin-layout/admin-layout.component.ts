import {
  Component,
  inject,
  OnInit,
  signal,
  computed,
  DestroyRef,
  afterNextRender,
  HostListener,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { filter, retry, timer } from 'rxjs';
import { NotificationBellComponent } from '../notification-bell/notification-bell.component';
import { AuthService, MenuService, LoadingService, resolveActiveMenuUrl, menuUrlPath } from '@sohoa.frontend/shared/core';
import { LoadingComponent } from '../common/loading/loading.component';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, NotificationBellComponent, LoadingComponent],
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
  profileMenuOpen = signal(false);
  displayName = computed(() => this.authService.currentUserProfile()?.fullName || this.username);

  /** Signal tránh NG0100 khi menu API trả về sau vòng CD đầu. */
  items = signal<MenuItem[]>([]);
  menuLoaded = signal(false);
  headerSearchKeyword = '';

  canUseHeaderSearch = computed(() => {
    const perms = this.authService.currentUserPermissions();
    return perms.includes('SUPER_ADMIN') || perms.includes('DOCUMENT_FULLTEXT_SEARCH_VIEW');
  });

  constructor() {
    afterNextRender(() => {
      this.loadSidebarMenu();
    });
  }

  ngOnInit() {
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      this.isMobileSidebarOpen = false;
      this.syncMenuExpandedState();
      this.syncHeaderSearchFromRoute();
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

      this.authService.loadProfile().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        error: () => { }
      });
    }

    const savedTheme = localStorage.getItem('theme');
    if (savedTheme === 'dark') {
      this.isDarkMode = true;
      document.documentElement.classList.add('dark-mode');
    } else {
      this.isDarkMode = false;
      document.documentElement.classList.remove('dark-mode');
    }

    this.syncHeaderSearchFromRoute();
    this.authService.ensurePermissionsLoaded().pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();
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

  @HostListener('document:click')
  closeProfileMenu() {
    this.profileMenuOpen.set(false);
  }

  toggleProfileMenu(event: Event) {
    event.stopPropagation();
    this.profileMenuOpen.update(open => !open);
  }

  goToProfile(event: Event) {
    event.stopPropagation();
    this.profileMenuOpen.set(false);
    this.router.navigate(['/profile']);
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

  private menuLinkPath(item: MenuItem): string {
    if (!item.routerLink?.length) {
      return '';
    }
    const link = item.routerLink.join('/');
    return menuUrlPath(link.startsWith('/') ? link : `/${link}`);
  }

  private collectAllMenuLinks(): string[] {
    const links: string[] = [];
    for (const group of this.items()) {
      const groupLink = this.menuLinkPath(group);
      if (groupLink) {
        links.push(groupLink);
      }
      group.items?.forEach((sub) => {
        const subLink = this.menuLinkPath(sub);
        if (subLink) {
          links.push(subLink);
        }
      });
    }
    return links;
  }

  /** Khớp menu dài nhất trên toàn sidebar — tránh /dossier-management active khi đang ở digitization. */
  private getActiveMenuLink(): string | null {
    return resolveActiveMenuUrl(this.router.url || '', this.collectAllMenuLinks());
  }

  isSubMenuActive(sub: MenuItem, _siblings: MenuItem[]): boolean {
    const subLink = this.menuLinkPath(sub);
    if (!subLink) {
      return false;
    }
    return this.getActiveMenuLink() === subLink;
  }

  isGroupActive(group: MenuItem): boolean {
    const activeLink = this.getActiveMenuLink();
    if (!activeLink) {
      return false;
    }

    const groupLink = this.menuLinkPath(group);
    if (groupLink && groupLink === activeLink) {
      return true;
    }

    return !!group.items?.some((sub) => this.menuLinkPath(sub) === activeLink);
  }

  private syncMenuExpandedState(): void {
    const activeLink = this.getActiveMenuLink();
    this.items.update((groups) =>
      groups.map((group) => {
        if (!group.items?.length) {
          return group;
        }
        const shouldExpand = group.items.some((sub) => this.menuLinkPath(sub) === activeLink);
        if (!shouldExpand) {
          return group;
        }
        return { ...group, expanded: true };
      })
    );
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
    const menusCopy = this.filterSidebarNavigationMenus([...flatMenus]);

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
      const url = m.url === '/dossier-management' ? '/dossier-management/my-dossiers' : m.url;
      const item: MenuItem = {
        id: m.id.toString(),
        label: m.name,
        icon: m.icon || undefined,
        routerLink: url ? [url] : undefined,
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

    const activeLink = resolveActiveMenuUrl(
      currentUrl,
      menusCopy
        .map((m) => menuUrlPath(m.url === '/dossier-management' ? '/dossier-management/my-dossiers' : m.url))
        .filter((link): link is string => !!link)
    );

    for (const group of rootItems) {
      if (!group.items?.length) continue;
      const shouldExpand = group.items.some((sub) => this.menuLinkPath(sub) === activeLink);
      if (shouldExpand) {
        group.expanded = true;
      }
    }

    return rootItems
      .map((group) => ({
        ...group,
        items: group.items?.filter((sub) => !!sub.routerLink?.length),
      }))
      .filter((group) => !!group.routerLink?.length || (group.items?.length ?? 0) > 0);
  }

  /**
   * Sidebar: giữ menu có URL hoặc menu cha có con; bỏ menu không URL và không có con
   * (menu chỉ dùng cho phân quyền, ví dụ Tìm kiếm toàn văn).
   */
  private filterSidebarNavigationMenus(flatMenus: any[]): any[] {
    const parentIdsWithChildren = new Set<number>();
    for (const menu of flatMenus) {
      const parentId = menu.parentId ?? menu.ParentId;
      if (parentId != null && parentId !== '') {
        parentIdsWithChildren.add(Number(parentId));
      }
    }

    return flatMenus.filter((menu) => {
      const url = String(menu.url ?? menu.Url ?? '').trim();
      const id = Number(menu.id ?? menu.Id);
      if (url) {
        return true;
      }
      return parentIdsWithChildren.has(id);
    });
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  onHeaderSearch() {
    if (!this.canUseHeaderSearch()) {
      return;
    }

    const keyword = this.headerSearchKeyword.trim();
    this.router.navigate(['/search/documents'], {
      queryParams: { keyword: keyword || null }
    });
  }

  private syncHeaderSearchFromRoute() {
    const url = this.router.url || '';
    if (!url.includes('/search/documents')) {
      return;
    }
    const queryIndex = url.indexOf('?');
    if (queryIndex < 0) {
      return;
    }
    const params = new URLSearchParams(url.slice(queryIndex + 1));
    const keyword = (params.get('keyword') || params.get('q') || '').trim();
    if (keyword) {
      this.headerSearchKeyword = keyword;
    }
  }
}
