import {
  Component,
  OnInit,
  OnDestroy,
  inject,
  signal,
  computed,
  ViewChild,
  ElementRef,
  input,
  output,
  effect,
  untracked,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { MessageService, MenuItem } from 'primeng/api';
import { Menu, MenuModule } from 'primeng/menu';
import { SignalRService, DigitizationProgressEvent } from '@sohoa.frontend/shared/core';
import { EcoPaginatorComponent } from '@sohoa.frontend/shared/layout';
import { Subject, debounceTime, distinctUntilChanged, finalize, takeUntil } from 'rxjs';
import { EquipmentService } from '../../data-access/equipment.service';
import { FileDownloadService } from '../../data-access/file-download.service';
import { EquipmentDocumentDetailDialogComponent } from '../equipment-document-detail-dialog/equipment-document-detail-dialog.component';
import { OcrInsightsPanelComponent, OcrInsightsSourceDocument } from '@sohoa.frontend/features/ocr-module';

import {
  formatDocumentDate,
  formatDocumentFileSize,
  getDocumentFileIcon,
  getExtractionBarPercent,
  getOcrBarPercent,
  isOcrBarActive,
  isOcrBarFailed,
  isOcrComplete,
  isExtractionComplete,
  isExtractionFailed,
  shouldShowExtractionProgress,
  canRetryDigitization,
  canSubmitOcrAndExtract,
  canReExtract,
  isReExtracting,
  isRetryingDigitization,
  DossierDocumentEditDialogComponent,
} from '@sohoa.frontend/features/dossier-management';

export interface EquipmentDocumentItem {
  id: string;
  name: string;
  folderId: string | null;
  dossierId: string | null;
  createdBy: string | null;
  createdByName: string | null;
  createdDate: string;
  fileSize: number;
  mimeType: string | null;
  latestVersionId: string | null;
  documentTypeId: string | null;
  documentTypeName: string | null;
  isEquipmentProfile: boolean;
  ocrProgress: {
    id: string;
    documentVersionId: string;
    phase: string;
    currentPage: number;
    totalPages: number;
    progress: number;
    status: string;
    processOption: string | null;
  } | null;
  extractionResult: {
    id: string;
    documentVersionId: string;
    status: string;
  } | null;
}

interface DocumentTableAction {
  key: string;
  title: string;
  btnClass: string;
  iconClasses: string;
  disabled: boolean;
  overflowOnly?: boolean;
  run: (doc: EquipmentDocumentItem) => void;
}

const MAX_INLINE_DOCUMENT_ACTIONS = 3;

@Component({
  selector: 'app-equipment-documents',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    ButtonModule,
    MenuModule,
    EcoPaginatorComponent,
    EquipmentDocumentDetailDialogComponent,
    DossierDocumentEditDialogComponent,
    OcrInsightsPanelComponent,
  ],
  templateUrl: './equipment-documents.component.html',
  styleUrl: './equipment-documents.component.scss',
})
export class EquipmentDocumentsComponent implements OnInit, OnDestroy {
  @ViewChild('docActionMenu') docActionMenu?: Menu;
  @ViewChild('tableWrap') tableWrap?: ElementRef<HTMLElement>;

  private equipmentService = inject(EquipmentService);
  private fileDownloadService = inject(FileDownloadService);
  private messageService = inject(MessageService);
  private signalRService = inject(SignalRService);
  private destroy$ = new Subject<void>();
  private search$ = new Subject<string>();

  /** Id thiết bị — bắt buộc; parent chỉ mount khi đã có id. */
  equipmentId = input.required<string>();
  canEdit = input(false);
  /** Ẩn trạng thái xử lý OCR/bóc tách tại màn chỉ xem thuộc phân hệ Tra cứu. */
  hideDigitizationColumns = input(false);
  /** Mở chi tiết tài liệu theo đúng ngữ cảnh Tra cứu hồ sơ thiết bị. */
  dossierLookupMode = input(false);
  /** Id hồ sơ trên route, dùng dự phòng khi item tài liệu không trả dossierId. */
  lookupDossierId = input<string | null>(null);
  factoryAcceptanceOnly = input(false);
  externalAccess = input(false);
  factoryProfileAccess = input(false);
  cbmDocumentsOnly = input(false);
  documentProcessed = output<void>();

