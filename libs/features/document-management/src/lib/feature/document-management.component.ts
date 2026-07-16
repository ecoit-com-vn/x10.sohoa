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
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { FormsModule } from '@angular/forms';
import { MenuItem, MessageService } from 'primeng/api';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { PaginatorModule } from 'primeng/paginator';
import { Menu, MenuModule } from 'primeng/menu';
import { FileUploadZoneComponent, FileDownloadService, ScannerPanelComponent, UPLOAD_SOURCE } from '@sohoa.frontend/features/equipment';
import { finalize } from 'rxjs';
import { HttpResponse } from '@angular/common/http';
import { DocumentManagementService } from '../data-access/document-management.service';
import {
  FolderNode,
  Document,
  CreateFolderRequest,
  UpdateFolderRequest,
  CreateDocumentRequest,
  UpdateDocumentRequest,
  DocumentFilter,
} from '../models/document.models';
import {
  convertFlatToTree,
  findBreadcrumbPath,
  getBreadcrumbLabel,
} from '../utils/folder-tree.util';

type ViewMode = 'list' | 'add_folder' | 'edit_folder' | 'upload';
type FolderUploadMode = 'web' | 'scan';

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
    MenuModule,
    FileUploadZoneComponent,
    ScannerPanelComponent,
    WfBreadcrumbComponent,
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
  @ViewChild('scannerPanel') scannerPanel?: ScannerPanelComponent;

  readonly UPLOAD_SOURCE = UPLOAD_SOURCE;
  scanInProgress = signal(false);

  // ===== SIGNALS =====
  currentView = signal<ViewMode>('list');
  uploadMode = signal<FolderUploadMode>('web');
  folderTree = signal<FolderNode[]>([]);
  flatFolderList = signal<FolderNode[]>([]); // Keep flat list for breadcrumb/search
  selectedFolder = signal<FolderNode | null>(null);
  documents = signal<Document[]>([]);
  first = signal(0);
  rows = signal(10);
  page = computed(() => Math.floor(this.first() / this.rows()) + 1);
  pageSize = computed(() => this.rows());
  totalDocuments = signal(0);
  loadingTree = signal(false);
  loadingDocuments = signal(false);
  deletingFolder = signal(false);
  deletingDocument = signal(false);
  savingFolder = signal(false);
  downloadingDocumentIds = signal<Set<string>>(new Set());
  downloadingFolderZip = signal(false);

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

  // Document Edit states
  showEditDocument = signal(false);
  documentFormName = signal('');
  editingDocumentId = signal<string | null>(null);
  editingDocumentRowVersion = signal(0);
  savingDocument = signal(false);

  folderActionMenuItems: MenuItem[] = [];
  documentActionMenuItems: MenuItem[] = [];

  @ViewChild('documentNameInput') documentNameInput?: ElementRef<HTMLInputElement>;

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

    // Watch showEditDocument change and focus input
    effect(() => {
      if (this.showEditDocument()) {
        setTimeout(() => {
          this.documentNameInput?.nativeElement?.focus();
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

  subFolders = computed(() => {
    const selected = this.selectedFolder();
    const flat = this.flatFolderList();
    if (!selected) {
      return flat
        .filter(f => !f.parentId)
        .sort((a, b) => a.name.localeCompare(b.name, 'vi'));
    }
    return flat
      .filter(f => f.parentId === selected.id)
      .sort((a, b) => a.name.localeCompare(b.name, 'vi'));
  });

  totalItems = computed(() => this.subFolders().length + this.totalDocuments());

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

        const current = this.selectedFolder();
        if (current) {
          const refreshed = folders.find(f => f.id === current.id) ?? null;
          this.selectedFolder.set(refreshed);
          if (refreshed) {
            this.loadDocuments();
          } else {
            this.documents.set([]);
            this.totalDocuments.set(0);
          }
        }
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
    this.first.set(0);
    this.loadDocuments();
  }

  onSelectSubFolder(folder: FolderNode) {
    this.selectFolder(folder);
    const expanded = new Set(this.expandedFolders());
    if (folder.parentId) {
      expanded.add(folder.parentId);
    }
    this.expandedFolders.set(expanded);
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
    this.savingFolder.set(false);
    this.currentView.set('add_folder');
    this.folderFormName.set('');
    this.editingFolderId.set(null);
    this.editingFolderRowVersion.set(0);
  }

  onUploadDocuments() {
    if (!this.ensureFolderSelectedForUpload()) return;
    this.uploadMode.set('web');
    this.currentView.set('upload');
    this.showFolderMenu.set(false);
  }

  onScanDocuments() {
    if (!this.ensureFolderSelectedForUpload()) return;
    this.uploadMode.set('scan');
    this.currentView.set('upload');
    this.showFolderMenu.set(false);
  }

  private ensureFolderSelectedForUpload(): boolean {
    if (this.selectedFolder()?.id) return true;
    this.messageService.add({
      severity: 'warn',
      summary: 'Cảnh báo',
      detail: 'Vui lòng chọn thư mục trước khi upload hoặc quét tài liệu',
    });
    return false;
  }

  onDownloadFolder() {
    const folder = this.selectedFolder();
    if (!folder) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Cảnh báo',
        detail: 'Vui lòng chọn thư mục trước khi tải ZIP',
      });
      return;
    }

    if (this.downloadingFolderZip()) return;

    this.downloadingFolderZip.set(true);
    this.messageService.add({
      severity: 'info',
      summary: 'Đang chuẩn bị',
      detail: 'Hệ thống đang nén thư mục, vui lòng chờ...',
    });
    this.showFolderMenu.set(false);

    this.documentService.downloadFolderZip(folder.id).subscribe({
      next: (response) => {
        const blob = response.body;
        if (!blob) {
          this.downloadingFolderZip.set(false);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không nhận được dữ liệu file ZIP',
          });
          return;
        }

        const fileName = this.resolveZipDownloadFileName(response, folder.name);
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        window.URL.revokeObjectURL(url);
        link.remove();
        this.downloadingFolderZip.set(false);
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: `Đã tải xuống file ZIP thư mục "${folder.name}"`,
        });
      },
      error: (err) => {
        this.downloadingFolderZip.set(false);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err.error?.message || 'Tạo file ZIP thư mục thất bại',
        });
      },
    });
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

  onScanInProgress(inProgress: boolean): void {
    this.scanInProgress.set(inProgress);
  }

  closeUploadModal(): void {
    if (this.scanInProgress()) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Đang quét',
        detail: 'Vui lòng hoàn tất hoặc hủy quét trên EcoScanner trước khi đóng',
      });
      return;
    }
    this.currentView.set('list');
  }

  onUploadError(event: { fileName: string; error: string }) {
    this.messageService.add({
      severity: 'error',
      summary: 'Lỗi upload',
      detail: `${event.fileName}: ${event.error}`,
    });
  }

  onEditFolder(folder: FolderNode) {
    this.savingFolder.set(false);
    this.editingFolderId.set(folder.id);
    this.editingFolderRowVersion.set(folder.rowVersion ?? 0);
    this.folderFormName.set(folder.name);
    this.currentView.set('edit_folder');
  }

  openFolderActionMenu(folder: FolderNode, event: MouseEvent, menu: Menu): void {
    this.folderActionMenuItems = [
      {
        label: 'Chỉnh sửa thư mục',
        title: 'Chỉnh sửa thư mục',
        icon: 'pi pi-pencil color-blue',
        command: () => this.onEditFolder(folder),
      },
      {
        label: 'Xóa thư mục',
        title: 'Xóa thư mục',
        icon: 'pi pi-trash color-red',
        command: () => this.onDeleteFolder(folder),
      },
    ];
    menu.toggle(event);
  }

  openDocumentActionMenu(doc: Document, event: MouseEvent, menu: Menu): void {
    this.documentActionMenuItems = [
      {
        label: 'Chỉnh sửa tài liệu',
        title: 'Chỉnh sửa tài liệu',
        icon: 'pi pi-pencil color-blue',
        command: () => this.onEditDocument(doc),
      },
      {
        label: 'Tải tài liệu',
        title: 'Tải tài liệu',
        icon: this.isDownloadingDocument(doc.id)
          ? 'pi pi-spin pi-spinner color-blue'
          : 'pi pi-download color-blue',
        disabled: !doc.latestVersionId || this.isDownloadingDocument(doc.id),
        command: () => this.onDownloadDocument(doc),
      },
      {
        label: 'Xóa tài liệu',
        title: 'Xóa tài liệu',
        icon: 'pi pi-trash color-red',
        command: () => this.onDeleteDocument(doc),
      },
    ];
    menu.toggle(event);
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
      this.documentService.updateFolder(this.editingFolderId()!, updateReq).pipe(
        finalize(() => this.savingFolder.set(false)),
      ).subscribe({
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
        },
      });
    } else {
      const createReq: CreateFolderRequest = {
        name,
        parentId: this.selectedFolder()?.id ?? null,
      };
      this.documentService.createFolder(createReq).pipe(
        finalize(() => this.savingFolder.set(false)),
      ).subscribe({
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

        if (this.selectedFolder()?.id === folder.id) {
          const parent = folder.parentId
            ? this.flatFolderList().find(f => f.id === folder.parentId) ?? null
            : null;
          this.selectedFolder.set(parent);
          this.first.set(0);
        }

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
    this.first.set(event.first);
    this.rows.set(event.rows);
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
    this.savingFolder.set(false);
    this.currentView.set('list');
  }

  toggleFolderMenu(event: any) {
    this.showFolderMenu.set(!this.showFolderMenu());
  }

  trackByFolderId(index: number, folder: FolderNode): string {
    return folder.id;
  }

  onEditDocument(doc: Document) {
    this.savingDocument.set(false);
    this.editingDocumentId.set(doc.id);
    this.editingDocumentRowVersion.set(doc.rowVersion ?? 0);
    this.documentFormName.set(doc.name);
    this.showEditDocument.set(true);
  }

  onCancelEditDocument() {
    this.savingDocument.set(false);
    this.showEditDocument.set(false);
    this.editingDocumentId.set(null);
    this.editingDocumentRowVersion.set(0);
    this.documentFormName.set('');
  }

  onSaveDocument() {
    const name = this.documentFormName().trim();
    if (!name) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Cảnh báo',
        detail: 'Tên tài liệu không được để trống',
      });
      return;
    }

    this.savingDocument.set(true);
    const updateReq: UpdateDocumentRequest = {
      name,
      rowVersion: this.editingDocumentRowVersion() || 0,
    };

    this.documentService.updateDocument(this.editingDocumentId()!, updateReq).pipe(
      finalize(() => this.savingDocument.set(false)),
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: 'Cập nhật tên tài liệu thành công',
        });
        this.showEditDocument.set(false);
        this.loadDocuments();
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err.error?.message || 'Cập nhật tên tài liệu thất bại',
        });
      },
    });
  }

  private resolveZipDownloadFileName(response: HttpResponse<Blob>, folderName: string): string {
    const disposition = response.headers.get('Content-Disposition');
    if (disposition) {
      const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
      if (utf8Match?.[1]) {
        return decodeURIComponent(utf8Match[1].trim());
      }
      const asciiMatch = /filename="?([^";]+)"?/i.exec(disposition);
      if (asciiMatch?.[1]) {
        return asciiMatch[1].trim();
      }
    }
    return `${folderName.replace(/\s+/g, '_')}.zip`;
  }
}
