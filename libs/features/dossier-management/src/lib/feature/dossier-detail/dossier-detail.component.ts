import { Component, OnInit, OnDestroy, signal, computed, inject, Output, EventEmitter, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { catchError, finalize, of, switchMap, takeUntil, Subject } from 'rxjs';
import { DossierManagementService } from '../../data-access/dossier-management.service';
import { DossierDocumentsTabComponent } from '../dossier-documents/dossier-documents-tab.component';
import { DossierVersionsTabComponent } from '../dossier-versions-tab/dossier-versions-tab.component';
import { DossierWorkflowTabComponent } from '../dossier-workflow-tab/dossier-workflow-tab.component';
import { AuthService } from '@sohoa.frontend/shared/core';
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

function pickFirst<T>(...values: T[]): T | undefined {
  for (const v of values) {
    if (v !== undefined && v !== null && v !== '') return v;
  }
  return undefined;
}

@Component({
  selector: 'app-dossier-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, DialogModule, DossierDocumentsTabComponent, DossierVersionsTabComponent, DossierWorkflowTabComponent],
  template: `
    <div class="wf-card" style="position: relative;">
      <!-- Header -->
      <div class="edit-header">
        <div style="display: flex; align-items: flex-start; gap: 10px;">
          <button (click)="onCancel()" class="btn-back btn-small" title="Quay lại" style="margin-top: 2px;">
            <i class="pi pi-arrow-left"></i>
          </button>
          <div>
            <h2 class="edit-title">Chi tiết Hồ sơ</h2>
            <div style="display: flex; flex-wrap: wrap; gap: 16px; margin-top: 8px; font-size: 0.83rem;">
              <span class="text-muted"><i class="pi pi-tag" style="margin-right: 4px;"></i> Loại hồ sơ: <b style="color: #374151;">{{ dossierMeta()?.dossierTypeName || '-' }}</b></span>
              <span class="text-muted"><i class="pi pi-map-marker" style="margin-right: 4px;"></i> Trạm/ĐZ: <b style="color: #374151;">{{ dossierMeta()?.infrastructureName || '-' }}</b></span>
              <span class="text-muted" style="display: inline-flex; align-items: center; gap: 6px;">
                Trạng thái:
                <span class="status-pill" [ngStyle]="getStatusStyle(dossierMeta()?.status)">
                  {{ getStatusText(dossierMeta()?.status) }}
                </span>
              </span>
            </div>
          </div>
        </div>
        <div class="edit-actions">
          <button *ngIf="canEditDossier()"
                  (click)="onEdit()" class="btn-save btn-small">
            <i class="pi pi-pencil"></i> Sửa hồ sơ
          </button>
          <!-- Gửi duyệt -->
          <button *ngIf="dossier()?.status === 'Draft' || dossier()?.status === 'Returned'"
                  (click)="showSubmitConfirm.set(true)" class="btn-green btn-small" [disabled]="submitting()">
            <i class="pi pi-send" *ngIf="!submitting()"></i>
            <i class="pi pi-spin pi-spinner" *ngIf="submitting()"></i>
            Gửi duyệt
          </button>
          <!-- Workflow action buttons: Duyệt / Tiếp tục / Từ chối (luôn hiện khi có pending task) -->
          <ng-container *ngIf="detailPendingTask() && isUserAuthorizedForDetailAction">
            <button *ngFor="let btn of detailDynamicButtons()"
                    class="btn-small"
                    [class.btn-cancel]="isRejectLabel(btn.label)"
                    [class.btn-save]="isApproveLabel(btn.label)"
                    [class.btn-green]="!isRejectLabel(btn.label) && !isApproveLabel(btn.label)"
                    (click)="openActionDialog(btn)">
              <i class="pi"
                 [class.pi-check]="!isRejectLabel(btn.label)"
                 [class.pi-times]="isRejectLabel(btn.label)"
                 style="margin-right: 4px;"></i>
              {{ btn.label }}
            </button>
          </ng-container>
        </div>
      </div>

      <!-- TABS -->
      <div class="tab-bar">
        <button class="tab-item" [class.tab-active]="activeTab() === 'info'" (click)="activeTab.set('info')">
          <i class="pi pi-info-circle" style="margin-right: 6px;"></i>
          Dữ liệu Hồ sơ
        </button>
        <button class="tab-item" [class.tab-active]="activeTab() === 'documents'" (click)="activeTab.set('documents')">
          <i class="pi pi-file" style="margin-right: 6px;"></i> Tài liệu đính kèm
        </button>
        <button class="tab-item" [class.tab-active]="activeTab() === 'versions'" (click)="activeTab.set('versions')">
          <i class="pi pi-history" style="margin-right: 6px;"></i> Lịch sử phiên bản
        </button>
        <button class="tab-item" [class.tab-active]="activeTab() === 'workflow'" (click)="activeTab.set('workflow')">
          <i class="pi pi-sitemap" style="margin-right: 6px;"></i> Quy trình & Lịch sử
        </button>
      </div>

      <!-- TAB CONTENT -->
      <div class="tab-content">

        <!-- ═══ Tab: Dữ liệu Hồ sơ (chỉ xem) ═══ -->
        <div *ngIf="activeTab() === 'info'" class="dossier-readonly-view">
          <div *ngIf="loadingType()" style="display: flex; align-items: center; gap: 8px; color: #6b7280; padding: 12px 0;">
            <i class="pi pi-spin pi-spinner"></i> Đang tải biểu mẫu...
          </div>

          <ng-container *ngIf="!loadingType()">
            <div class="readonly-line">
              <span class="readonly-label">Loại hồ sơ:</span>
              <span class="readonly-value">{{ dossierMeta()?.dossierTypeName || '—' }}</span>
            </div>
            <div class="readonly-line">
              <span class="readonly-label">Loại lưới điện:</span>
              <span class="readonly-value">{{ viewMeta()?.gridTypeName || '—' }}</span>
            </div>
            <div class="readonly-line">
              <span class="readonly-label">Trạm / Đường dây:</span>
              <span class="readonly-value">
                {{ viewMeta()?.infrastructureName || '—' }}
                <span *ngIf="viewMeta()?.infrastructureCode" class="text-muted" style="font-size: 0.85rem;"> ({{ viewMeta()?.infrastructureCode }})</span>
              </span>
            </div>

            <div *ngFor="let field of dynamicFields(); trackBy: trackByFieldKey" class="readonly-line">
              <span class="readonly-label">{{ field.label }}:</span>
              <span class="readonly-value">{{ formatFieldDisplayValue(field, detailFormData[field.key]) }}</span>
            </div>

            <div *ngIf="dynamicFields().length === 0"
                 style="padding: 24px; text-align: center; color: #9ca3af; background: #f8fafc; border-radius: 8px; border: 1px dashed #e2e8f0; font-size: 0.85rem; margin-top: 8px;">
              Loại hồ sơ này chưa được cấu hình mẫu dữ liệu động.
            </div>

            <div class="equipment-section">
              <h3 class="equipment-title">Thiết bị liên quan</h3>
              <div *ngIf="equipments().length === 0" class="equipment-empty">
                Chưa có thiết bị nào được gắn vào hồ sơ.
              </div>
              <div *ngIf="equipments().length > 0" class="wf-table-wrap">
                <table class="wf-table">
                  <thead>
                    <tr>
                      <th class="col-stt">STT</th>
                      <th>Mã TB</th>
                      <th>Tên TB</th>
                      <th>Số serial</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr *ngFor="let eq of equipments(); let i = index">
                      <td class="col-stt text-muted">{{ i + 1 }}</td>
                      <td>{{ eq.equipmentCode || eq.EquipmentCode || eq.code || '—' }}</td>
                      <td>{{ eq.equipmentName || eq.EquipmentName || eq.name || '—' }}</td>
                      <td>{{ eq.serialNumber || eq.SerialNumber || '—' }}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </ng-container>
        </div>

        <!-- ═══ Tab: Tài liệu (chỉ xem + tải) ═══ -->
        <div *ngIf="activeTab() === 'documents'">
          <app-dossier-documents-tab
            [dossierId]="dossierId"
            [canEdit]="canEditDossier()"
            [hasFormTemplate]="!!dossierMeta()?.formId"
            [formId]="dossierMeta()?.formId ?? null"
          ></app-dossier-documents-tab>
        </div>

        <div *ngIf="activeTab() === 'versions'">
          <app-dossier-versions-tab [dossierId]="dossierId" />
        </div>

        <div *ngIf="activeTab() === 'workflow'">
          <app-dossier-workflow-tab
            [dossierId]="dossierId"
            [refreshToken]="workflowRefreshToken()"
            canvasId="bpmn-canvas-dossier-view"
          />
        </div>

      </div>

      <!-- Loading Overlay -->
      <div *ngIf="loading()" style="position: absolute; inset: 0; background: rgba(255,255,255,0.6); display: flex; align-items: center; justify-content: center; z-index: 50; border-radius: 12px;">
        <i class="pi pi-spin pi-spinner" style="font-size: 2rem; color: #002D72;"></i>
      </div>
    </div>

    <!-- Dialog Xác nhận Hành động Quy trình -->
    <p-dialog
      [visible]="showActionDialog()"
      (visibleChange)="$event ? null : showActionDialog.set(false)"
      [header]="pendingActionBtn()?.label || 'Xác nhận'"
      [modal]="true"
      [style]="{ width: '460px' }"
      styleClass="evn-dialog-custom"
      [closable]="!detailActionSubmitting()">
      <div style="display: flex; flex-direction: column; gap: 16px; padding: 4px 0 8px;">
        <!-- Chọn người xử lý tiếp theo (chỉ khi duyệt/tiếp tục) -->
        <div class="form-group" *ngIf="pendingActionBtn()?.requiresUser && !isRejectLabel(pendingActionBtn()?.label)">
          <label class="form-label">
            <span class="required">*</span> Người xử lý bước tiếp theo
          </label>
          <select class="wf-select w-full"
                  [ngModel]="selectedNextUserId()"
                  (ngModelChange)="selectedNextUserId.set($event)">
            <option value="" disabled selected>-- Chọn người xử lý --</option>
            <option *ngFor="let u of filteredNextUsers()" [value]="u.id">
              {{ u.fullName }} ({{ u.username }})
            </option>
          </select>
        </div>
        <!-- Ý kiến -->
        <div class="form-group">
          <label class="form-label">Ý kiến xử lý <span style="color: #94a3b8; font-weight: 400;">(tuỳ chọn)</span></label>
          <textarea class="wf-textarea w-full" rows="3"
                    [ngModel]="detailActionComment()"
                    (ngModelChange)="detailActionComment.set($event)"
                    [placeholder]="isRejectLabel(pendingActionBtn()?.label) ? 'Nhập lý do từ chối / trả lại...' : 'Nhập ý kiến xử lý (nếu có)...'">
          </textarea>
        </div>
      </div>
      <ng-template #footer>
        <button class="btn-cancel btn-small" (click)="showActionDialog.set(false)" [disabled]="detailActionSubmitting()">Hủy</button>
        <button class="btn-small"
                [class.btn-cancel]="isRejectLabel(pendingActionBtn()?.label)"
                [class.btn-save]="isApproveLabel(pendingActionBtn()?.label)"
                [class.btn-green]="!isRejectLabel(pendingActionBtn()?.label) && !isApproveLabel(pendingActionBtn()?.label)"
                (click)="confirmAction()" [disabled]="detailActionSubmitting()">
          <i class="pi pi-spin pi-spinner" *ngIf="detailActionSubmitting()"></i>
          <i class="pi pi-check" *ngIf="!detailActionSubmitting() && !isRejectLabel(pendingActionBtn()?.label)"></i>
          <i class="pi pi-times" *ngIf="!detailActionSubmitting() && isRejectLabel(pendingActionBtn()?.label)"></i>
          {{ pendingActionBtn()?.label }}
        </button>
      </ng-template>
    </p-dialog>

    <!-- Dialog Xác nhận Gửi duyệt -->
    <p-dialog
      [visible]="showSubmitConfirm()"
      (visibleChange)="$event ? null : showSubmitConfirm.set(false)"
      header="Xác nhận gửi duyệt"
      [modal]="true"
      [style]="{ width: '420px' }"
      styleClass="evn-dialog-custom"
      [closable]="!submitting()">
      <div style="display: flex; align-items: flex-start; gap: 12px; padding: 8px 0 16px;">
        <i class="pi pi-send" style="font-size: 1.8rem; color: #3BA962;"></i>
        <div>
          <p style="margin: 0 0 6px 0; font-weight: 600; color: #1e293b;">Bạn có chắc chắn muốn gửi duyệt?</p>
          <p style="margin: 0; color: #64748b; font-size: 0.875rem;">
            Hồ sơ sẽ được chuyển sang trạng thái <b style="color: #1d4ed8;">Chờ phê duyệt</b> và bắt đầu quy trình duyệt. Bạn không thể chỉnh sửa trong thời gian chờ duyệt.
          </p>
        </div>
      </div>
      <ng-template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9; padding-top: 12px;">
          <button class="btn-cancel btn-small" (click)="showSubmitConfirm.set(false)" [disabled]="submitting()">
            <i class="pi pi-times"></i> Hủy
          </button>
          <button class="btn-save btn-small" (click)="onConfirmSubmit()" [disabled]="submitting()"
                  style="background-color: #3BA962; border-color: #3BA962;">
            <i class="pi pi-spin pi-spinner" *ngIf="submitting()"></i>
            <i class="pi pi-send" *ngIf="!submitting()"></i>
            Gửi duyệt
          </button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .dossier-readonly-view {
      display: flex;
      flex-direction: column;
      padding: 4px 0 8px;
    }
    .readonly-line {
      display: flex;
      gap: 10px;
      padding: 11px 0;
      border-bottom: 1px dotted #d1d5db;
      font-size: 0.9rem;
      line-height: 1.55;
      align-items: flex-start;
    }
    .readonly-label {
      font-weight: 600;
      color: #374151;
      min-width: 200px;
      flex-shrink: 0;
    }
    .readonly-value {
      color: #1e293b;
      flex: 1;
      white-space: pre-wrap;
      word-break: break-word;
    }
    .equipment-section {
      margin-top: 28px;
      padding-top: 18px;
      border-top: 2px solid #e5e7eb;
    }
    .equipment-title {
      font-size: 0.95rem;
      font-weight: 700;
      color: #002D72;
      margin: 0 0 12px 0;
    }
    .equipment-empty {
      padding: 20px;
      background: #f8fafc;
      border: 1px dashed #e2e8f0;
      border-radius: 8px;
      text-align: center;
      color: #9ca3af;
      font-size: 0.85rem;
    }
  `]
})
export class DossierDetailComponent implements OnInit, OnDestroy {
  @Input() dossierId!: string;
  @Output() cancel = new EventEmitter<void>();
  @Output() edit = new EventEmitter<void>();

