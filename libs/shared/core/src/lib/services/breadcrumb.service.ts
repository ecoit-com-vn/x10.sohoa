import { Injectable, inject, signal } from '@angular/core';
import { Observable, of, tap, catchError, map, finalize, shareReplay } from 'rxjs';
import { MenuService } from './menu.service';
import { AuthService } from './auth.service';
import {
  augmentSidebarMenus,
  buildMenuAncestorChain,
  findMenuByUrl,
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
  private menusLoaded = signal(false);
  private menus$: Observable<SidebarMenuRecord[]> | null = null;

  ensureMenusLoaded(): Observable<SidebarMenuRecord[]> {
    if (this.menusLoaded()) {
      return of(this.flatMenus());
    }

    if (this.menus$) {
      return this.menus$;
    }

    this.menus$ = this.menuService.getSidebarMenu().pipe(
      map((res) => augmentSidebarMenus(normalizeSidebarMenus(res), this.authService)),
      tap((menus) => {
        this.flatMenus.set(menus);
        this.menusLoaded.set(true);
      }),
      catchError((err) => {
        console.error('Không thể load menu cho breadcrumb', err);
        return of([]);
      }),
      finalize(() => {
        this.menus$ = null;
      }),
      shareReplay(1)
    );

    return this.menus$;
  }

  resolveTrail(currentUrl: string): BreadcrumbTrailItem[] {
    const matchedMenu = findMenuByUrl(this.flatMenus(), currentUrl);
    if (!matchedMenu) {
      const path = currentUrl.split('?')[0];
      if (path === '/search/dossier' || path.startsWith('/search/dossier/')) {
        return [
          { label: 'Tìm kiếm hồ sơ trong kho', url: '/search/dossier' }
        ];
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
    this.menus$ = null;
  }
}
