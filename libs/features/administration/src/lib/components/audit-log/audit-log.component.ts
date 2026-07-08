import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { DialogModule } from 'primeng/dialog';
import { Subject, catchError, finalize, of, switchMap } from 'rxjs';
import { AuthService, AuditLogService } from '@sohoa.frontend/shared/core';

interface AuditLogView {
  id: string;
  action: string;
  user: string;
  timestamp: string;
  occurredAtMs: number;
  details: string;
  resourceType?: string;
  resourceName?: string;
  serviceName?: string;
}

@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, DialogModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './audit-log.component.html',
  styleUrl: './audit-log.component.scss'
})
export class AuditLogComponent implements OnInit {
  searchTerm = signal('');
  filterAction = signal('');
  filterResourceType = signal('');
  filterServiceName = signal('');
  filterUserName = signal('');
  filterFromDate = signal('');
  filterToDate = signal('');

  appliedSearchTerm = signal('');
  appliedAction = signal('');
  appliedResourceType = signal('');
  appliedServiceName = signal('');
  appliedUserName = signal('');
  appliedFromDate = signal('');
  appliedToDate = signal('');

  logs = signal<AuditLogView[]>([]);
  loading = signal(false);
  exporting = signal(false);
  currentPage = signal(1);
  pageSize = signal(10);
  totalCount = signal(0);

  private readonly loadTrigger = new Subject<void>();
  private auditLogService = inject(AuditLogService);
  private messageService = inject(MessageService);
  public authService = inject(AuthService);

  canExport = computed(() => {
    const perms = this.authService.currentUserPermissions();
    return perms.includes('AUDIT_LOG_EXPORT') || perms.includes('AUDIT_LOG_VIEW');
  });

  canDelete = computed(() => this.authService.currentUserPermissions().includes('AUDIT_LOG_DELETE'));

  selectedIds = signal<string[]>([]);
  selectedCount = computed(() => this.selectedIds().length);

  filteredLogs = computed(() => this.logs());

  constructor() {
    this.loadTrigger.pipe(
      switchMap(() => {
        this.loading.set(true);
        return this.auditLogService.getAuditLogs(this.buildQueryParams()).pipe(
          catchError(() => {
            this.messageService.add({
              severity: 'error',
              summary: 'Lỗi tải dữ liệu',
              detail: 'Không thể kết nối đến máy chủ để tải lịch sử thao tác hệ thống.'
            });
            return of({ items: [], totalCount: 0, page: 1, pageSize: 10 });
          }),
          finalize(() => this.loading.set(false))
        );
      }),
      takeUntilDestroyed()
    ).subscribe((res) => {
      const backendLogs = res?.items || [];
      const mapped = Array.isArray(backendLogs) ? backendLogs.map((item) => this.mapLog(item)) : [];
      mapped.sort((a, b) => b.occurredAtMs - a.occurredAtMs);
      this.logs.set(mapped);
      this.totalCount.set(res?.totalCount || 0);
    });
  }

  ngOnInit() {
    const today = new Date();
    const monthAgo = new Date();
    monthAgo.setDate(today.getDate() - 30);

    const from = this.toDateInputValue(monthAgo);
    const to = this.toDateInputValue(today);
    this.filterFromDate.set(from);
    this.filterToDate.set(to);
    this.appliedFromDate.set(from);
    this.appliedToDate.set(to);

    this.authService.ensurePermissionsLoaded().subscribe(() => this.loadAuditLogs());
  }

  private toDateInputValue(date: Date): string {
    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, '0');
    const d = String(date.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
  }

  private toIsoStartOfDay(dateStr: string): string {
    const [y, m, d] = dateStr.split('-').map(Number);
    return new Date(y, m - 1, d, 0, 0, 0, 0).toISOString();
  }

  private toIsoEndOfDay(dateStr: string): string {
    const [y, m, d] = dateStr.split('-').map(Number);
    return new Date(y, m - 1, d, 23, 59, 59, 999).toISOString();
  }

