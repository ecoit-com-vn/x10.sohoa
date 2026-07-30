import { Component, OnInit, OnDestroy, signal, computed, inject, Output, EventEmitter, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { catchError, finalize, of, switchMap, takeUntil, Subject } from 'rxjs';
import { Router } from '@angular/router';
import { PaginatorModule } from 'primeng/paginator';
import { DossierManagementService } from '../../data-access/dossier-management.service';
import { DossierPublishService } from '../../data-access/dossier-publish.service';
import { DossierDocumentsTabComponent } from '../dossier-documents/dossier-documents-tab.component';
import { DossierVersionsTabComponent } from '../dossier-versions-tab/dossier-versions-tab.component';
import { DossierWorkflowTabComponent } from '../dossier-workflow-tab/dossier-workflow-tab.component';
import { AuthService } from '../../../../../../shared/core/src/lib/services/auth.service';
import { WorkflowService } from '@sohoa.frontend/shared/core';
import {
  EavField,
  formatFieldDisplayValue,
  guidsEqual,
  normalizeDossierDetail,
  normalizeField,
  parseFormDataJson,
  pickFormDataForSchema,
  readFormSchemaJson,
} from '../../utils/dossier-form-schema.util';
import {
  isApproveWorkflowLabel,
  isRejectWorkflowLabel,
  parseWorkflowActionButtons,
  resolveEligibleAssigneeGroupParams,
  resolveDefaultNextAssignee,
  resolveNextUserCandidates,
} from '../../utils/dossier-workflow-bpmn.util';
import { DossierMenuScope, getDossierStatusLabel, getDossierStatusPillClass } from '../../utils/dossier-status.util';
import {
  isUserAuthorizedForWorkflowAction,
  mapAvailableActionsToButtons,
} from '../../utils/dossier-workflow-auth.util';

function pickFirst<T>(...values: T[]): T | undefined {
  for (const v of values) {
    if (v !== undefined && v !== null && v !== '') return v;
  }
  return undefined;
}

@Component({
  selector: 'app-dossier-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    DialogModule,
    DossierDocumentsTabComponent,
    DossierVersionsTabComponent,
    DossierWorkflowTabComponent,
    PaginatorModule,
  ],
  templateUrl: './dossier-detail.component.html',
  styleUrl: './dossier-detail.component.scss',
})
export class DossierDetailComponent implements OnInit, OnDestroy {
  @Input() dossierId!: string;
  @Input() menuScope: DossierMenuScope = 'creator';
  @Output() cancel = new EventEmitter<void>();
  @Output() edit = new EventEmitter<void>();

  private service = inject(DossierManagementService);
  private publishService = inject(DossierPublishService);
  private authService = inject(AuthService);
  private workflowSvc = inject(WorkflowService);
  private router = inject(Router);

  // Related Dossiers tab state
  relatedDossiers = signal<any[]>([]);
  loadingRelated = signal<boolean>(false);
  relatedFirst = signal<number>(0);
  relatedRows = signal<number>(10);
  totalRelatedDossiers = computed(() => this.relatedDossiers().length);

  paginatedRelatedDossiers = computed(() => {
    const list = this.relatedDossiers();
    const first = this.relatedFirst();
    const rows = this.relatedRows();
    return list.slice(first, first + rows);
  });

  relatedEquipments = computed(() => this.equipments());
  dossierTypes = signal<any[]>([]);

  // Related Dossiers Filters
  filterKeyword = '';
  filterEquipmentId = '';
  filterDossierTypeId = '';

  onRelatedPageChange(event: any) {
    this.relatedFirst.set(event.first);
    this.relatedRows.set(event.rows);
  }

  onRelatedFilterChange() {
    this.relatedFirst.set(0);
    this.loadRelatedDossiers();
  }

  loadRelatedDossiers() {
    const d = this.dossier();
    if (!d) return;
    const dossierId = d.id || d.Id || this.dossierId;
    if (!dossierId) return;

    this.loadingRelated.set(true);
    this.service.getRelatedDossiers(dossierId, {
      keyword: this.filterKeyword,
      equipmentId: this.filterEquipmentId,
      dossierTypeId: this.filterDossierTypeId
    }, this.dossierKindId()).subscribe({
      next: (res) => {
        this.relatedDossiers.set(Array.isArray(res) ? res : []);
        this.loadingRelated.set(false);
      },
      error: () => {
        this.relatedDossiers.set([]);
        this.loadingRelated.set(false);
      }
    });
  }

  loadDossierTypes() {
    this.service.getDossierTypeLookup().subscribe({
      next: (types) => {
        this.dossierTypes.set(Array.isArray(types) ? types : []);
      }
    });
  }

