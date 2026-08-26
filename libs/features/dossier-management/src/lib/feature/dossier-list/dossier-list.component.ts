import { Component, OnInit, signal, computed, inject, Output, EventEmitter, Input } from '@angular/core';

import { CommonModule } from '@angular/common';

import { FormsModule } from '@angular/forms';

import { ToastModule } from 'primeng/toast';

import { DialogModule } from 'primeng/dialog';

import { MenuModule } from 'primeng/menu';

import { SelectModule } from 'primeng/select';

import { MessageService, MenuItem } from 'primeng/api';

import { BhsCatalogColumn, DossierManagementService, DossierWorkflowAction, normalizeDossierWorkflowAction } from '../../data-access/dossier-management.service';
import { AuthService, WorkflowService } from '@sohoa.frontend/shared/core';
import {
  isRejectWorkflowLabel,
  isApproveWorkflowLabel,
  isRejectWorkflowAction,
  sortWorkflowActionsRejectLast,
  filterUsersByRequiredRole,
  resolveEligibleAssigneeGroupParams,
  resolveDefaultNextAssignee,
  resolveNextUserCandidates,
} from '../../utils/dossier-workflow-bpmn.util';
import {
  DossierListTab,
  DossierMenuScope,
  DossierTabCounts,
  getDefaultTabForMenuScope,
  getDossierStatusLabel,
  getDossierStatusPillClass,
  getTabsForMenuScope,
} from '../../utils/dossier-status.util';
import {
  canMutateDossierOnCreatorMenu as hasCreatorMenuMutatePermission,
  hasDossierCreatePermission,
} from '../../utils/dossier-permission.util';
import { isUserAuthorizedForWorkflowAction, buildListItemPatchFromSources, shouldKeepItemOnTab, DossierListItemPatch } from '../../utils/dossier-workflow-auth.util';
import { catchError, finalize, forkJoin, map, of, switchMap } from 'rxjs';

function tabLabel(tab: DossierListTab, kindId?: number): string {
  const labels: Partial<Record<DossierListTab, string>> = {
    draft: kindId === 1 ? 'Nháp' : 'Tạo mới',
    'pending-action': 'Chờ xử lý',
    'in-progress': 'Đang xử lý',
    completed: kindId === 1 ? 'Hoàn thành' : 'Đã duyệt',
    returned: 'Trả lại',
  };
  return labels[tab] ?? '';
}

function normalizeTabCounts(raw: unknown): DossierTabCounts {
  const source = (raw ?? {}) as Record<string, unknown>;
  return {
    draft: Number(source['draft'] ?? source['Draft'] ?? 0),
    pendingAction: Number(source['pendingAction'] ?? source['PendingAction'] ?? 0),
    inProgress: Number(source['inProgress'] ?? source['InProgress'] ?? 0),
    completed: Number(source['completed'] ?? source['Completed'] ?? 0),
    returned: Number(source['returned'] ?? source['Returned'] ?? 0),
  };
}

@Component({

  selector: 'app-dossier-list',

  standalone: true,

  imports: [CommonModule, FormsModule, ToastModule, DialogModule, MenuModule, SelectModule],

  templateUrl: './dossier-list.component.html',

  styleUrls: ['./dossier-list.component.scss'],

})

export class DossierListComponent implements OnInit {

  @Input({ required: true }) set menuScope(value: DossierMenuScope) {
    const changed = this.menuScopeSignal() !== value;
    this.menuScopeSignal.set(value);
    this.activeTab.set(getDefaultTabForMenuScope(value));
    if (changed && this.listBootstrapped) {
      this.refreshList();
    }
  }

  @Input() set kindId(value: number | undefined) {
    const id = value ?? 2;
    const changed = this.kindIdSignal() !== id;
    this.kindIdSignal.set(id);
    this.service.setKindContext(id);
    if (changed && this.listBootstrapped) {
      this.refreshList();
    }
  }

  kindIdSignal = signal<number>(2);
  private listBootstrapped = false;
  private menuScopeSignal = signal<DossierMenuScope>('creator');
  visibleTabs = computed(() => getTabsForMenuScope(this.menuScopeSignal(), this.kindIdSignal()));
  isCreatorMenu = computed(() => this.menuScopeSignal() === 'creator');
  isApproverMenu = computed(() => this.menuScopeSignal() === 'approver');
  isDigitization = computed(() => this.kindIdSignal() === 1);

  tabLabel = tabLabel;

  canCreateDossier(): boolean {
    return hasDossierCreatePermission(this.authService, this.isDigitization());
  }

  private service = inject(DossierManagementService);

  private messageService = inject(MessageService);

  authService = inject(AuthService);

  private workflowSvc = inject(WorkflowService);

  @Output() viewDetail = new EventEmitter<string>();

  @Output() edit = new EventEmitter<string>();

  @Output() create = new EventEmitter<void>();



  items = signal<any[]>([]);

  loading = signal<boolean>(false);

  totalCount = signal<number>(0);



  currentPage = signal<number>(1);

  pageSize = signal<number>(10);



  searchKeyword = signal<string>('');

  filterInfrastructureId = signal<string | null>(null);

  filterDossierTypeId = signal<string | null>(null);

  filterEquipmentId = signal<string | null>(null);

  private appliedFilters = signal<{
    keyword: string;
    infrastructureId: string | null;
    dossierTypeId: string | null;
    equipmentId: string | null;
  }>({
    keyword: '',
    infrastructureId: null,
    dossierTypeId: null,
    equipmentId: null,
  });

  totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));

  infrastructureSearchKeyword = signal<string>('');

  isInfrastructureDropdownOpen = signal<boolean>(false);

  equipmentSearchKeyword = signal<string>('');

  isEquipmentDropdownOpen = signal<boolean>(false);



  infrastructures = signal<any[]>([]);

  dossierTypes = signal<any[]>([]);

  equipments = signal<any[]>([]);

  filteredInfrastructures = computed(() => {
    const keyword = this.infrastructureSearchKeyword().trim().toLocaleLowerCase();
    if (!keyword) return this.infrastructures();
    return this.infrastructures().filter((item) =>
      String(item.name ?? '').toLocaleLowerCase().includes(keyword)
    );
  });

  filteredEquipments = computed(() => {
    const keyword = this.equipmentSearchKeyword().trim().toLocaleLowerCase();
    if (!keyword) return this.equipments();
    return this.equipments().filter((item) => {
      const name = String(item.name ?? '').toLocaleLowerCase();
      const code = String(item.code ?? '').toLocaleLowerCase();
      return name.includes(keyword) || code.includes(keyword);
    });
  });

  bhsColumns = signal<BhsCatalogColumn[]>([]);

  activeTab = signal<DossierListTab>('draft');

  tabCounts = signal<DossierTabCounts | null>(null);

  getTabBadgeCount(tab: DossierListTab): number {
    const counts = this.tabCounts();
    if (!counts) return 0;
    switch (tab) {
      case 'draft': return counts.draft;
      case 'pending-action': return counts.pendingAction;
      case 'in-progress': return counts.inProgress;
      case 'completed': return counts.completed;
      case 'returned': return counts.returned;
      default: return 0;
    }
  }



  showDeleteConfirm = signal<boolean>(false);
  showImportResultDialog = signal<boolean>(false);
  importResult = signal<any>(null);

  deleteTarget = signal<any>(null);

  deleting = signal<boolean>(false);

  showQuickActionDialog = signal<boolean>(false);
  quickActionLoading = signal<boolean>(false);
  quickActionUsersLoading = signal<boolean>(false);
  quickActionSubmitting = signal<boolean>(false);
  quickActionComment = signal<string>('');
  selectedNextUserId = signal<string>('');
  quickActionDossierId = signal<string | null>(null);
  pendingQuickAction = signal<DossierWorkflowAction | null>(null);
  pendingQuickActionMeta = signal<{
    label: string;
    targetNodeId: string;
    requiresUser: boolean;
    requiredRole: string;
    unitGroupIds?: string | null;
    systemGroupIds?: string | null;
    requireSameUnit?: boolean;
    staticAssigneeId?: string | null;
  } | null>(null);
  eligibleQuickActionUsers = signal<any[]>([]);
  loadingEligibleQuickActionUsers = signal<boolean>(false);
  users = signal<any[]>([]);
  showQuickCompleteConfirm = signal<boolean>(false);
  showQuickSubmitConfirm = signal<boolean>(false);
  selectedQuickItem = signal<any>(null);
  quickSubmitNextStepInfo = signal<any>(null);
  quickSubmitSelectedNextUser = signal<string>('');
  quickSubmitSubmitting = signal<boolean>(false);
  eligibleQuickSubmitUsers = signal<any[]>([]);
  loadingEligibleQuickSubmitUsers = signal<boolean>(false);

  // Hợp nhất nhóm quyền hệ thống/đơn vị/người cụ thể đã cấu hình trên bước; nếu không cấu hình
  // gì cả, danh sách để trống — không dùng toàn bộ user làm dự phòng (xem resolveNextUserCandidates).
  filteredQuickSubmitNextUsers = computed(() => resolveNextUserCandidates({
    info: this.quickSubmitNextStepInfo(),
    allUsers: this.users(),
    eligibleUsers: this.eligibleQuickSubmitUsers(),
  }));

  private loadEligibleQuickSubmitUsers(info: any): void {
    this.eligibleQuickSubmitUsers.set([]);
    this.quickSubmitSelectedNextUser.set('');
    const groupParams = resolveEligibleAssigneeGroupParams(info);
    if (!groupParams) return;
    const unitId = info.requireSameUnit ? (this.authService.getUserUnitId() ?? undefined) : undefined;
    this.loadingEligibleQuickSubmitUsers.set(true);
    this.workflowSvc.getEligibleAssignees(groupParams.systemGroupIds, groupParams.unitGroupIds, unitId, undefined, groupParams.assigneeIds)
      .pipe(finalize(() => this.loadingEligibleQuickSubmitUsers.set(false)))
      .subscribe({
        next: (list) => {
          const arr = Array.isArray(list) ? list : [];
          this.eligibleQuickSubmitUsers.set(arr);
          this.quickSubmitSelectedNextUser.set(resolveDefaultNextAssignee(info, arr));
        },
        error: () => this.eligibleQuickSubmitUsers.set([])
      });
  }

  // Hợp nhất nhóm quyền hệ thống/đơn vị/người cụ thể đã cấu hình trên bước đích; nếu không cấu
  // hình gì cả, danh sách để trống (xem resolveNextUserCandidates).
  filteredNextUsers = computed(() => resolveNextUserCandidates({
    info: this.pendingQuickActionMeta(),
    allUsers: this.users(),
    eligibleUsers: this.eligibleQuickActionUsers(),
  }));

  private loadEligibleQuickActionUsers(meta: any): void {
    this.eligibleQuickActionUsers.set([]);
    const groupParams = resolveEligibleAssigneeGroupParams(meta);
    if (!groupParams) return;
    const unitId = meta?.requireSameUnit ? (this.authService.getUserUnitId() ?? undefined) : undefined;
    this.loadingEligibleQuickActionUsers.set(true);
    this.workflowSvc.getEligibleAssignees(groupParams.systemGroupIds, groupParams.unitGroupIds, unitId, undefined, groupParams.assigneeIds)
      .pipe(finalize(() => this.loadingEligibleQuickActionUsers.set(false)))
      .subscribe({
        next: (list) => {
          const arr = Array.isArray(list) ? list : [];
          this.eligibleQuickActionUsers.set(arr);
          this.selectedNextUserId.set(resolveDefaultNextAssignee(meta, arr));
        },
        error: () => this.eligibleQuickActionUsers.set([])
      });
  }

  // Bulk Approval Signals
  selectedDossierIds = signal<Set<string>>(new Set());
  showBulkApproveConfirm = signal<boolean>(false);
  bulkApproveComment = signal<string>('Đồng ý phê duyệt hàng loạt');
  bulkApproveSubmitting = signal<boolean>(false);

  selectedDossiersList = computed(() => {
    const ids = this.selectedDossierIds();
    return this.items().filter((item) => ids.has(item.id));
  });

  tableColSpan = computed(() => {
    const base = this.bhsColumns().length + 6;
    return (this.isApproverMenu() && this.activeTab() === 'pending-action') ? base + 1 : base;
  });

  getDossierStatusPillClass = getDossierStatusPillClass;

  getDossierStatusLabel = getDossierStatusLabel;

  deleteTargetLabel = computed(() => {

    const item = this.deleteTarget();

    if (!item) return '';

    const firstCol = this.bhsColumns()[0];

    if (firstCol) {

      const val = this.getCatalogValue(item, firstCol);

      if (val !== '-') return val;

    }

    return item.infrastructureName || item.dossierTypeName || 'này';

  });

  completeTargetLabel = computed(() => {
    const item = this.selectedQuickItem();
    if (!item) return '';
    const firstCol = this.bhsColumns()[0];
    if (firstCol) {
      const val = this.getCatalogValue(item, firstCol);
      if (val !== '-') return val;
    }
    return item.infrastructureName || item.dossierTypeName || 'này';
  });



  ngOnInit() {
    this.loadLookups();
    this.listBootstrapped = true;
    this.refreshList();
  }



  selectTab(tab: DossierListTab) {

    if (this.activeTab() === tab) return;

    this.clearSelection();

    this.activeTab.set(tab);

    this.currentPage.set(1);

    this.loadData();

  }



  onApplyFilters() {

    this.currentPage.set(1);

    this.appliedFilters.set({
      keyword: this.searchKeyword().trim(),
      infrastructureId: this.filterInfrastructureId(),
      dossierTypeId: this.filterDossierTypeId(),
      equipmentId: this.filterEquipmentId(),
    });

    this.refreshList();

  }

  onResetFilters() {
    this.searchKeyword.set('');
    this.filterInfrastructureId.set(null);
    this.filterDossierTypeId.set(null);
    this.filterEquipmentId.set(null);
    this.infrastructureSearchKeyword.set('');
    this.equipmentSearchKeyword.set('');
    this.isInfrastructureDropdownOpen.set(false);
    this.isEquipmentDropdownOpen.set(false);
    this.equipments.set([]);
    this.currentPage.set(1);
    this.appliedFilters.set({
      keyword: '',
      infrastructureId: null,
      dossierTypeId: null,
      equipmentId: null,
    });
    this.refreshList();
  }

  toggleInfrastructureDropdown() {
    this.isInfrastructureDropdownOpen.update((open) => !open);
    this.isEquipmentDropdownOpen.set(false);
  }

  toggleEquipmentDropdown() {
    if (!this.filterInfrastructureId()) return;
    this.isEquipmentDropdownOpen.update((open) => !open);
    this.isInfrastructureDropdownOpen.set(false);
  }

  onDossierTypeFilterChange(dossierTypeId: string | null) {
    this.filterDossierTypeId.set(dossierTypeId || null);
    this.onApplyFilters();
  }

  onEquipmentFilterChange(equipmentId: string | null) {
    this.filterEquipmentId.set(equipmentId || null);
    this.onApplyFilters();
  }

  selectEquipment(equipmentId: string | null) {
    this.equipmentSearchKeyword.set('');
    this.isEquipmentDropdownOpen.set(false);
    this.onEquipmentFilterChange(equipmentId);
  }

  selectInfrastructure(infrastructureId: string | null) {
    this.filterInfrastructureId.set(infrastructureId);
    this.filterEquipmentId.set(null);
    this.equipments.set([]);
    this.infrastructureSearchKeyword.set('');
    this.equipmentSearchKeyword.set('');
    this.isInfrastructureDropdownOpen.set(false);
    this.isEquipmentDropdownOpen.set(false);

    this.onApplyFilters();

    if (infrastructureId) {
      this.service.getEquipmentLookup({ infrastructureId, pageSize: 1000 }).subscribe({
        next: (res) => this.equipments.set(Array.isArray(res) ? res : (res?.items ?? [])),
        error: () => {
          this.equipments.set([]);
          console.error('Failed to load equipments');
        },
      });
    }

  }

  selectedInfrastructureLabel(): string {
    const infrastructureId = this.filterInfrastructureId();
    if (!infrastructureId) return '-- Tất cả trạm/đường dây --';
    return this.infrastructures().find((item) => String(item.id) === String(infrastructureId))?.name
      ?? '-- Tất cả trạm/đường dây --';
  }

  selectedEquipmentLabel(): string {
    const equipmentId = this.filterEquipmentId();
    if (!equipmentId) return '-- Tất cả thiết bị --';
    const selected = this.equipments().find((item) => String(item.id) === String(equipmentId));
    if (!selected) return '-- Tất cả thiết bị --';
    return selected.code ? `${selected.code} - ${selected.name}` : selected.name;
  }



  refreshList() {

    this.loadTabCounts();

    this.loadData();

  }

  /** Cập nhật 1 dòng từ API Oracle + workflow (tránh chờ ES đồng bộ). */
  private refreshListItemAfterMutation(id: string, onDone?: () => void) {
    forkJoin({
      detail: this.service.getDossierById(id),
      workflow: this.service.getWorkflowDetail(id, this.kindIdSignal()).pipe(catchError(() => of(null))),
    }).subscribe({
      next: ({ detail, workflow }) => {
        const patch = buildListItemPatchFromSources(detail, workflow);
        this.applyListItemPatch(id, patch);
        onDone?.();
      },
      error: () => {
        this.refreshList();
        onDone?.();
      },
    });
  }

  private applyListItemPatch(id: string, patch: DossierListItemPatch) {
    const tab = this.activeTab();

    if (tab === 'pending-action') {
      const stillInInbox = isUserAuthorizedForWorkflowAction({
        authService: this.authService,
        menuScope: this.menuScopeSignal(),
        currentAssignees: patch.currentAssignees,
        assigneeUserId: patch.currentAssignees[0],
      });
      if (!stillInInbox) {
        this.items.update((list) => list.filter((item) => item.id !== id));
        this.totalCount.update((count) => Math.max(0, count - 1));
        return;
      }
    } else if (!shouldKeepItemOnTab(tab, {
      statusId: patch.statusId,
      workflowInstanceId: patch.workflowInstanceId,
    })) {
      this.items.update((list) => list.filter((item) => item.id !== id));
      this.totalCount.update((count) => Math.max(0, count - 1));
      return;
    }

    this.items.update((list) =>
      list.map((item) => (item.id === id ? { ...item, ...patch } : item))
    );
  }



  loadTabCounts() {
    const tabs = this.visibleTabs();
    const appliedFilters = this.appliedFilters();
    const baseFilter = {
      menuScope: this.menuScopeSignal(),
      kindId: this.kindIdSignal(),
      keyword: appliedFilters.keyword,
      infrastructureId: appliedFilters.infrastructureId || undefined,
      dossierTypeId: appliedFilters.dossierTypeId || undefined,
      equipmentId: appliedFilters.equipmentId || undefined,
      page: 1,
      pageSize: 1,
    };

    forkJoin(tabs.map((tab) => this.service.getDossiers({ ...baseFilter, tab }))).subscribe({
      next: (responses) => {
        const counts: DossierTabCounts = {
          draft: 0,
          pendingAction: 0,
          inProgress: 0,
          completed: 0,
          returned: 0,
        };

        tabs.forEach((tab, index) => {
          const total = Number(responses[index]?.totalCount ?? responses[index]?.TotalCount ?? 0);
          if (tab === 'draft') counts.draft = total;
          if (tab === 'pending-action') counts.pendingAction = total;
          if (tab === 'in-progress') counts.inProgress = total;
          if (tab === 'completed') counts.completed = total;
          if (tab === 'returned') counts.returned = total;
        });

        this.tabCounts.set(counts);
      },
      error: () => console.error('Failed to load dossier tab counts'),
    });

  }



  loadLookups() {

    this.service.getInfrastructureLookup().subscribe({

      next: (res) => this.infrastructures.set(res),

      error: () => console.error('Failed to load infrastructures')

    });

    this.service.getDossierTypeLookup().subscribe({

      next: (res) => this.dossierTypes.set(res),

      error: () => console.error('Failed to load dossier types')

    });

    this.service.getBhsCatalogColumns().subscribe({

      next: (cols) => this.bhsColumns.set(cols),

      error: () => console.error('Failed to load BHS catalog columns')

    });

  }



  loadData() {

    this.loading.set(true);
    this.items.set([]);
    const appliedFilters = this.appliedFilters();

    const filter = {
      menuScope: this.menuScopeSignal(),
      kindId: this.kindIdSignal(),
      tab: this.activeTab(),
      keyword: appliedFilters.keyword,
      infrastructureId: appliedFilters.infrastructureId || undefined,
      dossierTypeId: appliedFilters.dossierTypeId || undefined,
      equipmentId: appliedFilters.equipmentId || undefined,
      page: this.currentPage(),
      pageSize: this.pageSize()
    };



    this.service.getDossiers(filter).subscribe({

      next: (res) => {
        this.items.set(res.items || []);
        this.totalCount.set(res.totalCount || 0);
        this.loading.set(false);
        if (filter.tab === 'draft') {
          const counts = this.tabCounts();
          if (counts) {
            this.tabCounts.set({
              ...counts,
              draft: res.totalCount || 0
            });
          }
        }
      },

      error: () => {

        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách hồ sơ' });

        this.loading.set(false);

      }

    });

  }



  exporting = signal<boolean>(false);

  /**
   * Xuất Excel toàn bộ hồ sơ khớp bộ lọc/tab hiện tại (không chỉ trang đang xem) — tải tuần tự nhiều
   * trang (giống audit-log.component.ts) rồi dựng file .xlsx phía trình duyệt bằng đúng các cột đang
   * hiển thị trên bảng (kể cả cột BHS động), tái dùng các hàm định dạng đã có của bảng.
   */
  onExportExcel(): void {
    if (this.exporting()) return;

    const appliedFilters = this.appliedFilters();
    const exportPageSize = 500;
    const baseFilter = {
      menuScope: this.menuScopeSignal(),
      kindId: this.kindIdSignal(),
      tab: this.activeTab(),
      keyword: appliedFilters.keyword,
      infrastructureId: appliedFilters.infrastructureId || undefined,
      dossierTypeId: appliedFilters.dossierTypeId || undefined,
      equipmentId: appliedFilters.equipmentId || undefined,
    };

    this.exporting.set(true);
    this.service.getDossiers({ ...baseFilter, page: 1, pageSize: exportPageSize })
      .pipe(
        switchMap((firstPage) => {
          const pageCount = Math.ceil((firstPage.totalCount || 0) / exportPageSize);
          if (pageCount <= 1) return of(firstPage.items || []);
          const remainingPages = Array.from({ length: pageCount - 1 }, (_, index) =>
            this.service.getDossiers({ ...baseFilter, page: index + 2, pageSize: exportPageSize })
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
          if (!items.length) {
            this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Không có dữ liệu để xuất.' });
            return;
          }

          const cols = this.bhsColumns();
          const handlerColumnLabel = this.activeTab() === 'draft' ? 'Ngày tạo' : 'Người xử lý hiện tại';
          const headers = ['STT', ...cols.map((c) => c.label), 'Trạm / Đường dây', 'Số lượng tài liệu', handlerColumnLabel, 'Trạng thái duyệt'];

          const worksheetRows = items.map((item: any, index: number) => {
            const row: Record<string, any> = { STT: index + 1 };
            cols.forEach((col) => {
              row[col.label] = this.getCatalogValue(item, col);
            });
            row['Trạm / Đường dây'] = this.getInfrastructureNames(item).join(', ');
            row['Số lượng tài liệu'] = item.documentCount ?? 0;
            row[handlerColumnLabel] = this.activeTab() === 'draft'
              ? this.formatCreatedDate(item?.createdDate ?? item?.CreatedDate)
              : this.getCurrentHandlerName(item);
            row['Trạng thái duyệt'] = this.getDossierStatusLabel(item.statusId, item.statusName);
            return row;
          });

          const XLSX = await import('xlsx');
          const worksheet = XLSX.utils.json_to_sheet(worksheetRows, { header: headers });
          const workbook = XLSX.utils.book_new();
          XLSX.utils.book_append_sheet(workbook, worksheet, 'Danh sách hồ sơ');
          const blob = new Blob([XLSX.write(workbook, { bookType: 'xlsx', type: 'array' })], {
            type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
          });
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = `DanhSachHoSo_${new Date().toISOString().slice(0, 10)}.xlsx`;
          link.click();
          window.URL.revokeObjectURL(url);
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xuất danh sách hồ sơ.' });
        },
        error: (err: any) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi xuất file',
            detail: err?.error?.message || 'Không thể xuất danh sách hồ sơ.'
          });
        }
      });
  }

  getCatalogValue(item: any, col: BhsCatalogColumn): string {

    const data = item?.catalogData ?? item?.CatalogData ?? {};

    const value = data[col.key] ?? data[col.code];

    return value != null && String(value).trim() !== '' ? String(value) : '-';

  }

  getInfrastructureName(item: any): string {
    return this.getInfrastructureNames(item).join(', ');
  }

  getInfrastructureNames(item: any): string[] {
    const infrastructures = item?.infrastructures ?? item?.Infrastructures;
    if (Array.isArray(infrastructures)) {
      const names = infrastructures
        .map((infrastructure: any) => {
          const name = infrastructure?.infrastructureName ?? infrastructure?.InfrastructureName;
          const code = infrastructure?.infrastructureCode ?? infrastructure?.InfrastructureCode;
          if (name == null || String(name).trim() === '') return '';

          const normalizedName = String(name).trim();
          const normalizedCode = code == null ? '' : String(code).trim();
          return normalizedCode ? `${normalizedName} (${normalizedCode})` : normalizedName;
        })
        .filter(Boolean);
      if (names.length) return [...new Set(names)];
    }

    const name = item?.infrastructureName ?? item?.InfrastructureName;
    const code = item?.infrastructureCode ?? item?.InfrastructureCode;
    if (name != null && String(name).trim() !== '') {
      const codes = String(code ?? '').split(',').map((value) => value.trim());
      return String(name)
        .split(',')
        .map((value) => value.trim())
        .filter(Boolean)
        .map((value, index) => codes[index] ? `${value} (${codes[index]})` : value);
    }
    return ['-'];
  }

  getCurrentHandlerName(item: any): string {
    const handlerName = item?.currentHandlerName ?? item?.CurrentHandlerName;
    const creatorUsername = item?.creator?.username ?? item?.Creator?.Username;
    const normalizedUsername = creatorUsername ? String(creatorUsername).trim().toLowerCase() : '';

    if (handlerName != null && String(handlerName).trim() !== '') {
      const normalizedHandler = String(handlerName).trim();
      if (!normalizedUsername || normalizedHandler.toLowerCase() !== normalizedUsername) {
        return normalizedHandler;
      }
    }

    const creatorName = item?.creator?.name ?? item?.Creator?.Name;
    if (creatorName != null && String(creatorName).trim() !== '') {
      const normalizedCreator = String(creatorName).trim();
      if (!normalizedUsername || normalizedCreator.toLowerCase() !== normalizedUsername) {
        return normalizedCreator;
      }
    }

    return '-';
  }

  formatCreatedDate(value: unknown): string {
    if (!value) return '-';
    const date = new Date(String(value));
    if (Number.isNaN(date.getTime())) return '-';
    const day = String(date.getDate()).padStart(2, '0');
    const month = String(date.getMonth() + 1).padStart(2, '0');
    return `${day}/${month}/${date.getFullYear()}`;
  }

  onQuickSubmitNextUserChange(event: Event) {
    const target = event.target as HTMLSelectElement | null;
    this.quickSubmitSelectedNextUser.set(target?.value || '');
  }



  nextPage() {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update(page => page + 1);
      this.loadData();
    }
  }

  prevPage() {
    if (this.currentPage() > 1) {
      this.currentPage.update(page => page - 1);
      this.loadData();
    }
  }

  goToPage(page: any) {
    const targetPage = Number(page);
    if (targetPage >= 1 && targetPage <= this.totalPages()) {
      this.currentPage.set(targetPage);
      this.loadData();
    }
  }

  onPageSizeChange(event: Event) {
    const target = event.target as HTMLSelectElement | null;
    this.pageSize.set(Number(target?.value) || 10);
    this.currentPage.set(1);
    this.loadData();
  }



  onCreateNew() {

    this.create.emit();

  }



  onViewDetail(id: string) {

    this.viewDetail.emit(id);

  }



  onRowPrimaryAction(item: { id: string; status?: string; Status?: string }) {

    if (this.canEditItem(item)) {

      this.onEdit(item.id);

      return;

    }

    this.onViewDetail(item.id);

  }



  isAssignedToCurrentUser(item: any): boolean {
    if (!item) return false;

    const userId = this.authService.getUserId();
    const roles = this.authService.getUserRoles() || [];

    if (roles.includes('ADMIN')) return true;

    const status = item.status ?? item.Status;

    if (status === 'Returned') {
      return this.isCurrentUserCreator(item);
    }

    if (this.activeTab() === 'draft' || status === 'Draft' || status === 'New' || status === 'CompletedInput') {
      return this.isCurrentUserCreator(item);
    }

    if (item.currentAssignees && item.currentAssignees.length > 0) {
      return item.currentAssignees.some((assignee: string) =>
        String(assignee).toLowerCase() === String(userId).toLowerCase()
      );
    }

    return false;
  }

  private isCurrentUserCreator(item: any): boolean {
    const userId = this.authService.getUserId();
    const creatorId = item.creator?.id ?? item.Creator?.Id ?? item.creatorId ?? item.CreatorId;
    const creatorUsername = item.creator?.username ?? item.Creator?.Username ?? item.creatorUsername ?? item.CreatorUsername ?? item.createdBy ?? item.CreatedBy;

    const normalizeGuid = (val: unknown) => val ? String(val).replace(/-/g, '').toLowerCase().trim() : '';
    const normCreatorId = normalizeGuid(creatorId);
    const normUserId = normalizeGuid(userId);

    if (normCreatorId !== '' && normCreatorId === normUserId) return true;

    const normCreatorUsername = creatorUsername ? String(creatorUsername).toLowerCase().trim() : '';
    const normUserUsername = userId ? String(userId).toLowerCase().trim() : '';
    return normCreatorUsername !== '' && normCreatorUsername === normUserUsername;
  }

  canEditItem(item: any): boolean {
    if (this.isApproverMenu()) return false;
    if (!this.canMutateDossierOnCreatorMenu()) return false;

    const status = item.status ?? item.Status;
    const isDraftState = this.activeTab() === 'draft' || this.activeTab() === 'returned' || status === 'Draft' || status === 'New' || status === 'CompletedInput' || status === 'Returned';
    const stepAllowEdit = item.currentStepAllowEdit ?? item.CurrentStepAllowEdit;

    if (isDraftState || stepAllowEdit) {
      return this.isAssignedToCurrentUser(item);
    }

    return false;
  }



  onEdit(id: string) {

    this.edit.emit(id);

  }

  onExportTemplate() {
    this.service.downloadImportTemplate().subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `Mau_Import_Ho_So_${new Date().getTime()}.xlsx`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Tải file mẫu thành công' });
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Thất bại', detail: 'Không thể tải file mẫu. ' + (err?.error?.message || err?.message || '') });
      }
    });
  }

  onFileSelected(event: any) {
    const file = event.target?.files?.[0];
    if (!file) return;

    this.loading.set(true);
    this.service.importDossiers(file).pipe(
      finalize(() => {
        this.loading.set(false);
        if (event.target) {
          event.target.value = '';
        }
      })
    ).subscribe({
      next: (res) => {
        this.importResult.set(res);
        this.showImportResultDialog.set(true);
        this.refreshList();
        this.messageService.add({
          severity: 'info',
          summary: 'Import kết thúc',
          detail: `Thành công: ${res?.successDossiers?.length || 0}, Thất bại: ${res?.failedDossiers?.length || 0}`
        });
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi import',
          detail: err?.error?.message || err?.message || 'Có lỗi xảy ra trong quá trình tải tệp lên.'
        });
      }
    });
  }

  closeImportResultDialog() {
    this.showImportResultDialog.set(false);
    this.importResult.set(null);
  }



  onDelete(item: any) {

    this.deleteTarget.set(item);

    this.showDeleteConfirm.set(true);

  }



  onConfirmDelete() {

    const item = this.deleteTarget();

    if (!item) return;

    this.deleting.set(true);

    this.service.deleteDossier(item.id).subscribe({

      next: () => {

        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xóa hồ sơ' });

        this.showDeleteConfirm.set(false);

        this.deleteTarget.set(null);

        this.deleting.set(false);

        this.refreshList();

      },

      error: (err) => {

        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể xóa hồ sơ' });

        this.deleting.set(false);

        this.showDeleteConfirm.set(false);

      }

    });

  }



  onCancelDelete() {

    this.showDeleteConfirm.set(false);

    this.deleteTarget.set(null);

  }

  checkQuickActionPermission(item: any): boolean {
    const actions = this.getItemAvailableActions(item);
    if (!item || actions.length === 0) return false;
    if (!item.currentAssignees || item.currentAssignees.length === 0) return false;

    const statusId = item.statusId ?? item.StatusId;
    const status = item.status ?? item.Status;

    return isUserAuthorizedForWorkflowAction({
      authService: this.authService,
      menuScope: this.menuScopeSignal(),
      currentAssignees: item.currentAssignees,
      statusId: statusId ?? (status === 'Returned' ? 5 : undefined),
      isCreator: this.isCurrentUserCreator(item),
    });
  }

  private shouldUseResubmit(item: any): boolean {
    if (!this.isCreatorMenu()) return false;
    const status = item.status ?? item.Status;
    return status === 'Returned' || this.activeTab() === 'returned';
  }

  /** Menu quản lý: sửa form/tài liệu cần EDIT hoặc CREATE theo loại hồ sơ. */
  private canMutateDossierOnCreatorMenu(): boolean {
    if (!this.isCreatorMenu()) return false;
    return hasCreatorMenuMutatePermission(this.authService, this.isDigitization());
  }

  onQuickAction(
    dossierId: string,
    action: DossierWorkflowAction,
    meta: { label: string; targetNodeId: string; requiresUser: boolean; requiredRole: string }
  ) {
    const isReject = this.isRejectLabel(meta.label);
    if (meta.requiresUser && !isReject && !this.selectedNextUserId()) {
      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi',
        detail: 'Vui lòng chọn người xử lý bước tiếp theo.',
      });
      return;
    }

    this.quickActionSubmitting.set(true);
    const request = {
      nextNodeId: meta.targetNodeId || action.nextNodeId,
      actionLabel: meta.label || action.name,
      comment: this.quickActionComment() || undefined,
      nextAssigneeUserId: (!isReject && meta.requiresUser) ? this.selectedNextUserId() : undefined,
    };
    const targetItem = this.items().find(i => i.id === dossierId);
    const workflowCall = this.shouldUseResubmit(targetItem ?? { status: this.activeTab() === 'returned' ? 'Returned' : '' })
      ? this.service.resubmitWorkflow(dossierId, request, this.kindIdSignal())
      : this.service.moveWorkflow(dossierId, request, this.kindIdSignal());

    workflowCall.pipe(
      finalize(() => this.quickActionSubmitting.set(false))
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: `Thao tác "${meta.label || action.name}" thành công!`,
        });
        this.closeQuickActionDialog(true);
        this.refreshListItemAfterMutation(dossierId, () => this.loadTabCounts());
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err?.error?.message || 'Không thể thực hiện thao tác nhanh.',
        });
      },
    });
  }

  openQuickActionDialog(item: any, rawAction: Record<string, unknown>) {
    const action = normalizeDossierWorkflowAction(rawAction);
    const meta = this.buildQuickActionMeta(action);

    this.quickActionDossierId.set(item.id);
    this.pendingQuickAction.set(action);
    this.pendingQuickActionMeta.set(meta);
    this.quickActionComment.set('');
    this.selectedNextUserId.set('');
    this.users.set([]);
    this.showQuickActionDialog.set(true);
    this.quickActionLoading.set(false);
    this.loadQuickActionUsers(meta);
    this.loadEligibleQuickActionUsers(meta);
  }

  private getItemAvailableActions(item: any): any[] {
    const actions = item?.availableActions ?? item?.AvailableActions;
    return Array.isArray(actions) ? actions : [];
  }

  private buildQuickActionMeta(action: DossierWorkflowAction) {
    return {
      label: action.name,
      targetNodeId: action.nextNodeId,
      requiresUser: !!action.requiresNextAssignee,
      requiredRole: action.nextStepRole ?? '',
      unitGroupIds: action.unitGroupIds ?? null,
      systemGroupIds: action.systemGroupIds ?? null,
      requireSameUnit: !!action.requireSameUnit,
      staticAssigneeId: action.staticAssigneeId ?? null,
    };
  }

  /** Gọi API users/lookup theo role từ availableActions ES. */
  private loadQuickActionUsers(meta: { requiresUser: boolean; requiredRole: string }) {
    if (!meta.requiresUser) {
      this.users.set([]);
      this.quickActionUsersLoading.set(false);
      return;
    }

    this.quickActionUsersLoading.set(true);
    const roles = meta.requiredRole.split(',').map((r) => r.trim()).filter(Boolean);
    const apiRole = roles.length === 1 ? roles[0] : null;

    this.service.getUsersLookup(apiRole).subscribe({
      next: (users) => {
        const list = Array.isArray(users) ? users : [];
        this.users.set(roles.length > 1 ? filterUsersByRequiredRole(list, meta.requiredRole) : list);
        this.quickActionUsersLoading.set(false);
      },
      error: () => {
        this.messageService.add({
          severity: 'warn',
          summary: 'Cảnh báo',
          detail: 'Không thể tải danh sách người xử lý.',
        });
        this.users.set([]);
        this.quickActionUsersLoading.set(false);
      },
    });
  }

  confirmQuickAction() {
    const dossierId = this.quickActionDossierId();
    const action = this.pendingQuickAction();
    const meta = this.pendingQuickActionMeta();
    if (!dossierId || !action || !meta) return;
    this.onQuickAction(dossierId, action, meta);
  }

  closeQuickActionDialog(force = false) {
    if (!force && this.quickActionSubmitting()) return;
    this.showQuickActionDialog.set(false);
    this.quickActionDossierId.set(null);
    this.pendingQuickAction.set(null);
    this.pendingQuickActionMeta.set(null);
    this.quickActionComment.set('');
    this.selectedNextUserId.set('');
    this.quickActionUsersLoading.set(false);
  }

  isRejectLabel(label?: string | null): boolean {
    return isRejectWorkflowLabel(label);
  }

  isApproveLabel(label?: string | null): boolean {
    return isApproveWorkflowLabel(label);
  }

  quickActionMenuItems: MenuItem[] = [];
  rowActionMenuItems: MenuItem[] = [];

  openRowActionMenu(event: Event, item: any, menu: any): void {
    event.stopPropagation();
    const actions: MenuItem[] = [
      { label: 'Xem chi tiết', title: 'Xem chi tiết', icon: 'pi pi-eye color-teal', command: () => this.onViewDetail(item.id) },
      ...(this.canEditItem(item) ? [{ label: 'Sửa thông tin', title: 'Sửa thông tin', icon: 'pi pi-pencil color-blue', command: () => this.onEdit(item.id) }] : []),
      ...(this.isCreatorMenu() && this.activeTab() === 'draft' && item.statusId === 1
        ? [{ label: 'Hoàn thành nhập liệu', title: 'Hoàn thành nhập liệu', icon: 'pi pi-check-circle color-teal', command: () => this.onQuickCompleteInput(item) }]
        : []),
      ...(this.isCreatorMenu() && this.activeTab() === 'draft' && item.statusId === 2
        ? [{ label: 'Gửi duyệt', title: 'Gửi duyệt', icon: 'pi pi-send color-blue', command: () => this.onQuickSubmitForApproval(item) }]
        : []),
      ...(this.checkQuickActionPermission(item)
        ? sortWorkflowActionsRejectLast(this.getItemAvailableActions(item)).map((act: any) => {
            const isReject = isRejectWorkflowAction(act);
            return {
              label: act.name,
              title: isReject ? 'Từ chối hồ sơ' : 'Duyệt hồ sơ',
              icon: isReject ? 'pi pi-times-circle color-red' : 'pi pi-check-circle color-teal',
              command: () => this.openQuickActionDialog(item, act),
            };
          })
        : []),
      ...(this.isCreatorMenu() && (item.statusId === 1 || item.statusId === 2 || !item.workflowInstanceId
          || this.activeTab() === 'returned' || (item.status ?? item.Status) === 'Returned')
        ? [{ label: 'Xóa', title: 'Xóa', icon: 'pi pi-trash color-red', command: () => this.onDelete(item) }]
        : []),
    ];
    this.rowActionMenuItems = actions;
    menu.toggle(event);
  }

  openQuickActionMenu(event: Event, item: any, menu: any) {
    event.stopPropagation();
    const actions = sortWorkflowActionsRejectLast(this.getItemAvailableActions(item));
    if (actions.length === 0) return;

    this.quickActionMenuItems = actions.map((act: any) => {
      const isReject = isRejectWorkflowAction(act);
      return {
        label: act.name,
        icon: isReject ? 'pi pi-times-circle' : 'pi pi-check-circle',
        styleClass: isReject ? 'quick-action-reject' : 'quick-action-approve',
        command: () => {
          this.openQuickActionDialog(item, act);
        }
      };
    });

    menu.toggle(event);
  }

  onQuickCompleteInput(item: any) {
    this.selectedQuickItem.set(item);
    this.showQuickCompleteConfirm.set(true);
  }

  confirmQuickComplete() {
    const item = this.selectedQuickItem();
    if (!item || this.quickActionSubmitting()) return;

    this.quickActionSubmitting.set(true);
    this.service.completeInput(item.id).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã hoàn thành nhập liệu thành công' });
        this.showQuickCompleteConfirm.set(false);
        this.quickActionSubmitting.set(false);
        this.items.update((list) =>
          list.map((row) =>
            row.id === item.id
              ? { ...row, statusId: 2, status: 'CompletedInput', statusName: 'Hoàn thành' }
              : row
          )
        );
        this.refreshListItemAfterMutation(item.id, () => this.loadTabCounts());
      },
      error: (err: any) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể hoàn thành nhập liệu' });
        this.quickActionSubmitting.set(false);
      }
    });
  }

  onQuickSubmitForApproval(item: any) {
    this.selectedQuickItem.set(item);
    this.quickSubmitSubmitting.set(true);
    this.service.getNextStepInfo(this.kindIdSignal()).subscribe({
      next: (res) => {
        if (res?.autoApprove) {
          this.service.submitForApproval(item.id, {
            nextNodeId: '',
            actionLabel: 'Tự động duyệt',
            comment: 'Tự động phê duyệt — chưa cấu hình quy trình.'
          }, this.kindIdSignal()).subscribe({
            next: () => {
              this.messageService.add({ severity: 'success', summary: 'Thành công', detail: res.message || 'Đã tự động phê duyệt hồ sơ' });
              this.quickSubmitSubmitting.set(false);
              this.showQuickSubmitConfirm.set(false);
              this.refreshListItemAfterMutation(item.id, () => this.loadTabCounts());
            },
            error: (err: any) => {
              this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể tự động phê duyệt hồ sơ.' });
              this.quickSubmitSubmitting.set(false);
            }
          });
          return;
        }
        this.quickSubmitNextStepInfo.set(res);
        this.quickSubmitSelectedNextUser.set('');
        this.loadEligibleQuickSubmitUsers(res);
        this.service.getUsersLookup().subscribe({
          next: (users) => {
            this.users.set(Array.isArray(users) ? users : []);
            this.showQuickSubmitConfirm.set(true);
            this.quickSubmitSubmitting.set(false);
          },
          error: () => {
            this.users.set([]);
            this.showQuickSubmitConfirm.set(true);
            this.quickSubmitSubmitting.set(false);
          }
        });
      },
      error: (err: any) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể lấy thông tin bước duyệt tiếp theo.' });
        this.quickSubmitSubmitting.set(false);
      }
    });
  }

  confirmQuickSubmit() {
    const item = this.selectedQuickItem();
    const info = this.quickSubmitNextStepInfo();
    if (!item || !info || this.quickActionSubmitting()) return;

    if (info.requiresNextAssignee && !this.quickSubmitSelectedNextUser()) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Vui lòng chọn người duyệt tiếp theo.' });
      return;
    }

    this.quickActionSubmitting.set(true);
    this.service.submitForApproval(item.id, {
      nextNodeId: info.nextNodeId,
      actionLabel: 'Trình duyệt',
      nextAssigneeUserId: this.quickSubmitSelectedNextUser() || undefined,
      comment: 'Kính trình phê duyệt hồ sơ.'
    }, this.kindIdSignal()).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã gửi duyệt hồ sơ thành công' });
        this.showQuickSubmitConfirm.set(false);
        this.quickActionSubmitting.set(false);
        this.refreshListItemAfterMutation(item.id, () => this.loadTabCounts());
      },
      error: (err: any) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể gửi duyệt hồ sơ' });
        this.quickActionSubmitting.set(false);
      }
    });
  }

  isSelected(id: string): boolean {
    return this.selectedDossierIds().has(id);
  }

  toggleSelectDossier(id: string): void {
    const current = new Set(this.selectedDossierIds());
    if (current.has(id)) {
      current.delete(id);
    } else {
      current.add(id);
    }
    this.selectedDossierIds.set(current);
  }

  allPageSelected(): boolean {
    const list = this.items();
    if (list.length === 0) return false;
    const current = this.selectedDossierIds();
    return list.every((item) => current.has(item.id));
  }

  toggleSelectAllPage(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    const current = new Set(this.selectedDossierIds());
    if (checked) {
      this.items().forEach((item) => current.add(item.id));
    } else {
      this.items().forEach((item) => current.delete(item.id));
    }
    this.selectedDossierIds.set(current);
  }

  clearSelection(): void {
    if (this.selectedDossierIds().size > 0) {
      this.selectedDossierIds.set(new Set());
    }
  }

  openBulkApproveDialog(): void {
    if (this.selectedDossierIds().size === 0) return;
    this.bulkApproveComment.set('Đồng ý phê duyệt hàng loạt');
    this.showBulkApproveConfirm.set(true);
  }

  onCancelBulkApprove(): void {
    if (this.bulkApproveSubmitting()) return;
    this.showBulkApproveConfirm.set(false);
  }

  confirmBulkApprove(): void {
    const selectedItems = this.selectedDossiersList();
    if (selectedItems.length === 0 || this.bulkApproveSubmitting()) return;

    this.bulkApproveSubmitting.set(true);
    const comment = this.bulkApproveComment().trim() || 'Đồng ý phê duyệt hàng loạt';

    const requests = selectedItems.map((item) => {
      const actions = sortWorkflowActionsRejectLast(this.getItemAvailableActions(item));
      const approveAction = actions.find((a: any) => !isRejectWorkflowAction(a)) ?? actions[0];
      const actionLabel = approveAction?.name || 'Phê duyệt';
      const nextNodeId = approveAction?.nextNodeId || '';

      const reqPayload = {
        nextNodeId,
        actionLabel,
        comment,
      };

      const workflowCall = this.shouldUseResubmit(item)
        ? this.service.resubmitWorkflow(item.id, reqPayload, this.kindIdSignal())
        : this.service.moveWorkflow(item.id, reqPayload, this.kindIdSignal());

      return workflowCall.pipe(
        map(() => ({ id: item.id, success: true, error: null })),
        catchError((err) => of({ id: item.id, success: false, error: err?.error?.message || 'Lỗi' }))
      );
    });

    forkJoin(requests)
      .pipe(finalize(() => this.bulkApproveSubmitting.set(false)))
      .subscribe({
        next: (results) => {
          const successCount = results.filter((r) => r.success).length;
          const failCount = results.filter((r) => !r.success).length;

          if (successCount > 0) {
            this.messageService.add({
              severity: 'success',
              summary: 'Thành công',
              detail: `Đã phê duyệt thành công ${successCount} hồ sơ!`,
            });
          }
          if (failCount > 0) {
            this.messageService.add({
              severity: 'warn',
              summary: 'Cảnh báo',
              detail: `${failCount} hồ sơ không thể phê duyệt.`,
            });
          }

          this.showBulkApproveConfirm.set(false);
          this.clearSelection();
          this.refreshList();
          this.loadTabCounts();
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể thực hiện phê duyệt hàng loạt.',
          });
        },
      });
  }
}


