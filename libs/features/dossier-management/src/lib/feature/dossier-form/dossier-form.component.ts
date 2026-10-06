import { ChangeDetectorRef, Component, OnInit, signal, inject, Output, EventEmitter, Input, computed, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { DossierManagementService } from '../../data-access/dossier-management.service';
import { DossierPublishService } from '../../data-access/dossier-publish.service';
import { DossierDocumentsTabComponent } from '../dossier-documents/dossier-documents-tab.component';
import { DossierVersionsTabComponent } from '../dossier-versions-tab/dossier-versions-tab.component';
import { DossierWorkflowTabComponent } from '../dossier-workflow-tab/dossier-workflow-tab.component';
import {
  EavField,
  guidsEqual,
  normalizeField,
  normalizeDossierDetail,
  parseFormDataJson,
  pickFormDataForSchema,
  readFormSchemaJson,
  serializeFormDataForSchema,
} from '../../utils/dossier-form-schema.util';
import { finalize, forkJoin, Subject, of } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePickerModule } from 'primeng/datepicker';
import { AuthService } from '../../../../../../shared/core/src/lib/services/auth.service';
import { EavFormService } from '../../../../../../shared/core/src/lib/services/eav-form.service';
import { WorkflowService } from '@sohoa.frontend/shared/core';
import {
  isApproveWorkflowLabel,
  isRejectWorkflowLabel,
  parseWorkflowActionButtons,
  resolveEligibleAssigneeGroupParams,
  resolveDefaultNextAssignee,
  resolveNextUserCandidates,
} from '../../utils/dossier-workflow-bpmn.util';
import { isUserAuthorizedForWorkflowAction } from '../../utils/dossier-workflow-auth.util';
import { normalizeDossierKindId } from '../../utils/dossier-permission.util';

@Component({
  selector: 'app-dossier-form',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, DialogModule, SelectModule, MultiSelectModule, DossierDocumentsTabComponent, DossierVersionsTabComponent, DossierWorkflowTabComponent, DatePickerModule],
  templateUrl: './dossier-form.component.html',
  styleUrl: './dossier-form.component.scss',
})
export class DossierFormComponent implements OnInit {

  @Input() usePublishApi = false;
  @Input() hideInfrastructureField = false;
  @Input() hideGridTypeField = false;
  @Input() showHeaderBackButton = true;
  @Input() dossierId: string | null = null;
  @Input() set kindId(value: number | undefined) {
    const id = normalizeDossierKindId(value, this.kindIdSignal());
    this.kindIdSignal.set(id);
    this.service.setKindContext(id);
  }
  kindIdSignal = signal<number>(2);
  @Output() cancel = new EventEmitter<void>();
  @Output() saved = new EventEmitter<string>();
  @Output() inputCompleted = new EventEmitter<void>();
  @Output() statusLoaded = new EventEmitter<number>();

  private service = inject(DossierManagementService);
  private publishService = inject(DossierPublishService);
  private messageService = inject(MessageService);
  private changeDetectorRef = inject(ChangeDetectorRef);
  private authService = inject(AuthService);
  private eavFormService = inject(EavFormService);
  private workflowSvc = inject(WorkflowService);

  isEditMode = computed(() => !!this.dossierId);
  activeTab = signal<'info' | 'documents' | 'versions' | 'workflow'>('info');
  loading = signal<boolean>(false);
  isSaving = signal<boolean>(false);
  completingInput = signal<boolean>(false);
  showCompleteInputConfirm = signal<boolean>(false);
  loadingForm = signal<boolean>(false);
  dossierStatus = signal<string>('');
  dossierStatusId = signal<number>(0);
  workflowInstanceId = signal<string | null>(null);

  // Workflow actions in edit form (Returned statusId = 5)
  formPendingTask = signal<any>(null);
  formDynamicButtons = signal<any[]>([]);
  formWorkflowXml = signal<string>('');
  formCurrentNodeId = signal<string>('');
  showFormActionDialog = signal<boolean>(false);
  pendingActionBtn = signal<any>(null);
  formActionComment = signal<string>('');
  selectedNextUserId = signal<string>('');
  formActionSubmitting = signal<boolean>(false);
  formWorkflowUsers = signal<any[]>([]);
  eligibleFormNextUsers = signal<any[]>([]);
  loadingEligibleFormNextUsers = signal<boolean>(false);

  // Hợp nhất nhóm quyền hệ thống/đơn vị/người cụ thể đã cấu hình trên bước ĐÍCH; nếu bước không
  // cấu hình gì cả, danh sách để trống — không dùng toàn bộ user làm dự phòng (xem resolveNextUserCandidates).
  filteredFormNextUsers = computed(() => resolveNextUserCandidates({
    info: this.pendingActionBtn(),
    allUsers: this.formWorkflowUsers(),
    eligibleUsers: this.eligibleFormNextUsers(),
  }));

  private loadEligibleFormNextUsers(info: any): void {
    this.eligibleFormNextUsers.set([]);
    const groupParams = resolveEligibleAssigneeGroupParams(info);
    if (!groupParams) return;
    const unitId = info?.requireSameUnit ? (this.authService.getUserUnitId() ?? undefined) : undefined;
    this.loadingEligibleFormNextUsers.set(true);
    this.workflowSvc.getEligibleAssignees(groupParams.systemGroupIds, groupParams.unitGroupIds, unitId, undefined, groupParams.assigneeIds)
      .pipe(finalize(() => this.loadingEligibleFormNextUsers.set(false)))
      .subscribe({
        next: (list) => {
          const arr = Array.isArray(list) ? list : [];
          this.eligibleFormNextUsers.set(arr);
          this.selectedNextUserId.set(resolveDefaultNextAssignee(info, arr));
        },
        error: () => this.eligibleFormNextUsers.set([])
      });
  }

  submitting = signal<boolean>(false);
  showSubmitConfirm = signal<boolean>(false);
  nextStepInfo = signal<any>(null);
  selectedNextUser = signal<string>('');
  users = signal<any[]>([]);
  eligibleSubmitUsers = signal<any[]>([]);
  loadingEligibleSubmitUsers = signal<boolean>(false);

  // Hợp nhất nhóm quyền hệ thống/đơn vị/người cụ thể đã cấu hình trên bước; nếu không cấu hình
  // gì cả, danh sách để trống — không dùng toàn bộ user làm dự phòng (xem resolveNextUserCandidates).
  filteredSubmitNextUsers = computed(() => resolveNextUserCandidates({
    info: this.nextStepInfo(),
    allUsers: this.users(),
    eligibleUsers: this.eligibleSubmitUsers(),
  }));