  openRelatedDetail(rel: any) {
    const id = rel.id || rel.dossierId;
    if (!id) return;

    const scope = this.menuScope;
    const kind = Number(
      (this.dossier() as Record<string, unknown>)?.['kindId']
      ?? (this.dossier() as Record<string, unknown>)?.['KindId']
      ?? 2
    );

    const segments: string[] = [];
    if (scope === 'publisher') {
      segments.push('publish');
    } else if (kind === 1) {
      segments.push('digitization');
      segments.push(scope === 'approver' ? 'approve' : 'my-dossiers');
    } else {
      if (scope === 'approver') segments.push('approve');
      else segments.push('my-dossiers');
    }

    void this.router.navigate(['/dossier-management', ...segments, id]);
  }
  private messageService = inject(MessageService);
  private destroy$ = new Subject<void>();

  loading = signal<boolean>(true);
  submitting = signal<boolean>(false);
  activeTab = signal<'info' | 'documents' | 'versions' | 'workflow'>('info');
  workflowRefreshToken = signal(0);

  dossier = signal<any>(null);
  dossierKindId = signal<number>(2);
  dossierMeta = computed(() => normalizeDossierDetail(this.dossier()));

  viewMeta = computed(() => {
    const d = this.dossier();
    if (!d) return null;
    const shelfName = pickFirst(d.shelfName, d.ShelfName) as string | undefined;
    const shelfCode = pickFirst(d.shelfCode, d.ShelfCode) as string | undefined;
    const floorName = pickFirst(d.floorName, d.FloorName) as string | undefined;
    const floorCode = pickFirst(d.floorCode, d.FloorCode) as string | undefined;
    const boxName = pickFirst(d.boxName, d.BoxName) as string | undefined;
    const boxCode = pickFirst(d.boxCode, d.BoxCode) as string | undefined;
    const boxId = pickFirst(d.boxId, d.BoxId);

    const shelf = shelfName || shelfCode;
    const floor = floorName || floorCode;
    const box = boxName || boxCode;
    const storageLabel = boxId
      ? [shelf, floor, box].filter(Boolean).join(' / ') || `Hộp #${boxId}`
      : '';

    return {
      dossierGroupName: pickFirst(d.dossierGroupName, d.DossierGroupName) as string | undefined,
      isEquipmentDossier: (() => {
        const flag = pickFirst(d.isEquipmentDossier, d.IsEquipmentDossier);
        return flag === true || flag === 1 || flag === '1';
      })(),
      gridTypeName: pickFirst(d.gridTypeName, d.GridTypeName) as string | undefined,
      infrastructureName: pickFirst(d.infrastructureName, d.InfrastructureName) as string | undefined,
      infrastructureCode: pickFirst(d.infrastructureCode, d.InfrastructureCode) as string | undefined,
      storageLabel,
      shelfName,
      shelfCode,
      floorName,
      floorCode,
      boxName,
      boxCode,
    };
  });

  equipments = computed(() => {
    const d = this.dossier();
    const list = d?.equipments ?? d?.Equipments ?? [];
    return Array.isArray(list) ? list : [];
  });

  formatFieldDisplayValue = formatFieldDisplayValue;

  getFieldValueText(field: EavField): string {
    const value = this.detailFormData[field.key];
    if (value === null || value === undefined || value === '') {
      return '-';
    }
    if (field.type === 'select') {
      const option = field.options?.find(opt => opt.value === value);
      return option ? option.label : value;
    }
    if (field.type === 'checkbox') {
      return value ? 'Có' : 'Không';
    }
    if (field.type === 'date') {
      try {
        const date = new Date(value);
        if (!isNaN(date.getTime())) {
          return date.toLocaleDateString('vi-VN');
        }
      } catch (e) {
        // ignore
      }
    }
    return value;
  }

  // Catalog columns map for dynamic fields
  catalogColumnsMap = signal<Record<string, string>>({});

  formatKeyToLabel(key: string): string {
    if (!key) return '';
    return key
      .split('_')
      .map(word => word.charAt(0).toUpperCase() + word.slice(1))
      .join(' ');
  }

  // Restore and display all dynamic fields including formDataJson
  allFields = computed(() => {
    const dossierVal = this.dossier(); // tracks dossier load
    const fields = [...this.dynamicFields()];
    const data = this.detailFormData || {};
    const colMap = this.catalogColumnsMap();

    const existingKeys = new Set(fields.map(f => f.key));

    Object.keys(data).forEach(key => {
      if (!existingKeys.has(key)) {
        const label = colMap[key] || this.formatKeyToLabel(key);
        fields.push({
          key: key,
          label: label,
          type: 'text' as const
        });
      }
    });

    return fields;
  });

