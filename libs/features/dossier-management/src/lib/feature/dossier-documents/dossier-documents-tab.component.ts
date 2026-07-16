import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnInit,
  OnDestroy,
  OnChanges,
  SimpleChanges,
  AfterViewInit,
  inject,
  signal,
  computed,
  ViewChild,
  ElementRef,
  HostListener,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { MessageService, MenuItem } from 'primeng/api';
import { Menu, MenuModule } from 'primeng/menu';
import { SignalRService, DigitizationProgressEvent } from '../../../../../../shared/core/src/lib/services/signalr.service';
import { AuthService } from '../../../../../../shared/core/src/lib/services/auth.service';

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
  DocumentVersion,
} from '../../data-access/dossier-document.service';
import {
  formatDocumentDate,
  formatDocumentFileSize,
  getDocumentFileIcon,
} from '../../utils/document-display.util';
import {
  getExtractionBarPercent,
  getOcrBarPercent,
  getRetryProcessOption,
  isActiveDigitizationStatus,
  isOcrBarActive,
  isOcrBarFailed,
  isOcrComplete,
  isExtractionComplete,
  isExtractionFailed,
  shouldShowExtractionProgress,
  canEditDossierDocument,
  canRetryDigitization,
  canReExtract,
  canSubmitOcrAndExtract,
  isReExtracting,
  isRetryingDigitization,
} from '../../utils/dossier-digitization.util';
import {
  hasDossierDigitizationImportPermission,
  normalizeDossierKindId,
} from '../../utils/dossier-permission.util';
import { DossierUploadMenuComponent, DossierUploadAction } from '../../components/dossier-upload-menu/dossier-upload-menu.component';
import { DossierFolderPickerDialogComponent } from '../../components/dossier-folder-picker-dialog/dossier-folder-picker-dialog.component';
import { DossierDirectUploadDialogComponent } from '../../components/dossier-direct-upload-dialog/dossier-direct-upload-dialog.component';
import { DossierDocumentEditDialogComponent } from '../../components/dossier-document-edit-dialog/dossier-document-edit-dialog.component';

interface DocumentTableAction {
  key: string;
  title: string;
  btnClass: string;
  iconClasses: string;
  disabled: boolean;
  /** Luôn hiển thị trong menu ba chấm, không đưa ra cột thao tác chính. */
  overflowOnly?: boolean;
  run: (doc: DossierDocumentItem) => void;
}

const MAX_INLINE_DOCUMENT_ACTIONS = 3;

@Component({
  selector: 'app-dossier-documents-tab',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    ButtonModule,
    MenuModule,
    DossierUploadMenuComponent,
    DossierFolderPickerDialogComponent,
    DossierDirectUploadDialogComponent,
    DossierDocumentEditDialogComponent,
  ],
  templateUrl: './dossier-documents-tab.component.html',
  styleUrl: './dossier-documents-tab.component.scss',
})
export class DossierDocumentsTabComponent implements OnInit, OnDestroy, OnChanges, AfterViewInit {
  @ViewChild('docActionMenu') docActionMenu?: Menu;
  @ViewChild('tableWrap') tableWrap?: ElementRef<HTMLElement>;

  private tableResizeObserver?: ResizeObserver;

  private documentService = inject(DossierDocumentService);
  private messageService = inject(MessageService);
  private signalRService = inject(SignalRService);
  private authService = inject(AuthService);
  private destroy$ = new Subject<void>();
  private search$ = new Subject<string>();
  private signalRConnected = false;
  private lastProgressEventAt = 0;
  private lastSignalRDossierId: string | null = null;

  @Input({ required: true }) dossierId!: string;
  @Input() canEdit = false;
  @Input() kindId = 2;
  @Input() hasFormTemplate = false;
  @Input() formId: string | null = null;
  @Input() menuScope: 'creator' | 'approver' | 'publisher' = 'creator';
  @Output() formDataSaved = new EventEmitter<void>();

  documents = signal<DossierDocumentItem[]>([]);
  loading = signal(false);
  deleting = signal(false);
  page = signal(1);
  pageSize = signal(10);
  totalDocuments = signal(0);
  searchKeyword = signal('');
  downloadingIds = signal<Set<string>>(new Set());
  retryingIds = signal<Set<string>>(new Set());
  reExtractingIds = signal<Set<string>>(new Set());

  showDeleteConfirm = signal(false);
  deleteTarget = signal<DossierDocumentItem | null>(null);

  showReExtractConfirm = signal(false);
  reExtractTarget = signal<DossierDocumentItem | null>(null);
  reExtractSubmitting = signal(false);

  showFolderPicker = signal(false);
  showDirectUpload = signal(false);
  uploadSource = signal(3);
  uploadDialogTitle = signal('Upload trực tiếp vào hồ sơ');

