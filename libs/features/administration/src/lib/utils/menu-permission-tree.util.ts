export interface MenuLookupItem {
  id: number;
  name: string;
  url?: string | null;
  icon?: string | null;
  parentId?: number | null;
  sortOrder?: number;
  isActive?: boolean;
  permissionCode?: string | null;
}

export interface PermissionLookupItem {
  code: string;
  name?: string;
}

export interface MenuPermissionTreeNode {
  id: number;
  name: string;
  icon?: string | null;
  url?: string | null;
  subMenus: Array<{
    id: number;
    name: string;
    url?: string | null;
    icon?: string | null;
    permissions: PermissionLookupItem[];
    expanded?: boolean;
  }>;
  permissions: PermissionLookupItem[];
  expanded: boolean;
}

export interface MenuDisplayTreeNode {
  id: number;
  name: string;
  url?: string | null;
  icon?: string | null;
  parentId?: number | null;
  sortOrder?: number;
  isActive?: boolean;
  permissionCode?: string | null;
  children: MenuDisplayTreeNode[];
  expanded: boolean;
}

/** Chuẩn hóa dữ liệu menu từ API (camelCase / PascalCase). */
export function normalizeMenuLookupItem(raw: Record<string, unknown>): MenuLookupItem {
  return {
    id: Number(raw['id'] ?? raw['Id']),
    name: String(raw['name'] ?? raw['Name'] ?? ''),
    url: (raw['url'] ?? raw['Url']) as string | null | undefined,
    icon: (raw['icon'] ?? raw['Icon']) as string | null | undefined,
    parentId: (raw['parentId'] ?? raw['ParentId']) as number | null | undefined,
    sortOrder: Number(raw['sortOrder'] ?? raw['SortOrder'] ?? 0),
    isActive: (raw['isActive'] ?? raw['IsActive'] ?? true) as boolean,
    permissionCode: (raw['permissionCode'] ?? raw['PermissionCode']) as string | null | undefined
  };
}

/** Chuẩn hóa dữ liệu quyền từ API (camelCase / PascalCase). */
export function normalizePermissionLookupItem(raw: Record<string, unknown>): PermissionLookupItem {
  return {
    code: String(raw['code'] ?? raw['Code'] ?? ''),
    name: (raw['name'] ?? raw['Name']) as string | undefined
  };
}

export function normalizeMenuLookupList(items: unknown[]): MenuLookupItem[] {
  return items.map((item) => normalizeMenuLookupItem(item as Record<string, unknown>));
}

export function normalizePermissionLookupList(items: unknown[]): PermissionLookupItem[] {
  return items.map((item) => normalizePermissionLookupItem(item as Record<string, unknown>));
}

/** Quyền mịn dùng để gán menu — chỉ loại xem (VIEW). */
export function isMenuViewPermission(code: string): boolean {
  if (!code) {
    return false;
  }

  return code === 'VIEW_DASHBOARD' || code.endsWith('_VIEW');
}

/** Tách prefix resource từ mã quyền (USER_CREATE → USER). */
export function extractPermissionPrefix(code: string): string | null {
  if (!code) {
    return null;
  }

  if (code === 'VIEW_DASHBOARD') {
    return 'VIEW_DASHBOARD';
  }

  const parts = code.split('_');
  if (parts.length < 2) {
    return null;
  }

  return parts.slice(0, parts.length - 1).join('_');
}

function getMenuDepth(menu: MenuLookupItem, menusList: MenuLookupItem[]): number {
  let depth = 0;
  let parentId = menu.parentId ?? null;

  while (parentId) {
    depth++;
    const parent = menusList.find((item) => item.id === parentId);
    if (!parent) {
      break;
    }
    parentId = parent.parentId ?? null;
  }

  return depth;
}

function sortMenusByDisplayOrder(a: MenuLookupItem, b: MenuLookupItem): number {
  const sortDiff = (a.sortOrder ?? 0) - (b.sortOrder ?? 0);
  if (sortDiff !== 0) {
    return sortDiff;
  }
  return a.id - b.id;
}

function getPrefixSiblingCount(menu: MenuLookupItem, menusList: MenuLookupItem[]): number {
  const prefix = menu.permissionCode ? extractPermissionPrefix(menu.permissionCode) : null;
  if (!prefix) {
    return 0;
  }

  return menusList.filter(
    (candidate) =>
      candidate.parentId === menu.parentId &&
      candidate.isActive !== false &&
      candidate.permissionCode &&
      extractPermissionPrefix(candidate.permissionCode) === prefix
  ).length;
}