  leftDynamicFields = computed(() => {
    const fields = this.allFields();
    return fields.slice(0, Math.ceil(fields.length / 2));
  });

  rightDynamicFields = computed(() => {
    const fields = this.allFields();
    return fields.slice(Math.ceil(fields.length / 2));
  });

  loadCatalogColumns() {
    this.service.getBhsCatalogColumns().subscribe({
      next: (cols) => {
        const map: Record<string, string> = {};
        if (Array.isArray(cols)) {
          cols.forEach(c => {
            if (c.code) map[c.code] = c.label || c.key;
          });
        }
        this.catalogColumnsMap.set(map);
      }
    });
  }

  // EAV Form
  loadingType = signal<boolean>(false);
  formTemplate = signal<any>(null);
  dynamicFields = signal<EavField[]>([]);
  detailFormData: Record<string, any> = {};
  private pendingFormData: Record<string, unknown> = {};

  // Submit for approval confirmation
  showSubmitConfirm = signal<boolean>(false);
  showCompleteInputConfirm = signal<boolean>(false);
  showPublishActionConfirm = signal<boolean>(false);
  pendingPublishAction = signal<'publish' | 'unpublish' | 'republish' | null>(null);
  publishActionSubmitting = signal<boolean>(false);
  nextStepInfo = signal<any>(null);
  selectedNextUser = signal<string>('');
  eligibleSubmitUsers = signal<any[]>([]);
  loadingEligibleSubmitUsers = signal<boolean>(false);

  // Workflow — core
  workflowDetail = signal<any>(null);
  myTask = signal<any>(null);
  loadingBpmn = signal<boolean>(false);

  // Workflow — BPMN viewer state
  detailWorkflowXml = signal<string>('');
  detailCurrentNodeId = signal<string>('');
  detailPendingTask = signal<any>(null);
  detailDynamicButtons = signal<any[]>([]);
  detailActionComment = signal<string>('');
  detailActionSubmitting = signal<boolean>(false);
  selectedNextUserId = signal<string>('');

  // Users lookup for next-assignee selector
  users = signal<any[]>([]);

  hasForwardActionWithUserRequirement = computed(() =>
    this.detailDynamicButtons().some(btn =>
      btn.requiresUser && !this.isRejectLabel(btn.label)
    )
  );

  eligibleNextUsers = signal<any[]>([]);
  loadingEligibleNextUsers = signal<boolean>(false);

  // Danh sách người xử lý bước tiếp theo (nút chuyển bước giữa luồng), ưu tiên theo cấu hình
  // của bước ĐÍCH: Nhóm quyền đơn vị > Nhóm quyền hệ thống > requiredRole cũ > toàn bộ user.
  // Giao việc đích danh không giới hạn danh sách — chỉ chọn sẵn mặc định (xem openActionDialog).
  filteredNextUsers = computed(() => {
    const forwardBtn = this.detailDynamicButtons().find(btn =>
      btn.requiresUser && !this.isRejectLabel(btn.label)
    );
    return resolveNextUserCandidates({
      info: forwardBtn ?? null,
      allUsers: this.users(),
      eligibleUsers: this.eligibleNextUsers(),
    });
  });

  private loadEligibleNextUsers(info: any): void {
    this.eligibleNextUsers.set([]);
    const groupParams = resolveEligibleAssigneeGroupParams(info);
    if (!groupParams) return;
    const unitId = info?.requireSameUnit ? (this.authService.getUserUnitId() ?? undefined) : undefined;
    this.loadingEligibleNextUsers.set(true);
    this.workflowSvc.getEligibleAssignees(groupParams.systemGroupIds, groupParams.unitGroupIds, unitId)
      .pipe(finalize(() => this.loadingEligibleNextUsers.set(false)))
      .subscribe({
        next: (list) => this.eligibleNextUsers.set(Array.isArray(list) ? list : []),
        error: () => this.eligibleNextUsers.set([])
      });
  }

  // Danh sách người xử lý tiếp theo, ưu tiên theo đúng thứ tự nghiệp vụ:
  // Nhóm quyền đơn vị > Nhóm quyền hệ thống > (cũ) requiredRole > toàn bộ user.
  // Giao việc đích danh không giới hạn danh sách — chỉ chọn sẵn mặc định (xem loadEligibleSubmitUsers).
  filteredSubmitNextUsers = computed(() => resolveNextUserCandidates({
    info: this.nextStepInfo(),
    allUsers: this.users(),
    eligibleUsers: this.eligibleSubmitUsers(),
  }));