  documents = signal<EquipmentDocumentItem[]>([]);
  loading = signal(false);
  page = signal(1);
  pageSize = signal(10);
  totalDocuments = signal(0);
  searchKeyword = signal('');
  /** Tên biểu mẫu TEMPLATE gắn loại thiết bị (version active mới nhất). */
  formTemplateName = signal<string | null>(null);
  formTemplateMissing = signal(false);
  submittingIds = signal<Set<string>>(new Set());
  retryingIds = signal<Set<string>>(new Set());
  reExtractingIds = signal<Set<string>>(new Set());

  // Phân hệ Module OCR (Nhóm A) — chỉ đọc kết quả OCR đã có, không đụng logic OCR/bóc tách ở trên.
  ocrInsightsVisible = signal(false);
  ocrInsightsSource = signal<OcrInsightsSourceDocument | null>(null);

  showEditDocument = signal(false);
  editTarget = signal<EquipmentDocumentItem | null>(null);
  detailDossierId = computed(() => this.editTarget()?.dossierId ?? this.lookupDossierId());

  totalPages = computed(() => Math.ceil(this.totalDocuments() / this.pageSize()));

  constructor() {
    effect(() => {
      const id = this.equipmentId();
      const factoryAcceptanceOnly = this.factoryAcceptanceOnly() || this.cbmDocumentsOnly();
      if (!id) return;
      untracked(() => {
        if (!factoryAcceptanceOnly) {
          this.loadFormTemplate();
        } else {
          this.formTemplateName.set(null);
          this.formTemplateMissing.set(false);
        }
        this.loadDocuments();
      });
    });
  }