/** Ưu tiên menu sở hữu duy nhất prefix trong nhóm cha, rồi URL/heuristic. */
function resolveBestMenuForPermission(
  permission: PermissionLookupItem,
  candidates: MenuLookupItem[],
  menusList: MenuLookupItem[]
): number | null {
  if (candidates.length === 0) {
    return null;
  }

  if (candidates.length === 1) {
    return candidates[0].id;
  }

  const exactMatches = candidates.filter((menu) => menu.permissionCode === permission.code);
  if (exactMatches.length === 1) {
    return exactMatches[0].id;
  }

  const permPrefix = extractPermissionPrefix(permission.code);
  const uniqueOwners = candidates.filter((menu) => getPrefixSiblingCount(menu, menusList) === 1);
  const ownerPool = uniqueOwners.length > 0 ? uniqueOwners : candidates;

  if (permPrefix === 'EQUIPMENT') {
    const equipmentListMenu = ownerPool.find((menu) => menu.url?.includes('/equipment/list'));
    if (equipmentListMenu) {
      return equipmentListMenu.id;
    }
  }

  if (permPrefix === 'EAV_FORM_APPROVAL') {
    const approvalMenu = menusList.find(
      (menu) => menu.isActive !== false && menu.url?.includes('/equipment/form-approval')
    );
    if (approvalMenu) {
      return approvalMenu.id;
    }
  }

  if (permPrefix === 'EAV_COMPLETED_FORM') {
    const completedMenu = menusList.find(
      (menu) => menu.isActive !== false && menu.url?.includes('/equipment/completed-forms')
    );
    if (completedMenu) {
      return completedMenu.id;
    }
  }

  if (permPrefix === 'EAV_FORM_TEMPLATE') {
    const designMenu = menusList.find(
      (menu) => menu.isActive !== false && menu.url?.includes('/equipment/form-management')
    );
    if (designMenu) {
      return designMenu.id;
    }
  }

  const sorted = [...ownerPool].sort((a, b) => {
    const depthDiff = getMenuDepth(b, menusList) - getMenuDepth(a, menusList);
    if (depthDiff !== 0) {
      return depthDiff;
    }

    const urlDiff = Number(Boolean(b.url)) - Number(Boolean(a.url));
    if (urlDiff !== 0) {
      return urlDiff;
    }

    const parentDiff = (b.parentId ?? 0) - (a.parentId ?? 0);
    if (parentDiff !== 0) {
      return parentDiff;
    }

    return sortMenusByDisplayOrder(a, b);
  });

  return sorted[0]?.id ?? null;
}

/** Chọn một menu tốt nhất trong phạm vi scope (ưu tiên menu con trực tiếp có URL). */
export function findMenuIdForPermissionInScope(
  code: string,
  menusList: MenuLookupItem[],
  scopeIds: Set<number>,
  scopeRootId?: number
): number | null {
  const prefix = extractPermissionPrefix(code);
  if (!prefix) {
    return null;
  }

  const candidates = menusList.filter(
    (menu) =>
      scopeIds.has(menu.id) &&
      menu.isActive !== false &&
      menu.permissionCode &&
      extractPermissionPrefix(menu.permissionCode) === prefix
  );

  if (candidates.length === 0) {
    return null;
  }

  const scopedCandidates =
    scopeRootId != null
      ? candidates.filter((menu) => menu.parentId === scopeRootId && menu.url)
      : candidates;

  const pool = scopedCandidates.length > 0 ? scopedCandidates : candidates;
  const permission = { code };

  return resolveBestMenuForPermission(permission, pool, menusList);
}

/** Map mỗi quyền → một menu (menu sở hữu prefix phù hợp nhất). */
function buildGlobalPermissionAssignment(
  menusList: MenuLookupItem[],
  permissions: PermissionLookupItem[]
): Map<string, number> {
  const assignment = new Map<string, number>();

  permissions.forEach((permission) => {
    const exactMenu = menusList.find(
      (menu) => menu.isActive !== false && menu.permissionCode === permission.code
    );
    if (exactMenu) {
      assignment.set(permission.code, exactMenu.id);
    }
  });

  permissions.forEach((permission) => {
    if (assignment.has(permission.code)) {
      return;
    }

    const permPrefix = extractPermissionPrefix(permission.code);
    if (!permPrefix) {
      return;
    }

    const candidates = menusList.filter(
      (menu) =>
        menu.isActive !== false &&
        menu.permissionCode &&
        extractPermissionPrefix(menu.permissionCode) === permPrefix
    );

    const menuId = resolveBestMenuForPermission(permission, candidates, menusList);
    if (menuId != null) {
      assignment.set(permission.code, menuId);
    }
  });

  return assignment;
}

