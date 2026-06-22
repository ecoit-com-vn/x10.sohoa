import {
  Component,
  Input,
  OnInit,
  OnDestroy,
  inject,
  signal,
  computed,
  effect,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { SignalRService, DigitizationProgressEvent } from '@sohoa.frontend/shared/core';
import {
  Subject,
  debounceTime,
  distinctUntilChanged,
  finalize,
  takeUntil,
  interval,
  filter,
} from 'rxjs';
import {
  DossierDocumentItem,
  DossierDocumentService,
  DocumentOcrProgress,
} from '../../data-access/dossier-document.service';
import {
  formatDocumentDate,
  formatDocumentFileSize,
  getDocumentFileIcon,
} from '../../utils/document-display.util';
import {
  getExtractionColumnState,
  getOcrBarPercent,
  getRetryProcessOption,
  isActiveDigitizationStatus,
  isOcrBarActive,
  isOcrBarFailed,
  canEditDossierDocument,
  canRetryDigitization,
  isRetryingDigitization,
} from '../../utils/dossier-digitization.util';
import { DossierUploadMenuComponent, DossierUploadAction } from '../../components/dossier-upload-menu/dossier-upload-menu.component';
import { DossierFolderPickerDialogComponent } from '../../components/dossier-folder-picker-dialog/dossier-folder-picker-dialog.component';
import { DossierDirectUploadDialogComponent } from '../../components/dossier-direct-upload-dialog/dossier-direct-upload-dialog.component';
import { DossierDocumentEditDialogComponent } from '../../components/dossier-document-edit-dialog/dossier-document-edit-dialog.component';

@Component({
  selector: 'app-dossier-documents-tab',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    ButtonModule,
    DossierUploadMenuComponent,
    DossierFolderPickerDialogComponent,
    DossierDirectUploadDialogComponent,
    DossierDocumentEditDialogComponent,
  ],
  templateUrl: './dossier-documents-tab.component.html',
  styleUrl: './dossier-documents-tab.component.scss',
})
export class DossierDocumentsTabComponent implements OnInit, OnDestroy {
  private documentService = inject(DossierDocumentService);
  private messageService = inject(MessageService);
  private signalRService = inject(SignalRService);
  private destroy$ = new Subject<void>();
  private search$ = new Subject<string>();
  private signalRConnected = false;
  private lastSignalRDossierId: string | null = null;

  @Input({ required: true }) dossierId!: string;
  @Input() canEdit = false;
  @Input() hasFormTemplate = false;
  @Input() formId: string | null = null;

  documents = signal<DossierDocumentItem[]>([]);
  loading = signal(false);
  deleting = signal(false);
  page = signal(1);
  pageSize = signal(10);
  totalDocuments = signal(0);
  searchKeyword = signal('');
  downloadingIds = signal<Set<string>>(new Set());
  retryingIds = signal<Set<string>>(new Set());

  showDeleteConfirm = signal(false);
  deleteTarget = signal<DossierDocumentItem | null>(null);

  showFolderPicker = signal(false);
  showDirectUpload = signal(false);
  uploadSource = signal(3);
  uploadDialogTitle = signal('Upload trực tiếp vào hồ sơ');

  showEditDocument = signal(false);
  editTarget = signal<DossierDocumentItem | null>(null);

  totalPages = computed(() => {
    const total = this.totalDocuments();
    const size = this.pageSize();
    return total > 0 ? Math.ceil(total / size) : 0;
  });

  hasActiveDigitization = computed(() =>
    this.documents().some((doc) => isActiveDigitizationStatus(doc.ocrProgress?.status))
  );

  getOcrBarPercent = getOcrBarPercent;
  isOcrBarActive = isOcrBarActive;
  isOcrBarFailed = isOcrBarFailed;
  getExtractionColumnState = getExtractionColumnState;
  canRetryDigitization = canRetryDigitization;
  isRetryingDigitization = isRetryingDigitization;