  ngOnInit(): void {
    this.search$
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe((kw) => {
        this.searchKeyword.set(kw);
        this.page.set(1);
        this.loadDocuments();
      });

    this.setupSignalRProgress();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadDocuments(): void {
    const equipmentId = this.equipmentId();
    if (!equipmentId) return;
    this.loading.set(true);

    const request$ = this.cbmDocumentsOnly()
      ? this.equipmentService.getCbmDocumentsEquipmentDetail(
          equipmentId,
          this.page(),
          this.pageSize(),
          this.searchKeyword()
        )
      : this.factoryProfileAccess()
      ? this.equipmentService.getFactoryProfileEquipmentDetail(
          equipmentId,
          this.page(),
          this.pageSize(),
          this.searchKeyword()
        )
      : this.externalAccess()
      ? this.equipmentService.getFactoryAcceptanceEquipmentDetail(
          equipmentId,
          this.page(),
          this.pageSize(),
          this.searchKeyword()
        )
      : this.factoryAcceptanceOnly()
        ? this.equipmentService.getFactoryAcceptanceDocuments(
            equipmentId,
            this.page(),
            this.pageSize(),
            this.searchKeyword()
          )
        : this.equipmentService.getProfileDocuments(
            equipmentId,
            this.page(),
            this.pageSize(),
            this.searchKeyword()
          );

    request$
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (res) => {
          this.documents.set(
            this.externalAccess() || this.factoryProfileAccess() || this.cbmDocumentsOnly()
              ? res?.documents || []
              : res?.items || []
          );
          const total = res?.totalCount ?? res?.total ?? res?.count ?? res?.totalRecords;
          this.totalDocuments.set(Number(total ?? this.documents().length) || 0);
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err.error?.message || 'Không thể tải danh sách tài liệu lý lịch.',
          });
        },
      });
  }

  private loadFormTemplate(): void {
    const equipmentId = this.equipmentId();
    if (!equipmentId) return;

    this.formTemplateName.set(null);
    this.formTemplateMissing.set(false);

    this.equipmentService
      .getFormTemplate(equipmentId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (template) => {
          const name = template?.name?.trim();
          if (name) {
            this.formTemplateName.set(name);
            this.formTemplateMissing.set(false);
          } else {
            this.formTemplateName.set(null);
            this.formTemplateMissing.set(true);
          }
        },
        error: () => {
          this.formTemplateName.set(null);
          this.formTemplateMissing.set(true);
        },
      });
  }

  onSearchChange(val: string): void {
    this.search$.next(val);
  }

  trackByDocumentId(index: number, item: EquipmentDocumentItem): string {
    return item.id;
  }

  fileIcon(name: string): string {
    return getDocumentFileIcon(name);
  }

  formatSize(bytes: number): string {
    return formatDocumentFileSize(bytes);
  }

  formatDate(dateStr: string): string {
    return formatDocumentDate(dateStr);
  }

  displayCreatorName(doc: EquipmentDocumentItem): string {
    return doc.createdByName || doc.createdBy || 'Hệ thống';
  }

  // Digitization utils mapping
  getOcrProgress(doc: EquipmentDocumentItem) {
    return doc.ocrProgress;
  }

  isOcrComplete(ocr: any): boolean {
    return isOcrComplete(ocr);
  }

  isOcrBarFailed(ocr: any): boolean {
    return isOcrBarFailed(ocr);
  }

  isOcrBarActive(ocr: any): boolean {
    return isOcrBarActive(ocr);
  }

  getOcrBarPercent(ocr: any): number | null {
    return getOcrBarPercent(ocr);
  }

  shouldShowExtractionProgress(doc: EquipmentDocumentItem): boolean {
    return shouldShowExtractionProgress(doc as any);
  }

  isExtractionComplete(doc: EquipmentDocumentItem): boolean {
    return isExtractionComplete(doc as any);
  }

  isExtractionFailed(doc: EquipmentDocumentItem): boolean {
    return isExtractionFailed(doc as any);
  }

  getExtractionBarPercent(doc: EquipmentDocumentItem): number | null {
    return getExtractionBarPercent(doc as any);
  }

  // Pagination
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
    this.pageSize.set(size);
    this.page.set(1);
    this.loadDocuments();
  }

  onDocumentPageChange(event: { page: number; rows: number }): void {
    const nextPage = event.page + 1;
    const nextPageSize = event.rows;
    if (nextPage === this.page() && nextPageSize === this.pageSize()) return;

    this.page.set(nextPage);
    this.pageSize.set(nextPageSize);
    this.loadDocuments();
  }

  // Responsive Table Scroll Check
  hasHorizontalScroll(): boolean {
    if (!this.tableWrap) return false;
    const el = this.tableWrap.nativeElement;
    return el.scrollWidth > el.clientWidth;
  }

  // Document Actions
  getPrimaryDocumentActions(doc: EquipmentDocumentItem): DocumentTableAction[] {
    return this.buildAllDocumentActions(doc).filter((a) => !a.overflowOnly).slice(0, MAX_INLINE_DOCUMENT_ACTIONS);
  }

  getOverflowDocumentActions(doc: EquipmentDocumentItem): DocumentTableAction[] {
    const actions = this.buildAllDocumentActions(doc);
    const overflowOnly = actions.filter((a) => a.overflowOnly);
    const inline = actions.filter((a) => !a.overflowOnly);
    const inlineOverflow = inline.slice(MAX_INLINE_DOCUMENT_ACTIONS);
    return [...overflowOnly, ...inlineOverflow];
  }

  private buildAllDocumentActions(doc: EquipmentDocumentItem): DocumentTableAction[] {
    const actions: DocumentTableAction[] = [];
    const showDigitization = this.canEdit();

    actions.push({
      key: 'view',
      title: 'Xem tài liệu',
      btnClass: 'act-view',
      iconClasses: 'pi pi-eye color-teal',
      disabled: !doc.latestVersionId,
      run: (d) => this.editDocument(d),
    });

    actions.push({
      key: 'download',
      title: 'Tải tài liệu xuống',
      btnClass: 'act-download',
      iconClasses: 'pi pi-download color-blue',
      disabled: !doc.latestVersionId,
      run: (d) => this.downloadDocument(d),
    });

    if (showDigitization && this.canSubmitOcrAndExtract(doc)) {
      const noTemplate = this.formTemplateMissing();
      actions.push({
        key: 'ocr',
        title: noTemplate ? 'OCR (thiếu biểu mẫu thiết bị)' : 'OCR',
        btnClass: 'act-retry',
        iconClasses: this.isOcrSubmitting(doc.id) ? 'pi pi-spin pi-spinner color-blue' : 'pi pi-file-edit color-blue',
        disabled: noTemplate || this.isOcrSubmitting(doc.id) || !doc.latestVersionId,
        overflowOnly: true,
        run: (d) => this.onSubmitOcr(d),
      });
      actions.push({
        key: 'ocr-extract',
        title: noTemplate ? 'OCR + bóc tách (thiếu biểu mẫu thiết bị)' : 'OCR + bóc tách',
        btnClass: 'act-retry',
        iconClasses: this.isOcrExtractSubmitting(doc.id) ? 'pi pi-spin pi-spinner color-blue' : 'pi pi-refresh color-blue',
        disabled: noTemplate || this.isOcrExtractSubmitting(doc.id) || !doc.latestVersionId,
        overflowOnly: true,
        run: (d) => this.onOcrAndExtract(d),
      });
    } else if (this.canRetryDigitization(doc) && showDigitization) {
      actions.push({
        key: 'retry',
        title: 'Xử lý lại OCR/bóc tách',
        btnClass: 'act-retry',
        iconClasses: isRetryingDigitization(doc.id, this.retryingIds())
          ? 'pi pi-spin pi-spinner color-blue'
          : 'pi pi-refresh color-blue',
        disabled: 
        this.formTemplateMissing() ||
        isRetryingDigitization(doc.id, this.retryingIds()) || 
        !doc.latestVersionId,
        overflowOnly: true,
        run: (d) => this.onRetryDigitization(d),
      });
    }

    if (showDigitization && this.canReExtract(doc)) {
      actions.push({
        key: 'reextract',
        title: 'Bóc tách lại',
        btnClass: 'act-reextract',
        iconClasses: isReExtracting(doc.id, this.reExtractingIds())
          ? 'pi pi-spin pi-spinner color-blue'
          : 'pi pi-sync color-blue',
        disabled: 
          this.formTemplateMissing() ||
          isReExtracting(doc.id, this.reExtractingIds()) || 
          !doc.latestVersionId,
        overflowOnly: true,
        run: (d) => this.onReExtract(d),
      });
    }

    if (showDigitization && this.canReExtract(doc)) {
      actions.push({
        key: 'ocr-insights',
        title: 'Phân tích OCR nâng cao',
        btnClass: 'act-ocr-insights',
        iconClasses: 'pi pi-chart-bar',
        disabled: !doc.latestVersionId,
        overflowOnly: true,
        run: (d) => this.onOpenOcrInsights(d),
      });
    }

    return actions;
  }

  canSubmitOcrAndExtract(doc: EquipmentDocumentItem): boolean {
    return canSubmitOcrAndExtract(doc as any);
  }

  canRetryDigitization(doc: EquipmentDocumentItem): boolean {
    return canRetryDigitization(doc as any);
  }

  canReExtract(doc: EquipmentDocumentItem): boolean {
    return canReExtract(doc as any);
  }

  private isOcrSubmitting(docId: string): boolean {
    return this.submittingIds().has(docId);
  }

  private isOcrExtractSubmitting(docId: string): boolean {
    return this.retryingIds().has(docId);
  }

  selectedDocMenuTarget: EquipmentDocumentItem | null = null;
  docActionMenuItems = signal<MenuItem[]>([]);

  openDocActionMenu(doc: EquipmentDocumentItem, event: MouseEvent): void {
    this.selectedDocMenuTarget = doc;
    const actions = this.buildAllDocumentActions(doc);
    const items: MenuItem[] = actions.map((act) => ({
      label: act.title,
      icon: act.iconClasses,
      disabled: act.disabled,
      command: () => {
        act.run(doc);
      },
    }));
    this.docActionMenuItems.set(items);
    if (this.docActionMenu) {
      this.docActionMenu.toggle(event);
    }
  }

  downloadDocument(doc: EquipmentDocumentItem): void {
    if (!doc.latestVersionId) return;
    this.fileDownloadService.downloadFile(doc.latestVersionId, doc.name, this.equipmentId())
      .catch(() => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải tệp xuống.',
        });
      });
  }

  editDocument(doc: EquipmentDocumentItem): void {
    if (this.dossierLookupMode() && !doc.dossierId && !this.lookupDossierId()) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Không thể xem tài liệu',
        detail: 'Không xác định được hồ sơ chứa tài liệu.',
      });
      return;
    }
    this.editTarget.set(doc);
    this.showEditDocument.set(true);
  }

  onSubmitOcr(doc: EquipmentDocumentItem): void {
    if (!doc.latestVersionId || !this.canEdit()) return;

    const ids = new Set(this.submittingIds());
    ids.add(doc.id);
    this.submittingIds.set(ids);

    this.equipmentService
      .submitDocumentDigitizationOnly(this.equipmentId(), doc.latestVersionId)
      .pipe(
        finalize(() => {
          const next = new Set(this.submittingIds());
          next.delete(doc.id);
          this.submittingIds.set(next);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Đã gửi OCR',
            detail: `"${doc.name}" đang được xử lý OCR và bóc tách.`,
          });
          this.loadDocuments();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi OCR',
            detail: err.error?.message || 'Không thể gửi yêu cầu OCR.',
          });
        },
      });
  }

  onOcrAndExtract(doc: EquipmentDocumentItem): void {
    if (!doc.latestVersionId || !this.canEdit()) return;

    const ids = new Set(this.retryingIds());
    ids.add(doc.id);
    this.retryingIds.set(ids);

    this.equipmentService
      .submitDocumentDigitizationOnly(this.equipmentId(), doc.latestVersionId)
      .pipe(
        finalize(() => {
          const next = new Set(this.retryingIds());
          next.delete(doc.id);
          this.retryingIds.set(next);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Đã gửi OCR + bóc tách',
            detail: `"${doc.name}" đang được xử lý lại OCR và bóc tách.`,
          });
          this.loadDocuments();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err.error?.message || 'Không thể gửi OCR + bóc tách.',
          });
        },
      });
  }

  onRetryDigitization(doc: EquipmentDocumentItem): void {
    if (!doc.latestVersionId || !this.canEdit()) return;

    const ids = new Set(this.retryingIds());
    ids.add(doc.id);
    this.retryingIds.set(ids);

    this.equipmentService
      .submitDocumentDigitizationOnly(this.equipmentId(), doc.latestVersionId)
      .pipe(
        finalize(() => {
          const next = new Set(this.retryingIds());
          next.delete(doc.id);
          this.retryingIds.set(next);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Đã gửi xử lý lại',
            detail: `"${doc.name}" đang được xử lý lại.`,
          });
          this.loadDocuments();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err.error?.message || 'Không thể gửi xử lý lại.',
          });
        },
      });
  }

  onReExtract(doc: EquipmentDocumentItem): void {
    if (!doc.latestVersionId || !this.canEdit()) return;

    const ids = new Set(this.reExtractingIds());
    ids.add(doc.id);
    this.reExtractingIds.set(ids);

    this.equipmentService
      .rerunEquipmentDocumentExtraction(this.equipmentId(), doc.latestVersionId)
      .pipe(
        finalize(() => {
          const next = new Set(this.reExtractingIds());
          next.delete(doc.id);
          this.reExtractingIds.set(next);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Đã gửi bóc tách lại',
            detail: `"${doc.name}" đang được bóc tách lại theo biểu mẫu thiết bị.`,
          });
          this.loadDocuments();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err.error?.message || 'Không thể gửi bóc tách lại.',
          });
        },
      });
  }

  /**
   * Mở panel "Phân tích OCR nâng cao" (phân hệ Module OCR, Nhóm A) — chỉ đọc lại kết quả OCR
   * đã có sẵn của tài liệu này, không gọi lại pipeline OCR/bóc tách ở trên.
   */
  onOpenOcrInsights(doc: EquipmentDocumentItem): void {
    if (!doc.latestVersionId) return;

    this.equipmentService.getDigitizationProgressForEquipment(this.equipmentId(), doc.latestVersionId).subscribe({
      next: (progress) => {
        if (!progress?.bucketName || !progress?.filePath) {
          this.messageService.add({
            severity: 'warn',
            summary: 'Chưa thể phân tích',
            detail: 'Tài liệu này chưa có đủ thông tin vị trí lưu trữ file OCR.',
          });
          return;
        }
        this.ocrInsightsSource.set({
          bucket: progress.bucketName,
          filePath: progress.filePath,
          documentVersionId: doc.latestVersionId ?? undefined,
          totalPages: progress.totalPages ?? 0,
          documentLabel: doc.name,
        });
        this.ocrInsightsVisible.set(true);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không tải được thông tin OCR của tài liệu để phân tích.',
        });
      },
    });
  }

  onDocumentEdited(): void {
    this.loadDocuments();
    this.documentProcessed.emit();
  }

  // SignalR Realtime Progress Update
  private setupSignalRProgress(): void {
    // Lắng nghe SignalR bóc tách tài liệu
    this.signalRService.digitizationProgress$
      .pipe(takeUntil(this.destroy$))
      .subscribe((event: DigitizationProgressEvent) => {
        if (!event) return;

        this.documents.update((docs) => {
          const list = [...docs];
          const matched = list.find((d) => d.latestVersionId === event.documentVersionId);
          if (matched) {
            // Update OCR progress
            if (!matched.ocrProgress) {
              matched.ocrProgress = {
                id: '',
                documentVersionId: event.documentVersionId,
                phase: event.phase,
                currentPage: event.currentPage,
                totalPages: event.totalPages,
                progress: event.progress,
                status: event.status,
                processOption: null,
              };
            } else {
              matched.ocrProgress.phase = event.phase;
              matched.ocrProgress.currentPage = event.currentPage;
              matched.ocrProgress.totalPages = event.totalPages;
              matched.ocrProgress.progress = event.progress;
              matched.ocrProgress.status = event.status;
            }

            // Update Extraction result
            if (event.extractionStatus) {
              if (!matched.extractionResult) {
                matched.extractionResult = {
                  id: '',
                  documentVersionId: event.documentVersionId,
                  status: event.extractionStatus,
                };
              } else {
                matched.extractionResult.status = event.extractionStatus;
              }
            }

            // Nếu bóc tách hoàn thành, cập nhật reload
            if (event.extractionStatus === 'Success' || event.status === 'Completed') {
              setTimeout(() => this.loadDocuments(), 1000);
            }
          }
          return list;
        });
      });

    // Sub SignalR channel của thiết bị hoặc dùng chung channel dossier
    // Vì SignalR backend gửi event cho mọi client lắng nghe hoặc theo fileId, 
    // SignalRService đã kết nối tự động, chỉ cần subscribe event ở trên.
  }
}