  private loadEligibleSubmitUsers(info: any): void {
    this.eligibleSubmitUsers.set([]);
    this.selectedNextUser.set(resolveDefaultNextAssignee(info));
    const groupParams = resolveEligibleAssigneeGroupParams(info);
    if (!groupParams) return;
    const unitId = info?.requireSameUnit ? (this.authService.getUserUnitId() ?? undefined) : undefined;
    this.loadingEligibleSubmitUsers.set(true);
    this.workflowSvc.getEligibleAssignees(groupParams.systemGroupIds, groupParams.unitGroupIds, unitId)
      .pipe(finalize(() => this.loadingEligibleSubmitUsers.set(false)))
      .subscribe({
        next: (list) => this.eligibleSubmitUsers.set(Array.isArray(list) ? list : []),
        error: () => this.eligibleSubmitUsers.set([])
      });
  }

  // Dialog xác nhận hành động
  showActionDialog = signal<boolean>(false);
  pendingActionBtn = signal<any>(null);

  get isDraftOrReturned(): boolean {
    const statusId = this.dossier()?.statusId ?? this.dossier()?.StatusId;
    return statusId === 1 || statusId === 2 || statusId === 5;
  }

  showCompleteInputButton(): boolean {
    if (this.menuScope !== 'creator') return false;
    const d = this.dossier();
    if (!d) return false;
    const statusId = d.statusId ?? d.StatusId;
    return statusId === 1;
  }

  showSubmitForApprovalButton(): boolean {
    if (this.menuScope !== 'creator') return false;
    const d = this.dossier();
    if (!d) return false;
    const statusId = d.statusId ?? d.StatusId;
    const wfId = d.workflowInstanceId ?? d.WorkflowInstanceId
      ?? this.workflowDetail()?.instance?.id
      ?? this.workflowDetail()?.instance?.Id;
    return statusId === 2 && !wfId;
  }

  private isCurrentUserCreator(): boolean {
    const d = this.dossier();
    if (!d) return false;

    const userId = this.authService.getUserId();
    const creatorId = d.creator?.id ?? d.Creator?.Id ?? d.creatorId ?? d.CreatorId;
    const creatorUsername = d.creator?.username ?? d.Creator?.Username
      ?? d.createdBy ?? d.CreatedBy ?? d.creatorUsername ?? d.CreatorUsername;

    const normalizeGuid = (val: unknown) => val ? String(val).replace(/-/g, '').toLowerCase().trim() : '';
    const normCreatorId = normalizeGuid(creatorId);
    const normUserId = normalizeGuid(userId);

    if (normCreatorId !== '' && normCreatorId === normUserId) return true;

    const normCreatorUsername = creatorUsername ? String(creatorUsername).toLowerCase().trim() : '';
    const normUserUsername = userId ? String(userId).toLowerCase().trim() : '';
    return normCreatorUsername !== '' && normCreatorUsername === normUserUsername;
  }