  private buildQueryParams() {
    const params: Record<string, string | number | undefined> = {
      page: this.currentPage(),
      pageSize: this.pageSize()
    };

    const keyword = this.appliedSearchTerm().trim();
    const resourceType = this.appliedResourceType().trim();
    const serviceName = this.appliedServiceName().trim();
    const userName = this.appliedUserName().trim();

    if (keyword) params['keyword'] = keyword;
    if (this.appliedAction()) params['action'] = this.appliedAction();
    if (resourceType) params['resourceType'] = resourceType;
    if (serviceName) params['serviceName'] = serviceName;
    if (userName) params['userName'] = userName;
    if (this.appliedFromDate()) params['fromDate'] = this.toIsoStartOfDay(this.appliedFromDate());
    if (this.appliedToDate()) params['toDate'] = this.toIsoEndOfDay(this.appliedToDate());

    return params;
  }

  loadAuditLogs() {
    this.loadTrigger.next();
  }

  private mapLog(item: {
    id?: string;
    action?: string;
    userName?: string;
    user?: string;
    timestamp?: string;
    occurredAt?: string;
    details?: string;
    requestPath?: string;
    resourceType?: string;
    resourceName?: string;
    serviceName?: string;
  }): AuditLogView {
    const rawTime = item.timestamp || item.occurredAt || new Date().toISOString();
    const occurredAt = new Date(rawTime);
    return {
      id: item.id || '',
      action: item.action || 'USER_ACTION',
      user: item.userName || item.user || 'system',
      timestamp: occurredAt.toLocaleString('vi-VN'),
      occurredAtMs: occurredAt.getTime(),
      details: this.buildDetails(item),
      resourceType: item.resourceType,
      resourceName: item.resourceName,
      serviceName: item.serviceName
    };
  }

  private buildDetails(item: { details?: string; requestPath?: string }): string {
    const parts: string[] = [];
    if (item.details) parts.push(item.details);
    if (item.requestPath) parts.push(item.requestPath);
    return parts.join(' — ') || 'Thao tác hệ thống';
  }

  onSearch() {
    this.appliedSearchTerm.set(this.searchTerm());
    this.appliedAction.set(this.filterAction());
    this.appliedResourceType.set(this.filterResourceType());
    this.appliedServiceName.set(this.filterServiceName());
    this.appliedUserName.set(this.filterUserName());
    this.appliedFromDate.set(this.filterFromDate());
    this.appliedToDate.set(this.filterToDate());
    this.currentPage.set(1);
    this.selectedIds.set([]);
    this.loadAuditLogs();
  }

  isSelected(id: string): boolean {
    return this.selectedIds().includes(id);
  }

  isAllPageSelected(): boolean {
    const pageLogs = this.logs();
    return pageLogs.length > 0 && pageLogs.every((log) => this.selectedIds().includes(log.id));
  }

  toggleSelect(id: string) {
    this.selectedIds.update((ids) => {
      const next = [...ids];
      const index = next.indexOf(id);
      if (index >= 0) {
        next.splice(index, 1);
      } else {
        next.push(id);
      }
      return next;
    });
  }

  toggleSelectAllPage(event: Event) {
    const checked = (event.target as HTMLInputElement).checked;
    const pageIds = this.logs().map((log) => log.id).filter(Boolean);
    this.selectedIds.update((ids) => {
      if (!checked) {
        return ids.filter((id) => !pageIds.includes(id));
      }
      return [...new Set([...ids, ...pageIds])];
    });
  }

  get paginatedLogs(): AuditLogView[] {
    return this.logs();
  }

  get totalPages(): number {
    return Math.ceil(this.totalCount() / this.pageSize());
  }

  nextPage() {
    if (this.currentPage() < this.totalPages) {
      this.currentPage.update((page) => page + 1);
      this.loadAuditLogs();
    }
  }

  prevPage() {
    if (this.currentPage() > 1) {
      this.currentPage.update((page) => page - 1);
      this.loadAuditLogs();
    }
  }

  goToPage(page: string | number) {
    const p = Number(page);
    if (p >= 1 && p <= this.totalPages) {
      this.currentPage.set(p);
      this.loadAuditLogs();
    }
  }

  onPageSizeChange(event: Event) {
    const target = event.target as HTMLSelectElement;
    this.pageSize.set(Number(target.value));
    this.currentPage.set(1);
    this.loadAuditLogs();
  }