  showEditDocument = signal(false);
  editTarget = signal<DossierDocumentItem | null>(null);
  /** true = sửa tài liệu; false = chỉ xem (kể cả khi tab cho phép sửa hồ sơ). */
  documentDialogEditable = signal(false);
  docActionMenuItems = signal<MenuItem[]>([]);
  hasHorizontalScroll = signal(false);

  // Document Version states
  showHistoryDialog = signal(false);
  documentVersions = signal<DocumentVersion[]>([]);
  historyTargetDocument = signal<DossierDocumentItem | null>(null);
  loadingVersions = signal(false);
  rollingBack = signal(false);
  deletingVersion = signal(false);

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
  isOcrComplete = isOcrComplete;
  getExtractionBarPercent = getExtractionBarPercent;
  isExtractionComplete = isExtractionComplete;
  isExtractionFailed = isExtractionFailed;
  shouldShowExtractionProgress = shouldShowExtractionProgress;
  canRetryDigitization = canRetryDigitization;
  canReExtract = canReExtract;
  canSubmitOcrAndExtract = canSubmitOcrAndExtract;
  isReExtracting = isReExtracting;
  isRetryingDigitization = isRetryingDigitization;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['kindId']) {
      this.applyKindContext();
    }
  }

  ngOnInit(): void {
    this.applyKindContext();
    if (this.dossierId) {
      this.loadDocuments();
      void this.switchDossierSignalRGroup(this.dossierId);
    }

    this.search$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.page.set(1);
        this.loadDocuments();
      });

    this.setupDigitizationSignalR();
    void this.signalRService.ensureConnection().catch(() => {
      // fallback polling sẽ cập nhật tiến trình OCR
    });

    interval(5000)
      .pipe(
        takeUntil(this.destroy$),
        filter(() => {
          if (!this.dossierId || !this.hasActiveDigitization() || this.loading()) {
            return false;
          }
          const recentlyViaSignalR =
            this.signalRConnected &&
            this.lastProgressEventAt > 0 &&
            Date.now() - this.lastProgressEventAt < 12000;
          return !recentlyViaSignalR;
        })
      )
      .subscribe(() => this.loadDocuments(true));
  }

  ngAfterViewInit(): void {
    this.setupHorizontalScrollHint();
  }

  @HostListener('window:resize')
  onWindowResize(): void {
    this.updateHorizontalScrollHint();
  }

  ngOnDestroy(): void {
    this.tableWrap?.nativeElement?.removeEventListener('scroll', this.onTableWrapScroll);
    this.tableResizeObserver?.disconnect();
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

  private setupHorizontalScrollHint(): void {
    const el = this.tableWrap?.nativeElement;
    if (!el) return;

    el.addEventListener('scroll', this.onTableWrapScroll, { passive: true });
    this.updateHorizontalScrollHint();

    if (typeof ResizeObserver === 'undefined') return;

    this.tableResizeObserver?.disconnect();
    this.tableResizeObserver = new ResizeObserver(() => this.updateHorizontalScrollHint());
    this.tableResizeObserver.observe(el);
    const table = el.querySelector('.wf-table');
    if (table) this.tableResizeObserver.observe(table);
  }

  private onTableWrapScroll = (): void => {
    this.updateHorizontalScrollHint();
  };

  private updateHorizontalScrollHint(): void {
    const el = this.tableWrap?.nativeElement;
    if (!el) return;
    const hasOverflow = el.scrollWidth > el.clientWidth + 1;
    const atRightEnd = el.scrollLeft + el.clientWidth >= el.scrollWidth - 2;
    this.hasHorizontalScroll.set(hasOverflow && !atRightEnd);
  }

  private async switchDossierSignalRGroup(dossierId: string): Promise<void> {
    if (this.lastSignalRDossierId && this.lastSignalRDossierId !== dossierId) {
      await this.signalRService.leaveDossierGroup(this.lastSignalRDossierId);
    }
    try {
      await this.signalRService.ensureConnection();
      const joined = await this.signalRService.joinDossierGroup(dossierId);
      if (joined) {
        this.lastSignalRDossierId = dossierId;
      }
    } catch {
      // fallback polling sẽ cập nhật
    }
  }

  private applyDigitizationProgress(event: DigitizationProgressEvent): void {
    if (
      !event.dossierId ||
      event.dossierId.toLowerCase() !== this.dossierId.toLowerCase()
    ) {
      return;
    }

    this.signalRConnected = true;
    this.lastProgressEventAt = Date.now();

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
        queueMicrotask(() => {
          this.updateHorizontalScrollHint();
          setTimeout(() => this.updateHorizontalScrollHint(), 0);
        });
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

  private applyKindContext(): void {
    this.documentService.setKindContext(normalizeDossierKindId(this.kindId, 2));
  }

  /** Menu Quản lý hồ sơ (kỹ thuật) & Nhập liệu số hóa — hiển thị OCR/bóc tách mặc định khi có quyền IMPORT. */
  showCreatorDigitizationActions(): boolean {
    if (!this.canEdit || this.menuScope !== 'creator') return false;
    const kind = normalizeDossierKindId(this.kindId, 2);
    if (kind !== 1 && kind !== 2) return false;
    return hasDossierDigitizationImportPermission(this.authService, kind === 1);
  }

  getDocumentActions(doc: DossierDocumentItem): DocumentTableAction[] {
    const showDigitization = this.showCreatorDigitizationActions();
    const actions: DocumentTableAction[] = [
      {
        key: 'view',
        title: 'Xem tài liệu',
        btnClass: 'act-view',
        iconClasses: 'pi pi-eye',
        disabled: !doc.latestVersionId,
        run: (d) => this.onViewDocument(d),
      },
      {
        key: 'download',
        title: 'Tải tài liệu',
        btnClass: 'act-download',
        iconClasses: this.isDownloading(doc.id) ? 'pi pi-spin pi-spinner' : 'pi pi-download',
        disabled: !doc.latestVersionId || this.isDownloading(doc.id),
        run: (d) => this.onDownload(d),
      },
      {
        key: 'history',
        title: 'Lịch sử phiên bản',
        btnClass: 'act-history',
        iconClasses: 'pi pi-history',
        disabled: !doc.latestVersionId,
        overflowOnly: true,
        run: (d) => this.onViewHistory(d),
      },
    ];

    if (showDigitization && this.canSubmitOcrAndExtract(doc)) {
      actions.push({
        key: 'ocr-extract',
        title: 'OCR + bóc tách lại',
        btnClass: 'act-retry',
        iconClasses: this.isRetryingDigitization(doc.id, this.retryingIds())
          ? 'pi pi-spin pi-spinner'
          : 'pi pi-refresh',
        disabled: this.isRetryingDigitization(doc.id, this.retryingIds()),
        overflowOnly: true,
        run: (d) => this.onOcrAndExtract(d),
      });
    } else if (this.canRetryDigitization(doc) && this.canEdit) {
      actions.push({
        key: 'retry',
        title: 'Xử lý lại OCR/bóc tách',
        btnClass: 'act-retry',
        iconClasses: this.isRetryingDigitization(doc.id, this.retryingIds())
          ? 'pi pi-spin pi-spinner'
          : 'pi pi-refresh',
        disabled: this.isRetryingDigitization(doc.id, this.retryingIds()),
        run: (d) => this.onRetryDigitization(d),
      });
    }

    if (showDigitization && this.canReExtract(doc)) {
      actions.push({
        key: 'reextract',
        title: 'Bóc tách lại',
        btnClass: 'act-reextract',
        iconClasses: this.isReExtracting(doc.id, this.reExtractingIds())
          ? 'pi pi-spin pi-spinner'
          : 'pi pi-sync',
        disabled: this.isReExtracting(doc.id, this.reExtractingIds()),
        overflowOnly: true,
        run: (d) => this.onReExtract(d),
      });
    } else if (this.canReExtract(doc) && this.canEdit) {
      actions.push({
        key: 'reextract',
        title: 'Bóc tách lại (tải biểu mẫu mới)',
        btnClass: 'act-reextract',
        iconClasses: this.isReExtracting(doc.id, this.reExtractingIds())
          ? 'pi pi-spin pi-spinner'
          : 'pi pi-sync',
        disabled: this.isReExtracting(doc.id, this.reExtractingIds()),
        run: (d) => this.onReExtract(d),
      });
    }

    if (this.canEditDocument(doc) && this.canEdit) {
      actions.push({
        key: 'edit',
        title: 'Sửa tài liệu',
        btnClass: 'act-edit',
        iconClasses: 'pi pi-pencil',
        disabled: !doc.latestVersionId,
        run: (d) => this.onEditDocument(d),
      });
    }

    if (this.canEdit) {
      actions.push({
        key: 'delete',
        title: 'Xóa tài liệu',
        btnClass: 'act-delete',
        iconClasses: 'pi pi-trash',
        disabled: false,
        run: (d) => this.onDelete(d),
      });
    }

    return actions;
  }

  getPrimaryDocumentActions(doc: DossierDocumentItem): DocumentTableAction[] {
    const inline = this.getDocumentActions(doc).filter((action) => !action.overflowOnly);
    return inline.slice(0, MAX_INLINE_DOCUMENT_ACTIONS);
  }

  getOverflowDocumentActions(doc: DossierDocumentItem): DocumentTableAction[] {
    const actions = this.getDocumentActions(doc);
    const overflowOnly = actions.filter((action) => action.overflowOnly);
    const inline = actions.filter((action) => !action.overflowOnly);
    const inlineOverflow = inline.slice(MAX_INLINE_DOCUMENT_ACTIONS);
    return [...overflowOnly, ...inlineOverflow];
  }

  openDocActionMenu(doc: DossierDocumentItem, event: Event): void {
    event.stopPropagation();
    const overflow = this.getOverflowDocumentActions(doc);
    if (!overflow.length) return;

    this.docActionMenuItems.set(
      overflow.map((action) => ({
        label: action.title,
        icon: action.iconClasses,
        disabled: action.disabled,
        command: () => action.run(doc),
      }))
    );
    this.docActionMenu?.toggle(event);
  }

  onEditDocument(doc: DossierDocumentItem): void {
    if (!this.canEdit || !doc.latestVersionId) return;
    this.documentDialogEditable.set(true);
    this.editTarget.set(doc);
    this.showEditDocument.set(true);
  }

  onViewDocument(doc: DossierDocumentItem): void {
    if (!doc.latestVersionId) return;
    this.documentDialogEditable.set(false);
    this.editTarget.set(doc);
    this.showEditDocument.set(true);
  }

  onDocumentEdited(): void {
    this.showEditDocument.set(false);
    this.editTarget.set(null);
    this.loadDocuments();
    this.formDataSaved.emit();
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
      this.uploadDialogTitle.set('Quét tài liệu vào hồ sơ');
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

  onOcrAndExtract(doc: DossierDocumentItem): void {
    if (!doc.latestVersionId || !this.canEdit) return;

    const ids = new Set(this.retryingIds());
    ids.add(doc.id);
    this.retryingIds.set(ids);

    this.documentService
      .retryDigitization(this.dossierId, doc.latestVersionId, 'OcrAndExtract')
      .pipe(finalize(() => {
        const next = new Set(this.retryingIds());
        next.delete(doc.id);
        this.retryingIds.set(next);
      }))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Đã gửi OCR + bóc tách',
            detail: `"${doc.name}" đang được xử lý OCR và bóc tách`,
          });
          this.loadDocuments(true);
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể gửi OCR + bóc tách',
          });
        },
      });
  }

  onReExtract(doc: DossierDocumentItem): void {
    if (!doc.latestVersionId || !this.canEdit) return;
    this.reExtractTarget.set(doc);
    this.showReExtractConfirm.set(true);
  }

  cancelReExtract(): void {
    if (this.reExtractSubmitting()) return;
    this.showReExtractConfirm.set(false);
    this.reExtractTarget.set(null);
  }

  confirmReExtract(): void {
    const doc = this.reExtractTarget();
    if (!doc?.latestVersionId || !this.canEdit || this.reExtractSubmitting()) return;

    const ids = new Set(this.reExtractingIds());
    ids.add(doc.id);
    this.reExtractingIds.set(ids);
    this.reExtractSubmitting.set(true);

    this.documentService
      .reExtractDigitization(this.dossierId, doc.latestVersionId)
      .pipe(
        finalize(() => {
          this.reExtractSubmitting.set(false);
          const next = new Set(this.reExtractingIds());
          next.delete(doc.id);
          this.reExtractingIds.set(next);
        })
      )
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Đã gửi bóc tách lại',
            detail: `"${doc.name}" đang được bóc tách lại với biểu mẫu mới nhất`,
          });
          this.showReExtractConfirm.set(false);
          this.reExtractTarget.set(null);
          this.loadDocuments(true);
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể gửi bóc tách lại',
          });
        },
      });
  }

  // ===== DOCUMENT VERSION LOGIC =====

  onViewHistory(doc: DossierDocumentItem) {
    this.historyTargetDocument.set(doc);
    this.showHistoryDialog.set(true);
    this.loadDocumentVersions(doc.id);
  }

  loadDocumentVersions(documentId: string) {
    this.loadingVersions.set(true);
    this.documentService.getDocumentVersions(this.dossierId, documentId)
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
    this.documentService.rollbackDocumentVersion(this.dossierId, version.id)
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
    const doc = this.historyTargetDocument();
    if (!doc) return;

    this.deletingVersion.set(true);
    this.documentService.deleteDocumentVersion(this.dossierId, version.id)
      .pipe(finalize(() => this.deletingVersion.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `Đã xóa phiên bản số ${version.versionNumber} của tài liệu`,
          });
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

  onDownloadVersion(version: DocumentVersion) {
    const doc = this.historyTargetDocument();
    if (!doc) return;

    this.documentService.downloadFile(this.dossierId, version.id, doc.name)
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
    this.historyTargetDocument.set(null);
  }
}
