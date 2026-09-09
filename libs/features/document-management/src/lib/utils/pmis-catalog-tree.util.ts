import { PmisCatalogNode } from '../models/pmis-catalog.models';

/** Cùng thuật toán convertFlatToTree/findBreadcrumbPath/getBreadcrumbLabel ở folder-tree.util.ts, viết
 * riêng cho PmisCatalogNode (khác FolderNode — bắt buộc unitId, không phù hợp với node PMIS). */
export function convertPmisFlatToTree(nodes: PmisCatalogNode[]): PmisCatalogNode[] {
  const nodeMap = new Map<string, PmisCatalogNode>();
  const roots: PmisCatalogNode[] = [];

  nodes.forEach((node) => nodeMap.set(node.id, { ...node, children: [] }));

  nodes.forEach((node) => {
    const current = nodeMap.get(node.id)!;
    if (node.parentId) {
      const parent = nodeMap.get(node.parentId);
      if (parent) {
        parent.children = parent.children || [];
        parent.children.push(current);
      } else {
        roots.push(current);
      }
    } else {
      roots.push(current);
    }
  });

  const sortByName = (node: PmisCatalogNode) => {
    if (node.children) {
      node.children.sort((a, b) => a.name.localeCompare(b.name));
      node.children.forEach(sortByName);
    }
  };
  roots.forEach(sortByName);
  roots.sort((a, b) => a.name.localeCompare(b.name));

  return roots;
}

export function findPmisBreadcrumbPath(nodeId: string | null, nodes: PmisCatalogNode[]): PmisCatalogNode[] {
  if (!nodeId) return [];

  const nodeMap = new Map<string, PmisCatalogNode>();
  nodes.forEach((node) => nodeMap.set(node.id, node));

  const path: PmisCatalogNode[] = [];
  let currentId: string | null = nodeId;
  while (currentId) {
    const node = nodeMap.get(currentId);
    if (!node) break;
    path.unshift(node);
    currentId = node.parentId;
  }
  return path;
}