  exportExcel() {
    if (!this.canExport()) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Không có quyền',
        detail: 'Bạn không có quyền xuất nhật ký hệ thống.'
      });
      return;
    }

    if (!this.appliedFromDate() || !this.appliedToDate()) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Cảnh báo',
        detail: 'Vui lòng chọn khoảng thời gian và bấm Tìm trước khi xuất file.'
      });
      return;
    }

    const { page, pageSize, ...exportParams } = this.buildQueryParams();
    this.exporting.set(true);
    this.auditLogService.exportExcel(exportParams)
      .pipe(finalize(() => this.exporting.set(false)))
      .subscribe({
        next: (res) => {
          const blob = res.body;
          if (!blob) {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không nhận được dữ liệu file.' });
            return;
          }
          const disposition = res.headers.get('content-disposition') || '';
          const match = disposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
          const fileName = match?.[1]?.replace(/['"]/g, '')
            || `AuditLog_${this.appliedFromDate()}_${this.appliedToDate()}.xlsx`;
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = fileName;
          link.click();
          window.URL.revokeObjectURL(url);
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xuất tệp tin nhật ký hệ thống.' });
        },
        error: async (err) => {
          let detail = 'Không thể xuất nhật ký hệ thống.';
          if (err.error instanceof Blob) {
            try {
              const text = await err.error.text();
              const json = JSON.parse(text);
              detail = json.message || detail;
            } catch { /* ignore */ }
          } else if (err.error?.message) {
            detail = err.error.message;
          }
          this.messageService.add({ severity: 'error', summary: 'Lỗi xuất file', detail });
        }
      });
  }

  displayDeleteDialog = signal(false);
  displayDeleteSelectedDialog = signal(false);
  deleting = signal(false);
  deleteParams = { fromDate: '', toDate: '' };

  onOpenDeleteSelectedDialog() {
    if (this.selectedCount() === 0) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Cảnh báo',
        detail: 'Vui lòng tick chọn ít nhất một nhật ký cần xóa.'
      });
      return;
    }
    this.displayDeleteSelectedDialog.set(true);
  }

  onConfirmDeleteSelected() {
    const ids = this.selectedIds();
    if (ids.length === 0) {
      return;
    }

    this.deleting.set(true);
    this.auditLogService.deleteByIds(ids)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: (res) => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: res.message || 'Đã xóa các nhật ký đã chọn.'
          });
          this.displayDeleteSelectedDialog.set(false);
          this.selectedIds.set([]);
          this.loadAuditLogs();
        },
        error: (err) => {
          const msg = err.error?.message || 'Không thể xóa nhật ký đã chọn.';
          this.messageService.add({ severity: 'error', summary: 'Lỗi xóa nhật ký', detail: msg });
        }
      });
  }

  onOpenDeleteDialog() {
    const today = new Date();
    const thirtyDaysAgo = new Date();
    thirtyDaysAgo.setDate(today.getDate() - 30);

    this.deleteParams = {
      fromDate: this.toDateInputValue(thirtyDaysAgo),
      toDate: this.toDateInputValue(today)
    };
    this.displayDeleteDialog.set(true);
  }

  onConfirmDelete() {
    if (!this.deleteParams.fromDate || !this.deleteParams.toDate) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Vui lòng chọn khoảng thời gian cần xóa.' });
      return;
    }

    const fromDateObj = new Date(this.deleteParams.fromDate);
    const toDateObj = new Date(this.deleteParams.toDate);
    fromDateObj.setHours(0, 0, 0, 0);
    toDateObj.setHours(23, 59, 59, 999);

    if (fromDateObj > toDateObj) {
      this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Từ ngày không thể lớn hơn Đến ngày.' });
      return;
    }

    this.deleting.set(true);
    this.auditLogService.deleteByDateRange(fromDateObj.toISOString(), toDateObj.toISOString())
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: (res) => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: res.message || 'Đã thực hiện dọn dẹp nhật ký hệ thống.'
          });
          this.displayDeleteDialog.set(false);
          this.loadAuditLogs();
        },
        error: (err) => {
          const msg = err.error?.message || 'Không thể xóa nhật ký do lỗi kết nối.';
          this.messageService.add({ severity: 'error', summary: 'Lỗi xóa nhật ký', detail: msg });
        }
      });
  }
}
