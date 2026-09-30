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
  /** Trạng thái tải file vật lý từ PMIS: NO_URL (PMIS không kèm file) | PENDING (chờ tải) | DONE | FAILED (lỗi, sẽ thử lại thưa). */
  fileStatus: 'NO_URL' | 'PENDING' | 'DONE' | 'FAILED';
  /** Lý do tải file lỗi gần nhất, nếu có. */
  fileLastError: string | null;
}

export interface PmisCatalogDocumentsResponse {
  items: PmisDocumentItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}
