import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { InputNumberModule } from 'primeng/inputnumber';
import { ToastModule } from 'primeng/toast';
import { finalize, firstValueFrom, forkJoin, timer } from 'rxjs';
import { environment } from '@env/environment';
import { AuthService, AuditLogRetentionIndex, AuditLogRetentionStatusResponse, AuditLogService } from '@sohoa.frontend/shared/core';
import { DeleteConfirmDialogComponent, EcoPaginatorComponent, WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';

interface SystemParamResponse {
  paramKey: string;
  paramValue: string;
  description: string;
  dataType: string;
  createdAt?: string | null;
  updatedAt?: string | null;
}

const AUDIT_LOG_RETENTION_DAYS_KEY = 'AuditLogRetentionDays';
const AUDIT_LOG_DOMAIN_KEY = 'AuditLogDomain';

@Component({
  selector: 'app-system-param',
  standalone: true,
  imports: [CommonModule, FormsModule, InputNumberModule, ToastModule, WfBreadcrumbComponent, EcoPaginatorComponent, DeleteConfirmDialogComponent],
  providers: [MessageService],
  templateUrl: './system-param.component.html',
  styleUrl: './system-param.component.scss',
})
export class SystemParam implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly messageService = inject(MessageService);
  private readonly destroyRef = inject(DestroyRef);
  public readonly authService = inject(AuthService);
  private readonly auditLogService = inject(AuditLogService);
  private readonly apiUrl = `${environment.apiGatewayUrl}/api/v1/system-params`;

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly exporting = signal(false);
  readonly retentionDays = signal<number | null>(null);
  readonly auditLogDomain = signal('');
  readonly retentionStatus = signal<AuditLogRetentionStatusResponse | null>(null);
  readonly retentionItems = signal<AuditLogRetentionIndex[]>([]);
  readonly retentionLoading = signal(false);
  readonly retentionLoadFailed = signal(false);
  readonly retentionPage = signal(1);
  readonly retentionPageSize = signal(10);
  readonly nextCleanupAtUtc = signal<string | null>(null);
  readonly cleanupCountdownSeconds = signal<number | null>(null);
  readonly showDeleteRetentionIndexConfirm = signal(false);
  readonly deletingRetentionIndex = signal(false);
  readonly retentionIndexToDelete = signal<AuditLogRetentionIndex | null>(null);
  readonly showDeleteAllRetentionConfirm = signal(false);
  readonly loadingDeleteAllCount = signal(false);
  readonly deletingAllRetentionIndices = signal(false);
  readonly deleteAllRetentionCount = signal(0);
  readonly deleteAllRetentionDocumentCount = signal(0);
  readonly canDeleteAuditLogs = computed(() => this.authService.currentUserPermissions().includes('AUDIT_LOG_DELETE'));
  private nextCleanupAtUtcMs: number | null = null;
  systemParam: SystemParamResponse | null = null;
  auditLogDomainParam: SystemParamResponse | null = null;

  ngOnInit(): void {
    this.loadParam();
    this.loadRetentionStatus();
    timer(0, 1000).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.updateCleanupCountdown());
  }

  canEdit(): boolean {
    return this.authService.hasPermission('SYSTEM_PARAM_EDIT');
  }

  loadParam(): void {
    this.loading.set(true);
    forkJoin({
      retentionDays: this.http.get<SystemParamResponse>(`${this.apiUrl}/${AUDIT_LOG_RETENTION_DAYS_KEY}`),
      auditLogDomain: this.http.get<SystemParamResponse>(`${this.apiUrl}/${AUDIT_LOG_DOMAIN_KEY}`),
    }).pipe(
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: params => {
        this.systemParam = params.retentionDays;
        this.auditLogDomainParam = params.auditLogDomain;
        const value = Number(params.retentionDays.paramValue);
        this.retentionDays.set(Number.isSafeInteger(value) && value > 0 ? value : null);
        this.auditLogDomain.set(params.auditLogDomain.paramValue);
      },
      error: error => this.showError(error, 'Không thể tải tham số hệ thống.'),
    });
  }

  save(): void {
    if (this.saving() || !this.canEdit()) {
      return;
    }

    const retentionDays = this.retentionDays();
    if (retentionDays === null || !Number.isSafeInteger(retentionDays) || retentionDays <= 0) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Giá trị không hợp lệ',
        detail: 'Vui lòng nhập số nguyên dương.',
      });
      return;
    }

    const auditLogDomain = this.auditLogDomain().trim();
    if (!auditLogDomain) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Giá trị không hợp lệ',
        detail: 'Vui lòng nhập domain dịch vụ nhật ký.',
      });
      return;
    }

    if (!this.systemParam || !this.auditLogDomainParam) {
      this.messageService.add({
        severity: 'error',
        summary: 'Không thể lưu',
        detail: 'Tham số hệ thống chưa được tải đầy đủ.',
      });
      return;
    }

    const payload: SystemParamResponse = {
      ...this.systemParam,
      paramKey: AUDIT_LOG_RETENTION_DAYS_KEY,
      paramValue: String(retentionDays),
    };
    const auditLogDomainPayload: SystemParamResponse = {
      ...this.auditLogDomainParam,
      paramKey: AUDIT_LOG_DOMAIN_KEY,
      paramValue: auditLogDomain,
    };

    this.saving.set(true);
    forkJoin([
      this.http.put<void>(`${this.apiUrl}/${AUDIT_LOG_RETENTION_DAYS_KEY}`, payload),
      this.http.put<void>(`${this.apiUrl}/${AUDIT_LOG_DOMAIN_KEY}`, auditLogDomainPayload),
    ]).pipe(
      finalize(() => this.saving.set(false))
    ).subscribe({
      next: () => {
        this.systemParam = payload;
        this.auditLogDomainParam = auditLogDomainPayload;
        this.auditLogDomain.set(auditLogDomain);
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: 'Đã lưu thời gian lưu log hệ thống.',
        });
        this.loadParam();
        this.loadRetentionStatus();
      },
      error: error => this.showError(error, 'Không thể lưu tham số hệ thống.'),
    });
  }

  loadRetentionStatus(): void {
    this.retentionLoading.set(true);
    this.retentionLoadFailed.set(false);
    this.auditLogService.getRetentionStatus(this.retentionPage(), this.retentionPageSize()).pipe(
      finalize(() => this.retentionLoading.set(false))
    ).subscribe({
      next: response => {
        this.retentionStatus.set(response);
        this.retentionItems.set(response.items ?? []);
        this.setNextCleanupAtUtc(response.nextCleanupAtUtc);
      },
      error: () => {
        this.retentionStatus.set(null);
        this.retentionItems.set([]);
        this.setNextCleanupAtUtc(null);
        this.retentionLoadFailed.set(true);
      },
    });
  }

  onRetentionPageChange(page: number): void {
    this.retentionPage.set(page);
    this.loadRetentionStatus();
  }

  onRetentionPageSizeChange(pageSize: number): void {
    this.retentionPageSize.set(pageSize);
    this.retentionPage.set(1);
    this.loadRetentionStatus();
  }

  async exportExcel(): Promise<void> {
    if (this.exporting()) {
      return;
    }

    const exportPageSize = 50;
    this.exporting.set(true);

    try {
      const firstPage = await firstValueFrom(this.auditLogService.getRetentionStatus(1, exportPageSize));
      const totalPages = Math.ceil(firstPage.totalCount / exportPageSize);
      const remainingRequests = Array.from(
        { length: Math.max(0, totalPages - 1) },
        (_, index) => this.auditLogService.getRetentionStatus(index + 2, exportPageSize)
      );
      const remainingPages = remainingRequests.length > 0
        ? await firstValueFrom(forkJoin(remainingRequests))
        : [];
      const items = [
        ...(firstPage.items ?? []),
        ...remainingPages.flatMap(page => page.items ?? []),
      ];

      const XLSX = await import('xlsx');
      const headers = [
        'Ngày nhật ký',
        'Số bản ghi',
        'Dung lượng',
        'Ngày dự kiến xóa',
        'Còn lại',
        'Trạng thái',
      ];
      const rows = items.map(item => ({
        'Ngày nhật ký': this.formatDate(item.logDate),
        'Số bản ghi': item.documentCount,
        'Dung lượng': this.formatSize(item.sizeBytes),
        'Ngày dự kiến xóa': this.formatDateTime(item.estimatedDeleteAtUtc),
        'Còn lại': this.formatRemainingDays(item.remainingDays),
        'Trạng thái': this.isExpiringSoon(item.status) ? 'Sắp xóa' : 'Đang lưu',
      }));
      const worksheet = XLSX.utils.json_to_sheet(rows, { header: headers });
      worksheet['!cols'] = [
        { wch: 16 },
        { wch: 16 },
        { wch: 16 },
        { wch: 22 },
        { wch: 14 },
        { wch: 14 },
      ];

      const workbook = XLSX.utils.book_new();
      XLSX.utils.book_append_sheet(workbook, worksheet, 'Trạng thái dọn log');
      const workbookBlob = new Blob([XLSX.write(workbook, { bookType: 'xlsx', type: 'array' })], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      });
      const url = URL.createObjectURL(workbookBlob);
      const link = document.createElement('a');
      const now = new Date();
      const timestamp = `${now.getFullYear()}${this.padTime(now.getMonth() + 1)}${this.padTime(now.getDate())}_${this.padTime(now.getHours())}${this.padTime(now.getMinutes())}${this.padTime(now.getSeconds())}`;
      link.href = url;
      link.download = `TrangThaiDonLog_${timestamp}.xlsx`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);

      this.messageService.add({
        severity: 'success',
        summary: 'Thành công',
        detail: `Đã xuất ${items.length.toLocaleString('vi-VN')} bản ghi.`,
      });
    } catch (error) {
      this.showError(error, 'Không thể xuất dữ liệu Excel.');
    } finally {
      this.exporting.set(false);
    }
  }

  canDeleteRetentionIndex(item: AuditLogRetentionIndex): boolean {
    const match = /^audit_logs-(\d{4})\.(\d{2})\.(\d{2})$/.exec(item.indexName);
    if (!match || `${match[1]}-${match[2]}-${match[3]}` !== item.logDate) {
      return false;
    }

    return item.logDate !== this.getCurrentUtcDate();
  }

  openDeleteRetentionIndexConfirm(item: AuditLogRetentionIndex): void {
    if (!this.canDeleteAuditLogs() || !this.canDeleteRetentionIndex(item)) {
      return;
    }

    this.retentionIndexToDelete.set(item);
    this.showDeleteRetentionIndexConfirm.set(true);
  }

  cancelDeleteRetentionIndex(): void {
    if (this.deletingRetentionIndex()) {
      return;
    }

    this.showDeleteRetentionIndexConfirm.set(false);
    this.retentionIndexToDelete.set(null);
  }

  confirmDeleteRetentionIndex(): void {
    const item = this.retentionIndexToDelete();
    if (!item || this.deletingRetentionIndex() || !this.canDeleteAuditLogs() || !this.canDeleteRetentionIndex(item)) {
      return;
    }

    this.deletingRetentionIndex.set(true);
    this.auditLogService.deleteRetentionIndex(item.logDate).pipe(
      finalize(() => this.deletingRetentionIndex.set(false))
    ).subscribe({
      next: response => {
        this.showDeleteRetentionIndexConfirm.set(false);
        this.retentionIndexToDelete.set(null);
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: response.message || 'Đã xóa vật lý index nhật ký.',
        });
        this.loadRetentionStatus();
      },
      error: error => this.showError(error, 'Không thể xóa index nhật ký.'),
    });
  }

  deleteRetentionIndexTargetName(): string {
    const item = this.retentionIndexToDelete();
    if (!item) return '';

    return `${this.formatDate(item.logDate)} - ${item.documentCount.toLocaleString('vi-VN')} bản ghi - ${this.formatSize(item.sizeBytes)}`;
  }

  async openDeleteAllRetentionConfirm(): Promise<void> {
    if (!this.canDeleteAuditLogs() || this.loadingDeleteAllCount() || this.deletingAllRetentionIndices()) {
      return;
    }

    this.loadingDeleteAllCount.set(true);
    try {
      const pageSize = 100;
      const firstPage = await firstValueFrom(this.auditLogService.getRetentionStatus(1, pageSize));
      const totalPages = Math.ceil(firstPage.totalCount / pageSize);
      const remainingRequests = Array.from(
        { length: Math.max(0, totalPages - 1) },
        (_, index) => this.auditLogService.getRetentionStatus(index + 2, pageSize)
      );
      const remainingPages = remainingRequests.length
        ? await firstValueFrom(forkJoin(remainingRequests))
        : [];
      const deletableItems = [firstPage, ...remainingPages]
        .flatMap(page => page.items ?? [])
        .filter(item => this.canDeleteRetentionIndex(item));
      const count = deletableItems.length;

      if (count === 0) {
        this.messageService.add({
          severity: 'warn',
          summary: 'Không có dữ liệu',
          detail: 'Không có index nhật ký nào có thể xóa.',
        });
        return;
      }

      this.deleteAllRetentionCount.set(count);
      this.deleteAllRetentionDocumentCount.set(
        deletableItems.reduce((total, item) => total + item.documentCount, 0)
      );
      this.showDeleteAllRetentionConfirm.set(true);
    } catch (error) {
      this.showError(error, 'Không thể xác định số index nhật ký sẽ xóa.');
    } finally {
      this.loadingDeleteAllCount.set(false);
    }
  }

  cancelDeleteAllRetention(): void {
    if (this.deletingAllRetentionIndices()) {
      return;
    }

    this.showDeleteAllRetentionConfirm.set(false);
    this.deleteAllRetentionCount.set(0);
    this.deleteAllRetentionDocumentCount.set(0);
  }

  confirmDeleteAllRetention(): void {
    if (!this.canDeleteAuditLogs() || this.deletingAllRetentionIndices() || this.deleteAllRetentionCount() <= 0) {
      return;
    }

    this.deletingAllRetentionIndices.set(true);
    this.auditLogService.deleteAllRetentionIndices().pipe(
      finalize(() => this.deletingAllRetentionIndices.set(false))
    ).subscribe({
      next: response => {
        this.showDeleteAllRetentionConfirm.set(false);
        this.deleteAllRetentionCount.set(0);
        this.deleteAllRetentionDocumentCount.set(0);
        this.retentionPage.set(1);
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: response.message || `Đã xóa ${response.deletedIndices ?? 0} index nhật ký.`,
        });
        this.loadRetentionStatus();
      },
      error: error => this.showError(error, 'Không thể xóa toàn bộ index nhật ký.'),
    });
  }

  formatDate(value: string): string {
    const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
    return match ? `${match[3]}/${match[2]}/${match[1]}` : '--';
  }

  formatDateTime(value: string | null | undefined): string {
    if (!value) return '--';

    const timestamp = Date.parse(value);
    if (Number.isNaN(timestamp)) return '--';

    const date = new Date(timestamp);
    return `${this.padTime(date.getDate())}/${this.padTime(date.getMonth() + 1)}/${date.getFullYear()} ${this.padTime(date.getHours())}:${this.padTime(date.getMinutes())}`;
  }

  formatSize(sizeBytes: number): string {
    if (!Number.isFinite(sizeBytes) || sizeBytes < 0) return '-';

    const units = ['KB', 'MB', 'GB'];
    let value = sizeBytes / 1024;
    let unitIndex = 0;
    while (value >= 1024 && unitIndex < units.length - 1) {
      value /= 1024;
      unitIndex++;
    }
    return `${value.toLocaleString('vi-VN', { maximumFractionDigits: 1 })} ${units[unitIndex]}`;
  }

  formatRemainingDays(remainingDays: number): string {
    if (remainingDays <= 0) return 'Hôm nay';
    return `${remainingDays} ngày`;
  }

  isExpiringSoon(status: string): boolean {
    return status === 'EXPIRING_SOON';
  }

  formatLocalDateTime(value: string): string {
    const timestamp = Date.parse(value);
    if (Number.isNaN(timestamp)) return '-';

    return new Intl.DateTimeFormat('vi-VN', {
      dateStyle: 'short',
      timeStyle: 'medium',
    }).format(new Date(timestamp));
  }

  formatCleanupCountdown(remainingSeconds: number | null): string {
    if (remainingSeconds === null) return '-';

    const days = Math.floor(remainingSeconds / 86_400);
    const hours = Math.floor((remainingSeconds % 86_400) / 3_600);
    const minutes = Math.floor((remainingSeconds % 3_600) / 60);
    const seconds = remainingSeconds % 60;
    return `${days} ngày ${this.padTime(hours)}:${this.padTime(minutes)}:${this.padTime(seconds)}`;
  }

  private setNextCleanupAtUtc(value: string | null | undefined): void {
    const timestamp = value ? Date.parse(value) : Number.NaN;
    this.nextCleanupAtUtcMs = Number.isNaN(timestamp) ? null : timestamp;
    this.nextCleanupAtUtc.set(this.nextCleanupAtUtcMs === null ? null : value ?? null);
    this.updateCleanupCountdown();
  }

  private updateCleanupCountdown(): void {
    if (this.nextCleanupAtUtcMs === null) {
      this.cleanupCountdownSeconds.set(null);
      return;
    }

    const remainingSeconds = Math.max(0, Math.ceil((this.nextCleanupAtUtcMs - Date.now()) / 1_000));
    this.cleanupCountdownSeconds.set(remainingSeconds);

    if (remainingSeconds === 0 && !this.retentionLoading()) {
      this.loadRetentionStatus();
    }
  }

  private padTime(value: number): string {
    return String(value).padStart(2, '0');
  }

  private getCurrentUtcDate(): string {
    return new Date().toISOString().slice(0, 10);
  }

  private showError(error: any, fallback: string): void {
    this.messageService.add({
      severity: 'error',
      summary: 'Lỗi',
      detail: error?.error?.message || fallback,
    });
  }
}
