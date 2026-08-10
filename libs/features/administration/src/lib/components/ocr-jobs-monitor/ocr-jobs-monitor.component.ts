import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ToastModule } from 'primeng/toast';
import { DatePickerModule } from 'primeng/datepicker';
import { MessageService } from 'primeng/api';
import { Subject, Subscription, catchError, finalize, of, switchMap } from 'rxjs';
import { EcoPaginatorComponent, WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { APP_CONFIG, AuthService, OcrJobListItem, OcrJobsMonitorService } from '@sohoa.frontend/shared/core';
import { DossierDocumentService } from '@sohoa.frontend/features/dossier-management';
import { EquipmentService } from '@sohoa.frontend/features/equipment';

const STATUS_OPTIONS = ['Pending', 'Running', 'OcrCompleted', 'Extracting', 'Completed', 'Failed'];
const STATUS_LABELS: Record<string, string> = {
  Pending: 'Đang chờ',
  Running: 'Đang chạy',
  OcrCompleted: 'OCR hoàn thành',
  Extracting: 'Đang bóc tách',
  Completed: 'Hoàn thành',
  Failed: 'Lỗi',
};
const PHASE_OPTIONS: Array<{ value: string; label: string }> = [
  { value: 'ocr', label: 'OCR' },
  { value: 'extraction', label: 'Bóc tách' },
];

/**
 * Màn hình giám sát job OCR/bóc tách toàn hệ thống (chỉ pipeline production —
 * DOCUMENT_OCR_PROGRESS/DOCUMENT_EXTRACTION_RESULTS). Chỉ đọc + "Chạy lại" — nút Chạy lại tái
 * sử dụng đúng các endpoint submit-digitization/rerun-extraction đã có sẵn, không có logic
 * retry riêng ở đây.
 */
@Component({
  selector: 'app-ocr-jobs-monitor',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, DatePickerModule, WfBreadcrumbComponent, EcoPaginatorComponent],
  providers: [MessageService],
  templateUrl: './ocr-jobs-monitor.component.html',
  styleUrl: './ocr-jobs-monitor.component.scss',
})
export class OcrJobsMonitorComponent implements OnInit, OnDestroy {
  private readonly ocrJobsService = inject(OcrJobsMonitorService);
  private readonly dossierDocumentService = inject(DossierDocumentService);
  private readonly equipmentService = inject(EquipmentService);
  private readonly messageService = inject(MessageService);
  private readonly authService = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly config = inject(APP_CONFIG);

  readonly statusOptions = STATUS_OPTIONS;
  readonly phaseOptions = PHASE_OPTIONS;
  documentTypeOptions = signal<{ id: string; name: string }[]>([]);

  canView = computed(() => this.authService.hasPermission('OCR_JOBS_MONITOR_VIEW'));

  // Bộ lọc đang gõ — chỉ áp dụng khi bấm "Tìm" (theo đúng mẫu audit-log).
  filterKeyword = signal('');
  filterDocumentTypeId = signal('');
  filterResourceKeyword = signal('');
  filterStatus = signal('');
  filterPhase = signal('');
  filterFromDate: Date | null = null;
  filterToDate: Date | null = null;

  private appliedKeyword = signal('');
  private appliedDocumentTypeId = signal('');
  private appliedResourceKeyword = signal('');
  private appliedStatus = signal('');
  private appliedPhase = signal('');
  private appliedFromDate: Date | null = null;
  private appliedToDate: Date | null = null;

  loading = signal(false);
  jobs = signal<OcrJobListItem[]>([]);
  totalCount = signal(0);
  page = signal(1);
  pageSize = signal(10);

  retryingIds = signal<Set<string>>(new Set());

  private readonly loadTrigger = new Subject<void>();
  private loadSub?: Subscription;

  ngOnInit(): void {
    this.loadDocumentTypeOptions();

    this.loadSub = this.loadTrigger
      .pipe(
        switchMap(() => {
          this.loading.set(true);
          return this.ocrJobsService
            .getJobs({
              page: this.page(),
              pageSize: this.pageSize(),
              status: this.appliedStatus() || undefined,
              phase: this.appliedPhase() || undefined,
              keyword: this.appliedKeyword() || undefined,
              documentTypeId: this.appliedDocumentTypeId() || undefined,
              resourceKeyword: this.appliedResourceKeyword() || undefined,
              fromDate: this.appliedFromDate?.toISOString(),
              toDate: this.appliedToDate?.toISOString(),
            })
            .pipe(
              catchError(() => {
                this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được danh sách job OCR.' });
                return of(null);
              }),
              finalize(() => this.loading.set(false)),
            );
        }),
      )
      .subscribe((res) => {
        if (!res) return;
        this.jobs.set(res.items);
        this.totalCount.set(res.totalCount);
      });

    this.loadTrigger.next();
  }

