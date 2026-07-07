import { Component, OnInit, OnDestroy, OnChanges, SimpleChanges, signal, computed, inject, Output, EventEmitter, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { catchError, finalize, of, switchMap, takeUntil, Subject } from 'rxjs';
import { DossierManagementService } from '../../data-access/dossier-management.service';
import { DossierPublishService } from '../../data-access/dossier-publish.service';
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
import {
  isApproveWorkflowLabel,
  isRejectWorkflowLabel,
  parseWorkflowActionButtons,
} from '../../utils/dossier-workflow-bpmn.util';
import { DossierMenuScope, getDossierStatusLabel, getDossierStatusPillClass } from '../../utils/dossier-status.util';
import {
  canMutateDossierOnCreatorMenu,
  hasDossierCreatePermission,
  hasDossierEditPermission,
  normalizeDossierKindId,
} from '../../utils/dossier-permission.util';
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
                <span [class]="getStatusClass(dossierMeta()?.statusId)">
                  {{ getStatusText(dossierMeta()?.statusId, dossierMeta()?.statusName) }}
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
          <!-- Hoàn thành nhập liệu — chỉ khi trạng thái Tạo mới -->
          <button *ngIf="showCompleteInputButton()"
                  (click)="onCompleteInput()" class="btn-green btn-small" [disabled]="submitting()">
            <i class="pi pi-check" *ngIf="!submitting()"></i>
            <i class="pi pi-spin pi-spinner" *ngIf="submitting()"></i>
            Hoàn thành nhập liệu
          </button>
          <!-- Gửi duyệt — chỉ khi đã Hoàn thành nhập liệu -->
          <button *ngIf="showSubmitForApprovalButton()"
                  (click)="openSubmitWorkflowDialog()" class="btn-save btn-small" [disabled]="submitting()">
            <i class="pi pi-send" *ngIf="!submitting()"></i>
            <i class="pi pi-spin pi-spinner" *ngIf="submitting()"></i>
            Gửi duyệt
          </button>
          <!-- Workflow action buttons: Duyệt / Tiếp tục / Từ chối -->
          <ng-container *ngIf="detailDynamicButtons().length > 0 && isUserAuthorizedForDetailAction">
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
        <button type="button" *ngIf="isDetailTabVisible('info')" class="tab-item" [class.tab-active]="activeTab() === 'info'" (click)="activeTab.set('info')">
          <i class="pi pi-info-circle" style="margin-right: 6px;"></i>
          Dữ liệu Hồ sơ
        </button>
        <button type="button" *ngIf="isDetailTabVisible('documents')" class="tab-item" [class.tab-active]="activeTab() === 'documents'" (click)="activeTab.set('documents')">
          <i class="pi pi-file" style="margin-right: 6px;"></i> Tài liệu đính kèm
        </button>
        <button type="button" *ngIf="isDetailTabVisible('versions')" class="tab-item" [class.tab-active]="activeTab() === 'versions'" (click)="activeTab.set('versions')">
          <i class="pi pi-history" style="margin-right: 6px;"></i> Lịch sử phiên bản
        </button>
        <button type="button" *ngIf="isDetailTabVisible('workflow')" class="tab-item" [class.tab-active]="activeTab() === 'workflow'" (click)="activeTab.set('workflow')">
          <i class="pi pi-sitemap" style="margin-right: 6px;"></i> Quy trình & Lịch sử
        </button>
      </div>

      <!-- TAB CONTENT -->
      <div class="tab-content">

        <!-- ═══ Tab: Dữ liệu Hồ sơ (chỉ xem) ═══ -->
        <div *ngIf="activeTab() === 'info'" class="dossier-readonly-view">
          <div *ngIf="loading() && !dossier()" style="display: flex; align-items: center; gap: 8px; color: #6b7280; padding: 12px 0;">
            <i class="pi pi-spin pi-spinner"></i> Đang tải dữ liệu hồ sơ...
          </div>

          <div *ngIf="loadingType() && dossier()" style="display: flex; align-items: center; gap: 8px; color: #6b7280; padding: 0 0 12px 0; font-size: 0.83rem;">
            <i class="pi pi-spin pi-spinner"></i> Đang tải biểu mẫu...
          </div>

          <ng-container *ngIf="dossier()">
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
            <div class="readonly-line">
              <span class="readonly-label">Loại hồ sơ:</span>
              <span class="readonly-value">{{ dossierMeta()?.dossierTypeName || '—' }}</span>
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

        <!-- ═══ Tab: Tài liệu (chỉ xem) ═══ -->
        <div *ngIf="activeTab() === 'documents'">
          <app-dossier-documents-tab
            [dossierId]="dossierId"
            [canEdit]="canEditDossier()"
            [kindId]="kindIdSignal()"
            [menuScope]="menuScope"
            [hasFormTemplate]="!!dossierMeta()?.formId"
            [formId]="dossierMeta()?.formId ?? null"
            (formDataSaved)="loadDetail()"
          ></app-dossier-documents-tab>
        </div>

        <div *ngIf="activeTab() === 'versions'">
          <app-dossier-versions-tab [dossierId]="dossierId" />
        </div>

        <div *ngIf="activeTab() === 'workflow'">
          <app-dossier-workflow-tab
            [dossierId]="dossierId"
            [kindId]="kindIdSignal()"
            [refreshToken]="workflowRefreshToken()"
            canvasId="bpmn-canvas-dossier-view"
          />
        </div>

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

    <!-- Dialog Xác nhận Gửi Duyệt -->
    <p-dialog
      [visible]="showSubmitConfirm()"
      (visibleChange)="$event ? null : showSubmitConfirm.set(false)"
      header="Gửi duyệt hồ sơ"
      [modal]="true"
      [style]="{ width: '450px' }"
      styleClass="evn-dialog-custom"
      [closable]="!submitting()">
      <div style="display: flex; flex-direction: column; gap: 16px; padding: 8px 0 16px;">
        <div style="display: flex; align-items: flex-start; gap: 12px;">
          <i class="pi pi-send" style="font-size: 1.8rem; color: #1d4ed8;"></i>
          <div>
            <p style="margin: 0 0 6px 0; font-weight: 600; color: #1e293b;">Xác nhận gửi duyệt hồ sơ lên cấp trên</p>
            <p style="margin: 0; color: #64748b; font-size: 0.875rem;">
              Hồ sơ sẽ đi vào quy trình phê duyệt bước: <b style="color: #1e293b;">{{ nextStepInfo()?.stepName || 'Phê duyệt' }}</b>.
            </p>
          </div>
        </div>

        <!-- Chọn người xử lý tiếp theo nếu được yêu cầu -->
        <div *ngIf="nextStepInfo()?.requiresNextAssignee" class="form-group">
          <label class="form-label required">Người duyệt tiếp theo ({{ nextStepInfo()?.stepName }})</label>
          <select class="wf-select" [value]="selectedNextUser()" (change)="onNextUserChange($event)">
            <option value="">-- Chọn người phê duyệt --</option>
            <option *ngFor="let u of filteredSubmitNextUsers()" [value]="u.id || u.Id || u.userId || u.username">
              {{ u.fullName || u.FullName || u.name || u.username }}
            </option>
          </select>
        </div>
      </div>
      <ng-template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9; padding-top: 12px;">
          <button class="btn-cancel btn-small" (click)="showSubmitConfirm.set(false)" [disabled]="submitting()">
            <i class="pi pi-times"></i> Hủy
          </button>
          <button class="btn-save btn-small" (click)="onConfirmSubmitAndMove()" [disabled]="submitting()">
            <i class="pi pi-spin pi-spinner" *ngIf="submitting()"></i>
            <i class="pi pi-check" *ngIf="!submitting()"></i>
            Xác nhận gửi
          </button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .dossier-readonly-view {
      display: flex;
      flex-direction: column;
      padding: 0;
      gap: 2px;
    }
    .readonly-line {
      display: flex;
      gap: 8px;
      padding: 4px 0;
      font-size: 0.875rem;
      line-height: 1.45;
      align-items: flex-start;
    }
    .readonly-label {
      font-weight: 600;
      color: #374151;
      min-width: 180px;
      flex-shrink: 0;
    }
    .readonly-value {
      color: #1e293b;
      flex: 1;
      white-space: pre-wrap;
      word-break: break-word;
    }
    .equipment-section {
      margin-top: 12px;
      padding-top: 0;
    }
    .equipment-title {
      font-size: 0.9rem;
      font-weight: 700;
      color: #002D72;
      margin: 0 0 8px 0;
    }
    .equipment-empty {
      padding: 12px;
      background: #f8fafc;
      border: 1px dashed #e2e8f0;
      border-radius: 8px;
      text-align: center;
      color: #9ca3af;
      font-size: 0.85rem;
    }
  `]
})
export class DossierDetailComponent implements OnInit, OnDestroy, OnChanges {
  @Input() dossierId!: string;
  @Input() menuScope: DossierMenuScope = 'creator';
  @Input() kindId = 2;
  @Output() cancel = new EventEmitter<void>();
  @Output() edit = new EventEmitter<void>();

  private service = inject(DossierManagementService);
  private publishService = inject(DossierPublishService);
  private authService = inject(AuthService);
  private messageService = inject(MessageService);
  private destroy$ = new Subject<void>();

  loading = signal<boolean>(true);
  submitting = signal<boolean>(false);
  activeTab = signal<'info' | 'documents' | 'versions' | 'workflow'>('info');
  workflowRefreshToken = signal(0);

  dossier = signal<any>(null);
  kindIdSignal = signal<number>(2);
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
  nextStepInfo = signal<any>(null);
  selectedNextUser = signal<string>('');

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

  filteredSubmitNextUsers = computed(() => {
    const info = this.nextStepInfo();
    if (!info || !info.requiredRole) return [];
    const roles = info.requiredRole.split(',').map((r: string) => r.trim().toUpperCase());
    return this.users().filter((u: any) => {
      const uRoles: string[] = (u.roles || u.Roles || []).map((r: string) => r.toUpperCase());
      return uRoles.some(r => roles.includes(r));
    });
  });

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
    this.applyRouteKindContext();
    this.loadDetail();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['kindId']) {
      this.applyRouteKindContext();
    }
  }

  private applyRouteKindContext(): void {
    const id = normalizeDossierKindId(this.kindId, this.kindIdSignal());
    this.kindIdSignal.set(id);
    if (this.menuScope !== 'publisher') {
      this.service.setKindContext(id);
    }
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

        const normalizedKindId = normalizeDossierKindId(
          (res as Record<string, unknown>)?.['kindId'] ?? (res as Record<string, unknown>)?.['KindId'],
          this.kindIdSignal()
        );
        this.kindIdSignal.set(normalizedKindId);
        if (this.menuScope !== 'publisher') {
          this.service.setKindContext(normalizedKindId);
        }

        this.dossier.set(res);
        this.pendingFormData = parseFormDataJson(meta.formDataJson);
        this.detailFormData = { ...this.pendingFormData };
        this.loading.set(false);

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
    this.service.getWorkflowDetail(this.dossierId, this.kindIdSignal()).pipe(
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

        this.service.getMyTasks(String(instanceId), this.kindIdSignal()).subscribe({
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
    const payload = {
      nextNodeId: targetNodeId,
      actionLabel,
      comment: this.detailActionComment(),
      nextAssigneeUserId: (!isCancel && requiresUser) ? this.selectedNextUserId() : undefined
    };
    const statusId = this.dossier()?.statusId ?? this.dossier()?.StatusId;
    const useResubmit = this.menuScope === 'creator' && statusId === 5;
    const workflowCall = useResubmit
      ? this.service.resubmitWorkflow(this.dossierId, payload, this.kindIdSignal())
      : this.service.moveWorkflow(this.dossierId, payload, this.kindIdSignal());

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
    const isDigitization = this.kindIdSignal() === 1;

    if (roles.includes('ADMIN')) return true;

    const statusId = d.statusId ?? d.StatusId;
    if (statusId === 1 || statusId === 2) {
      if (this.menuScope !== 'creator') return false;
      if (!canMutateDossierOnCreatorMenu(this.authService, isDigitization)) {
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
      if (!hasDossierEditPermission(this.authService, isDigitization)) return false;
      return this.isCurrentUserCreator();
    }

    // Các trạng thái WF khác: bước hiện tại phải AllowEdit và user là assignee cụ thể
    if (!hasDossierEditPermission(this.authService, isDigitization)) {
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
    this.submitting.set(true);
    this.service.completeInput(this.dossierId).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã hoàn thành nhập liệu thành công' });
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
    this.service.getNextStepInfo(this.kindIdSignal()).subscribe({
      next: (res) => {
        this.nextStepInfo.set(res);
        this.selectedNextUser.set('');
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
    }, this.kindIdSignal()).subscribe({
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
        return !!wfId || this.menuScope === 'approver';
      default:
        return false;
    }
  }
}
