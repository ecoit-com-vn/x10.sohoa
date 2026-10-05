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

/** Chèn 2 thư mục cứng "Trạm biến áp"/"Đường dây" vào giữa mỗi Đơn vị (unit) và các Trạm/Đường dây thật
 * của nó - áp dụng cho MỌI đơn vị, kể cả khi 1 trong 2 loại rỗng (vẫn hiện thư mục rỗng thay vì ẩn đi,
 * để cấu trúc luôn nhất quán giữa các công ty). Chỉ ảnh hưởng tới cây hiển thị (children lồng nhau) -
 * KHÔNG đổi parentId của các node Trạm/Đường dây/Thiết bị thật trong flatNodes, nên breadcrumb (dựa trên
 * flatNodes) vẫn đi thẳng từ Đơn vị tới Trạm/Đường dây như cũ, bỏ qua thư mục ảo này. */
export function groupInfrastructureNodesByType(units: PmisCatalogNode[]): PmisCatalogNode[] {
  return units.map((unit) => {
    if (unit.nodeType !== 'unit') return unit;

    const children = unit.children ?? [];
    const substations = children.filter((c) => c.nodeType === 'substation');
    const lines = children.filter((c) => c.nodeType === 'line');

    const substationGroup: PmisCatalogNode = {
      id: `${unit.id}__group_substation`,
      name: 'Trạm biến áp',
      parentId: unit.id,
      nodeType: 'group',
      documentCount: 0,
      children: substations,
    };
    const lineGroup: PmisCatalogNode = {
      id: `${unit.id}__group_line`,
      name: 'Đường dây',
      parentId: unit.id,
      nodeType: 'group',
      documentCount: 0,
      children: lines,
    };

    return { ...unit, children: [substationGroup, lineGroup] };
  });
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