  ngOnDestroy(): void {
    this.loadSub?.unsubscribe();
  }

  private loadDocumentTypeOptions(): void {
    this.http
      .get<any[]>(`${this.config.apiGatewayUrl}/api/catalog/document-type/lookup`)
      .pipe(catchError(() => of([])))
      .subscribe((items) => {
        this.documentTypeOptions.set(
          (items || []).map((item) => ({ id: item.id ?? item.Id, name: item.name ?? item.Name }))
        );
      });
  }

  onSearch(): void {
    this.appliedKeyword.set(this.filterKeyword());
    this.appliedDocumentTypeId.set(this.filterDocumentTypeId());
    this.appliedResourceKeyword.set(this.filterResourceKeyword());
    this.appliedStatus.set(this.filterStatus());
    this.appliedPhase.set(this.filterPhase());
    this.appliedFromDate = this.filterFromDate;
    this.appliedToDate = this.filterToDate;
    this.page.set(1);
    this.loadTrigger.next();
  }

  onRefresh(): void {
    this.filterKeyword.set('');
    this.filterDocumentTypeId.set('');
    this.filterResourceKeyword.set('');
    this.filterStatus.set('');
    this.filterPhase.set('');
    this.filterFromDate = null;
    this.filterToDate = null;
    this.appliedKeyword.set('');
    this.appliedDocumentTypeId.set('');
    this.appliedResourceKeyword.set('');
    this.appliedStatus.set('');
    this.appliedPhase.set('');
    this.appliedFromDate = null;
    this.appliedToDate = null;
    this.page.set(1);
    this.loadTrigger.next();
  }

  onComboboxChange(): void {
    this.onSearch();
  }

  
  onPageChange(newPage: number): void {
    this.page.set(newPage);
    this.loadTrigger.next();
  }

  onPageSizeChange(newSize: number): void {
    this.pageSize.set(newSize);
    this.page.set(1);
    this.loadTrigger.next();
  }

  canRetryJob(job: OcrJobListItem): boolean {
    // Quyền thực thi retry đã được kiểm tra phía server bởi chính endpoint
    // submit-digitization/rerun-extraction (báo lỗi nếu người dùng không đủ quyền).
    return job.status === 'Failed';
  }

  isRetrying(job: OcrJobListItem): boolean {
    return this.retryingIds().has(job.progressId);
  }

  /**
   * Chạy lại đúng bước đã thất bại — Phase=ocr thì gọi lại toàn bộ (OCR+bóc tách), Phase=extraction
   * thì chỉ bóc tách lại. Ưu tiên hồ sơ (dossierId) nếu có, vì service phía sau tự resolve đúng biểu
   * mẫu EAV; chỉ dùng luồng thiết bị khi tài liệu không thuộc hồ sơ nào.
   */
  retryJob(job: OcrJobListItem): void {
    if (!this.canRetryJob(job)) return;

    const ids = new Set(this.retryingIds());
    ids.add(job.progressId);
    this.retryingIds.set(ids);

    const isOcrPhase = job.phase === 'ocr';
    const request$ = job.dossierId
      ? isOcrPhase
        ? this.dossierDocumentService.retryDigitization(job.dossierId, job.documentVersionId, 'OcrAndExtract')
        : this.dossierDocumentService.reExtractDigitization(job.dossierId, job.documentVersionId)
      : job.equipmentId
        ? isOcrPhase
          ? this.equipmentService.submitDocumentDigitizationOnly(job.equipmentId, job.documentVersionId)
          : this.equipmentService.rerunEquipmentDocumentExtraction(job.equipmentId, job.documentVersionId)
        : null;

    if (!request$) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Không thể chạy lại',
        detail: 'Tài liệu này không thuộc hồ sơ hay thiết bị nào để xác định luồng chạy lại.',
      });
      const next = new Set(this.retryingIds());
      next.delete(job.progressId);
      this.retryingIds.set(next);
      return;
    }

    request$
      .pipe(
        finalize(() => {
          const next = new Set(this.retryingIds());
          next.delete(job.progressId);
          this.retryingIds.set(next);
        }),
      )
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Đã gửi chạy lại',
            detail: `"${job.documentName}" đang được xử lý lại.`,
          });
          this.loadTrigger.next();
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không gửi được yêu cầu chạy lại.' });
        },
      });
  }

  getStatusLabel(status: string): string {
    return STATUS_LABELS[status] ?? status;
  }

  getStatusBadgeClass(status: string): string {
    switch (status) {
      case 'Completed':
        return 'badge-completed';
      case 'Failed':
        return 'badge-failed';
      case 'Running':
      case 'Extracting':
        return 'badge-running';
      default:
        return 'badge-pending';
    }
  }
}
