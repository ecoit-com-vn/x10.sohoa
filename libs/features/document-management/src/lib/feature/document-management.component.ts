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
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import {
  DeleteConfirmDialogComponent,
  EcoPaginatorComponent,
  WfBreadcrumbComponent
} from '@sohoa.frontend/shared/layout';
import { FormsModule } from '@angular/forms';
import { MessageService, MenuItem } from 'primeng/api';
import { Menu, MenuModule } from 'primeng/menu';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { DatePickerModule } from 'primeng/datepicker';
import { PaginatorModule } from 'primeng/paginator';
import { FileUploadZoneComponent, FileDownloadService, ScannerPanelComponent, UPLOAD_SOURCE, extractApiErrorMessage } from '@sohoa.frontend/features/equipment';
import { finalize, lastValueFrom } from 'rxjs';
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
  DocumentVersion,
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
    EcoPaginatorComponent,
    MenuModule,
    FileUploadZoneComponent,
    ScannerPanelComponent,
    WfBreadcrumbComponent,
    DeleteConfirmDialogComponent,
    DatePickerModule,
  ],
  templateUrl: './document-management.component.html',
  styleUrl: './document-management.component.css',
})
export class DocumentManagementComponent implements OnInit {
  private documentService = inject(DocumentManagementService);
  private messageService = inject(MessageService);
  private fileDownloadService = inject(FileDownloadService);
  private sanitizer = inject(DomSanitizer);

  @ViewChild('folderNameInput') folderNameInput?: ElementRef<HTMLInputElement>;
  @ViewChild('uploadZone') uploadZone?: FileUploadZoneComponent;
  @ViewChild('scannerPanel') scannerPanel?: ScannerPanelComponent;
  @ViewChild('quickNewVersionFileInput') quickNewVersionFileInput?: ElementRef<HTMLInputElement>;

  readonly UPLOAD_SOURCE = UPLOAD_SOURCE;
  scanInProgress = signal(false);

  // ===== PREVIEW SIGNALS =====
  showPreviewDialog = signal(false);
  previewLoading = signal(false);
  previewTitle = signal('');
  previewVersionId = signal('');
  previewTargetDoc = signal<Document | null>(null);
  previewBlobUrl = signal<string | null>(null);
  previewFileType = signal<'pdf' | 'image' | 'unsupported'>('unsupported');

  // ===== SIGNALS =====
  currentView = signal<ViewMode>('list');
  uploadMode = signal<FolderUploadMode>('web');
  folderTree = signal<FolderNode[]>([]);
  flatFolderList = signal<FolderNode[]>([]); // Keep flat list for breadcrumb/search
  selectedFolder = signal<FolderNode | null>(null);
  documents = signal<Document[]>([]);
  first = signal(0);
  rows = signal(10);
  folderFirst = signal(0);
  folderRows = signal(10);
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
  // Chuẩn hóa tên thư mục hiển thị trong popup xóa dùng chung.
  readonly deleteTargetFolderLabel = computed(() => this.deleteTargetFolder()?.name ?? '');
  // Chuẩn hóa tên tài liệu hiển thị trong popup xóa dùng chung.
  readonly deleteTargetDocumentLabel = computed(() => this.deleteTargetDocument()?.name ?? '');

  // Form states
  folderFormName = signal('');
  editingFolderId = signal<string | null>(null);
  editingFolderRowVersion = signal(0);
  expandedFolders = signal<Set<string>>(new Set()); // Track expanded folder IDs

  // Search Filter states (input bindings)
  filterKeyword = signal('');
  filterCreator = signal('');
  filterStartDate = signal('');
  filterEndDate = signal('');

  // Applied Filter states (actually used for queries)
  appliedKeyword = signal('');
  appliedCreator = signal('');
  appliedStartDate = signal('');
  appliedEndDate = signal('');

  // Sort states
  sortField = signal<string>('createdDate');
  sortOrder = signal<'asc' | 'desc'>('desc');

  // Document Edit states
  showEditDocument = signal(false);
  documentFormName = signal('');
  editingDocumentId = signal<string | null>(null);
  editingDocumentRowVersion = signal(0);
  savingDocument = signal(false);

  folderActionMenuItems: MenuItem[] = [];
  documentActionMenuItems: MenuItem[] = [];