  private loadEligibleSubmitUsers(info: any): void {
    this.eligibleSubmitUsers.set([]);
    this.selectedNextUser.set('');
    const groupParams = resolveEligibleAssigneeGroupParams(info);
    if (!groupParams) return;
    const unitId = info.requireSameUnit ? (this.authService.getUserUnitId() ?? undefined) : undefined;
    this.loadingEligibleSubmitUsers.set(true);
    this.workflowSvc.getEligibleAssignees(groupParams.systemGroupIds, groupParams.unitGroupIds, unitId, undefined, groupParams.assigneeIds)
      .pipe(finalize(() => this.loadingEligibleSubmitUsers.set(false)))
      .subscribe({
        next: (list) => {
          const arr = Array.isArray(list) ? list : [];
          this.eligibleSubmitUsers.set(arr);
          this.selectedNextUser.set(resolveDefaultNextAssignee(info, arr));
        },
        error: () => this.eligibleSubmitUsers.set([])
      });
  }

  dossier = {
    id: '',
    dossierTypeId: '',
    dossierGroupId: null as number | null,
    gridTypeId: null as number | null,
    infrastructureId: null as string | null,
    infrastructureIds: [] as string[],
    dossierSetId: null as string | null,
    rowVersion: 1,
    shelfId: null as number | null,
    floorId: null as number | null,
    boxId: null as number | null,
    shelfName: null as string | null,
    floorName: null as string | null,
    boxName: null as string | null,
    shelfCode: null as string | null,
    floorCode: null as string | null,
    boxCode: null as string | null,
  };

  formGridTypeId = signal<number | null>(null);
  /** Signal để computed nhóm hồ sơ / IsEquipmentDossier cập nhật khi đổi select. */
  dossierGroupIdSignal = signal<number | null>(null);

  storageTree = signal<any[]>([]);
  storageTreeOpen = signal(false);
  expandedStorageNodes = signal<Set<string>>(new Set());
  /** Signal để UI (zoneless) cập nhật sau khi chọn/xóa hộp — không dùng computed trên object thuần. */
  selectedStorageBoxId = signal<number | null>(null);
  storageSelectionLabel = signal('');

  private refreshStorageSelectionLabel() {
    if (!this.dossier.boxId) {
      this.selectedStorageBoxId.set(null);
      this.storageSelectionLabel.set('');
      return;
    }
    this.selectedStorageBoxId.set(Number(this.dossier.boxId));
    const shelf = this.dossier.shelfName || this.dossier.shelfCode || (this.dossier.shelfId ? `Kệ #${this.dossier.shelfId}` : '');
    const floor = this.dossier.floorName || this.dossier.floorCode || (this.dossier.floorId ? `Tầng #${this.dossier.floorId}` : '');
    const box = this.dossier.boxName || this.dossier.boxCode || `Hộp #${this.dossier.boxId}`;
    this.storageSelectionLabel.set([shelf, floor, box].filter(Boolean).join(' / '));
  }

  formInfrastructures = computed(() => {
    const gtId = this.formGridTypeId();
    const group = this.selectedDossierGroup();
    const infraTypeId = group
      ? Number(group.infraTypeId ?? group.InfraTypeId)
      : null;

    const selectedIds = this.dossier.infrastructureIds || [];

    // Lấy UnitId của hạ tầng đầu tiên đã chọn (nếu có)
    let enforcedUnitId: number | null = null;
    if (selectedIds.length > 0) {
      const firstSelected = this.infrastructures().find(inf => (inf.id ?? inf.Id) === selectedIds[0]);
      if (firstSelected) {
        enforcedUnitId = firstSelected.unitId ?? firstSelected.UnitId ?? null;
      }
    }

    return this.infrastructures().filter(inf => {
      const itemGridType = Number(inf.gridTypeId ?? inf.GridTypeId);
      const itemInfraType = Number(inf.infraTypeId ?? inf.InfraTypeId);
      const itemUnitId = inf.unitId ?? inf.UnitId ?? null;

      // 1. Chỉ lấy đúng loại hạ tầng theo Nhóm hồ sơ (1 = Trạm biến áp, 2 = Đường dây)
      if (infraTypeId != null && !Number.isNaN(infraTypeId) && itemInfraType !== infraTypeId) {
        return false;
      }

      // 2. Khi đã chọn 1 hạ tầng -> chỉ hiển thị các hạ tầng CÙNG ĐƠN VỊ
      if (enforcedUnitId != null && itemUnitId != null && Number(itemUnitId) !== Number(enforcedUnitId)) {
        return false;
      }

      if (gtId && itemGridType !== Number(gtId)) {
        return false;
      }
      return true;
    });
  });

  // Lookups
  dossierTypes = signal<any[]>([]);
  dossierGroups = signal<any[]>([]);
  gridTypes = signal<any[]>([]);
  infrastructures = signal<any[]>([]);
  dossierSets = signal<any[]>([]);
  organizationUnits = signal<any[]>([]);
  private autoCodeRequestId = 0;

  selectedDossierGroup = computed(() => {
    const id = this.dossierGroupIdSignal();
    if (id == null) return null;
    return this.dossierGroups().find(g => Number(g.id ?? g.Id) === Number(id)) ?? null;
  });

  /**
   * Combobox "Nhóm hồ sơ" bỏ 2 nhóm thiết bị (Hồ sơ thiết bị của trạm/đường dây) — thiết bị giờ được
   * phân loại ở cấp Tài liệu đính kèm, không còn chọn ở cấp Hồ sơ. Vẫn giữ lại nhóm hiện tại của hồ sơ
   * đang sửa trong danh sách (dù là nhóm thiết bị cũ) để field không bị trống khi mở hồ sơ cũ.
   */
  selectableDossierGroups = computed(() => {
    const currentId = this.dossierGroupIdSignal();
    return this.dossierGroups().filter(g => {
      const flag = g.isEquipmentDossier ?? g.IsEquipmentDossier;
      const isEquipmentGroup = flag === true || flag === 1 || flag === '1';
      return !isEquipmentGroup || Number(g.id ?? g.Id) === Number(currentId);
    });
  });

  infrastructureFieldLabel = computed(() => {
    const g = this.selectedDossierGroup();
    const infraTypeId = g ? Number(g.infraTypeId ?? g.InfraTypeId) : null;
    if (infraTypeId === 2) return 'Đường dây';
    if (infraTypeId === 1) return 'Trạm biến áp';
    return 'Trạm / Đường dây';
  });

