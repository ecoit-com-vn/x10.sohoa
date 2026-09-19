/** 1 node của cây "Kho tài liệu PMIS": Đơn vị → Trạm biến áp/Đường dây → Thiết bị — tổng hợp từ dữ
 * liệu thật (không có bảng folder riêng), chỉ đọc. */
export interface PmisCatalogNode {
  id: string;
  name: string;
  parentId: string | null;
  /** 'root' chỉ tồn tại phía FE (node ảo bao toàn bộ cây), không đến từ API. 'group' cũng chỉ tồn tại
   * phía FE - 2 thư mục cứng "Trạm biến áp"/"Đường dây" chèn vào giữa mỗi Đơn vị và các Trạm/Đường dây
   * thật của nó, xem groupInfrastructureNodesByType() trong pmis-catalog-tree.util.ts. */
  nodeType: 'root' | 'unit' | 'group' | 'substation' | 'line' | 'equipment';
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

/** 1 dòng kết quả tìm Trạm/Đường dây trên TOÀN BỘ công ty (ô tìm kiếm phía trên cây) - cho phép nhảy
 * thẳng tới đúng Trạm/Đường dây mà không cần duyệt tay qua từng công ty trong cây. */
export interface PmisInfrastructureLookupItem {
  id: string;
  name: string;
  code: string | null;
  nodeType: 'substation' | 'line';
  documentCount: number;
  unitNodeId: string;
  unitName: string | null;
}
