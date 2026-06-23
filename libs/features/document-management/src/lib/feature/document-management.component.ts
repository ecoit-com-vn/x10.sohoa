import {
  Component,
  OnInit,
  inject,
  signal,
  computed,
  ViewChild,
  ElementRef,
  effect,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { PaginatorModule } from 'primeng/paginator';
import { FileUploadZoneComponent, FileDownloadService, ScannerPanelComponent, UPLOAD_SOURCE } from '@sohoa.frontend/features/equipment';
import { DocumentManagementService } from '../data-access/document-management.service';
import {
  FolderNode,
  Document,
  CreateFolderRequest,
  UpdateFolderRequest,
  CreateDocumentRequest,
  DocumentFilter,
} from '../models/document.models';
import {
  convertFlatToTree,
  findBreadcrumbPath,
  getBreadcrumbLabel,
} from '../utils/folder-tree.util';

type ViewMode = 'list' | 'add_folder' | 'edit_folder' | 'upload';

@Component({
  selector: 'app-document-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    DialogModule,
    PaginatorModule,
    FileUploadZoneComponent,
    ScannerPanelComponent,
  ],
  templateUrl: './document-management.component.html',
  styleUrl: './document-management.component.css',
})
export class DocumentManagementComponent implements OnInit {
  private documentService = inject(DocumentManagementService);
  private messageService = inject(MessageService);
  private fileDownloadService = inject(FileDownloadService);

  @ViewChild('folderNameInput') folderNameInput?: ElementRef<HTMLInputElement>;
  @ViewChild('uploadZone') uploadZone?: FileUploadZoneComponent;

  readonly UPLOAD_SOURCE = UPLOAD_SOURCE;

  // ===== SIGNALS =====
  currentView = signal<ViewMode>('list');
  folderTree = signal<FolderNode[]>([]);
  flatFolderList = signal<FolderNode[]>([]); // Keep flat list for breadcrumb/search
  selectedFolder = signal<FolderNode | null>(null);
  documents = signal<Document[]>([]);
  page = signal(1);
  pageSize = signal(10);
  totalDocuments = signal(0);
  loadingTree = signal(false);
  loadingDocuments = signal(false);
  deletingFolder = signal(false);
  deletingDocument = signal(false);
  savingFolder = signal(false);
  downloadingDocumentIds = signal<Set<string>>(new Set());

  // Dialog states
  showDeleteFolderConfirm = signal(false);
  showDeleteDocumentConfirm = signal(false);
  showFolderMenu = signal(false);
  deleteTargetFolder = signal<FolderNode | null>(null);
  deleteTargetDocument = signal<Document | null>(null);

  // Form states
  folderFormName = signal('');
  editingFolderId = signal<string | null>(null);
  editingFolderRowVersion = signal(0);
  expandedFolders = signal<Set<string>>(new Set()); // Track expanded folder IDs

  constructor() {
    // Watch currentView changes and focus input when modal opens
    effect(() => {
      const view = this.currentView();
      if (view === 'add_folder' || view === 'edit_folder') {
        setTimeout(() => {
          this.folderNameInput?.nativeElement?.focus();
        }, 50);
      }
    });
  }

  // Computed signals
  breadcrumbLabel = computed(() => {
    const folder = this.selectedFolder();
    if (!folder) return 'Kho tài liệu thiết bị';
    return getBreadcrumbLabel(folder.id, this.flatFolderList());
  });

  breadcrumbPath = computed(() => {
    const folder = this.selectedFolder();
    if (!folder) return [];
    return findBreadcrumbPath(folder?.id ?? null, this.flatFolderList());
  });

  totalPages = computed(() => {
    const total = this.totalDocuments();
    const size = this.pageSize();
    return size > 0 ? Math.ceil(total / size) : 0;
  });

  ngOnInit() {
    this.loadFolderTree();
  }

  private selectDefaultFolder(flatList: FolderNode[]) {
    const rootFolders = flatList.filter(f => !f.parentId);
    if (rootFolders.length > 0) {
      this.selectFolder(rootFolders[0]);
    }
  }

  // ===== FOLDER OPERATIONS =====

