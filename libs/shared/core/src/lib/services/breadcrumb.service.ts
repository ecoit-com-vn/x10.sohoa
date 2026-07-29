import { Injectable, inject, signal } from '@angular/core';
import { Observable, of, tap, catchError, map, shareReplay, retry, timer, finalize } from 'rxjs';
import { MenuService } from './menu.service';
import { AuthService } from './auth.service';
import {
  augmentSidebarMenus,
  buildMenuAncestorChain,
  findMenuByUrl,
  normalizeRoutePath,
  normalizeSidebarMenus,
  SidebarMenuRecord,
} from '../utils/sidebar-menu.util';

export interface BreadcrumbTrailItem {
  label: string;
  url?: string;
}

@Injectable({ providedIn: 'root' })
export class BreadcrumbService {
  private menuService = inject(MenuService);
  private authService = inject(AuthService);

  private flatMenus = signal<SidebarMenuRecord[]>([]);
  /** Tăng mỗi lần menu thay đổi — component trail phụ thuộc để re-render. */
  readonly menusVersion = signal(0);
  private menusLoaded = signal(false);
  /** Chia sẻ request đang chạy — tránh race local khi nhiều wf-breadcrumb mount. */
  private inflight$: Observable<SidebarMenuRecord[]> | null = null;

  ensureMenusLoaded(): Observable<SidebarMenuRecord[]> {
    if (this.menusLoaded() && this.flatMenus().length > 0) {
      return of(this.flatMenus());
    }

    if (this.inflight$) {
      return this.inflight$;
    }

    this.inflight$ = this.menuService.getSidebarMenu().pipe(
      // Local gateway/Identity thường chậm lần đầu — đồng bộ với AdminLayout sidebar.
      retry({ count: 2, delay: (_err, retryCount) => timer(300 * retryCount) }),
      map((res) => augmentSidebarMenus(normalizeSidebarMenus(res), this.authService)),
      tap((menus) => {
        this.flatMenus.set(menus);
        this.menusLoaded.set(true);
        this.menusVersion.update((v) => v + 1);
      }),
      catchError((err) => {
        console.error('Không thể load menu cho breadcrumb', err);
        // Không mark loaded khi lỗi + rỗng — cho phép load lại lần sau.
        this.flatMenus.set([]);
        this.menusLoaded.set(false);
        this.menusVersion.update((v) => v + 1);
        return of([]);
      }),
      finalize(() => {
        this.inflight$ = null;
      }),
      shareReplay({ bufferSize: 1, refCount: true })
    );

    return this.inflight$;
  }

  resolveTrail(currentUrl: string): BreadcrumbTrailItem[] {
    // Đọc version để computed ở component track được khi menu vừa load xong.
    this.menusVersion();

    const url = normalizeRoutePath(currentUrl);
    const matchedMenu = findMenuByUrl(this.flatMenus(), url);
    if (!matchedMenu) {
      if (url === '/search/dossier' || url.startsWith('/search/dossier/')) {
        return [{ label: 'Tìm kiếm hồ sơ trong kho', url: '/search/dossier' }];
      }
      return [];
    }

    return buildMenuAncestorChain(this.flatMenus(), matchedMenu).map((menu) => ({
      label: menu.name,
      url: menu.url ? menu.url : undefined,
    }));
  }

  invalidateCache(): void {
    this.flatMenus.set([]);
    this.menusLoaded.set(false);
    this.menusVersion.update((v) => v + 1);
    this.inflight$ = null;
  }
}