  infrastructurePlaceholder = computed(() => {
    const g = this.selectedDossierGroup();
    const infraTypeId = g ? Number(g.infraTypeId ?? g.InfraTypeId) : null;
    if (infraTypeId === 2) return '-- Chọn đường dây --';
    if (infraTypeId === 1) return '-- Chọn trạm biến áp --';
    return '-- Chọn trạm/đường dây --';
  });

  // Dynamic form state
  dynamicFields = signal<EavField[]>([]);
  formData: Record<string, any> = {};
  selectedFormId = signal<string | null>(null);
  /** Cache catalog items: key = catalogType code, value = array of {label, value} */
  catalogCache: Record<string, { label: string; value: string }[]> = {};

  selectedTypeName = computed(() => {
    const found = this.dossierTypes().find(t => t.id === this.dossier.dossierTypeId);
    return found?.name ?? '';
  });

  private readonly destroyRef = inject(DestroyRef);
  /** Danh sách trạm/đường dây mặc định (chưa lọc theo từ khóa) — dùng để khôi phục khi xóa ô tìm kiếm. */
  private defaultInfrastructureSource: any[] = [];
  private readonly infrastructureFilterSubject = new Subject<string>();

  constructor() {
    if (typeof window !== 'undefined') {
      window.addEventListener('click', () => this.storageTreeOpen.set(false));
    }

    // Tìm kiếm Trạm/Đường dây phía server (debounce) — tránh phải render/lọc client-side
    // hàng nghìn option cùng lúc gây đứng UI khi đơn vị có quá nhiều hạ tầng.
    this.infrastructureFilterSubject
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((keyword) =>
          keyword ? this.service.getInfrastructureLookup(keyword) : of(this.defaultInfrastructureSource)
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((res) => this.applyInfrastructureItems(res || []));
  }

  onInfrastructureFilter(event: { filter: string }): void {
    this.infrastructureFilterSubject.next((event?.filter || '').trim());
  }

  ngOnInit() {
    this.loadLookups();
    if (this.dossierId) {
      this.loadDossierDetail(this.dossierId);
    }
  }

  loadLookups() {
    this.service.getDossierTypeLookup().subscribe(res => {
      this.dossierTypes.set(res || []);
      this.tryAutoGenerateDossierValues();
    });
    this.service.getDossierGroupLookup().subscribe(res => {
      const groups = res || [];
      this.dossierGroups.set(groups);

      if (!this.isEditMode() && !this.dossier.dossierGroupId) {
        const defaultGroup = groups.find((group: any) =>
          String(group.name ?? group.Name ?? '').trim().toLocaleLowerCase('vi-VN') === 'hồ sơ trạm biến áp'
        );

        if (defaultGroup) {
          this.onDossierGroupChange(defaultGroup.id ?? defaultGroup.Id);
        }
      }
    });
    this.service.getGridTypeLookup().subscribe(res => this.gridTypes.set(res || []));
    this.service.getDossierSets().subscribe(res => this.dossierSets.set(res || []));
    this.service.getOrganizationUnitsLookup().subscribe({
      next: (res) => {
        const payload = (res as any)?.items ?? (res as any)?.data ?? res;
        this.organizationUnits.set(Array.isArray(payload) ? payload : []);
        this.tryAutoGenerateDossierValues();
      },
      error: () => this.organizationUnits.set([]),
    });
    this.loadInfrastructures();
    this.loadPhysicalStorageTree();
    this.service.getUsersLookup().subscribe({
      next: (users) => this.users.set(Array.isArray(users) ? users : []),
      error: () => this.users.set([])
    });
  }

  loadPhysicalStorageTree() {
    const unitId = this.authService.getUserUnitId();
    this.service.getPhysicalStorageTree(unitId).subscribe({
      next: (res) => this.storageTree.set(Array.isArray(res) ? res : []),
      error: () => this.storageTree.set([])
    });
  }

  toggleStorageTree(event?: Event) {
    if (event) event.stopPropagation();
    this.storageTreeOpen.update(v => !v);
  }

  toggleStorageNode(key: string, event?: Event) {
    if (event) event.stopPropagation();
    const current = new Set(this.expandedStorageNodes());
    if (current.has(key)) current.delete(key);
    else current.add(key);
    this.expandedStorageNodes.set(current);
  }

  isStorageNodeExpanded(key: string): boolean {
    return this.expandedStorageNodes().has(key);
  }

  selectStorageBox(shelf: any, floor: any, box: any) {
    this.dossier.shelfId = Number(shelf.id);
    this.dossier.floorId = Number(floor.id);
    this.dossier.boxId = Number(box.id);
    this.dossier.shelfName = shelf.name ?? null;
    this.dossier.floorName = floor.name ?? null;
    this.dossier.boxName = box.name ?? null;
    this.dossier.shelfCode = shelf.code ?? null;
    this.dossier.floorCode = floor.code ?? null;
    this.dossier.boxCode = box.code ?? null;
    this.refreshStorageSelectionLabel();
    this.storageTreeOpen.set(false);
  }

  clearStorageSelection(event?: Event) {
    if (event) event.stopPropagation();
    this.dossier.shelfId = null;
    this.dossier.floorId = null;
    this.dossier.boxId = null;
    this.dossier.shelfName = null;
    this.dossier.floorName = null;
    this.dossier.boxName = null;
    this.dossier.shelfCode = null;
    this.dossier.floorCode = null;
    this.dossier.boxCode = null;
    this.refreshStorageSelectionLabel();
  }

  loadInfrastructures() {
    this.service.getInfrastructureLookup().subscribe(res => {
      const source = !this.isEditMode() && !this.usePublishApi
        ? (res || []).filter((inf: any) => {
            const isActive = inf.isActive ?? inf.IsActive;
            return isActive === true || isActive === 1 || isActive === '1';
          })
        : (res || []);
      this.defaultInfrastructureSource = source;
      this.applyInfrastructureItems(source);
    });
  }

  /** Áp dụng 1 danh sách trạm/đường dây (mặc định hoặc kết quả tìm kiếm server) vào signal `infrastructures`,
   * luôn giữ lại các hạ tầng đang được chọn dù chúng không nằm trong danh sách mới (tránh mất lựa chọn đã lưu). */
  private applyInfrastructureItems(source: any[]) {
    const items = (source || []).map((inf: any) => this.enrichInfrastructureOption(inf));
    const selectedIds = this.dossier.infrastructureIds?.length
      ? this.dossier.infrastructureIds
      : (this.dossier.infrastructureId ? [this.dossier.infrastructureId] : []);
    for (const selectedId of selectedIds) {
      if (!selectedId || items.some((inf: any) => (inf.id ?? inf.Id) === selectedId)) continue;
      const existing = this.infrastructures().find((inf) => (inf.id ?? inf.Id) === selectedId);
      if (existing) {
        items.push(this.enrichInfrastructureOption(existing));
      }
    }
    this.infrastructures.set(items);
  }

  private enrichInfrastructureOption(inf: any) {
    const name = inf.name ?? inf.Name ?? '';
    const code = inf.code ?? inf.Code ?? '';
    return {
      ...inf,
      id: inf.id ?? inf.Id,
      name,
      code,
      displayLabel: code ? `${name} (${code})` : name,
      infraTypeId: Number(inf.infraTypeId ?? inf.InfraTypeId ?? 0) || null,
      gridTypeId: inf.gridTypeId ?? inf.GridTypeId ?? null,
    };
  }

  /** Giữ lại các option trạm/đường dây hiện tại khi sửa hồ sơ (tránh mất giá trị đã lưu nếu hạ tầng đã inactive/xoá). */
  private ensureInfrastructureOptions(infraIds: string[], detail: Record<string, unknown>) {
    if (!infraIds?.length) return;

    const missingIds = infraIds.filter(
      (infraId) => infraId && !this.infrastructures().some((inf) => (inf.id ?? inf.Id) === infraId)
    );
    if (!missingIds.length) return;

    const group = this.selectedDossierGroup();
    const fallbackInfraType = group
      ? Number(group.infraTypeId ?? group.InfraTypeId)
      : Number(detail['infraTypeId'] ?? detail['InfraTypeId'] ?? 0) || null;

    // Backend chỉ trả tên/mã của hạ tầng đầu tiên (infrastructureName/infrastructureCode) — các hạ
    // tầng còn lại trong mảng (nếu chưa nằm trong lookup) tạm hiển thị theo id.
    const primaryInfraId = infraIds[0];
    const newOptions = missingIds.map((infraId) =>
      this.enrichInfrastructureOption({
        id: infraId,
        name: infraId === primaryInfraId
          ? ((detail['infrastructureName'] ?? detail['InfrastructureName'] ?? infraId) as string)
          : infraId,
        code: infraId === primaryInfraId ? (detail['infrastructureCode'] ?? detail['InfrastructureCode']) : null,
        gridTypeId: detail['gridTypeId'] ?? detail['GridTypeId'],
        infraTypeId: fallbackInfraType,
      })
    );

    this.infrastructures.update((list) => [...list, ...newOptions]);
  }

  loadDossierDetail(id: string) {
    this.loading.set(true);
    forkJoin({
      detail: this.usePublishApi
        ? this.publishService.getDetail(id)
        : this.service.getDossierById(id),
      types: this.service.getDossierTypeLookup(),
    }).subscribe({
      next: ({ detail: res, types }) => {
        if (types?.length) {
          this.dossierTypes.set(types);
        }
        if (res) {
          const normalizedKindId = normalizeDossierKindId(
            res.kindId ?? res.KindId,
            this.kindIdSignal()
          );
          this.kindIdSignal.set(normalizedKindId);
          this.service.setKindContext(normalizedKindId);

          const rawInfraIds = res.infrastructureIds ?? res.InfrastructureIds;
          const infraIds: string[] = Array.isArray(rawInfraIds)
            ? rawInfraIds
            : (res.infrastructureId ?? res.InfrastructureId ? [res.infrastructureId ?? res.InfrastructureId] : []);

          this.dossier = {
            id: res.id ?? res.Id,
            dossierTypeId: res.dossierTypeId ?? res.DossierTypeId,
            dossierGroupId: res.dossierGroupId != null || res.DossierGroupId != null
              ? Number(res.dossierGroupId ?? res.DossierGroupId)
              : 1,
            gridTypeId: res.gridTypeId != null ? Number(res.gridTypeId ?? res.GridTypeId) : null,
            infrastructureId: infraIds[0] ?? null,
            infrastructureIds: infraIds,
            dossierSetId: res.dossierSetId ?? res.DossierSetId,
            rowVersion: res.rowVersion ?? res.RowVersion,
            shelfId: res.shelfId ?? res.ShelfId ?? null,
            floorId: res.floorId ?? res.FloorId ?? null,
            boxId: res.boxId ?? res.BoxId ?? null,
            shelfName: res.shelfName ?? res.ShelfName ?? null,
            floorName: res.floorName ?? res.FloorName ?? null,
            boxName: res.boxName ?? res.BoxName ?? null,
            shelfCode: res.shelfCode ?? res.ShelfCode ?? null,
            floorCode: res.floorCode ?? res.FloorCode ?? null,
            boxCode: res.boxCode ?? res.BoxCode ?? null,
          };
          if (this.dossier.shelfId) {
            this.expandedStorageNodes.update(set => {
              const next = new Set(set);
              next.add('s-' + this.dossier.shelfId);
              if (this.dossier.floorId) next.add('f-' + this.dossier.floorId);
              return next;
            });
          }
          this.refreshStorageSelectionLabel();
          // Dùng cùng bộ chuẩn hóa với màn Xem chi tiết để không lệch trạng thái
          // giữa hai màn hình khi API trả về camelCase/PascalCase.
          const normalizedDetail = normalizeDossierDetail(res);
          const rawStatus = normalizedDetail?.status ?? res.status ?? res.Status ?? res.dossierStatus ?? res.DossierStatus
            ?? normalizedDetail?.statusName ?? res.statusName ?? res.StatusName
            ?? normalizedDetail?.statusCode ?? res.statusCode ?? res.StatusCode ?? '';
          const rawStatusId = normalizedDetail?.statusId
            ?? res.statusId ?? res.StatusId ?? res.dossierStatusId ?? res.DossierStatusId;
          const statusText = String(rawStatus ?? '').trim().toLowerCase();
          const parsedStatusId = Number(rawStatusId);
          const normalizedStatusId = Number.isFinite(parsedStatusId) && parsedStatusId > 0
            ? parsedStatusId
            : (statusText === 'new' || statusText === 'tạo mới' ? 1
              : statusText === 'completedinput' || statusText === 'hoàn thành' || statusText === 'hoàn thành nhập liệu' ? 2
              : statusText === 'returned' || statusText === 'trả lại' ? 5
              : 0);
          this.dossierStatus.set(String(rawStatus ?? ''));
          this.dossierStatusId.set(normalizedStatusId);
          this.statusLoaded.emit(normalizedStatusId);
          this.workflowInstanceId.set(res.workflowInstanceId ?? res.WorkflowInstanceId ?? null);
          if (res.workflowInstanceId ?? res.WorkflowInstanceId) {
            this.loadWorkflow();
          }
          this.formGridTypeId.set(this.dossier.gridTypeId);
          this.dossierGroupIdSignal.set(this.dossier.dossierGroupId);

          const typeId = res.dossierTypeId ?? res.DossierTypeId;
          const formId = res.formId ?? res.FormId;
          const formDataJson = res.formDataJson ?? res.FormDataJson;
          if (typeId) {
            this.loadFormForType(typeId, formDataJson, formId);
          }
          this.ensureInfrastructureOptions(infraIds, res);
        }
        this.loading.set(false);
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải chi tiết hồ sơ' });
        this.loading.set(false);
      }
    });
  }

  /** Gọi khi người dùng chọn Loại hồ sơ */
  onDossierTypeChange(typeId: string) {
    this.autoCodeRequestId++;
    this.dynamicFields.set([]);
    this.formData = {};
    this.selectedFormId.set(null);

    if (!typeId) return;
    // Mã và tiêu đề được sinh lại sau khi biểu mẫu của Loại hồ sơ mới được tải xong.
    this.loadFormForType(typeId);
  }

  onGridTypeChange(gtId: any) {
    const numericId = gtId != null && gtId !== '' ? Number(gtId) : null;
    this.dossier.gridTypeId = numericId;
    this.formGridTypeId.set(numericId);

    if (this.dossier.infrastructureIds && this.dossier.infrastructureIds.length > 0) {
      const allowed = this.infrastructures()
        .filter(inf => numericId == null || Number(inf.gridTypeId ?? inf.GridTypeId) === numericId)
        .map(inf => inf.id ?? inf.Id);
      this.dossier.infrastructureIds = this.dossier.infrastructureIds.filter(id => allowed.includes(id));
      this.dossier.infrastructureId = this.dossier.infrastructureIds[0] ?? null;
    }
  }

  onDossierGroupChange(groupId: any) {
    const numericId = groupId != null && groupId !== '' ? Number(groupId) : null;
    this.dossier.dossierGroupId = numericId;
    this.dossierGroupIdSignal.set(numericId);

    if (this.dossier.infrastructureIds && this.dossier.infrastructureIds.length > 0) {
      const allowedIds = this.formInfrastructures().map(inf => inf.id ?? inf.Id);
      this.dossier.infrastructureIds = this.dossier.infrastructureIds.filter(id => allowedIds.includes(id));
      this.dossier.infrastructureId = this.dossier.infrastructureIds[0] ?? null;
    }
  }

  onInfrastructureChange(selectedIds: any) {
    if (this.loading()) return;
    const ids: string[] = Array.isArray(selectedIds) ? selectedIds : (selectedIds ? [selectedIds] : []);
    this.dossier.infrastructureIds = ids;
    this.dossier.infrastructureId = ids[0] ?? null;

    // Sinh lại Mã hồ sơ/Tiêu đề hồ sơ theo trạm/đường dây mới chọn (nếu Loại hồ sơ đã được chọn
    // trước đó — tryAutoGenerateDossierValues() tự bỏ qua khi chưa có biểu mẫu/Loại hồ sơ).
    this.tryAutoGenerateDossierValues();
  }

  /**
   * Tự sinh mã/tên hồ sơ ở chế độ tạo mới theo đơn vị, trạm/đường dây và loại hồ sơ.
   */
  private tryAutoGenerateDossierValues() {
    if (this.isEditMode()) return;
    const fields = this.dynamicFields();
    if (!fields.length) return;

    const codeField = fields.find(
      (f) => f.key?.toUpperCase() === 'CODE' || f.name?.toUpperCase() === 'CODE' || f.id?.toUpperCase() === 'CODE'
    );
    const nameField = fields.find(
      (f) => f.key?.toUpperCase() === 'NAME' || f.name?.toUpperCase() === 'NAME' || f.id?.toUpperCase() === 'NAME'
    );
    if (!codeField && !nameField) return;

    const firstInfraId = this.dossier.infrastructureIds?.[0] || this.dossier.infrastructureId;
    const firstInfra = this.infrastructures().find((inf) => guidsEqual(inf.id ?? inf.Id, firstInfraId));
    const infraName = firstInfra ? String(firstInfra.name ?? firstInfra.Name ?? '').trim() : '';

    const typeId = this.dossier.dossierTypeId;
    const dossierType = this.dossierTypes().find((t) => guidsEqual(t.id ?? t.Id, typeId));
    const typeName = dossierType ? String(dossierType.name ?? dossierType.Name ?? '').trim() : '';

    if (nameField) {
      this.formData[nameField.key] = [typeName, infraName].filter(Boolean).join(' ');
      this.refreshAutoGeneratedValues();
    }

    if (!codeField || !firstInfraId || !typeId) return;

    const requestId = ++this.autoCodeRequestId;
    this.service.getNextDossierCode(String(firstInfraId), String(typeId)).subscribe({
      next: (res) => {
        if (requestId !== this.autoCodeRequestId) return;
        const code = String(res?.code ?? (res as any)?.Code ?? '').trim();
        if (!code) return;
        this.formData[codeField.key] = code;
        this.refreshAutoGeneratedValues();
      },
      error: () => {
        if (requestId === this.autoCodeRequestId) {
          this.messageService.add({
            severity: 'warn',
            summary: 'Mã hồ sơ',
            detail: 'Không thể tự sinh mã hồ sơ. Vui lòng chọn lại Loại hồ sơ.',
          });
        }
      },
    });
  }

  private refreshAutoGeneratedValues(): void {
    this.formData = { ...this.formData };
    this.changeDetectorRef.markForCheck();
  }

  /** Tìm formId từ dossierType rồi gọi API lấy form template */
  private loadFormForType(typeId: string, existingFormDataJson?: string, formIdFromDetail?: string | null) {
    const resolvedFormId = formIdFromDetail
      ?? this.dossierTypes().find((t) => guidsEqual(t.id ?? t.Id, typeId))?.formId
      ?? this.dossierTypes().find((t) => guidsEqual(t.id ?? t.Id, typeId))?.FormId
      ?? null;

    if (!resolvedFormId) {
      this.selectedFormId.set('');
      this.dynamicFields.set([]);
      return;
    }

    this.selectedFormId.set(resolvedFormId);
    this.loadingForm.set(true);
    const savedData = parseFormDataJson(existingFormDataJson);

    this.service.getFormTemplate(resolvedFormId).subscribe({
      next: (template) => {
        this.loadingForm.set(false);
        const schemaJson = readFormSchemaJson(template);
        if (!schemaJson) {
          this.dynamicFields.set([]);
          return;
        }

        try {
          const raw = JSON.parse(schemaJson);
          const fields: EavField[] = Array.isArray(raw) ? raw.map((f) => normalizeField(f)) : [];
           this.dynamicFields.set(fields);
           this.formData = pickFormDataForSchema(fields, savedData);
           
           // Convert date field strings to Date objects for p-datepicker compatibility
           fields.forEach(f => {
             if (f.type === 'date' && this.formData[f.key]) {
               const d = new Date(this.formData[f.key]);
               if (!isNaN(d.getTime())) {
                 this.formData[f.key] = d;
               }
             }
           });

           // Load catalog data cho các field có dataSourceType = 'catalog'
           this.loadCatalogForFields(fields);
           this.tryAutoGenerateDossierValues();
        } catch {
          this.dynamicFields.set([]);
          this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Không thể đọc cấu trúc biểu mẫu' });
        }
      },
      error: () => {
        this.loadingForm.set(false);
        this.dynamicFields.set([]);
      }
    });
  }

  /** Load catalog items cho các field có dataSourceType = 'catalog' */
  private loadCatalogForFields(fields: EavField[]) {
    const catalogFields = fields.filter(f => f.dataSourceType === 'catalog' && f.catalogType);
    if (!catalogFields.length) return;

    // Gom nhóm theo catalogType để tránh gọi API trùng
    const uniqueCatalogTypes = [...new Set(catalogFields.map(f => f.catalogType!))];

    uniqueCatalogTypes.forEach(catalogTypeCode => {
      if (this.catalogCache[catalogTypeCode]) {
        // Đã có trong cache → áp dụng luôn
        this.applyCatalogToFields(catalogTypeCode, this.catalogCache[catalogTypeCode]);
        return;
      }

      // Bước 1: Lấy catalogTypeId từ code
      this.eavFormService.getCatalogTypeByCode(catalogTypeCode).subscribe({
        next: (catalogTypeObj: any) => {
          const catalogTypeId = catalogTypeObj?.id ?? catalogTypeObj?.Id;
          if (!catalogTypeId) return;

          // Bước 2: Load lookup items
          this.eavFormService.getCatalogsLookup(catalogTypeId).subscribe({
            next: (items: any[]) => {
              const mappedItems = (items || []).map((item: any) => ({
                label: String(item.name ?? item.Name ?? item.label ?? item.Label ?? item.value ?? ''),
                value: String(item.id ?? item.Id ?? item.code ?? item.Code ?? item.value ?? ''),
              }));
              this.catalogCache[catalogTypeCode] = mappedItems;
              this.applyCatalogToFields(catalogTypeCode, mappedItems);
            },
            error: () => {
              console.warn('Không thể load catalog lookup cho:', catalogTypeCode);
            }
          });
        },
        error: () => {
          console.warn('Không thể load catalog type:', catalogTypeCode);
        }
      });
    });
  }

  /** Áp dụng catalogItems vào tất cả các field có catalogType tương ứng */
  private applyCatalogToFields(catalogTypeCode: string, items: { label: string; value: string }[]) {
    this.dynamicFields.update(fields =>
      fields.map(f => {
        if (f.catalogType === catalogTypeCode && f.dataSourceType === 'catalog') {
          return { ...f, catalogItems: items };
        }
        return f;
      })
    );
  }

  /** Kiểm tra xem option trong checkboxGroup có được chọn không */
  isCheckboxChecked(fieldKey: string, optionValue: string): boolean {
    const current = this.formData[fieldKey];
    if (!current) return false;
    if (Array.isArray(current)) {
      return current.includes(optionValue);
    }
    if (typeof current === 'string') {
      try {
        const parsed = JSON.parse(current);
        if (Array.isArray(parsed)) return parsed.includes(optionValue);
      } catch { /* ignore */ }
    }
    return false;
  }

  /** Xử lý thay đổi checkbox trong checkboxGroup */
  onCheckboxGroupChange(fieldKey: string, optionValue: string, checked: boolean) {
    let current: string[] = [];
    const rawVal = this.formData[fieldKey];
    if (Array.isArray(rawVal)) {
      current = [...rawVal];
    } else if (typeof rawVal === 'string' && rawVal) {
      try {
        const parsed = JSON.parse(rawVal);
        if (Array.isArray(parsed)) current = parsed;
      } catch { /* ignore */ }
    }

    if (checked) {
      if (!current.includes(optionValue)) {
        current.push(optionValue);
      }
    } else {
      current = current.filter(v => v !== optionValue);
    }

  }

  private getCheckboxOptionValues(field: EavField): string[] {
    if (field.dataSourceType === 'catalog') {
      return field.catalogItems?.map(item => item.value) || [];
    }
    return field.options?.map(opt => opt.value) || [];
  }

  isAllCheckboxesChecked(field: EavField): boolean {
    const optionValues = this.getCheckboxOptionValues(field);
    if (optionValues.length === 0) return false;
    
    let current: string[] = [];
    const rawVal = this.formData[field.key];
    if (Array.isArray(rawVal)) {
      current = rawVal;
    } else if (typeof rawVal === 'string' && rawVal) {
      try {
        const parsed = JSON.parse(rawVal);
        if (Array.isArray(parsed)) current = parsed;
      } catch { /* ignore */ }
    }
    
    return optionValues.every(val => current.includes(val));
  }

  toggleSelectAllCheckboxes(field: EavField, checked: boolean) {
    const optionValues = this.getCheckboxOptionValues(field);
    const newValues = checked ? [...optionValues] : [];
    this.formData = { ...this.formData, [field.key]: newValues };
  }

  isValid() {
    if (!this.dossier.dossierTypeId) return false;
    if (this.dossier.dossierGroupId == null) return false;
    // Bắt buộc chọn Trạm/Đường dây khi tạo mới và khi chỉnh sửa hồ sơ.
    if (!this.dossier.infrastructureIds || this.dossier.infrastructureIds.length === 0) return false;
    return true;
  }

  showCompleteInputButton(): boolean {
    return this.isEditMode() && this.dossierStatusId() === 1;
  }

  showSubmitForApprovalButton(): boolean {
    return this.isEditMode() && (this.dossierStatusId() === 2 || this.dossierStatusId() === 5);
  }

  openSubmitWorkflowDialog() {
    this.submitting.set(true);
    this.service.getNextStepInfo(this.kindIdSignal()).subscribe({
      next: (res) => {
        if (res?.autoApprove) {
          this.service.submitForApproval(this.dossier.id, {
            nextNodeId: '',
            actionLabel: 'Tự động duyệt',
            comment: 'Tự động phê duyệt — chưa cấu hình quy trình.'
          }, this.kindIdSignal()).subscribe({
            next: () => {
              this.messageService.add({ severity: 'success', summary: 'Thành công', detail: res.message || 'Đã tự động phê duyệt hồ sơ' });
              this.submitting.set(false);
              this.saved.emit(this.dossier.id);
            },
            error: (err) => {
              this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể tự động phê duyệt hồ sơ.' });
              this.submitting.set(false);
            }
          });
          return;
        }
        this.nextStepInfo.set(res);
        this.selectedNextUser.set('');
        this.loadEligibleSubmitUsers(res);
        this.showSubmitConfirm.set(true);
        this.submitting.set(false);
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể lấy thông tin bước duyệt tiếp theo.' });
        this.submitting.set(false);
      }
    });
  }

  onNextUserChange(event: any) {
    this.selectedNextUser.set(event.target?.value || '');
  }

  onConfirmSubmitAndMove() {
    const info = this.nextStepInfo();
    if (!info) return;
    if (info.requiresNextAssignee && !this.selectedNextUser()) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Vui lòng chọn người duyệt tiếp theo.' });
      return;
    }

    this.submitting.set(true);
    const isReturned = this.dossierStatusId() === 5;
    const call$ = isReturned
      ? this.service.resubmitWorkflow(this.dossier.id, {
          nextNodeId: info.nextNodeId,
          actionLabel: 'Trình duyệt',
          nextAssigneeUserId: this.selectedNextUser() || undefined,
          comment: 'Kính trình phê duyệt lại hồ sơ.'
        }, this.kindIdSignal())
      : this.service.submitForApproval(this.dossier.id, {
          nextNodeId: info.nextNodeId,
          actionLabel: 'Trình duyệt',
          nextAssigneeUserId: this.selectedNextUser() || undefined,
          comment: 'Kính trình phê duyệt hồ sơ.'
        }, this.kindIdSignal());

    call$.subscribe({
      next: (res: any) => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã gửi duyệt hồ sơ thành công' });
        this.showSubmitConfirm.set(false);
        const payload = res?.data;
        if (payload) {
          this.dossierStatus.set(payload.dossierStatus ?? this.dossierStatus());
          this.workflowInstanceId.set(payload.instanceId ?? this.workflowInstanceId());
        }
        this.submitting.set(false);
        this.saved.emit(this.dossier.id);
      },
      error: (err: any) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể gửi duyệt hồ sơ' });
        this.showSubmitConfirm.set(false);
        this.submitting.set(false);
      }
    });
  }

  requestCompleteInput(): void {
    if (!this.dossier.id || this.completingInput()) return;
    this.showCompleteInputConfirm.set(true);
  }

  onCancelCompleteInput(): void {
    if (this.completingInput()) return;
    this.showCompleteInputConfirm.set(false);
  }

  onConfirmCompleteInput(): void {
    if (!this.dossier.id || this.completingInput()) return;

    this.completingInput.set(true);
    this.service.completeInput(this.dossier.id).subscribe({
      next: () => {
        this.dossierStatus.set('CompletedInput');
        this.dossierStatusId.set(2);
        this.inputCompleted.emit();
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: 'Đã chuyển trạng thái sang Hoàn thành nhập liệu',
        });
        this.completingInput.set(false);
        this.showCompleteInputConfirm.set(false);
      },
      error: (err: any) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err.error?.message || 'Không thể hoàn thành nhập liệu',
        });
        this.completingInput.set(false);
        this.showCompleteInputConfirm.set(false);
      },
    });
  }

  isFormTabVisible(tab: 'info' | 'documents' | 'versions' | 'workflow'): boolean {
    switch (tab) {
      case 'info':
      case 'documents':
      case 'versions':
        return true;
      case 'workflow':
        return !!this.workflowInstanceId();
      default:
        return false;
    }
  }

  /**
   * Chuyển các field 'date' (đang là Date object cho p-datepicker) về chuỗi 'yyyy-mm-dd' theo
   * giờ local trước khi serialize — tránh JSON.stringify(Date) tự quy đổi UTC làm lùi ngày
   * (VD: 00:00 giờ VN (UTC+7) → 17:00 ngày hôm trước khi ép về UTC).
   */
  private toSerializableFormData(): Record<string, unknown> {
    const result: Record<string, unknown> = { ...this.formData };
    for (const f of this.dynamicFields()) {
      const val = result[f.key];
      if (f.type === 'date' && val instanceof Date && !isNaN(val.getTime())) {
        const y = val.getFullYear();
        const m = String(val.getMonth() + 1).padStart(2, '0');
        const d = String(val.getDate()).padStart(2, '0');
        result[f.key] = `${y}-${m}-${d}`;
      }
    }
    return result;
  }

  onSave() {
    if (!this.isValid()) {
      let detail = 'Vui lòng chọn loại hồ sơ và nhóm hồ sơ';
      if (this.dossier.dossierGroupId == null) {
        detail = 'Vui lòng chọn nhóm hồ sơ';
      } else if (!this.dossier.dossierTypeId) {
        detail = 'Vui lòng chọn loại hồ sơ';
      } else if (!this.dossier.infrastructureIds || this.dossier.infrastructureIds.length === 0) {
        detail = 'Vui lòng chọn trạm/đường dây';
      }
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail });
      return;
    }

    this.isSaving.set(true);
    const hasBox = !!this.dossier.boxId;
    const infraIds = this.dossier.infrastructureIds?.length
      ? this.dossier.infrastructureIds
      : (this.dossier.infrastructureId ? [this.dossier.infrastructureId] : []);

    const dto = {
      ...this.dossier,
      infrastructureIds: infraIds,
      infrastructureId: infraIds[0] || null,
      dossierGroupId: Number(this.dossier.dossierGroupId),
      gridTypeId: this.dossier.gridTypeId != null ? Number(this.dossier.gridTypeId) : null,
      statusId: this.usePublishApi && !this.isEditMode() ? 6 : undefined,
      publishStatusId: this.usePublishApi && !this.isEditMode() ? 1 : undefined,
      formDataJson: this.dynamicFields().length > 0
        ? serializeFormDataForSchema(this.dynamicFields(), this.toSerializableFormData())
        : undefined,
      // Chỉ gửi vị trí khi đã chọn đến hộp; ngược lại null để BE clear.
      shelfId: hasBox ? this.dossier.shelfId : null,
      floorId: hasBox ? this.dossier.floorId : null,
      boxId: hasBox ? this.dossier.boxId : null,
    };

    const req$ = this.isEditMode()
      ? this.usePublishApi
        ? this.publishService.update(this.dossier.id, dto)
        : this.service.updateDossier(this.dossier.id, dto)
      : this.usePublishApi
        ? this.publishService.create(dto)
        : this.service.createDossier(dto);

    req$.pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: (res: any) => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu thông tin hồ sơ' });
        const savedId = this.isEditMode()
          ? this.dossier.id
          : this.getSavedDossierId(res);

        if (savedId) {
          this.saved.emit(savedId);
          return;
        }

        // Một số API POST chỉ trả về thông báo thành công. Khi không có ID,
        // trở về danh sách để tránh điều hướng tới URL không hợp lệ.
        this.cancel.emit();
      },
      error: () => {
        // Lỗi HTTP đã được hiển thị thống nhất bởi httpErrorInterceptor.
      }
    });
  }

  private getSavedDossierId(response: unknown): string | null {
    if (typeof response === 'string' || typeof response === 'number') {
      return String(response);
    }

    if (!response || typeof response !== 'object') return null;

    const result = response as Record<string, any>;
    const payload = result['data'] ?? result['Data'] ?? result['result'] ?? result['Result'] ?? result;
    if (!payload || typeof payload !== 'object') return null;

    const id = payload['id'] ?? payload['Id'] ?? payload['dossierId'] ?? payload['DossierId'];
    return id != null && String(id).trim() ? String(id) : null;
  }

  onCancel() {
    this.cancel.emit();
  }

  trackByFieldKey(_index: number, field: EavField): string {
    return field.key;
  }

  loadWorkflow() {
    if (!this.dossierId) return;
    this.service.getWorkflowDetail(this.dossierId, this.kindIdSignal()).subscribe({
      next: (res: any) => {
        this.applyWorkflowDetailState(res);
      }
    });
  }

  applyWorkflowDetailState(res: any) {
    const userId = this.authService.getUserId();
    const roles = this.authService.getUserRoles?.() ?? [];
    const isAdmin = roles.includes('ADMIN') || roles.includes('OPERATOR');

    const tasks = res?.history ?? [];
    const pendingList = tasks.filter((t: any) =>
      String(t.status ?? t.Status ?? '').toLowerCase() === 'pending'
    );

    let myTask: any = null;
    if (pendingList.length > 0) {
      if (isAdmin || this.dossierStatusId() === 5) {
        myTask = pendingList[0];
      } else {
        myTask = pendingList.find((task: any) => {
          const assigneeId = task.assigneeUserId ?? task.AssigneeUserId;
          if (!assigneeId) return false;
          return String(assigneeId).toLowerCase() === String(userId).toLowerCase();
        });
      }
    }

    this.formPendingTask.set(myTask);

    const xml = res?.definition?.workflowXml ?? res?.definition?.WorkflowXml ?? '';
    const stepName = myTask?.workflowStatusName ?? myTask?.WorkflowStatusName ?? '';
    const currentNodeId = myTask?.currentNodeId ?? myTask?.CurrentNodeId ?? '';

    this.formWorkflowXml.set(xml);
    this.formCurrentNodeId.set(currentNodeId);

    if (myTask && xml) {
      this.formDynamicButtons.set(parseWorkflowActionButtons(xml, stepName, currentNodeId));
    } else {
      this.formDynamicButtons.set([]);
    }
  }

  get isUserAuthorizedForFormAction(): boolean {
    const task = this.formPendingTask();
    if (!task) return false;
    return isUserAuthorizedForWorkflowAction({
      authService: this.authService,
      menuScope: 'creator',
      assigneeUserId: task.assigneeUserId ?? task.AssigneeUserId,
      statusId: this.dossierStatusId(),
      isCreator: true,
    });
  }

  openFormActionDialog(btn: any) {
    this.pendingActionBtn.set(btn);
    this.formActionComment.set('');
    this.selectedNextUserId.set('');

    if (btn.requiresUser && !this.isRejectLabel(btn.label)) {
      this.loadEligibleFormNextUsers(btn);
      this.service.getUsersLookup(btn.requiredRole).subscribe({
        next: (users: any) => {
          this.formWorkflowUsers.set(Array.isArray(users) ? users : []);
          this.showFormActionDialog.set(true);
        },
        error: () => {
          this.formWorkflowUsers.set([]);
          this.showFormActionDialog.set(true);
        }
      });
    } else {
      this.formWorkflowUsers.set([]);
      this.showFormActionDialog.set(true);
    }
  }

  confirmFormAction() {
    const btn = this.pendingActionBtn();
    if (!btn || !this.dossierId || this.formActionSubmitting()) return;

    const isCancel = this.isRejectLabel(btn.label);
    if (btn.requiresUser && !isCancel && !this.selectedNextUserId()) {
      this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Vui lòng chọn người xử lý bước tiếp theo.' });
      return;
    }

    this.formActionSubmitting.set(true);
    const payload = {
      nextNodeId: btn.targetNodeId,
      actionLabel: btn.label,
      comment: this.formActionComment(),
      nextAssigneeUserId: (!isCancel && btn.requiresUser) ? this.selectedNextUserId() : undefined
    };

    const statusId = this.dossierStatusId();
    const useResubmit = statusId === 5;
    const workflowCall = useResubmit
      ? this.service.resubmitWorkflow(this.dossierId, payload, this.kindIdSignal())
      : this.service.moveWorkflow(this.dossierId, payload, this.kindIdSignal());

    workflowCall.subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: `Đã thực hiện: ${btn.label}` });
        this.formActionSubmitting.set(false);
        this.showFormActionDialog.set(false);
        this.formActionComment.set('');
        this.selectedNextUserId.set('');
        this.pendingActionBtn.set(null);
        this.onCancel(); // Thoát về danh sách sau khi chuyển tiếp thành công
      },
      error: (err: any) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể thực hiện.' });
        this.formActionSubmitting.set(false);
      }
    });
  }

  isRejectLabel(label?: string | null): boolean {
    return isRejectWorkflowLabel(label);
  }

  isApproveLabel(label?: string | null): boolean {
    return isApproveWorkflowLabel(label);
  }
}