  private loadFolderTree() {
    this.loadingTree.set(true);
    this.documentService.getFolderTree().subscribe({
      next: (folders) => {
        // Keep flat list for breadcrumb calculation
        this.flatFolderList.set(folders);
        // Build tree structure
        const treeStructure = convertFlatToTree(folders);
        this.folderTree.set(treeStructure);
        this.loadingTree.set(false);
        // Auto-select the first (root) folder
        this.selectDefaultFolder(folders);
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải cây thư mục',
        });
        this.loadingTree.set(false);
      },
    });
  }

  selectFolder(folder: FolderNode) {
    this.selectedFolder.set(folder);
    this.page.set(1);
    this.loadDocuments();
  }

  toggleFolderExpand(folder: FolderNode, event: Event) {
    event.stopPropagation();
    if (!folder.children || folder.children.length === 0) {
      // No children, just select
      this.selectFolder(folder);
      return;
    }

    const expanded = this.expandedFolders();
    if (expanded.has(folder.id)) {
      expanded.delete(folder.id);
    } else {
      expanded.add(folder.id);
    }
    this.expandedFolders.set(new Set(expanded));

    // Also select the folder
    this.selectFolder(folder);
  }

  onAddFolder() {
    this.currentView.set('add_folder');
    this.folderFormName.set('');
    this.editingFolderId.set(null);
  }

  onUploadDocuments() {
    this.currentView.set('upload');
    this.showFolderMenu.set(false);
  }

  onDownloadFolder() {
    const folder = this.selectedFolder();
    if (!folder) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Cảnh báo',
        detail: 'Vui lòng chọn thư mục trước',
      });
      return;
    }

    this.messageService.add({
      severity: 'info',
      summary: 'Thông báo',
      detail: 'Đang tạo ZIP... (tính năng sắp có)',
    });
    this.showFolderMenu.set(false);
  }

  onFileUploaded(event: any) {
    this.messageService.add({
      severity: 'success',
      summary: 'Thành công',
      detail: 'Upload tài liệu thành công',
    });
    this.currentView.set('list');
    this.loadDocuments();
  }

  onScannedFile(file: File): void {
    this.uploadZone?.ingestFile(file, UPLOAD_SOURCE.SCAN);
  }

  onUploadError(event: { fileName: string; error: string }) {
    this.messageService.add({
      severity: 'error',
      summary: 'Lỗi upload',
      detail: `${event.fileName}: ${event.error}`,
    });
  }

  onEditFolder(folder: FolderNode) {
    this.editingFolderId.set(folder.id);
    this.editingFolderRowVersion.set(0);
    this.folderFormName.set(folder.name);
    this.currentView.set('edit_folder');
  }

  onSaveFolder() {
    const name = this.folderFormName().trim();
    if (!name) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Cảnh báo',
        detail: 'Tên thư mục không được để trống',
      });
      return;
    }

    this.savingFolder.set(true);
    const isEdit = this.editingFolderId() !== null;

    if (isEdit) {
      const updateReq: UpdateFolderRequest = {
        name,
        rowVersion: this.editingFolderRowVersion() || 0,
      };
      this.documentService.updateFolder(this.editingFolderId()!, updateReq).subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: 'Cập nhật thư mục thành công',
          });
          this.currentView.set('list');
          this.loadFolderTree();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err.error?.message || 'Cập nhật thư mục thất bại',
          });
          this.savingFolder.set(false);
        },
      });
    } else {
      const createReq: CreateFolderRequest = {
        name,
        parentId: this.selectedFolder()?.id ?? null,
      };
      this.documentService.createFolder(createReq).subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: 'Tạo thư mục thành công',
          });
          this.currentView.set('list');
          this.loadFolderTree();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err.error?.message || 'Tạo thư mục thất bại',
          });
          this.savingFolder.set(false);
        },
      });
    }
  }

  onDeleteFolder(folder: FolderNode) {
    this.deleteTargetFolder.set(folder);
    this.showDeleteFolderConfirm.set(true);
  }

  onConfirmDeleteFolder() {
    const folder = this.deleteTargetFolder();
    if (!folder) return;

    this.deletingFolder.set(true);
    this.documentService.deleteFolder(folder.id).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: `Đã xóa thư mục "${folder.name}" thành công`,
        });
        this.showDeleteFolderConfirm.set(false);
        this.deleteTargetFolder.set(null);
        this.deletingFolder.set(false);
        this.loadFolderTree();
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err.error?.message || 'Xóa thư mục thất bại',
        });
        this.deletingFolder.set(false);
      },
    });
  }

  onCancelDeleteFolder() {
    this.showDeleteFolderConfirm.set(false);
    this.deleteTargetFolder.set(null);
  }

  // ===== DOCUMENT OPERATIONS =====

  private loadDocuments() {
    this.loadingDocuments.set(true);
    const filter: DocumentFilter = {
      folderId: this.selectedFolder()?.id,
      page: this.page(),
      pageSize: this.pageSize(),
    };

    this.documentService.getDocuments(filter).subscribe({
      next: (response) => {
        this.documents.set(response.items);
        this.totalDocuments.set(response.totalCount);
        this.loadingDocuments.set(false);
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải danh sách tài liệu',
        });
        this.loadingDocuments.set(false);
      },
    });
  }

  onPageChange(event: any) {
    this.page.set(event.page + 1);
    this.pageSize.set(event.rows);
    this.loadDocuments();
  }

  prevPage() {
    if (this.page() > 1) {
      this.page.update(p => p - 1);
      this.loadDocuments();
    }
  }

  nextPage() {
    if (this.page() < this.totalPages()) {
      this.page.update(p => p + 1);
      this.loadDocuments();
    }
  }

  goToPage(page: number | string) {
    const p = Number(page);
    if (p >= 1 && p <= this.totalPages()) {
      this.page.set(p);
      this.loadDocuments();
    }
  }

  onPageSizeChange(event: Event) {
    const value = Number((event.target as HTMLSelectElement).value);
    this.pageSize.set(value);
    this.page.set(1);
    this.loadDocuments();
  }

  formatFileSize(bytes?: number): string {
    if (!bytes || bytes <= 0) return '-';
    const units = ['B', 'KB', 'MB', 'GB'];
    let size = bytes;
    let unitIndex = 0;
    while (size >= 1024 && unitIndex < units.length - 1) {
      size /= 1024;
      unitIndex++;
    }
    return `${size.toFixed(unitIndex === 0 ? 0 : 1)} ${units[unitIndex]}`;
  }

  formatDate(value?: Date | string): string {
    if (!value) return '-';
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) return '-';
    return date.toLocaleString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  getFileIcon(fileName: string): string {
    const ext = fileName.split('.').pop()?.toLowerCase() ?? '';
    if (['jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp'].includes(ext)) return 'pi-image';
    if (['pdf'].includes(ext)) return 'pi-file-pdf';
    if (['doc', 'docx'].includes(ext)) return 'pi-file-word';
    if (['xls', 'xlsx'].includes(ext)) return 'pi-file-excel';
    return 'pi-file';
  }

  trackByDocumentId(index: number, doc: Document): string {
    return doc.id;
  }

  onDeleteDocument(doc: Document) {
    this.deleteTargetDocument.set(doc);
    this.showDeleteDocumentConfirm.set(true);
  }

  isDownloadingDocument(docId: string): boolean {
    return this.downloadingDocumentIds().has(docId);
  }

  onDownloadDocument(doc: Document) {
    if (!doc.latestVersionId) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Không thể tải',
        detail: 'Tài liệu chưa có phiên bản file để tải xuống.',
      });
      return;
    }

    const downloading = new Set(this.downloadingDocumentIds());
    downloading.add(doc.id);
    this.downloadingDocumentIds.set(downloading);

    this.fileDownloadService.downloadFile(doc.latestVersionId, doc.name)
      .then(() => {
        this.messageService.add({
          severity: 'success',
          summary: 'Đang tải',
          detail: `Đã bắt đầu tải "${doc.name}"`,
        });
      })
      .catch((error: unknown) => {
        const message = error instanceof Error ? error.message : 'Không thể tải file';
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi tải file',
          detail: `${doc.name}: ${message}`,
        });
      })
      .finally(() => {
        const next = new Set(this.downloadingDocumentIds());
        next.delete(doc.id);
        this.downloadingDocumentIds.set(next);
      });
  }

  onConfirmDeleteDocument() {
    const doc = this.deleteTargetDocument();
    if (!doc) return;

    this.deletingDocument.set(true);
    this.documentService.deleteDocument(doc.id).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: `Đã xóa tài liệu "${doc.name}" thành công`,
        });
        this.showDeleteDocumentConfirm.set(false);
        this.deleteTargetDocument.set(null);
        this.deletingDocument.set(false);
        this.loadDocuments();
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err.error?.message || 'Xóa tài liệu thất bại',
        });
        this.deletingDocument.set(false);
      },
    });
  }

  onCancelDeleteDocument() {
    this.showDeleteDocumentConfirm.set(false);
    this.deleteTargetDocument.set(null);
  }

  onCancelFolder() {
    this.currentView.set('list');
  }

  toggleFolderMenu(event: any) {
    this.showFolderMenu.set(!this.showFolderMenu());
  }

  trackByFolderId(index: number, folder: FolderNode): string {
    return folder.id;
  }
}
