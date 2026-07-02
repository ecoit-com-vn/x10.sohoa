import { FolderNode } from '../models/document.models';

/**
 * Chuyển đổi danh sách thư mục phẳng (flat) thành cấu trúc cây (tree)
 */
export function convertFlatToTree(folders: FolderNode[]): FolderNode[] {
  const folderMap = new Map<string, FolderNode>();
  const roots: FolderNode[] = [];

  // Tạo map folders
  folders.forEach(folder => {
    folderMap.set(folder.id, { ...folder, children: [] });
  });

  // Xây dựng cấu trúc cây
  folders.forEach(folder => {
    const node = folderMap.get(folder.id)!;
    if (folder.parentId) {
      const parent = folderMap.get(folder.parentId);
      if (parent) {
        parent.children = parent.children || [];
        parent.children.push(node);
      } else {
        // Parent is missing from the list, treat this as a local root node
        roots.push(node);
      }
    } else {
      roots.push(node);
    }
  });

  // Sắp xếp con theo tên
  const sortByName = (node: FolderNode) => {
    if (node.children) {
      node.children.sort((a, b) => a.name.localeCompare(b.name));
      node.children.forEach(sortByName);
    }
  };

  roots.forEach(sortByName);
  roots.sort((a, b) => a.name.localeCompare(b.name));

  return roots;
}

/**
 * Tìm breadcrumb path từ một folder ID
 * Ví dụ: từ folder 'D' với cấu trúc A > B > C > D
 * trả về: [A, B, C, D]
 */
export function findBreadcrumbPath(folderId: string | null, folders: FolderNode[]): FolderNode[] {
  if (!folderId) return [];

  const folderMap = new Map<string, FolderNode>();
  folders.forEach(folder => folderMap.set(folder.id, folder));

  const path: FolderNode[] = [];
  let currentId: string | null = folderId;

  while (currentId) {
    const folder = folderMap.get(currentId);
    if (!folder) break;
    path.unshift(folder);
    currentId = folder.parentId;
  }

  return path;
}

/**
 * Tìm một folder theo ID từ danh sách phẳng
 */
export function findFolderById(id: string, folders: FolderNode[]): FolderNode | undefined {
  return folders.find(f => f.id === id);
}

/**
 * Lấy tên hiển thị breadcrumb (chuỗi path)
 */
export function getBreadcrumbLabel(folderId: string | null, folders: FolderNode[]): string {
  const path = findBreadcrumbPath(folderId, folders);
  return path.length > 0 ? path.map(f => f.name).join(' / ') : 'Thư mục gốc';
}