/** Gom quyền theo cây menu — hiển thị đủ menu con, mỗi quyền chỉ một checkbox. */
export function buildMenuPermissionTree(
  menusList: MenuLookupItem[] | unknown[],
  permissions: PermissionLookupItem[] | unknown[]
): MenuPermissionTreeNode[] {
  const normalizedMenus = Array.isArray(menusList) ? normalizeMenuLookupList(menusList as unknown[]) : [];
  const normalizedPermissions = Array.isArray(permissions)
    ? normalizePermissionLookupList(permissions as unknown[])
    : [];

  const assignment = buildGlobalPermissionAssignment(normalizedMenus, normalizedPermissions);
  const permGroups = new Map<number, PermissionLookupItem[]>();

  normalizedPermissions.forEach((permission) => {
    const menuId = assignment.get(permission.code);
    if (menuId == null) {
      return;
    }

    if (!permGroups.has(menuId)) {
      permGroups.set(menuId, []);
    }
    permGroups.get(menuId)!.push(permission);
  });

  const unmappedPerms = normalizedPermissions.filter((permission) => !assignment.has(permission.code));
  const subMenusList = normalizedMenus.filter((menu) => menu.parentId && menu.isActive !== false);
  const tree: MenuPermissionTreeNode[] = [];

  normalizedMenus
    .filter((menu) => !menu.parentId && menu.isActive !== false)
    .sort(sortMenusByDisplayOrder)
    .forEach((parentMenu) => {
      const childMenus = subMenusList
        .filter((subMenu) => subMenu.parentId === parentMenu.id)
        .sort(sortMenusByDisplayOrder);

      const directPermissions = permGroups.get(parentMenu.id) || [];

      if (childMenus.length === 0 && directPermissions.length === 0) {
        return;
      }

      const isPermissionOnlyParent = childMenus.length === 0 && directPermissions.length > 0;

      tree.push({
        id: parentMenu.id,
        name: parentMenu.name,
        icon: parentMenu.icon,
        url: parentMenu.url,
        subMenus: isPermissionOnlyParent
          ? [{
              id: parentMenu.id,
              name: parentMenu.name,
              url: parentMenu.url,
              icon: parentMenu.icon,
              permissions: directPermissions,
              expanded: true
            }]
          : childMenus.map((subMenu) => ({
              id: subMenu.id,
              name: subMenu.name,
              url: subMenu.url,
              icon: subMenu.icon,
              permissions: permGroups.get(subMenu.id) || [],
              expanded: true
            })),
        permissions: isPermissionOnlyParent ? [] : directPermissions,
        expanded: isPermissionOnlyParent
      });
    });

  if (unmappedPerms.length > 0) {
    tree.push({
      id: -999,
      name: 'Hệ thống dùng chung / Quyền khác',
      icon: 'pi pi-key',
      url: '',
      subMenus: [],
      permissions: unmappedPerms,
      expanded: false
    });
  }

  return tree;
}

/** Cây menu cha/con cho màn cấu hình menu. */
export function buildMenuDisplayTree(
  menusList: MenuLookupItem[] | unknown[],
  keyword = ''
): MenuDisplayTreeNode[] {
  const normalizedMenus = Array.isArray(menusList) ? normalizeMenuLookupList(menusList as unknown[]) : [];
  const kw = keyword.trim().toLowerCase();

  const matchesKeyword = (menu: MenuLookupItem): boolean => {
    if (!kw) {
      return true;
    }

    return (
      menu.name.toLowerCase().includes(kw) ||
      (menu.url?.toLowerCase().includes(kw) ?? false) ||
      (menu.permissionCode?.toLowerCase().includes(kw) ?? false)
    );
  };

  const buildChildren = (parentId: number): MenuDisplayTreeNode[] => {
    const nodes: MenuDisplayTreeNode[] = [];

    normalizedMenus
      .filter((menu) => menu.parentId === parentId)
      .sort(sortMenusByDisplayOrder)
      .forEach((menu) => {
        const children = buildChildren(menu.id);
        if (!kw || matchesKeyword(menu) || children.length > 0) {
          nodes.push({
            ...menu,
            children,
            expanded: kw.length > 0 || children.length > 0
          });
        }
      });

    return nodes;
  };

  const tree: MenuDisplayTreeNode[] = [];

  normalizedMenus
    .filter((menu) => !menu.parentId)
    .sort(sortMenusByDisplayOrder)
    .forEach((parentMenu) => {
      const children = buildChildren(parentMenu.id);
      if (!kw || matchesKeyword(parentMenu) || children.length > 0) {
        tree.push({
          ...parentMenu,
          children,
          expanded: kw.length > 0 || children.length > 0
        });
      }
    });

  return tree;
}