  constructor() {
    effect(() => {
      const id = this.dossierId;
      if (id) {
        this.loadDocuments();
        void this.switchDossierSignalRGroup(id);
      }
    });
  }

  ngOnInit(): void {
    this.search$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.page.set(1);
        this.loadDocuments();
      });

    this.setupDigitizationSignalR();

    interval(15000)
      .pipe(
        takeUntil(this.destroy$),
        filter(() =>
          !!this.dossierId &&
          this.hasActiveDigitization() &&
          !this.loading() &&
          !this.signalRConnected
        )
      )
      .subscribe(() => this.loadDocuments(true));
  }

  ngOnDestroy(): void {
    if (this.lastSignalRDossierId) {
      void this.signalRService.leaveDossierGroup(this.lastSignalRDossierId);
    }
    this.destroy$.next();
    this.destroy$.complete();
  }

  private setupDigitizationSignalR(): void {
    this.signalRService.digitizationProgress$
      .pipe(takeUntil(this.destroy$))
      .subscribe((event) => this.applyDigitizationProgress(event));
  }

  private async switchDossierSignalRGroup(dossierId: string): Promise<void> {
    if (this.lastSignalRDossierId && this.lastSignalRDossierId !== dossierId) {
      await this.signalRService.leaveDossierGroup(this.lastSignalRDossierId);
    }
    try {
      await this.signalRService.ensureConnection();
      await this.signalRService.joinDossierGroup(dossierId);
      this.signalRConnected = this.signalRService.isConnected();
      this.lastSignalRDossierId = dossierId;
    } catch {
      this.signalRConnected = false;
    }
  }

  private applyDigitizationProgress(event: DigitizationProgressEvent): void {
    if (!event.dossierId || event.dossierId !== this.dossierId) return;

    const versionKey = event.documentVersionId.toLowerCase();
    this.documents.update((docs) =>
      docs.map((doc) => {
        const docVersion = doc.latestVersionId?.toLowerCase();
        if (!docVersion || docVersion !== versionKey) return doc;

        const ocrProgress: DocumentOcrProgress = {
          id: doc.ocrProgress?.id ?? '',
          documentId: event.documentId || doc.id,
          documentVersionId: event.documentVersionId,
          phase: event.phase,
          status: event.status,
          progress: event.progress,
          currentPage: event.currentPage,
          totalPages: event.totalPages,
          action: doc.ocrProgress?.action,
          processOption: doc.ocrProgress?.processOption,
          createdDate: doc.ocrProgress?.createdDate,
          modifiedDate: doc.ocrProgress?.modifiedDate,
        };

        let extractionResult = doc.extractionResult;
        if (event.extractionStatus) {
          extractionResult = {
            id: doc.extractionResult?.id ?? '',
            documentVersionId: event.documentVersionId,
            status: event.extractionStatus,
          };
        }

        return { ...doc, ocrProgress, extractionResult };
      })
    );
  }

  loadDocuments(silent = false): void {
    if (!this.dossierId) return;

    if (!silent) {
      this.loading.set(true);
    }

    this.documentService
      .getDocuments(this.dossierId, {
        keyword: this.searchKeyword(),
        page: this.page(),
        pageSize: this.pageSize(),
      })
      .pipe(finalize(() => {
        if (!silent) {
          this.loading.set(false);
        }
      }))
      .subscribe({
        next: (res) => {
          this.documents.set(res.items ?? []);
          this.totalDocuments.set(res.totalCount ?? 0);
        },
        error: () => {
          if (!silent) {
            this.messageService.add({
              severity: 'error',
              summary: 'Lỗi',
              detail: 'Không thể tải danh sách tài liệu',
            });
            this.documents.set([]);
            this.totalDocuments.set(0);
          }
        },
      });
  }

  getOcrProgress(doc: DossierDocumentItem): DocumentOcrProgress | undefined {
    return doc.ocrProgress ?? undefined;
  }

  displayCreatorName(doc: DossierDocumentItem): string {
    return doc.createdByName?.trim() || doc.createdBy?.trim() || '—';
  }

  canEditDocument = canEditDossierDocument;

  onEditDocument(doc: DossierDocumentItem): void {
    if (!doc.latestVersionId) return;
    this.editTarget.set(doc);
    this.showEditDocument.set(true);
  }

  onDocumentEdited(): void {
    this.showEditDocument.set(false);
    this.editTarget.set(null);
    this.loadDocuments();
  }

  onSearchChange(value: string): void {
    this.searchKeyword.set(value);
    this.search$.next(value);
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

  onPageSizeChange(size: number): void {
    this.pageSize.set(Number(size));
    this.page.set(1);
    this.loadDocuments();
  }

  formatSize = formatDocumentFileSize;
  formatDate = formatDocumentDate;
  fileIcon = getDocumentFileIcon;

  trackByDocumentId(_index: number, doc: DossierDocumentItem): string {
    return doc.id;
  }

  isDownloading(docId: string): boolean {
    return this.downloadingIds().has(docId);
  }

  onDownload(doc: DossierDocumentItem): void {
    if (!doc.latestVersionId) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Không thể tải',
        detail: 'Tài liệu chưa có phiên bản file.',
      });
      return;
    }

    const ids = new Set(this.downloadingIds());
    ids.add(doc.id);
    this.downloadingIds.set(ids);

    this.documentService
      .downloadFile(this.dossierId, doc.latestVersionId, doc.name)
      .then(() => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: `Đã tải "${doc.name}"`,
        });
      })
      .catch((err: unknown) => {
        const detail = err instanceof Error ? err.message : 'Không thể tải file';
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail });
      })
      .finally(() => {
        const next = new Set(this.downloadingIds());
        next.delete(doc.id);
        this.downloadingIds.set(next);
      });
  }

  onDelete(doc: DossierDocumentItem): void {
    this.deleteTarget.set(doc);
    this.showDeleteConfirm.set(true);
  }

  cancelDelete(): void {
    if (this.deleting()) return;
    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }

  confirmDelete(): void {
    const target = this.deleteTarget();
    if (!target) return;

    this.deleting.set(true);
    this.documentService
      .deleteDocument(this.dossierId, target.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `Đã xóa "${target.name}"`,
          });
          this.showDeleteConfirm.set(false);
          this.deleteTarget.set(null);
          this.loadDocuments();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể xóa tài liệu',
          });
        },
      });
  }

  refresh(): void {
    this.loadDocuments();
  }

  onUploadAction(action: DossierUploadAction): void {
    if (action === 'folder') {
      this.showFolderPicker.set(true);
    } else if (action === 'direct') {
      this.uploadSource.set(3);
      this.uploadDialogTitle.set('Upload trực tiếp vào hồ sơ');
      this.showDirectUpload.set(true);
    } else if (action === 'scan') {
      this.uploadSource.set(2);
      this.uploadDialogTitle.set('Scan tài liệu vào hồ sơ');
      this.showDirectUpload.set(true);
    }
  }

  onDocumentsAdded(): void {
    this.loadDocuments();
  }

  onDigitizationStarted(): void {
    this.loadDocuments(true);
  }

  onRetryDigitization(doc: DossierDocumentItem): void {
    if (!doc.latestVersionId || !this.canEdit) return;

    const ids = new Set(this.retryingIds());
    ids.add(doc.id);
    this.retryingIds.set(ids);

    const processOption = getRetryProcessOption(doc);
    this.documentService
      .retryDigitization(this.dossierId, doc.latestVersionId, processOption)
      .pipe(finalize(() => {
        const next = new Set(this.retryingIds());
        next.delete(doc.id);
        this.retryingIds.set(next);
      }))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Đã gửi xử lý lại',
            detail: `"${doc.name}" đang được xử lý lại`,
          });
          this.loadDocuments(true);
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể gửi xử lý lại',
          });
        },
      });
  }
}
