import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  DeleteConfirmDialogComponent,
  EcoPaginatorComponent,
  EcoInputTreeSelectComponent,
  WfBreadcrumbComponent
} from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService, TreeNode } from 'primeng/api';
import { DialogModule } from 'primeng/dialog';
import { DatePickerModule } from 'primeng/datepicker';
import { TabsModule } from 'primeng/tabs';
import { Subject, catchError, finalize, forkJoin, map, of, switchMap } from 'rxjs';
import { AuthService, AuditLogService, AuditLogLookupItem, AuditLogQueryParams } from '@sohoa.frontend/shared/core';
import { environment } from '@env/environment';

const LOG_GROUP_OPERATION = 'THAO_TAC';
const LOG_GROUP_BUSINESS = 'NGHIEP_VU';

interface AuditLogView {
  id: string;
  action: string;
  actionName: string;
  userName: string;
  fullName: string;
  timestamp: string;
  occurredAtMs: number;
  details: string;
  resourceType?: string;
  resourceTypeName: string;
  resourceName: string;
}

@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    DialogModule,
    DatePickerModule,
    TabsModule,
    WfBreadcrumbComponent,
    EcoInputTreeSelectComponent,
    EcoPaginatorComponent,
    DeleteConfirmDialogComponent
  ],
  providers: [MessageService],
  templateUrl: './audit-log.component.html',
  styleUrl: './audit-log.component.scss'
})
export class AuditLogComponent implements OnInit {
  activeTab = signal<'0' | '1'>('0');
  appliedLogGroup = signal(LOG_GROUP_OPERATION);

  searchTerm = signal('');
  filterAction = signal('');
  filterResourceType = signal('');
  filterFromDate = signal<Date | null>(null);
  filterToDate = signal<Date | null>(null);
  filterUnitIds = signal<number[]>([]);

  appliedSearchTerm = signal('');
  appliedAction = signal('');
  appliedResourceType = signal('');
  appliedFromDate = signal('');
  appliedToDate = signal('');
  appliedUnitIds = signal<number[]>([]);

  actionLookups = signal<AuditLogLookupItem[]>([]);
  resourceTypeLookups = signal<AuditLogLookupItem[]>([]);
  organizationUnits = signal<any[]>([]);

  orgUnitTree = computed(() => this.buildOrgTree(this.organizationUnits()));
  primengOrgUnitTree = computed((): TreeNode[] => {
    const buildPrimeNGNodes = (nodes: any[]): TreeNode[] =>
      nodes.map(n => {
        const children = n.children?.length ? buildPrimeNGNodes(n.children) : undefined;
        return {
          key: String(n.id),
          label: n.name,
          data: n,
          selectable: true,
          leaf: !children?.length,
          children
        } as TreeNode;
      });
    return buildPrimeNGNodes(this.orgUnitTree());
  });

  logs = signal<AuditLogView[]>([]);
  loading = signal(false);
  exporting = signal(false);
  currentPage = signal(1);
  pageSize = signal(10);
  totalCount = signal(0);

  private readonly loadTrigger = new Subject<void>();
  private auditLogService = inject(AuditLogService);
  private messageService = inject(MessageService);
  private http = inject(HttpClient);
  public authService = inject(AuthService);

  canExport = computed(() => {
    const perms = this.authService.currentUserPermissions();
    return perms.includes('AUDIT_LOG_EXPORT') || perms.includes('AUDIT_LOG_VIEW');
  });

  canDelete = computed(() => this.authService.currentUserPermissions().includes('AUDIT_LOG_DELETE'));

  selectedIds = signal<string[]>([]);
  selectedCount = computed(() => this.selectedIds().length);

  // Chuẩn hóa nhãn hiển thị theo số lượng nhật ký được chọn.
  readonly deleteEntityLabel = computed(() => {
    const count = this.selectedCount();

    return count > 1 ? `${count} nhật ký` : 'Nhật ký';
  });

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

    this.filterFromDate.set(monthAgo);
    this.filterToDate.set(today);
    this.appliedFromDate.set(this.toDateInputValue(monthAgo));
    this.appliedToDate.set(this.toDateInputValue(today));