  // Document Version states
  showHistoryDialog = signal(false);
  documentVersions = signal<DocumentVersion[]>([]);
  versionSearchQuery = signal('');
  filteredDocumentVersions = computed(() => {
    const query = this.versionSearchQuery().trim().toLowerCase();
    const versions = this.documentVersions();
    if (!query) return versions;

    return versions.filter(ver => {
      const versionStr = `v${ver.versionNumber}`.toLowerCase();
      const versionNumberStr = ver.versionNumber.toString();
      const creator = (ver.createdByName || ver.createdBy || 'Hệ thống').toLowerCase();
      
      let sourceStr = 'Khác';
      if (ver.uploadSource === 1) sourceStr = 'Thư mục';
      else if (ver.uploadSource === 2) sourceStr = 'Scan';
      else if (ver.uploadSource === 3) sourceStr = 'Web';
      sourceStr = sourceStr.toLowerCase();

      return versionStr.includes(query) ||
             versionNumberStr.includes(query) ||
             creator.includes(query) ||
             sourceStr.includes(query);
    });
  });
  historyTargetDocument = signal<Document | null>(null);
  loadingVersions = signal(false);
  rollingBack = signal(false);
  deletingVersion = signal(false);
  showDeleteVersionConfirm = signal(false);
  deleteTargetVersion = signal<DocumentVersion | null>(null);

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
    
    // Lọc theo cấu trúc cây trước
    let list = [];
    if (!selected) {
      list = flat.filter(f => !f.parentId);
    } else {
      list = flat.filter(f => f.parentId === selected.id);
    }

    // Áp dụng bộ lọc tìm kiếm client-side cho thư mục con
    const keyword = this.appliedKeyword().trim().toLowerCase();
    const creator = this.appliedCreator().trim().toLowerCase();
    const startDate = this.appliedStartDate();
    const endDate = this.appliedEndDate();

    if (keyword) {
      list = list.filter(f => f.name.toLowerCase().includes(keyword));
    }
    if (creator) {
      list = list.filter(f => f.createdBy && f.createdBy.toLowerCase().includes(creator));
    }
    if (startDate) {
      const start = new Date(startDate);
      list = list.filter(f => f.createdDate && new Date(f.createdDate) >= start);
    }
    if (endDate) {
      const end = new Date(endDate);
      end.setHours(23, 59, 59, 999);
      list = list.filter(f => f.createdDate && new Date(f.createdDate) <= end);
    }

    // Áp dụng Sort cho thư mục con (Client-side)
    const field = this.sortField();
    const order = this.sortOrder();
    
    list.sort((a, b) => {
      let compare = 0;
      if (field === 'name') {
        compare = a.name.localeCompare(b.name, 'vi');
      } else if (field === 'createdDate') {
        const dateA = a.createdDate ? new Date(a.createdDate).getTime() : 0;
        const dateB = b.createdDate ? new Date(b.createdDate).getTime() : 0;
        compare = dateA - dateB;
      } else if (field === 'createdBy') {
        const creatorA = a.createdBy || '';
        const creatorB = b.createdBy || '';
        compare = creatorA.localeCompare(creatorB, 'vi');
      }
      return order === 'asc' ? compare : -compare;
    });

