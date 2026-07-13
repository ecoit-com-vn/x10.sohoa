export interface SidebarMenuRecord {
  id: number;
  name: string;
  url?: string | null;
  icon?: string | null;
  parentId?: number | null;
  sortOrder?: number;
  isActive?: boolean;
  permissionCode?: string | null;
}

export interface MenuPermissionChecker {
  hasPermission(code: string): boolean;
}

export function normalizeSidebarMenus(raw: unknown): SidebarMenuRecord[] {
  const list = Array.isArray(raw)
    ? raw
    : raw && typeof raw === 'object' && Array.isArray((raw as { items?: unknown[] }).items)
      ? (raw as { items: unknown[] }).items
      : raw && typeof raw === 'object' && Array.isArray((raw as { value?: unknown[] }).value)
        ? (raw as { value: unknown[] }).value
        : [];

  return list
    .map((item) => normalizeSidebarMenuRecord(item))
    .filter((item): item is SidebarMenuRecord => item !== null);
}

function normalizeSidebarMenuRecord(raw: unknown): SidebarMenuRecord | null {
  if (!raw || typeof raw !== 'object') {
    return null;
  }

  const record = raw as Record<string, unknown>;
  const id = Number(record['id'] ?? record['Id']);
  const name = String(record['name'] ?? record['Name'] ?? '').trim();
  if (!Number.isFinite(id) || !name) {
    return null;
  }

  const parentRaw = record['parentId'] ?? record['ParentId'];
  const parentId =
    parentRaw === null || parentRaw === undefined || parentRaw === ''
      ? null
      : Number(parentRaw);

  const urlRaw = record['url'] ?? record['Url'];
  const url = typeof urlRaw === 'string' && urlRaw.trim() ? urlRaw.trim() : null;

  return {
    id,
    name,
    url,
    icon: (record['icon'] ?? record['Icon']) as string | null | undefined,
    parentId: Number.isFinite(parentId) ? parentId : null,
    sortOrder: Number(record['sortOrder'] ?? record['SortOrder'] ?? 0),
    isActive: Boolean(record['isActive'] ?? record['IsActive'] ?? true),
    permissionCode: (record['permissionCode'] ?? record['PermissionCode']) as string | null | undefined,
  };
}

/** Bổ sung menu FE tạm thời khi DB chưa có — đồng bộ với AdminLayout. */
export function augmentSidebarMenus(
  flatMenus: SidebarMenuRecord[],
  auth?: MenuPermissionChecker | null
): SidebarMenuRecord[] {
  const menusCopy = [...flatMenus];

  const canPublish =
    auth?.hasPermission('SUPER_ADMIN') ||
    auth?.hasPermission('DOSSIER_PUBLISH_VIEW') ||
    auth?.hasPermission('DOSSIER_PUBLISH_RELEASE');

  const hasPublishMenu = menusCopy.some((m) => m.url === '/dossier-management/publish');
  if (!hasPublishMenu && canPublish) {
    const dossierMgmtMenu = menusCopy.find(
      (m) => m.url === '/dossier-management/my-dossiers' || m.url === '/dossier-management/approve'
    );
    if (dossierMgmtMenu) {
      menusCopy.push({
        id: 999997,
        name: 'Xuất bản hồ sơ',
        icon: 'pi pi-cloud-upload',
        url: '/dossier-management/publish',
        parentId: dossierMgmtMenu.parentId ?? null,
      });
    }
  }

  const canViewProcessingCategory =
    auth?.hasPermission('SUPER_ADMIN') ||
    auth?.hasPermission('PROCESSING_CATEGORY_VIEW');

  const hasProcessingCategoryMenu = menusCopy.some((m) => m.url === '/catalog/processing-category');
  if (!hasProcessingCategoryMenu && canViewProcessingCategory) {
    const catalogParent = menusCopy.find(
      (m) =>
        !m.url &&
        (
          m.name === 'Quản lý danh mục' ||
          m.name === 'Danh mục hệ thống' ||
          m.permissionCode === 'CATALOG_VIEW'
        )
    );
    if (catalogParent) {
      menusCopy.push({
        id: 999996,
        name: 'Quy trình xử lý',
        icon: 'pi pi-sitemap',
        url: '/catalog/processing-category',
        parentId: catalogParent.id,
        sortOrder: 8,
        permissionCode: 'PROCESSING_CATEGORY_VIEW',
      });
    }
  }

  return menusCopy.map((menu) => {
    if (menu.url === '/dossier-management') {
      return { ...menu, url: '/dossier-management/my-dossiers' };
    }
    return menu;
  });
}

export function normalizeRoutePath(url: string): string {
  const path = (url.split('?')[0] || '').replace(/\/+$/, '');
  return path || '/';
}

export function menuUrlPath(url?: string | null): string {
  if (!url?.trim()) {
    return '';
  }
  const link = url.trim();
  return normalizeRoutePath(link.startsWith('/') ? link : `/${link}`);
}

export function urlMatchesMenuLink(currentUrl: string, menuLink: string): boolean {
  const current = normalizeRoutePath(currentUrl);
  const link = normalizeRoutePath(menuLink);
  if (!link) {
    return false;
  }
  return current === link || current.startsWith(`${link}/`);
}

/** Chọn menu khớp URL dài nhất — tránh /dossier-management active khi đang ở /approve. */
export function resolveActiveMenuUrl(currentUrl: string, candidates: string[]): string | null {
  let best: string | null = null;
  for (const link of candidates) {
    if (!urlMatchesMenuLink(currentUrl, link)) {
      continue;
    }
    if (!best || link.length > best.length) {
      best = link;
    }
  }
  return best;
}

export function findMenuByUrl(
  flatMenus: SidebarMenuRecord[],
  currentUrl: string
): SidebarMenuRecord | null {
  const candidates = flatMenus
    .map((menu) => menuUrlPath(menu.url))
    .filter((link): link is string => !!link);
  const activeUrl = resolveActiveMenuUrl(currentUrl, candidates);
  if (!activeUrl) {
    return null;
  }
  return flatMenus.find((menu) => menuUrlPath(menu.url) === activeUrl) ?? null;
}

export function buildMenuAncestorChain(
  flatMenus: SidebarMenuRecord[],
  menu: SidebarMenuRecord
): SidebarMenuRecord[] {
  const menuById = new Map(flatMenus.map((item) => [item.id, item]));
  const chain: SidebarMenuRecord[] = [];
  const visited = new Set<number>();

  let current: SidebarMenuRecord | undefined = menu;
  while (current) {
    if (visited.has(current.id)) {
      break;
    }
    visited.add(current.id);
    chain.unshift(current);
    if (!current.parentId) {
      break;
    }
    current = menuById.get(current.parentId);
  }

  return chain;
}