    this.loadLookups(this.appliedLogGroup());
    this.loadOrganizationUnits();
    this.authService.ensurePermissionsLoaded().subscribe(() => this.loadAuditLogs());
  }

  private loadLookups(logGroup: string) {
    this.auditLogService.getLookups(logGroup).subscribe({
      next: (res) => {
        this.actionLookups.set(res?.actions || []);
        this.resourceTypeLookups.set(res?.resourceTypes || []);
        this.pruneStaleResourceTypeFilter();
      },
      error: () => {
        this.actionLookups.set([]);
        this.resourceTypeLookups.set([]);
      }
    });
  }

  /** Bỏ chọn "Loại đối tượng" nếu giá trị hiện tại không còn hợp lệ ở tab vừa chuyển sang. */
  private pruneStaleResourceTypeFilter() {
    const validCodes = this.resourceTypeLookups().map((r) => r.code);
    if (this.filterResourceType() && !validCodes.includes(this.filterResourceType())) {
      this.filterResourceType.set('');
    }
    if (this.appliedResourceType() && !validCodes.includes(this.appliedResourceType())) {
      this.appliedResourceType.set('');
    }
  }

  private loadOrganizationUnits() {
    this.http.get<any[]>(`${environment.apiGatewayUrl}/api/v1/organization-units/lookup`).subscribe({
      next: (res) => this.organizationUnits.set(Array.isArray(res) ? res : []),
      error: () => this.organizationUnits.set([])
    });
  }

  private buildOrgTree(units: any[]): any[] {
    const map = new Map<number, any>();
    const roots: any[] = [];
    units.forEach(u => {
      const id = Number(u.id);
      if (!id) return;
      map.set(id, { ...u, id, parentId: u.parentId != null ? Number(u.parentId) : null, children: [] as any[] });
    });
    map.forEach(node => {
      if (node.parentId && map.has(node.parentId)) {
        map.get(node.parentId)!.children.push(node);
      } else {
        roots.push(node);
      }
    });
    return roots;
  }

  onTabChange(value: string | number | undefined) {
    const tab = String(value) === '1' ? '1' : '0';
    const logGroup = tab === '1' ? LOG_GROUP_BUSINESS : LOG_GROUP_OPERATION;
    this.activeTab.set(tab);
    this.appliedLogGroup.set(logGroup);
    this.currentPage.set(1);
    this.selectedIds.set([]);
    this.loadLookups(logGroup);
    this.loadAuditLogs();
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

  private buildQueryParams(): AuditLogQueryParams {
    const params: AuditLogQueryParams = {
      page: this.currentPage(),
      pageSize: this.pageSize(),
      logGroup: this.appliedLogGroup()
    };

    const keyword = this.appliedSearchTerm().trim();
    const resourceType = this.appliedResourceType().trim();
    const unitIds = this.appliedUnitIds();

    if (keyword) params['keyword'] = keyword;
    if (this.appliedAction()) params['action'] = this.appliedAction();
    if (resourceType) params['resourceType'] = resourceType;
    if (this.appliedFromDate()) params['fromDate'] = this.toIsoStartOfDay(this.appliedFromDate());
    if (this.appliedToDate()) params['toDate'] = this.toIsoEndOfDay(this.appliedToDate());
    if (unitIds.length) params['unitIds'] = unitIds.map(String);

    return params;
  }

  loadAuditLogs() {
    this.loadTrigger.next();
  }

  private mapLog(item: {
    id?: string;
    action?: string;
    actionName?: string;
    userName?: string;
    actorFullName?: string;
    fullName?: string;
    timestamp?: string;
    occurredAt?: string;
    details?: string;
    resourceType?: string;
    resourceTypeName?: string;
    resourceName?: string;
  }): AuditLogView {
    const rawTime = item.timestamp || item.occurredAt || new Date().toISOString();
    const occurredAt = new Date(rawTime);
    return {
      id: item.id || '',
      action: item.action || 'USER_ACTION',
      actionName: item.actionName || this.getActionLabel(item.action || 'USER_ACTION'),
      userName: item.userName || 'system',
      fullName: item.fullName || item.actorFullName || item.userName || 'system',
      timestamp: occurredAt.toLocaleString('vi-VN'),
      occurredAtMs: occurredAt.getTime(),
      details: item.details || 'Thao tác hệ thống',
      resourceType: item.resourceType,
      resourceTypeName: item.resourceTypeName || this.getResourceTypeLabel(item.resourceType),
      resourceName: item.resourceName?.trim() || 'Chưa xác định (dữ liệu cũ)'
    };
  }

  getActionLabel(code: string): string {
    return this.actionLookups().find((a) => a.code === code)?.label || code;
  }

  getResourceTypeLabel(code?: string): string {
    if (!code) return '—';
    return this.resourceTypeLookups().find((r) => r.code === code)?.label || code;
  }

  onSearch() {
    this.appliedSearchTerm.set(this.searchTerm());
    this.appliedAction.set(this.filterAction());
    this.appliedResourceType.set(this.filterResourceType());
    this.appliedFromDate.set(this.filterFromDate() ? this.toDateInputValue(this.filterFromDate()!) : '');
    this.appliedToDate.set(this.filterToDate() ? this.toDateInputValue(this.filterToDate()!) : '');
    this.appliedUnitIds.set(this.filterUnitIds());
    this.currentPage.set(1);
    this.selectedIds.set([]);
    this.loadAuditLogs();
  }

  onSelectFilterChange(): void {
    this.appliedAction.set(this.filterAction());
    this.appliedResourceType.set(this.filterResourceType());
    this.appliedUnitIds.set(this.filterUnitIds());
    this.currentPage.set(1);
    this.selectedIds.set([]);
    this.loadAuditLogs();
  }

  onReset() {
    const today = new Date();
    const monthAgo = new Date();
    monthAgo.setDate(today.getDate() - 30);

    this.searchTerm.set('');
    this.filterAction.set('');
    this.filterResourceType.set('');
    this.filterUnitIds.set([]);
    this.filterFromDate.set(monthAgo);
    this.filterToDate.set(today);
    this.onSearch();
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

  onPageSizeChange(pageSize: number) {
    this.pageSize.set(pageSize);
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
    const exportPageSize = 500;
    this.exporting.set(true);
    this.auditLogService.getAuditLogs({ ...exportParams, page: 1, pageSize: exportPageSize })
      .pipe(
        switchMap((firstPage) => {
          const pageCount = Math.ceil((firstPage.totalCount || 0) / exportPageSize);
          if (pageCount <= 1) return of(firstPage.items || []);
          const remainingPages = Array.from({ length: pageCount - 1 }, (_, index) =>
            this.auditLogService.getAuditLogs({
              ...exportParams,
              page: index + 2,
              pageSize: exportPageSize
            })
          );
          return forkJoin(remainingPages).pipe(
            map((responses) => [
              ...(firstPage.items || []),
              ...responses.flatMap((response) => response.items || [])
            ])
          );
        }),
        finalize(() => this.exporting.set(false))
      )
      .subscribe({
        next: async (items) => {
          const logs = items
            .map((item) => this.mapLog(item))
            .sort((a, b) => b.occurredAtMs - a.occurredAtMs);
          const worksheetRows = logs.map((log, index) => ({
            STT: index + 1,
            'Tên đăng nhập': log.userName,
            'Họ và tên': log.fullName,
            'Loại hình hành động': log.actionName,
            'Đối tượng': `${log.resourceTypeName}${log.resourceName ? ` - ${log.resourceName}` : ''}`,
            'Thời gian': log.timestamp,
            'Chi tiết hành động': log.details
          }));
          const XLSX = await import('xlsx');
          const worksheet = XLSX.utils.json_to_sheet(worksheetRows, {
            header: ['STT', 'Tên đăng nhập', 'Họ và tên', 'Loại hình hành động', 'Đối tượng', 'Thời gian', 'Chi tiết hành động']
          });
          worksheet['!cols'] = [
            { wch: 8 }, { wch: 22 }, { wch: 28 }, { wch: 28 },
            { wch: 40 }, { wch: 22 }, { wch: 60 }
          ];
          const workbook = XLSX.utils.book_new();
          XLSX.utils.book_append_sheet(workbook, worksheet, 'Nhật ký hệ thống');
          const blob = new Blob([XLSX.write(workbook, { bookType: 'xlsx', type: 'array' })], {
            type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
          });
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = `AuditLog_${this.appliedFromDate()}_${this.appliedToDate()}.xlsx`;
          link.click();
          window.URL.revokeObjectURL(url);
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xuất tệp tin nhật ký hệ thống.' });
        },
        error: (err) => {
          let detail = 'Không thể xuất nhật ký hệ thống.';
          if (err.error?.message) {
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

  onDeleteSingle(id: string) {
    this.selectedIds.set([id]);
    this.displayDeleteSelectedDialog.set(true);
  }

  onConfirmDeleteSelected(): void {
    const ids = this.selectedIds();

    // Chặn danh sách rỗng hoặc request xóa bị gửi trùng.
    if (ids.length === 0 || this.deleting()) {
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
          const msg =
            err?.error?.message ||
            err?.message ||
            'Không thể xóa nhật ký đã chọn.';
          this.messageService.add({ severity: 'error', summary: 'Lỗi xóa nhật ký', detail: msg });
        }
      });
  }

  onCancelDeleteSelected(): void {
    // Giữ nguyên lựa chọn hiện tại và không đóng popup khi request đang chạy.
    if (this.deleting()) {
      return;
    }

    this.displayDeleteSelectedDialog.set(false);
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

  onCancelDeleteByDate(): void {
    // Không đóng dialog nghiệp vụ khi request xóa đang được xử lý.
    if (this.deleting()) {
      return;
    }

    this.displayDeleteDialog.set(false);
  }

  onConfirmDelete(): void {
    // Chặn request xóa theo khoảng ngày bị gửi trùng.
    if (this.deleting()) {
      return;
    }

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
          const msg =
            err?.error?.message ||
            err?.message ||
            'Không thể xóa nhật ký do lỗi kết nối.';
          this.messageService.add({ severity: 'error', summary: 'Lỗi xóa nhật ký', detail: msg });
        }
      });
  }
}
