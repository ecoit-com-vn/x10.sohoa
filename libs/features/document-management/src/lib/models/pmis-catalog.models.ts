/** 1 node của cây "Kho tài liệu PMIS": Đơn vị → Trạm biến áp/Đường dây → Thiết bị — tổng hợp từ dữ
 * liệu thật (không có bảng folder riêng), chỉ đọc. */
export interface PmisCatalogNode {
  id: string;
  name: string;
  parentId: string | null;
  nodeType: 'unit' | 'substation' | 'line' | 'equipment';
  documentCount: number;
  children?: PmisCatalogNode[];
}

export interface PmisDocumentItem {
  id: string;
  pmisDocumentCode: string;
  ownerType: 'INFRASTRUCTURE' | 'EQUIPMENT';
  ownerId: string;
  documentName: string | null;
  documentType: string | null;
  objectKey: string | null;
  fileSize: number | null;
  syncedAt: string;
  /** true nếu do người dùng tự upload thủ công (khi đồng bộ tự động lỗi), false nếu đến từ đồng bộ PMIS thật. */
  isManual: boolean;
}

export interface PmisCatalogDocumentsResponse {
  items: PmisDocumentItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}