    return list;
  });

  folderTotalRecords = computed(() => this.subFolders().length);
  paginatedSubFolders = computed(() => {
    const folders = this.subFolders();
    const start = this.folderFirst();
    const end = start + this.folderRows();
    return folders.slice(start, end);
  });

  onSearch() {
    this.appliedKeyword.set(this.filterKeyword());
    this.appliedCreator.set(this.filterCreator());
    this.appliedStartDate.set(this.filterStartDate());
    this.appliedEndDate.set(this.filterEndDate());
    this.first.set(0); // Reset page to 1
    this.folderFirst.set(0);
    this.loadDocuments();
  }

  onResetFilters() {
    this.filterKeyword.set('');
    this.filterCreator.set('');
    this.filterStartDate.set('');
    this.filterEndDate.set('');

    this.appliedKeyword.set('');
    this.appliedCreator.set('');
    this.appliedStartDate.set('');
    this.appliedEndDate.set('');
    
    this.sortField.set('createdDate');
    this.sortOrder.set('desc');
    
    this.first.set(0); // Reset page to 1
    this.folderFirst.set(0);
    this.loadDocuments();
  }

  onSort(field: string) {
    if (this.sortField() === field) {
      this.sortOrder.set(this.sortOrder() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortField.set(field);
      this.sortOrder.set('asc');
    }
    this.first.set(0); // Reset page to 1
    this.folderFirst.set(0);
    this.loadDocuments();
  }

  totalItems = computed(() => this.folderTotalRecords() + this.totalDocuments());

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
    this.folderFirst.set(0);
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
    // Chỉ hiển thị toast, không đóng modal và loadDocuments tại đây để tránh race condition khi tải nhiều file
    this.messageService.add({
      severity: 'success',
      summary: 'Tải lên thành công',
      detail: `Đã lưu tệp: ${event.fileName}`,
    });
  }

  onAllUploadsFinished(event: { successCount: number; errorCount: number }) {
    if (event.successCount > 0) {
      this.messageService.add({
        severity: 'success',
        summary: 'Hoàn tất tải lên',
        detail: `Đã tải lên thành công ${event.successCount} tài liệu.`,
      });
      this.loadDocuments();
    }

    if (event.errorCount > 0) {
      this.messageService.add({
        severity: 'error',
        summary: 'Tải lên có lỗi',
        detail: `Có ${event.errorCount} tài liệu gặp lỗi khi tải lên.`,
      });
    }

    // Đóng popup và quay lại màn hình danh sách khi hàng đợi hoàn thành
    this.currentView.set('list');
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
        label: 'Xem trước tài liệu',
        title: 'Xem trước tài liệu',
        icon: 'pi pi-eye color-blue',
        disabled: !doc.latestVersionId,
        command: () => this.onPreviewDocument(doc),
      },
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
        label: 'Tải phiên bản mới',
        title: 'Tải phiên bản mới',
        icon: 'pi pi-upload color-blue',
        disabled: this.uploadingNewVersion(),
        command: () => this.onOpenQuickNewVersionUpload(doc),
      },
      {
        label: 'Lịch sử phiên bản',
        title: 'Lịch sử phiên bản',
        icon: 'pi pi-history color-blue',
        disabled: !doc.latestVersionId,
        command: () => this.onViewHistory(doc),
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
    // Chặn target không hợp lệ hoặc request xóa bị gửi trùng.
    if (!folder || this.deletingFolder()) return;

    this.deletingFolder.set(true);
    this.documentService.deleteFolder(folder.id).pipe(
      finalize(() => this.deletingFolder.set(false)),
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: `Đã xóa thư mục "${folder.name}" thành công`,
        });
        this.showDeleteFolderConfirm.set(false);
        this.deleteTargetFolder.set(null);

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
      },
    });
  }

  onCancelDeleteFolder() {
    // Không đóng popup khi request xóa đang được xử lý.
    if (this.deletingFolder()) return;

    this.showDeleteFolderConfirm.set(false);
    this.deleteTargetFolder.set(null);
  }

  // ===== DOCUMENT OPERATIONS =====

  private loadDocuments() {
    this.loadingDocuments.set(true);
    const filter: DocumentFilter = {
      folderId: this.selectedFolder()?.id,
      keyword: this.appliedKeyword() ? this.appliedKeyword() : undefined,
      createdBy: this.appliedCreator() ? this.appliedCreator() : undefined,
      startDate: this.appliedStartDate() ? this.appliedStartDate() : undefined,
      endDate: this.appliedEndDate() ? this.appliedEndDate() : undefined,
      sortField: this.sortField(),
      sortOrder: this.sortOrder(),
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

  onFolderPageChange(event: any) {
    this.folderFirst.set(event.first);
    this.folderRows.set(event.rows);
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
    // Chặn target không hợp lệ hoặc request xóa bị gửi trùng.
    if (!doc || this.deletingDocument()) return;

    this.deletingDocument.set(true);
    this.documentService.deleteDocument(doc.id).pipe(
      finalize(() => this.deletingDocument.set(false)),
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: `Đã xóa tài liệu "${doc.name}" thành công`,
        });
        this.showDeleteDocumentConfirm.set(false);
        this.deleteTargetDocument.set(null);
        this.loadDocuments();
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err.error?.message || 'Xóa tài liệu thất bại',
        });
      },
    });
  }

  onCancelDeleteDocument() {
    // Không đóng popup khi request xóa đang được xử lý.
    if (this.deletingDocument()) return;

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

  // ===== DOCUMENT VERSION LOGIC =====

  uploadingNewVersion = signal(false);

  onOpenQuickNewVersionUpload(doc: Document): void {
    const fileInput = this.quickNewVersionFileInput?.nativeElement;
    if (!fileInput) return;

    this.historyTargetDocument.set(doc);
    fileInput.value = '';
    fileInput.click();
  }

  onViewHistory(doc: Document) {
    this.historyTargetDocument.set(doc);
    this.showHistoryDialog.set(true);
    this.versionSearchQuery.set('');
    this.loadDocumentVersions(doc.id);
  }

  onUploadNewVersionSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    const doc = this.historyTargetDocument();
    if (!file || !doc) return;

    const folderId = this.selectedFolder()?.id || doc.folderId || '';
    this.uploadingNewVersion.set(true);

    const LARGE_FILE_THRESHOLD = 10 * 1024 * 1024; // 10MB

    if (file.size > LARGE_FILE_THRESHOLD) {
      this.uploadLargeNewVersionFile(doc.id, doc.name, file, input);
    } else {
      this.documentService.uploadNewVersion(doc.id, file, folderId, UPLOAD_SOURCE.WEB)
        .pipe(finalize(() => {
          this.uploadingNewVersion.set(false);
          input.value = '';
        }))
        .subscribe({
          next: () => {
            this.messageService.add({
              severity: 'success',
              summary: 'Thành công',
              detail: `Đã tải lên phiên bản mới cho tài liệu "${doc.name}"`,
            });
            this.loadDocuments();
            this.loadDocumentVersions(doc.id);
          },
          error: (err) => {
            this.messageService.add({
              severity: 'error',
              summary: 'Lỗi',
              detail: extractApiErrorMessage(err, 'Upload phiên bản mới thất bại'),
            });
          }
        });
    }
  }

  private uploadLargeNewVersionFile(documentId: string, docName: string, file: File, input: HTMLInputElement) {
    this.documentService.initiateNewVersionChunkedUpload(documentId, file.name, file.size)
      .subscribe({
        next: async (session) => {
          const { uploadId, chunkSize, totalChunks } = session;
          const parts: Array<{ chunkNumber: number; eTag: string }> = [];

          try {
            for (let i = 0; i < totalChunks; i++) {
              const start = i * chunkSize;
              const end = Math.min(file.size, start + chunkSize);
              const chunk = file.slice(start, end);
              const chunkNumber = i + 1;

              const res = await lastValueFrom(
                this.documentService.uploadNewVersionChunk(documentId, uploadId, chunkNumber, chunk)
              );
              parts.push({ chunkNumber: res.chunkNumber, eTag: res.eTag });
            }

            await lastValueFrom(
              this.documentService.completeNewVersionChunkedUpload(documentId, uploadId, parts)
            );

            this.uploadingNewVersion.set(false);
            input.value = '';
            this.messageService.add({
              severity: 'success',
              summary: 'Thành công',
              detail: `Đã tải lên phiên bản mới (file lớn) cho tài liệu "${docName}"`,
            });
            this.loadDocuments();
            this.loadDocumentVersions(documentId);
          } catch (err) {
            this.documentService.abortNewVersionChunkedUpload(documentId, uploadId).subscribe();
            this.uploadingNewVersion.set(false);
            input.value = '';
            this.messageService.add({
              severity: 'error',
              summary: 'Lỗi',
              detail: extractApiErrorMessage(err, 'Upload phiên bản mới file lớn thất bại'),
            });
          }
        },
        error: (err) => {
          this.uploadingNewVersion.set(false);
          input.value = '';
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: extractApiErrorMessage(err, 'Khởi tạo upload phiên bản mới thất bại'),
          });
        }
      });
  }

  loadDocumentVersions(documentId: string) {
    this.loadingVersions.set(true);
    this.documentService.getDocumentVersions(documentId)
      .pipe(finalize(() => this.loadingVersions.set(false)))
      .subscribe({
        next: (versions) => {
          this.documentVersions.set(versions);
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err.error?.message || 'Không thể lấy lịch sử phiên bản tài liệu',
          });
        }
      });
  }

  onRollbackVersion(version: DocumentVersion) {
    const doc = this.historyTargetDocument();
    if (!doc) return;

    this.rollingBack.set(true);
    this.documentService.rollbackDocumentVersion(version.id)
      .pipe(finalize(() => this.rollingBack.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `Đã khôi phục tài liệu về phiên bản ${version.versionNumber}`,
          });
          this.loadDocuments();
          this.loadDocumentVersions(doc.id);
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err.error?.message || 'Khôi phục phiên bản thất bại',
          });
        }
      });
  }

  onDeleteVersion(version: DocumentVersion) {
    const versions = this.documentVersions();
    const isLatest = versions.length > 0 && versions[0].id === version.id;
    if (isLatest) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Không thể xóa',
        detail: 'Phiên bản đang áp dụng không thể bị xóa',
      });
      return;
    }
    this.deleteTargetVersion.set(version);
    this.showDeleteVersionConfirm.set(true);
  }

  onConfirmDeleteVersion() {
    const version = this.deleteTargetVersion();
    const doc = this.historyTargetDocument();
    if (!version || !doc) return;

    this.deletingVersion.set(true);
    this.documentService.deleteDocumentVersion(version.id)
      .pipe(finalize(() => this.deletingVersion.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `Đã xóa phiên bản số ${version.versionNumber} của tài liệu`,
          });
          this.showDeleteVersionConfirm.set(false);
          this.deleteTargetVersion.set(null);
          this.loadDocuments();
          this.loadDocumentVersions(doc.id);
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err.error?.message || 'Xóa phiên bản thất bại',
          });
        }
      });
  }

  onCancelDeleteVersion() {
    this.showDeleteVersionConfirm.set(false);
    this.deleteTargetVersion.set(null);
  }

  onDownloadVersion(version: DocumentVersion) {
    const doc = this.historyTargetDocument();
    if (!doc) return;

    this.fileDownloadService.downloadFile(version.id, doc.name)
      .then(() => {
        this.messageService.add({
          severity: 'success',
          summary: 'Tải xuống',
          detail: `Bắt đầu tải phiên bản ${version.versionNumber} của tài liệu`,
        });
      })
      .catch((err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err.message || 'Không thể tải phiên bản này',
        });
      });
  }

  onCloseHistoryDialog() {
    this.showHistoryDialog.set(false);
    this.documentVersions.set([]);
    this.versionSearchQuery.set('');
    this.historyTargetDocument.set(null);
  }

  // ===== PREVIEW LOGIC =====

  async onPreviewDocument(doc: Document, versionId?: string) {
    const targetVersionId = versionId || doc.latestVersionId;
    if (!targetVersionId) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Cảnh báo',
        detail: 'Tài liệu chưa có phiên bản file để xem trước.',
      });
      return;
    }

    this.cleanupPreview();
    this.previewTitle.set(doc.name);
    this.previewTargetDoc.set(doc);
    this.previewVersionId.set(targetVersionId);
    this.showPreviewDialog.set(true);
    this.previewLoading.set(true);

    const ext = doc.name.split('.').pop()?.toLowerCase() ?? '';
    if (['pdf'].includes(ext)) {
      this.previewFileType.set('pdf');
    } else if (['jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp', 'svg'].includes(ext)) {
      this.previewFileType.set('image');
    } else {
      this.previewFileType.set('unsupported');
    }

    try {
      const blobUrl = await this.fileDownloadService.getPreviewBlobUrl(targetVersionId);
      this.previewBlobUrl.set(blobUrl);
    } catch (error: unknown) {
      const msg = extractApiErrorMessage(error, 'Không thể tải file xem trước');
      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi xem trước',
        detail: msg,
      });
      this.closePreviewDialog();
    } finally {
      this.previewLoading.set(false);
    }
  }

  getSafePreviewUrl(): SafeResourceUrl | null {
    const url = this.previewBlobUrl();
    return url ? this.sanitizer.bypassSecurityTrustResourceUrl(url) : null;
  }

  cleanupPreview(): void {
    const url = this.previewBlobUrl();
    if (url) {
      this.fileDownloadService.revokePreviewBlobUrl(url);
      this.previewBlobUrl.set(null);
    }
  }

  closePreviewDialog(): void {
    this.cleanupPreview();
    this.showPreviewDialog.set(false);
    this.previewTargetDoc.set(null);
    this.previewVersionId.set('');
  }
}
