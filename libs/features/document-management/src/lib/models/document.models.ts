/**
 * Document Management TypeScript Models & DTOs
 */

// ===== FOLDER MODELS =====

export interface FolderNode {
  id: string;
  name: string;
  parentId: string | null;
  unitId: number;
  createdBy?: string;
  createdByName?: string;
  createdDate?: Date;
  modifiedBy?: string;
  modifiedDate?: Date | null;
  rowVersion?: number;
  children?: FolderNode[]; // for tree structure
}

export interface CreateFolderRequest {
  name: string;
  parentId?: string | null;
}

export interface UpdateFolderRequest {
  name: string;
  rowVersion: number;
}

// ===== DOCUMENT MODELS =====

export interface Document {
  id: string;
  name: string;
  folderId: string | null;
  dossierId: string | null;
  createdBy?: string;
  createdByName?: string;
  createdDate?: Date;
  fileSize?: number;
  mimeType?: string;
  latestVersionId?: string | null;
  rowVersion?: number;
}

export interface CreateDocumentRequest {
  name: string;
  folderId?: string | null;
  dossierId?: string | null;
}

export interface UpdateDocumentRequest {
  name: string;
  rowVersion: number;
}

// ===== DOCUMENT VERSION MODELS =====

export interface DocumentVersion {
  id: string;
  documentId: string;
  versionNumber: number;
  uploadSource: number; // 1: Thư mục, 2: Scan, 3: Web
  filePath?: string;
  minioVersionId?: string;
  fileSize?: number;
  mimeType?: string;
  createdBy?: string;
  createdByName?: string;
  createdDate?: Date;
}

// ===== FILTER & QUERY MODELS =====

export interface DocumentFilter {
  folderId?: string | null;
  keyword?: string;
  createdBy?: string;
  startDate?: string;
  endDate?: string;
  sortField?: string;
  sortOrder?: string;
  page?: number;
  pageSize?: number;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