  private service = inject(DossierManagementService);
  private authService = inject(AuthService);
  private messageService = inject(MessageService);
  private destroy$ = new Subject<void>();

  loading = signal<boolean>(true);
  submitting = signal<boolean>(false);
  activeTab = signal<'info' | 'documents' | 'versions' | 'workflow'>('info');
  workflowRefreshToken = signal(0);

  dossier = signal<any>(null);
  dossierMeta = computed(() => normalizeDossierDetail(this.dossier()));

  viewMeta = computed(() => {
    const d = this.dossier();
    if (!d) return null;
    return {
      gridTypeName: pickFirst(d.gridTypeName, d.GridTypeName) as string | undefined,
      infrastructureName: pickFirst(d.infrastructureName, d.InfrastructureName) as string | undefined,
      infrastructureCode: pickFirst(d.infrastructureCode, d.InfrastructureCode) as string | undefined,
    };
  });

  equipments = computed(() => {
    const d = this.dossier();
    const list = d?.equipments ?? d?.Equipments ?? [];
    return Array.isArray(list) ? list : [];
  });

  formatFieldDisplayValue = formatFieldDisplayValue;

  // EAV Form
  loadingType = signal<boolean>(false);
  formTemplate = signal<any>(null);
  dynamicFields = signal<EavField[]>([]);
  detailFormData: Record<string, any> = {};
  private pendingFormData: Record<string, unknown> = {};