  ngOnInit() {
    this.loadCatalogColumns();
    this.loadDossierTypes();
    this.loadDetail();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadDetail() {
    this.loading.set(true);
    this.loadingType.set(true);

    const detail$ = this.menuScope === 'publisher'
      ? this.publishService.getDetail(this.dossierId)
      : this.service.getDossierById(this.dossierId);

    detail$.pipe(
      switchMap((res) => {
        const meta = normalizeDossierDetail(res);
        if (!meta) {
          throw new Error('Invalid dossier response');
        }

        const resolvedKindId = Number(
          (res as Record<string, unknown>)?.['kindId']
          ?? (res as Record<string, unknown>)?.['KindId']
          ?? 2
        );
        this.dossierKindId.set(resolvedKindId === 1 ? 1 : 2);

        this.dossier.set(res);
        this.pendingFormData = parseFormDataJson(meta.formDataJson);
        this.detailFormData = { ...this.pendingFormData };
        this.loading.set(false);

        // Load related dossiers
        this.loadRelatedDossiers();

        return this.resolveFormTemplate(meta.formId, meta.dossierTypeId);
      }),
      takeUntil(this.destroy$),
      finalize(() => {
        this.loading.set(false);
        this.loadingType.set(false);
      })
    ).subscribe({
      next: (template) => {
        this.applyFormTemplate(template);
        if (this.menuScope !== 'publisher') {
          this.loadWorkflow();
        }
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải chi tiết hồ sơ' });
        this.dynamicFields.set([]);
        this.loading.set(false);
      }
    });
  }

  /** Ưu tiên formId từ detail API; fallback lookup loại hồ sơ. */
  private resolveFormTemplate(formId: string | null, dossierTypeId: string) {
    const scope = this.menuScope === 'publisher' ? 'publish' as const : 'default' as const;

    if (formId) {
      return this.service.getDossierFormTemplate(this.dossierId, formId, scope);
    }

    if (!dossierTypeId) {
      return of(null);
    }

    return this.service.getDossierTypeLookup().pipe(
      catchError(() => of([] as any[])),
      switchMap((types) => {
        const found = Array.isArray(types)
          ? types.find((t: any) => guidsEqual(t.id ?? t.Id, dossierTypeId))
          : undefined;
        const resolvedFormId = found?.formId ?? found?.FormId ?? null;
        if (!resolvedFormId) {
          return of(null);
        }
        return this.service.getDossierFormTemplate(this.dossierId, resolvedFormId, scope);
      })
    );
  }

  private applyFormTemplate(template: any) {
    if (!template) {
      this.formTemplate.set(null);
      this.dynamicFields.set([]);
      this.detailFormData = { ...this.pendingFormData };
      return;
    }

    this.formTemplate.set(template);
    const schemaJson = readFormSchemaJson(template);
    if (!schemaJson) {
      this.dynamicFields.set([]);
      this.detailFormData = { ...this.pendingFormData };
      return;
    }

    try {
      const raw = JSON.parse(schemaJson);
      const fields: EavField[] = Array.isArray(raw) ? raw.map((f) => normalizeField(f)) : [];
      this.dynamicFields.set(fields);
      this.detailFormData = pickFormDataForSchema(fields, this.pendingFormData);
    } catch {
      this.dynamicFields.set([]);
      this.detailFormData = { ...this.pendingFormData };
    }
  }

  loadFormTemplate(dossierTypeId: string, formId?: string | null) {
    this.loadingType.set(true);
    this.resolveFormTemplate(formId ?? null, dossierTypeId).pipe(
      finalize(() => this.loadingType.set(false)),
      takeUntil(this.destroy$)
    ).subscribe({
      next: (template) => this.applyFormTemplate(template),
      error: () => {
        this.formTemplate.set(null);
        this.dynamicFields.set([]);
      }
    });
  }

  /** Gán state workflow từ response getWorkflowDetail — tách riêng để bọc try/catch an toàn */
  private applyWorkflowDetailState(res: any): void {
    this.workflowDetail.set(res);

    if (!res?.instance) {
      this.detailWorkflowXml.set('');
      this.detailPendingTask.set(null);
      this.detailCurrentNodeId.set('');
      this.detailDynamicButtons.set([]);
      return;
    }

    const instance = res.instance;
    const pendingList = Array.isArray(instance.pendingTasks)
      ? instance.pendingTasks
      : Array.isArray(instance.PendingTasks)
        ? instance.PendingTasks
        : [];
    const pending = pendingList.length > 0 ? pendingList[0] : null;
    this.detailPendingTask.set(pending);
    this.detailCurrentNodeId.set(pickFirst(instance.currentNodeId, instance.CurrentNodeId) || '');

    const availableActions = instance.availableActions ?? instance.AvailableActions;
    const mappedActions = mapAvailableActionsToButtons(availableActions);
    if (mappedActions.length > 0) {
      this.detailDynamicButtons.set(mappedActions);
      return;
    }

    const bpmnXml = res.definition?.bpmnXml ?? res.definition?.BpmnXml ?? res.definition?.workflowXml ?? res.definition?.WorkflowXml;
    if (bpmnXml) {
      this.detailWorkflowXml.set(bpmnXml);
      const stepName = pickFirst(
        pending?.stepName,
        pending?.StepName,
        instance.currentStepName,
        instance.CurrentStepName
      ) ?? '';
      const nodeId = pickFirst(instance.currentNodeId, instance.CurrentNodeId);
      if (nodeId) {
        this.parseDynamicButtons(bpmnXml, stepName, nodeId);
        return;
      }
    }

    this.detailWorkflowXml.set(bpmnXml ?? '');
    this.detailDynamicButtons.set([]);
  }

  loadWorkflow() {
    this.loadingBpmn.set(true);
    this.service.getWorkflowDetail(this.dossierId, this.dossierKindId()).pipe(
      finalize(() => this.loadingBpmn.set(false))
    ).subscribe({
      next: (res) => {
        try {
          this.applyWorkflowDetailState(res);
          this.workflowRefreshToken.update((v) => v + 1);
        } catch (err) {
          console.error('applyWorkflowDetailState error', err);
        }

        this.service.getUsersLookup().subscribe({
          next: (users) => this.users.set(Array.isArray(users) ? users : []),
          error: () => this.users.set([])
        });

        const instanceId = pickFirst(
          res?.instance?.instanceId,
          res?.instance?.InstanceId,
          res?.instance?.id,
          res?.instance?.Id
        );

        if (!instanceId) {
          this.myTask.set(null);
          return;
        }

        this.service.getMyTasks(String(instanceId)).subscribe({
          next: (tasks) => {
            const list = Array.isArray(tasks) ? tasks : [];
            this.myTask.set(list[0] ?? null);

            if (!this.detailDynamicButtons().length && list[0]) {
              const wf = this.workflowDetail();
              const bpmnXml = wf?.definition?.bpmnXml ?? wf?.definition?.BpmnXml ?? wf?.definition?.workflowXml ?? wf?.definition?.WorkflowXml;
              const task = list[0];
              const stepName = pickFirst(
                task.workflowStatusName,
                task.WorkflowStatusName,
                task.stepName,
                task.StepName
              ) ?? '';
              const nodeId = this.detailCurrentNodeId();
              if (bpmnXml && nodeId) {
                this.parseDynamicButtons(bpmnXml, stepName, nodeId);
              }
            }
          },
          error: () => this.myTask.set(null)
        });
      },
      error: () => {
        this.messageService.add({
          severity: 'warn',
          summary: 'Cảnh báo',
          detail: 'Không thể tải thông tin quy trình'
        });
      }
    });
  }

  parseDynamicButtons(xml: string, stepName: string, currentNodeId?: string) {
    this.detailDynamicButtons.set(parseWorkflowActionButtons(xml, stepName, currentNodeId));
  }

  isRejectLabel(label: string): boolean {
    return isRejectWorkflowLabel(label);
  }

  isApproveLabel(label: string): boolean {
    return isApproveWorkflowLabel(label);
  }

  get isUserAuthorizedForDetailAction(): boolean {
    if (this.menuScope === 'publisher') return false;

    const task = this.detailPendingTask();
    const d = this.dossier();
    const instance = this.workflowDetail()?.instance;
    const currentAssignees = Array.isArray(instance?.currentAssignees)
      ? instance.currentAssignees
      : Array.isArray(instance?.CurrentAssignees)
        ? instance.CurrentAssignees
        : [];

    return isUserAuthorizedForWorkflowAction({
      authService: this.authService,
      menuScope: this.menuScope,
      assigneeUserId: task?.assigneeUserId ?? task?.AssigneeUserId,
      currentAssignees,
      statusId: d?.statusId ?? d?.StatusId,
      isCreator: this.isCurrentUserCreator(),
      hasMyTask: !!this.myTask(),
    });
  }

  openActionDialog(btn: any) {
    this.pendingActionBtn.set(btn);
    this.detailActionComment.set('');
    this.selectedNextUserId.set(resolveDefaultNextAssignee(btn));
    this.showActionDialog.set(true);
    if (btn?.requiresUser && !this.isRejectLabel(btn?.label)) {
      this.loadEligibleNextUsers(btn);
    }
  }

  confirmAction() {
    const btn = this.pendingActionBtn();
    if (!btn) return;
    this.submitDetailMoveAction(btn.targetNodeId, btn.label, btn.requiresUser);
  }

  submitDetailMoveAction(targetNodeId: string, actionLabel: string, requiresUser?: boolean) {
    const isCancel = this.isRejectLabel(actionLabel);
    if (requiresUser && !isCancel && !this.selectedNextUserId()) {
      this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Vui lòng chọn người xử lý bước tiếp theo.' });
      return;
    }

    this.detailActionSubmitting.set(true);
    const payload = {
      nextNodeId: targetNodeId,
      actionLabel,
      comment: this.detailActionComment(),
      nextAssigneeUserId: (!isCancel && requiresUser) ? this.selectedNextUserId() : undefined
    };
    const statusId = this.dossier()?.statusId ?? this.dossier()?.StatusId;
    const useResubmit = this.menuScope === 'creator' && statusId === 5;
    const workflowCall = useResubmit
      ? this.service.resubmitWorkflow(this.dossierId, payload)
      : this.service.moveWorkflow(this.dossierId, payload);

    workflowCall.subscribe({
      next: (res) => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: `Đã thực hiện: ${actionLabel}` });
        this.detailActionSubmitting.set(false);
        this.showActionDialog.set(false);
        this.detailActionComment.set('');
        this.selectedNextUserId.set('');
        this.pendingActionBtn.set(null);

        this.detailDynamicButtons.set([]);
        this.myTask.set(null);
        if (res?.data?.workflow) {
          this.applyWorkflowDetailState({
            instance: res.data.workflow,
            definition: this.workflowDetail()?.definition ?? null,
            history: this.workflowDetail()?.history ?? [],
          });
        } else {
          this.detailPendingTask.set(null);
          this.detailWorkflowXml.set('');
          this.detailCurrentNodeId.set('');
        }

        this.workflowRefreshToken.update((v) => v + 1);
        this.loadDetail();
      },
      error: (err: any) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể thực hiện.' });
        this.detailActionSubmitting.set(false);
      }
    });
  }

  canEditDossier(): boolean {
    if (this.menuScope === 'publisher') return false;

    const d = this.dossier();
    if (!d) return false;

    const userId = this.authService.getUserId();
    const roles = this.authService.getUserRoles?.() ?? [];

    if (roles.includes('ADMIN')) return true;

    const statusId = d.statusId ?? d.StatusId;
    if (statusId === 1 || statusId === 2) {
      if (this.menuScope !== 'creator') return false;
      if (!this.authService.hasPermission('DOSSIER_EDIT') && !this.authService.hasPermission('DOSSIER_CREATE')) {
        return false;
      }
      const creatorId = d.creator?.id ?? d.Creator?.Id ?? d.creatorId ?? d.CreatorId;
      const creatorUsername = d.creator?.username ?? d.Creator?.Username ?? d.createdBy ?? d.CreatedBy ?? d.creatorUsername ?? d.CreatorUsername;
      
      const normalizeGuid = (val: any) => val ? String(val).replace(/[-]/g, '').toLowerCase().trim() : '';
      const normCreatorId = normalizeGuid(creatorId);
      const normUserId = normalizeGuid(userId);
      
      const normCreatorUsername = creatorUsername ? String(creatorUsername).toLowerCase().trim() : '';
      const normUserUsername = userId ? String(userId).toLowerCase().trim() : '';

      return (normCreatorId !== '' && normCreatorId === normUserId) ||
             (normCreatorUsername !== '' && normCreatorUsername === normUserUsername);
    }

    // Trả lại — cán bộ tạo được sửa trên menu quản lý (không cần cờ AllowEdit)
    if (statusId === 5) {
      if (this.menuScope !== 'creator') return false;
      if (!this.authService.hasPermission('DOSSIER_EDIT')) return false;
      return this.isCurrentUserCreator();
    }

    // Các trạng thái WF khác: bước hiện tại phải AllowEdit và user là assignee cụ thể
    if (!this.authService.hasPermission('DOSSIER_EDIT')) {
      return false;
    }

    const instance = this.workflowDetail()?.instance;
    if (!instance) return false;

    const stepAllowEdit = !!(instance.currentStepAllowEdit ?? instance.CurrentStepAllowEdit);
    if (!stepAllowEdit) return false;

    const pendingList = Array.isArray(instance.pendingTasks)
      ? instance.pendingTasks
      : Array.isArray(instance.PendingTasks)
        ? instance.PendingTasks
        : [];

    if (pendingList.length === 0) return false;

    return pendingList.some((task: any) => {
      const assigneeId = task.assigneeUserId ?? task.AssigneeUserId;
      if (!assigneeId) return false;
      return String(assigneeId).toLowerCase() === String(userId).toLowerCase();
    });
  }

  onEdit() {
    this.edit.emit();
  }

  onCancel() {
    this.cancel.emit();
  }

  onCompleteInput() {
    this.showCompleteInputConfirm.set(true);
  }

  confirmCompleteInput() {
    if (this.submitting()) return;

    this.submitting.set(true);
    this.service.completeInput(this.dossierId).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã hoàn thành nhập liệu thành công' });
        this.showCompleteInputConfirm.set(false);
        this.submitting.set(false);
        this.loadDetail();
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể hoàn thành nhập liệu' });
        this.submitting.set(false);
      }
    });
  }

  openSubmitWorkflowDialog() {
    this.submitting.set(true);
    this.service.getNextStepInfo().subscribe({
      next: (res) => {
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
    this.service.submitForApproval(this.dossierId, {
      nextNodeId: info.nextNodeId,
      actionLabel: 'Trình duyệt',
      nextAssigneeUserId: this.selectedNextUser() || undefined,
      comment: 'Kính trình phê duyệt hồ sơ.'
    }).subscribe({
      next: (res) => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã gửi duyệt hồ sơ thành công' });
        this.showSubmitConfirm.set(false);
        const payload = res?.data;
        if (payload) {
          this.dossier.update((current) =>
            current
              ? {
                  ...current,
                  statusId: payload.dossierStatusId ?? current.statusId,
                  statusName: payload.dossierStatusName ?? current.statusName,
                  workflowStatusName: payload.workflowStepName ?? current.workflowStatusName,
                  workflowInstanceId: payload.instanceId ?? current.workflowInstanceId,
                }
              : current
          );
        }
        this.submitting.set(false);
        this.loadWorkflow();
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể gửi duyệt hồ sơ' });
        this.showSubmitConfirm.set(false);
        this.submitting.set(false);
      }
    });
  }

  trackByFieldKey(_index: number, field: EavField): string {
    return field.key;
  }

  getStatusText(status?: string | number, statusName?: string): string {
    return getDossierStatusLabel(status, statusName);
  }

  getStatusClass(status?: string | number): string {
    return getDossierStatusPillClass(status);
  }

  getPublishStatusId(): number {
    const d = this.dossier();
    return Number(d?.publishStatusId ?? d?.PublishStatusId ?? 0);
  }

  canReleasePublish(): boolean {
    return this.authService.hasPermission('DOSSIER_PUBLISH_RELEASE');
  }

  showPublishButton(): boolean {
    return this.menuScope === 'publisher' && this.canReleasePublish() && this.getPublishStatusId() === 1;
  }

  showUnpublishButton(): boolean {
    return this.menuScope === 'publisher' && this.canReleasePublish() && this.getPublishStatusId() === 2;
  }

  showRepublishButton(): boolean {
    return this.menuScope === 'publisher' && this.canReleasePublish() && this.getPublishStatusId() === 3;
  }

  publishActionHeader(): string {
    switch (this.pendingPublishAction()) {
      case 'publish': return 'Xác nhận xuất bản';
      case 'unpublish': return 'Xác nhận hủy xuất bản';
      case 'republish': return 'Xác nhận tái xuất bản';
      default: return 'Xác nhận hành động';
    }
  }

  publishActionTitle(): string {
    switch (this.pendingPublishAction()) {
      case 'publish': return 'Bạn có chắc chắn muốn xuất bản hồ sơ này?';
      case 'unpublish': return 'Bạn có chắc chắn muốn hủy xuất bản hồ sơ này?';
      case 'republish': return 'Bạn có chắc chắn muốn tái xuất bản hồ sơ này?';
      default: return 'Xác nhận thực hiện hành động?';
    }
  }

  publishActionButtonColor(): string {
    return this.pendingPublishAction() === 'unpublish' ? '#dc2626' : '#22c55e';
  }

  requestPublishAction(type: 'publish' | 'unpublish' | 'republish') {
    this.pendingPublishAction.set(type);
    this.showPublishActionConfirm.set(true);
  }

  cancelPublishAction() {
    this.showPublishActionConfirm.set(false);
    this.pendingPublishAction.set(null);
  }

  confirmPublishAction() {
    const type = this.pendingPublishAction();
    if (!type || this.publishActionSubmitting()) return;

    this.publishActionSubmitting.set(true);
    let obs$;
    if (type === 'publish') {
      obs$ = this.publishService.publish(this.dossierId);
    } else if (type === 'unpublish') {
      obs$ = this.publishService.unpublish(this.dossierId);
    } else {
      obs$ = this.publishService.republish(this.dossierId);
    }

    obs$.pipe(
      finalize(() => {
        this.publishActionSubmitting.set(false);
        this.cancelPublishAction();
      })
    ).subscribe({
      next: () => {
        const detail =
          type === 'publish'
            ? 'Xuất bản hồ sơ thành công'
            : type === 'unpublish'
              ? 'Hủy xuất bản hồ sơ thành công'
              : 'Tái xuất bản hồ sơ thành công';
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail });
        this.loadDetail();
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err?.error?.message || 'Có lỗi xảy ra khi thực hiện thao tác',
        });
      },
    });
  }

  isDetailTabVisible(tab: 'info' | 'documents' | 'versions' | 'workflow'): boolean {
    const d = this.dossier();
    const wfId = d?.workflowInstanceId ?? d?.WorkflowInstanceId
      ?? this.workflowDetail()?.instance?.id
      ?? this.workflowDetail()?.instance?.Id
      ?? this.workflowDetail()?.instance?.instanceId
      ?? this.workflowDetail()?.instance?.InstanceId;

    switch (tab) {
      case 'info':
      case 'documents':
        return true;
      case 'versions':
        return true;
      case 'workflow':
        return this.menuScope !== 'publisher' && (!!wfId || this.menuScope === 'approver');
      default:
        return false;
    }
  }
}
