import {
  Component,
  Input,
  Output,
  EventEmitter,
  inject,
  signal,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { finalize } from 'rxjs';
import {
  DocumentManagementService,
  FolderNode,
  Document,
  convertFlatToTree,
} from '@sohoa.frontend/features/document-management';
import {
  DossierDocumentService,
  DocumentTypeLookupItem,
  DigitizationExtractionScope,
} from '../../data-access/dossier-document.service';
import { FolderAllocationService } from '@sohoa.frontend/features/digitization';
import { OcrMode } from '../../utils/dossier-digitization.util';
import {
  formatDocumentDate,
  formatDocumentFileSize,
  getDocumentFileIcon,
} from '../../utils/document-display.util';

type PickerPhase = 'pick' | 'configure';

@Component({
  selector: 'app-dossier-folder-picker-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ButtonModule],
  templateUrl: './dossier-folder-picker-dialog.component.html',
  styleUrl: './dossier-folder-picker-dialog.component.scss',
})
export class DossierFolderPickerDialogComponent {
  private documentService = inject(DocumentManagementService);
  private dossierDocumentService = inject(DossierDocumentService);
  private folderAllocationService = inject(FolderAllocationService);
  private messageService = inject(MessageService);

  @Input({ required: true }) dossierId!: string;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() documentsAdded = new EventEmitter<void>();
  @Output() digitizationStarted = new EventEmitter<void>();

  phase = signal<PickerPhase>('pick');
  folderTree = signal<FolderNode[]>([]);
  flatFolderList = signal<FolderNode[]>([]);
  selectedFolder = signal<FolderNode | null>(null);
  expandedFolders = signal<Set<string>>(new Set());
  folderSearch = signal('');

  documents = signal<Document[]>([]);
  loadingTree = signal(false);
  loadingDocuments = signal(false);
  moving = signal(false);
  docSearch = signal('');
  page = signal(1);
  pageSize = signal(10);
  totalDocuments = signal(0);
  selectedDocIds = signal<Set<string>>(new Set());

  documentTypes = signal<DocumentTypeLookupItem[]>([]);
  loadingDocTypes = signal(false);
  selectedDocumentTypeId = signal('');
  ocrMode: OcrMode = 'none';

  /**
   * Phạm vi trang cần bóc tách — giống dialog upload trực tiếp, mặc định 'FirstAndLastPage' vì dữ
   * liệu cần lấy hầu như chỉ ở trang đầu/cuối. Chỉ giới hạn bước bóc tách; OCR vẫn chạy đủ trang.
   */
  extractionScope: DigitizationExtractionScope = 'FirstAndLastPage';

  totalPages = computed(() => {
    const total = this.totalDocuments();
    const size = this.pageSize();
    return total > 0 ? Math.ceil(total / size) : 0;
  });

  selectedDocumentType = computed(() =>
    this.documentTypes().find((t) => t.id === this.selectedDocumentTypeId()) ?? null
  );

  hasDocumentTypeForm = computed(() => !!this.selectedDocumentType()?.formId);

  filteredFolderTree = computed(() => {
    const keyword = this.folderSearch().trim().toLowerCase();
    if (!keyword) return this.folderTree();
    const flat = this.flatFolderList().filter((f) => f.name.toLowerCase().includes(keyword));
    return convertFlatToTree(flat);
  });

  formatSize = formatDocumentFileSize;
  formatDate = formatDocumentDate;
  fileIcon = getDocumentFileIcon;

  onShow(): void {
    this.phase.set('pick');
    this.selectedDocIds.set(new Set());
    this.docSearch.set('');
    this.page.set(1);
    this.selectedDocumentTypeId.set('');
    this.ocrMode = 'none';
    this.extractionScope = 'FirstAndLastPage';
    if (this.flatFolderList().length === 0) {
      this.loadFolderTree();
    } else if (this.selectedFolder()) {
      this.loadDocuments();
    }
  }

  close(): void {
    if (this.moving()) return;
    this.visibleChange.emit(false);
  }

  backToPick(): void {
    if (this.moving()) return;
    this.phase.set('pick');
  }

  private loadFolderTree(): void {
    this.loadingTree.set(true);
    this.folderAllocationService.getMyFolders().subscribe({
      next: (folders) => {
        const mappedFolders: FolderNode[] = folders.map((f: any) => ({
          id: f.id,
          name: f.name,
          parentId: f.parent_id || null,
          unitId: f.unit_id
        }));

        this.flatFolderList.set(mappedFolders);
        this.folderTree.set(convertFlatToTree(mappedFolders));
        this.loadingTree.set(false);
        const root = mappedFolders.find((f) => !f.parentId) ?? mappedFolders[0];
        if (root) {
          this.selectFolder(root);
        } else {
          this.selectedFolder.set(null);
          this.documents.set([]);
        }
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải cây thư mục',
        });
        this.loadingTree.set(false);
      },
    });
  }

  private loadDocumentTypes(): void {
    this.loadingDocTypes.set(true);
    this.dossierDocumentService
      .getDocumentTypesForDossier(this.dossierId)
      .pipe(finalize(() => this.loadingDocTypes.set(false)))
      .subscribe({
        next: (items) => this.documentTypes.set(items),
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể tải danh mục loại văn bản',
          });
        },
      });
  }

  selectFolder(folder: FolderNode): void {
    this.selectedFolder.set(folder);
    this.page.set(1);
    this.selectedDocIds.set(new Set());
    this.loadDocuments();
  }

  toggleFolderExpand(folder: FolderNode, event: Event): void {
    event.stopPropagation();
    const expanded = new Set(this.expandedFolders());
    if (expanded.has(folder.id)) {
      expanded.delete(folder.id);
    } else {
      expanded.add(folder.id);
    }
    this.expandedFolders.set(expanded);
    this.selectFolder(folder);
  }

  loadDocuments(): void {
    const folder = this.selectedFolder();
    if (!folder) return;

    this.loadingDocuments.set(true);
    this.documentService
      .getDocuments({
        folderId: folder.id,
        keyword: this.docSearch().trim() || undefined,
        page: this.page(),
        pageSize: this.pageSize(),
      })
      .pipe(finalize(() => this.loadingDocuments.set(false)))
      .subscribe({
        next: (res) => {
          this.documents.set(res.items ?? []);
          this.totalDocuments.set(res.totalCount ?? 0);
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể tải tài liệu trong thư mục',
          });
          this.documents.set([]);
          this.totalDocuments.set(0);
        },
      });
  }

  onDocSearchChange(value: string): void {
    this.docSearch.set(value);
    this.page.set(1);
    this.loadDocuments();
  }

  prevPage(): void {
    if (this.page() > 1) {
      this.page.update((p) => p - 1);
      this.loadDocuments();
    }
  }

  nextPage(): void {
    if (this.page() < this.totalPages()) {
      this.page.update((p) => p + 1);
      this.loadDocuments();
    }
  }

  isSelected(docId: string): boolean {
    return this.selectedDocIds().has(docId);
  }

  toggleSelect(doc: Document, event: Event): void {
    event.stopPropagation();
    const next = new Set(this.selectedDocIds());
    if (next.has(doc.id)) {
      next.delete(doc.id);
    } else {
      next.add(doc.id);
    }
    this.selectedDocIds.set(next);
  }

  toggleSelectAll(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    if (checked) {
      this.selectedDocIds.set(new Set(this.documents().map((d) => d.id)));
    } else {
      this.selectedDocIds.set(new Set());
    }
  }

  allPageSelected(): boolean {
    const docs = this.documents();
    return docs.length > 0 && docs.every((d) => this.selectedDocIds().has(d.id));
  }

  selectedCount(): number {
    return this.selectedDocIds().size;
  }

  goToConfigure(): void {
    const ids = Array.from(this.selectedDocIds());
    if (ids.length === 0) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Chưa chọn tài liệu',
        detail: 'Vui lòng chọn ít nhất một tài liệu',
      });
      return;
    }
    this.phase.set('configure');
    if (this.documentTypes().length === 0) {
      this.loadDocumentTypes();
    }
  }

  confirmMove(): void {
    const ids = Array.from(this.selectedDocIds());
    const documentTypeId = this.selectedDocumentTypeId();
    if (!documentTypeId) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Thiếu loại văn bản',
        detail: 'Vui lòng chọn loại văn bản (bắt buộc)',
      });
      return;
    }

    if (this.ocrModeNeedsForm() && !this.hasDocumentTypeForm()) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Thiếu form EAV',
        detail: 'Loại văn bản chưa gắn biểu mẫu — không thể bóc tách',
      });
      return;
    }

    this.moving.set(true);
    this.dossierDocumentService
      .moveFromFolder(this.dossierId, ids, documentTypeId)
      .pipe(finalize(() => this.moving.set(false)))
      .subscribe({
        next: (res) => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `Đã chuyển ${res.movedCount} tài liệu vào hồ sơ`,
          });
          this.documentsAdded.emit();
          this.submitOcrForMoved(res.movedDocuments ?? []);
          this.selectedDocIds.set(new Set());
          this.visibleChange.emit(false);
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể chuyển tài liệu vào hồ sơ',
          });
        },
      });
  }

  private ocrModeNeedsForm(): boolean {
    return this.ocrMode !== 'none';
  }

  private submitOcrForMoved(items: Array<{ versionId: string; name: string }>): void {
    if (this.ocrMode === 'none' || items.length === 0) return;

    let started = false;
    for (const item of items) {
      this.dossierDocumentService
        .submitDigitization(this.dossierId, item.versionId, {
          processOption: this.ocrMode,
          extractionScope: this.extractionScope,
        })
        .subscribe({
          next: () => {
            if (!started) {
              started = true;
              this.digitizationStarted.emit();
            }
            this.messageService.add({
              severity: 'info',
              summary: 'OCR',
              detail: `Đã gửi xử lý OCR cho "${item.name}"`,
            });
          },
          error: (err) => {
            this.messageService.add({
              severity: 'error',
              summary: 'Lỗi OCR',
              detail: `${item.name}: ${err?.error?.message || 'Không thể gửi OCR'}`,
            });
          },
        });
    }
  }

  trackByFolderId(_index: number, folder: FolderNode): string {
    return folder.id;
  }

  trackByDocumentId(_index: number, doc: Document): string {
    return doc.id;
  }
}