  // Submit for approval confirmation
  showSubmitConfirm = signal<boolean>(false);

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

  filteredNextUsers = computed(() => {
    const forwardBtn = this.detailDynamicButtons().find(btn =>
      btn.requiresUser && !this.isRejectLabel(btn.label)
    );
    if (!forwardBtn?.requiredRole) return this.users();
    const roles = forwardBtn.requiredRole.split(',').map((r: string) => r.trim().toUpperCase());
    return this.users().filter((u: any) => {
      const uRoles: string[] = (u.roles || u.Roles || []).map((r: string) => r.toUpperCase());
      return uRoles.some(r => roles.includes(r));
    });
  });

  // Dialog xác nhận hành động
  showActionDialog = signal<boolean>(false);
  pendingActionBtn = signal<any>(null);

  get isDraftOrReturned(): boolean {
    const status = this.dossier()?.status;
    return status === 'Draft' || status === 'Returned';
  }

  ngOnInit() {
    this.loadDetail();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadDetail() {
    this.loading.set(true);
    this.loadingType.set(true);

    this.service.getDossierById(this.dossierId).pipe(
      switchMap((res) => {
        const meta = normalizeDossierDetail(res);
        if (!meta) {
          throw new Error('Invalid dossier response');
        }

        this.dossier.set(res);
        this.pendingFormData = parseFormDataJson(meta.formDataJson);
        this.detailFormData = { ...this.pendingFormData };

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
        this.loadWorkflow();
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải chi tiết hồ sơ' });
        this.dynamicFields.set([]);
      }
    });
  }

  /** Ưu tiên formId từ detail API; fallback lookup loại hồ sơ. */
  private resolveFormTemplate(formId: string | null, dossierTypeId: string) {
    if (formId) {
      return this.service.getFormTemplate(formId);
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
        return this.service.getFormTemplate(resolvedFormId);
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

    const bpmnXml = res.definition?.bpmnXml ?? res.definition?.BpmnXml;
    if (bpmnXml) {
      this.detailWorkflowXml.set(bpmnXml);
      if (pending) {
        const stepName = pickFirst(pending.stepName, pending.StepName) ?? '';
        const nodeId = pickFirst(instance.currentNodeId, instance.CurrentNodeId);
        this.parseDynamicButtons(bpmnXml, stepName, nodeId);
      }
    } else {
      this.detailWorkflowXml.set('');
    }
  }

  loadWorkflow() {
    this.loadingBpmn.set(true);
    this.service.getWorkflowDetail(this.dossierId).pipe(
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
    try {
      const parser = new DOMParser();
      const doc = parser.parseFromString(xml, 'application/xml');

      const byLocalName = (name: string): Element[] => {
        const res: Element[] = [];
        const all = doc.getElementsByTagName('*');
        for (let i = 0; i < all.length; i++) {
          if (all[i].localName === name) res.push(all[i]);
        }
        return res;
      };

      const allElements = doc.getElementsByTagName('*');

      const getElementById = (id: string): Element | null => {
        for (let i = 0; i < allElements.length; i++) {
          if (allElements[i].getAttribute('id') === id) return allElements[i];
        }
        return null;
      };

      const isTask = (id: string) => {
        const el = getElementById(id);
        return el ? el.localName === 'task' || el.localName === 'userTask' : false;
      };

      const getRole = (id: string) => getElementById(id)?.getAttribute('requiredRole') || '';

      const tasks = [...byLocalName('task'), ...byLocalName('userTask')];
      const seqFlows = byLocalName('sequenceFlow');

      let currentEl = currentNodeId ? tasks.find(t => t.getAttribute('id') === currentNodeId) : null;
      if (!currentEl) currentEl = tasks.find(t => t.getAttribute('name') === stepName) ?? null;

      if (!currentEl) {
        this.detailDynamicButtons.set([{ label: 'Xác nhận', targetNodeId: '', requiresUser: false }]);
        return;
      }

      const currentId = currentEl.getAttribute('id');
      const outFlow = seqFlows.find(f => f.getAttribute('sourceRef') === currentId);
      if (!outFlow) {
        this.detailDynamicButtons.set([{ label: 'Xác nhận', targetNodeId: '', requiresUser: false }]);
        return;
      }

      const targetRef = outFlow.getAttribute('targetRef') || '';
      const targetEl = getElementById(targetRef);
      if (!targetEl) {
        this.detailDynamicButtons.set([{ label: 'Xác nhận', targetNodeId: '', requiresUser: false }]);
        return;
      }

      if (targetEl.localName.includes('Gateway')) {
        const gwFlows = seqFlows.filter(f => f.getAttribute('sourceRef') === targetRef);
        this.detailDynamicButtons.set(gwFlows.map(flow => {
          const ftRef = flow.getAttribute('targetRef') || '';
          const label = flow.getAttribute('name') || 'Tiếp tục';
          const isReject = this.isRejectLabel(label);
          return { label, targetNodeId: ftRef, requiresUser: !isReject && isTask(ftRef), requiredRole: isReject ? '' : getRole(ftRef) };
        }));
      } else if (targetEl.localName === 'endEvent') {
        this.detailDynamicButtons.set([{ label: 'Hoàn thành', targetNodeId: targetRef, requiresUser: false, requiredRole: '' }]);
      } else {
        this.detailDynamicButtons.set([{ label: 'Chuyển tiếp', targetNodeId: targetRef, requiresUser: isTask(targetRef), requiredRole: getRole(targetRef) }]);
      }
    } catch {
      this.detailDynamicButtons.set([
        { label: 'Đồng ý', targetNodeId: '', requiresUser: true, requiredRole: '' },
        { label: 'Từ chối', targetNodeId: '', requiresUser: false, requiredRole: '' }
      ]);
    }
  }

  get isUserAuthorizedForDetailAction(): boolean {
    const task = this.detailPendingTask();
    if (!task) return false;
    const roles = this.authService.getUserRoles?.() ?? [];
    if (roles.includes('ADMIN') || roles.includes('OPERATOR')) return true;

    const assigneeId = task.assigneeUserId ?? task.AssigneeUserId;
    const token = this.authService.getToken();
    let currentUserId: string | null = null;
    if (token) {
      try {
        const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
        currentUserId = payload.sub
          ?? payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']
          ?? null;
      } catch { /* ignore */ }
    }

    if (assigneeId && currentUserId && String(assigneeId) === String(currentUserId)) return true;
    if (assigneeId) return false;

    const taskRoles = task.assignedRole ? task.assignedRole.split(',').map((r: string) => r.trim()) : [];
    return taskRoles.some((r: string) => roles.includes(r));
  }

  isRejectLabel(label: string): boolean {
    const l = (label || '').toLowerCase();
    return l.includes('từ chối') || l.includes('hủy') || l.includes('reject') || l.includes('cancel') || l.includes('trả lại');
  }

  isApproveLabel(label: string): boolean {
    const l = (label || '').toLowerCase();
    return l.includes('đồng ý') || l.includes('phê duyệt') || l.includes('xác nhận') || l.includes('approve');
  }

  openActionDialog(btn: any) {
    this.pendingActionBtn.set(btn);
    this.detailActionComment.set('');
    this.selectedNextUserId.set('');
    this.showActionDialog.set(true);
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
    this.service.moveWorkflow(this.dossierId, {
      nextNodeId: targetNodeId,
      actionLabel,
      comment: this.detailActionComment(),
      nextAssigneeUserId: (!isCancel && requiresUser) ? this.selectedNextUserId() : undefined
    }).subscribe({
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
    const status = this.dossier()?.status ?? this.dossier()?.Status;
    if (status === 'Draft' || status === 'Returned') return true;

    const instance = this.workflowDetail()?.instance;
    if (!instance) return false;

    return !!(instance.currentStepAllowEdit ?? instance.CurrentStepAllowEdit);
  }

  onEdit() {
    this.edit.emit();
  }

  onCancel() {
    this.cancel.emit();
  }

  onConfirmSubmit() {
    this.submitting.set(true);
    this.service.submitForApproval(this.dossierId).subscribe({
      next: (res) => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã gửi duyệt thành công' });
        this.showSubmitConfirm.set(false);
        const payload = res?.data;
        if (payload) {
          this.dossier.update((current) =>
            current
              ? {
                  ...current,
                  status: payload.dossierStatus ?? current.status,
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
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể gửi duyệt' });
        this.showSubmitConfirm.set(false);
        this.submitting.set(false);
      }
    });
  }

  trackByFieldKey(_index: number, field: EavField): string {
    return field.key;
  }

  getStatusText(status?: string): string {
    switch (status) {
      case 'Draft': return 'Nháp';
      case 'PendingApproval': return 'Đang chờ duyệt';
      case 'InProgress': return 'Đang xử lý';
      case 'Returned': return 'Bị trả lại';
      case 'Approved': return 'Đã phê duyệt';
      default: return status || '';
    }
  }

  getStatusStyle(status?: string): { [key: string]: string } {
    switch (status) {
      case 'Draft': return { background: '#f1f5f9', color: '#475569', border: '1px solid #e2e8f0' };
      case 'PendingApproval': return { background: '#eff6ff', color: '#1d4ed8', border: '1px solid #bfdbfe' };
      case 'InProgress': return { background: '#f5f3ff', color: '#6d28d9', border: '1px solid #ddd6fe' };
      case 'Returned': return { background: '#fef2f2', color: '#dc2626', border: '1px solid #fecaca' };
      case 'Approved': return { background: '#dcfce7', color: '#15803d', border: '1px solid #bbf7d0' };
      default: return { background: '#f1f5f9', color: '#475569', border: '1px solid #e2e8f0' };
    }
  }
}
