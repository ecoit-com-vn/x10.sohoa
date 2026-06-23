import { Component, OnInit, OnDestroy, signal, computed, inject, Output, EventEmitter, Input, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { catchError, finalize, of, switchMap, takeUntil, Subject } from 'rxjs';
import { DossierManagementService } from '../../data-access/dossier-management.service';
import { DossierDocumentsTabComponent } from '../dossier-documents/dossier-documents-tab.component';
import { AuthService } from '@sohoa.frontend/shared/core';
import {
  EavField,
  guidsEqual,
  normalizeDossierDetail,
  normalizeField,
  parseFormDataJson,
  pickFormDataForSchema,
  readFormSchemaJson,
  serializeFormDataForSchema,
} from '../../utils/dossier-form-schema.util';

/** Parse allowEdit từ API — tránh !!\"false\" === true */
function readAllowEdit(value: unknown): boolean | null {
  if (value === true || value === 1) return true;
  if (value === false || value === 0 || value === null || value === undefined) {
    if (value === false || value === 0) return false;
    return null;
  }
  if (typeof value === 'string') {
    const v = value.trim().toLowerCase();
    if (v === 'true' || v === '1') return true;
    if (v === 'false' || v === '0' || v === '') return false;
  }
  return null;
}

function pickFirst<T>(...values: T[]): T | undefined {
  for (const v of values) {
    if (v !== undefined && v !== null && v !== '') return v;
  }
  return undefined;
}

function isDossierInWorkflow(dossier: any, workflowDetail: any): boolean {
  if (!dossier) return false;
  if (pickFirst(dossier.workflowInstanceId, dossier.WorkflowInstanceId)) return true;
  if (workflowDetail?.instance) return true;
  const status = pickFirst(dossier.status, dossier.Status) as string | undefined;
  return status === 'PendingApproval' || status === 'Approved';
}

function findCurrentWorkflowStep(definition: any, instance: any, pending: any): any | null {
  const steps: any[] = definition?.steps ?? definition?.Steps ?? [];
  if (!steps.length) return null;

  const pendingName = pickFirst(pending?.stepName, pending?.StepName);
  const currentName = pickFirst(instance?.currentStepName, instance?.CurrentStepName);
  const order = pickFirst(instance?.currentStepOrder, instance?.CurrentStepOrder);

  if (pendingName) {
    const matched = steps.find(s => pickFirst(s.stepName, s.StepName) === pendingName);
    if (matched) return matched;
  }
  if (currentName) {
    const matched = steps.find(s => pickFirst(s.stepName, s.StepName) === currentName);
    if (matched) return matched;
  }
  if (order != null) {
    const matched = steps.find(s => pickFirst(s.order, s.Order) === order);
    if (matched) return matched;
  }
  return null;
}

function resolveCurrentStepAllowEdit(workflowDetail: any): boolean {
  const instance = workflowDetail?.instance;
  if (!instance) return false;

  const pendingList = instance.pendingTasks ?? instance.PendingTasks ?? [];
  const pending = pendingList.length > 0 ? pendingList[0] : null;
  const definition = workflowDetail?.definition;

  const fromInstance = readAllowEdit(
    pickFirst(instance.currentStepAllowEdit, instance.CurrentStepAllowEdit)
  );
  if (fromInstance !== null) return fromInstance;

  const fromPending = readAllowEdit(pickFirst(pending?.allowEdit, pending?.AllowEdit));
  if (fromPending !== null) return fromPending;

  const step = findCurrentWorkflowStep(definition, instance, pending);
  const fromStep = readAllowEdit(pickFirst(step?.allowEdit, step?.AllowEdit));
  if (fromStep !== null) return fromStep;

  return false;
}

@Component({
  selector: 'app-dossier-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, DialogModule, DossierDocumentsTabComponent],
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
          <!-- Lưu dữ liệu: nháp/trả lại hoặc bước WF có allowEdit -->
          <button *ngIf="activeTab() === 'info' && canEditFormData() && dynamicFields().length > 0"
                  (click)="saveFormData()" class="btn-save btn-small" [disabled]="savingForm()">
            <i class="pi pi-save" *ngIf="!savingForm()"></i>
            <i class="pi pi-spin pi-spinner" *ngIf="savingForm()"></i>
            Lưu dữ liệu
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
        <button class="tab-item"
                [class.tab-active]="activeTab() === 'info'"
                [class.tab-readonly]="isFormReadOnly()"
                (click)="activeTab.set('info')">
          <i class="pi" [class.pi-lock]="isFormReadOnly()" [class.pi-info-circle]="!isFormReadOnly()" style="margin-right: 6px;"></i>
          Dữ liệu Hồ sơ
        </button>
        <button class="tab-item" [class.tab-active]="activeTab() === 'documents'" (click)="activeTab.set('documents')">
          <i class="pi pi-file" style="margin-right: 6px;"></i> Tài liệu đính kèm
        </button>
        <button class="tab-item" [class.tab-active]="activeTab() === 'versions'" (click)="onOpenVersionsTab()">
          <i class="pi pi-history" style="margin-right: 6px;"></i> Lịch sử phiên bản
        </button>
        <button class="tab-item" [class.tab-active]="activeTab() === 'workflow'" (click)="onOpenWorkflowTab()">
          <i class="pi pi-sitemap" style="margin-right: 6px;"></i> Quy trình & Lịch sử
        </button>
      </div>

      <!-- TAB CONTENT -->
      <div class="tab-content">

        <!-- ═══ Tab: Dữ liệu Hồ sơ ═══ -->
        <div *ngIf="activeTab() === 'info'"
             class="dossier-info-tab"
             [class.dossier-info-tab--readonly]="isFormReadOnly()">
          <!-- Thông báo chỉ xem -->
          <div *ngIf="isFormReadOnly() && !loadingType() && dynamicFields().length > 0" class="readonly-notice">
            <i class="pi pi-lock"></i>
            <span>Chỉ xem — bước quy trình hiện tại không cho phép chỉnh sửa dữ liệu hồ sơ.</span>
          </div>

          <!-- Loading form -->
          <div *ngIf="loadingType()" style="display: flex; align-items: center; gap: 8px; color: #6b7280; padding: 12px 0;">
            <i class="pi pi-spin pi-spinner"></i> Đang tải biểu mẫu...
          </div>

          <!-- Dynamic form fields — event-based, không dùng ngModel để tránh bleeding -->
          <div *ngIf="!loadingType() && dynamicFields().length > 0"
               class="dossier-form-grid"
               [class.dossier-form-grid--readonly]="isFormReadOnly()"
               style="display: grid; grid-template-columns: 1fr 1fr; gap: 16px;">
            <ng-container *ngFor="let field of dynamicFields(); trackBy: trackByFieldKey">
              <div class="form-group" [style.grid-column]="field.type === 'textarea' ? '1 / -1' : 'auto'">
                <label class="form-label">
                  {{ field.label }}
                  <span class="required" *ngIf="field.required">*</span>
                </label>
                <ng-container [ngSwitch]="field.type">
                  <input *ngSwitchCase="'text'" type="text" class="wf-input w-full"
                         [placeholder]="field.placeholder || ''"
                         [value]="detailFormData[field.key] ?? ''"
                         [attr.readonly]="isFormReadOnly() ? true : null"
                         [disabled]="isFormReadOnly()"
                         (input)="setDetailField(field.key, $event)">

                  <div *ngSwitchCase="'number'" style="display: flex; gap: 6px; align-items: center;">
                    <input type="number" class="wf-input" style="flex: 1;"
                           [placeholder]="field.placeholder || ''"
                           [value]="detailFormData[field.key] ?? ''"
                           [attr.readonly]="isFormReadOnly() ? true : null"
                           [disabled]="isFormReadOnly()"
                           (input)="setDetailFieldNumber(field.key, $event)">
                    <span *ngIf="field.unit" style="font-size: 0.85rem; color: #6b7280; white-space: nowrap;">{{ field.unit }}</span>
                  </div>

                  <input *ngSwitchCase="'date'" type="date" class="wf-input w-full"
                         [value]="detailFormData[field.key] ?? ''"
                         [attr.readonly]="isFormReadOnly() ? true : null"
                         [disabled]="isFormReadOnly()"
                         (input)="setDetailField(field.key, $event)">

                  <textarea *ngSwitchCase="'textarea'" class="wf-textarea w-full" rows="3"
                            [placeholder]="field.placeholder || ''"
                            [value]="detailFormData[field.key] ?? ''"
                            [attr.readonly]="isFormReadOnly() ? true : null"
                            [disabled]="isFormReadOnly()"
                            (input)="setDetailField(field.key, $event)"></textarea>

                  <select *ngSwitchCase="'select'" class="wf-select w-full"
                          [disabled]="isFormReadOnly()"
                          (change)="setDetailField(field.key, $event)">
                    <option value="">-- Chọn --</option>
                    <option *ngFor="let opt of field.options" [value]="opt.value"
                            [selected]="detailFormData[field.key] === opt.value">{{ opt.label }}</option>
                  </select>

                  <label *ngSwitchCase="'checkbox'"
                         [style.cursor]="isFormReadOnly() ? 'not-allowed' : 'pointer'"
                         style="display: flex; align-items: center; gap: 8px; margin-top: 4px;">
                    <input type="checkbox"
                           [checked]="detailFormData[field.key]"
                           [disabled]="isFormReadOnly()"
                           (change)="setDetailCheckbox(field.key, $event)"
                           [style.cursor]="isFormReadOnly() ? 'not-allowed' : 'pointer'"
                           style="width: 16px; height: 16px; accent-color: #002D72;">
                    <span style="font-size: 0.9rem;">{{ field.placeholder || field.label }}</span>
                  </label>

                  <input *ngSwitchDefault type="text" class="wf-input w-full"
                         [value]="detailFormData[field.key] ?? ''"
                         [attr.readonly]="isFormReadOnly() ? true : null"
                         [disabled]="isFormReadOnly()"
                         (input)="setDetailField(field.key, $event)">
                </ng-container>
              </div>
            </ng-container>
          </div>

          <!-- Chưa có biểu mẫu -->
          <div *ngIf="!loadingType() && dynamicFields().length === 0"
               style="padding: 32px; text-align: center; color: #9ca3af; background: #f8fafc; border-radius: 8px; border: 1px dashed #e2e8f0; font-size: 0.85rem;">
            <i class="pi pi-file-edit" style="font-size: 2rem; display: block; margin-bottom: 8px;"></i>
            Loại hồ sơ này chưa được cấu hình mẫu dữ liệu động.
          </div>
        </div>

        <!-- ═══ Tab: Tài liệu ═══ -->
        <div *ngIf="activeTab() === 'documents'">
          <app-dossier-documents-tab
            [dossierId]="dossierId"
            [canEdit]="canEditFormData()"
            [hasFormTemplate]="!!dossierMeta()?.formId"
            [formId]="dossierMeta()?.formId ?? null"
          ></app-dossier-documents-tab>
        </div>

        <!-- ═══ Tab: Lịch sử phiên bản ═══ -->
        <div *ngIf="activeTab() === 'versions'" style="display: flex; flex-direction: column; gap: 12px;">
          <div *ngIf="loadingVersions()" style="display: flex; align-items: center; gap: 8px; color: #6b7280; padding: 12px 0;">
            <i class="pi pi-spin pi-spinner"></i> Đang tải lịch sử...
          </div>

          <div *ngIf="!loadingVersions() && versions().length === 0"
               style="padding: 32px; text-align: center; color: #9ca3af; background: #f8fafc; border-radius: 8px; border: 1px dashed #e2e8f0; font-size: 0.85rem;">
            Chưa có phiên bản nào được lưu.
          </div>

          <div *ngFor="let v of versions()" style="border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;">
            <div style="background: #f8fafc; padding: 10px 16px; border-bottom: 1px solid #e2e8f0; display: flex; justify-content: space-between; align-items: center;">
              <span style="font-weight: 600; color: #374151;">Phiên bản #{{ v.versionNumber }}</span>
              <span style="font-size: 0.78rem; color: #6b7280;">{{ v.createdDate | date:'dd/MM/yyyy HH:mm' }} — {{ v.createdBy }}</span>
            </div>
            <div style="padding: 12px 16px;">
              <div *ngIf="v.changeNote" style="font-size: 0.85rem; color: #374151; margin-bottom: 8px;">
                <i class="pi pi-comment" style="margin-right: 4px; color: #6b7280;"></i>{{ v.changeNote }}
              </div>
              <div *ngIf="v.documentsSnapshotJson" style="font-size: 0.82rem; color: #475569; margin-bottom: 8px;">
                <i class="pi pi-paperclip" style="margin-right: 4px; color: #6b7280;"></i>
                <span>Tài liệu tại thời điểm này: {{ parseDocumentSnapshotCount(v.documentsSnapshotJson) }}</span>
              </div>
              <details style="cursor: pointer;">
                <summary style="font-size: 0.8rem; color: #6b7280;">Xem dữ liệu JSON</summary>
                <pre style="font-size: 0.76rem; background: #f1f5f9; padding: 10px; border-radius: 6px; margin-top: 8px; overflow-x: auto; white-space: pre-wrap; word-break: break-all;">{{ v.formDataJson | json }}</pre>
              </details>
            </div>
          </div>
        </div>

        <!-- ═══ Tab: Quy trình & Lịch sử ═══ -->
        <div *ngIf="activeTab() === 'workflow'" style="display: flex; flex-direction: column; gap: 20px;">

          <!-- Loading BPMN -->
          <div *ngIf="loadingBpmn()" style="text-align: center; padding: 40px 0; color: #6b7280;">
            <i class="pi pi-spin pi-spinner" style="font-size: 2rem; display: block; margin-bottom: 12px; color: #002D72;"></i>
            Đang tải thông tin quy trình...
          </div>

          <ng-container *ngIf="!loadingBpmn()">

            <!-- Không có workflow -->
            <div *ngIf="!detailWorkflowXml()" style="text-align: center; padding: 36px; background: #fffbeb; border: 1px solid #fef3c7; border-radius: 8px; color: #b45309;">
              <i class="pi pi-exclamation-triangle" style="font-size: 2rem; display: block; margin-bottom: 10px;"></i>
              <p style="margin: 0; font-weight: 600;">Hồ sơ chưa được đưa vào quy trình phê duyệt</p>
              <p style="margin: 6px 0 0 0; font-size: 0.83rem;">Nhấn <b>Gửi duyệt</b> để khởi chạy quy trình.</p>
            </div>

            <!-- BPMN Canvas -->
            <div *ngIf="detailWorkflowXml()" style="border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;">
              <div style="background: #f8fafc; padding: 10px 16px; border-bottom: 1px solid #e2e8f0; font-weight: 600; color: #374151; display: flex; justify-content: space-between; align-items: center; font-size: 0.9rem;">
                <span><i class="pi pi-sitemap" style="margin-right: 6px; color: #002D72;"></i>Sơ đồ quy trình</span>
                <span *ngIf="workflowDetail()?.instance" class="status-pill"
                      [ngStyle]="{ background: '#eff6ff', color: '#1d4ed8', border: '1px solid #bfdbfe', fontSize: '0.78rem' }">
                  {{ workflowDetail()?.instance?.status }}
                </span>
              </div>
              <div style="padding: 12px 16px 4px;">
                <div id="bpmn-canvas-dossier" style="height: 360px; border: 1px solid #cbd5e1; border-radius: 8px; background: #fafafa; position: relative;"></div>
                <p style="font-size: 0.75rem; color: #94a3b8; margin: 6px 0 8px 2px;">
                  <i class="pi pi-info-circle" style="margin-right: 4px;"></i>Nút viền xanh là bước phê duyệt hiện tại.
                </p>
              </div>
            </div>

            <!-- Bước hiện tại info (không có buttons vì đã đưa lên header) -->
            <div *ngIf="detailWorkflowXml() && detailPendingTask()" style="background: #eff6ff; border: 1px solid #bfdbfe; border-radius: 8px; padding: 10px 16px; display: flex; justify-content: space-between; align-items: center; font-size: 0.85rem;">
              <span style="color: #1e40af;"><i class="pi pi-spin pi-spinner" style="font-size: 0.75rem; margin-right: 6px;"></i>Đang ở bước: <b>{{ detailPendingTask()?.stepName }}</b></span>
              <ng-container *ngIf="!isUserAuthorizedForDetailAction">
                <span style="color: #64748b; font-style: italic; font-size: 0.8rem;">🔒 Không có quyền xử lý bước này</span>
              </ng-container>
              <code *ngIf="detailPendingTask()?.assignedRole" style="background: #dbeafe; color: #1e40af; padding: 2px 8px; border-radius: 4px; font-size: 0.75rem;">{{ detailPendingTask()?.assignedRole }}</code>
            </div>

            <!-- Timeline lịch sử -->
            <div style="border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;" *ngIf="detailWorkflowXml()">
              <div style="background: #f8fafc; padding: 10px 16px; border-bottom: 1px solid #e2e8f0; font-weight: 600; color: #374151; font-size: 0.9rem;">
                <i class="pi pi-history" style="margin-right: 6px; color: #002D72;"></i>Lịch sử xử lý luồng duyệt
              </div>
              <div style="padding: 16px;">
                <div class="wf-timeline-wrapper" *ngIf="detailHistoryLogs().length > 0">
                  <div class="wf-timeline">
                    <div class="timeline-item" *ngFor="let h of detailHistoryLogs()">
                      <div class="timeline-badge"
                           [class.badge-submit]="h.action === 'Submit'"
                           [class.badge-approve]="h.action === 'Approve'"
                           [class.badge-reject]="h.action === 'Reject'">
                      </div>
                      <div class="timeline-content">
                        <div class="timeline-header">
                          <span class="timeline-title">
                            {{ h.stepName || h.actionLabel || 'Xử lý' }}
                            <span style="font-weight: 500; font-size: 11px; margin-left: 6px; color: #94a3b8;">
                              ({{ h.action === 'Submit' ? 'Khởi tạo' : (h.action === 'Approve' ? 'Duyệt qua' : (h.action === 'Reject' ? 'Trả về' : h.action)) }})
                            </span>
                          </span>
                          <span class="timeline-time">{{ (h.actionDate || h.createdDate) | date:'dd/MM/yyyy HH:mm:ss' }}</span>
                        </div>
                        <div class="timeline-user">
                          Thực hiện bởi:
                          <b>{{ h.actionByUsername && h.actionByFullName ? h.actionByUsername + ' - ' + h.actionByFullName : (h.actionByUsername || h.actorName || h.actionByUserId || h.actorId || 'Hệ thống') }}</b>
                        </div>
                        <div class="timeline-comment" *ngIf="h.comment"><b>Ý kiến:</b> {{ h.comment }}</div>
                      </div>
                    </div>
                  </div>
                </div>

                <div *ngIf="!detailHistoryLogs().length" style="text-align: center; padding: 32px; background: #f8fafc; border-radius: 8px; color: #64748b;">
                  <i class="pi pi-history" style="font-size: 28px; display: block; margin-bottom: 10px; color: #94a3b8;"></i>
                  <p style="margin: 0; font-weight: 500;">Chưa có lịch sử xử lý quy trình nào.</p>
                </div>
              </div>
            </div>

          </ng-container>
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
    ::ng-deep #bpmn-canvas-dossier .highlight-active-node:not(.djs-connection) .djs-visual > :first-child {
      fill: #dbeafe !important;
      stroke: #002D72 !important;
      stroke-width: 3px !important;
    }
    .wf-timeline-wrapper {
      max-height: 400px; overflow-y: auto; padding: 8px 16px 8px 8px; margin-top: 4px; border-radius: 8px;
    }
    .wf-timeline-wrapper::-webkit-scrollbar { width: 6px; }
    .wf-timeline-wrapper::-webkit-scrollbar-track { background: transparent; }
    .wf-timeline-wrapper::-webkit-scrollbar-thumb { background: #cbd5e1; border-radius: 3px; }
    .wf-timeline-wrapper::-webkit-scrollbar-thumb:hover { background: #94a3b8; }
    .wf-timeline { display: flex; flex-direction: column; position: relative; padding-left: 24px; }
    .wf-timeline::before { content: ''; position: absolute; top: 0; bottom: 0; left: 7px; width: 2px; background-color: #e2e8f0; }
    .timeline-item { position: relative; padding-bottom: 20px; }
    .timeline-item:last-child { padding-bottom: 0; }
    .timeline-badge {
      position: absolute; left: -24px; top: 4px; width: 16px; height: 16px;
      border-radius: 50%; background-color: #cbd5e1; border: 3px solid white;
    }
    .timeline-badge.badge-submit  { background-color: #3b82f6; }
    .timeline-badge.badge-approve { background-color: #10b981; }
    .timeline-badge.badge-reject  { background-color: #ef4444; }
    .timeline-content {
      background-color: #f8fafc; padding: 10px 14px; border-radius: 8px; border: 1px solid #f1f5f9;
    }
    .timeline-header { display: flex; justify-content: space-between; margin-bottom: 4px; font-size: 12px; }
    .timeline-title  { font-weight: 700; color: #1e293b; }
    .timeline-time   { color: #94a3b8; }
    .timeline-user   { font-size: 11px; color: #64748b; font-weight: 600; margin-bottom: 6px; }
    .timeline-comment {
      font-size: 12px; font-style: italic; color: #64748b;
      background-color: #e2e8f0; padding: 6px 10px; border-radius: 4px;
      margin-top: 6px; border-left: 3px solid #cbd5e1;
    }

    .tab-item.tab-readonly {
      color: #94a3b8;
      opacity: 0.85;
    }
    .tab-item.tab-readonly.tab-active {
      color: #64748b;
      border-bottom-color: #94a3b8;
      font-weight: 600;
    }

    .dossier-info-tab {
      display: flex;
      flex-direction: column;
      gap: 20px;
      transition: opacity 0.2s ease;
    }
    .dossier-info-tab--readonly {
      opacity: 0.72;
    }

    .readonly-notice {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 10px 14px;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      color: #64748b;
      font-size: 0.83rem;
    }
    .readonly-notice .pi-lock {
      color: #94a3b8;
      font-size: 0.9rem;
    }

    .dossier-form-grid--readonly {
      pointer-events: none;
      user-select: none;
    }
    .dossier-form-grid--readonly .wf-input,
    .dossier-form-grid--readonly .wf-select,
    .dossier-form-grid--readonly .wf-textarea {
      background-color: #f1f5f9 !important;
      color: #64748b !important;
      cursor: not-allowed !important;
      border-color: #e2e8f0 !important;
    }
    .dossier-form-grid--readonly .form-label {
      color: #94a3b8;
    }
  `]
})
export class DossierDetailComponent implements OnInit, OnDestroy {
  @Input() dossierId!: string;
  @Output() cancel = new EventEmitter<void>();

  private platformId = inject(PLATFORM_ID);
  private service = inject(DossierManagementService);
  private authService = inject(AuthService);
  private messageService = inject(MessageService);
  private destroy$ = new Subject<void>();

  loading = signal<boolean>(true);
  submitting = signal<boolean>(false);
  activeTab = signal<'info' | 'documents' | 'versions' | 'workflow'>('info');

  dossier = signal<any>(null);
  dossierMeta = computed(() => normalizeDossierDetail(this.dossier()));

  // EAV Form
  loadingType = signal<boolean>(false);
  formTemplate = signal<any>(null);
  dynamicFields = signal<EavField[]>([]);
  detailFormData: Record<string, any> = {};
  private pendingFormData: Record<string, unknown> = {};
  savingForm = signal<boolean>(false);

  // Phiên bản
  versions = signal<any[]>([]);
  loadingVersions = signal<boolean>(false);

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
  detailHistoryLogs = signal<any[]>([]);
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

  // bpmn-js viewer instance
  private bpmnViewer: any = null;

  get isDraftOrReturned(): boolean {
    const status = this.dossier()?.status;
    return status === 'Draft' || status === 'Returned';
  }

  /** Cho phép sửa dữ liệu form — reactive theo dossier + workflowDetail */
  canEditFormData = computed(() => {
    const d = this.dossier();
    const wf = this.workflowDetail();
    if (!d) return false;

    if (!isDossierInWorkflow(d, wf)) {
      const status = pickFirst(d.status, d.Status) as string | undefined;
      return status === 'Draft' || status === 'Returned';
    }

    return resolveCurrentStepAllowEdit(wf);
  });

  isFormReadOnly = computed(() => !this.canEditFormData());

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
      this.detailHistoryLogs.set(Array.isArray(res?.history) ? res.history : []);
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
    this.detailHistoryLogs.set(Array.isArray(res.history) ? res.history : []);

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

  onOpenWorkflowTab() {
    this.activeTab.set('workflow');
    // Re-render BPMN sau khi DOM tab hiện ra
    setTimeout(() => {
      const xml = this.detailWorkflowXml();
      const nodeId = this.detailCurrentNodeId();
      if (xml) this.initBpmnViewer(xml, nodeId);
    }, 150);
  }

  async initBpmnViewer(xml: string, currentNodeId: string) {
    if (!xml || !isPlatformBrowser(this.platformId)) return;

    if (this.bpmnViewer) {
      this.bpmnViewer.destroy();
      this.bpmnViewer = null;
    }

    try {
      const Viewer = (await import('bpmn-js/lib/Viewer')).default;
      this.bpmnViewer = new Viewer({ container: '#bpmn-canvas-dossier' });
      await this.bpmnViewer.importXML(xml);
      const canvas = this.bpmnViewer.get('canvas');
      canvas.zoom('fit-viewport');
      if (currentNodeId) {
        canvas.addMarker(currentNodeId, 'highlight-active-node');
      }
    } catch (err) {
      console.error('BPMN render error', err);
    }
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
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: `Đã thực hiện: ${actionLabel}` });
        this.detailActionSubmitting.set(false);
        this.showActionDialog.set(false);
        this.detailActionComment.set('');
        this.selectedNextUserId.set('');
        this.pendingActionBtn.set(null);
        this.loadDetail();
      },
      error: (err: any) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể thực hiện.' });
        this.detailActionSubmitting.set(false);
      }
    });
  }

  saveFormData() {
    this.savingForm.set(true);
    this.service.saveFormData(this.dossierId, {
      formDataJson: serializeFormDataForSchema(this.dynamicFields(), this.detailFormData),
      rowVersion: this.dossier()?.rowVersion,
      changeNote: 'Cập nhật dữ liệu từ giao diện chi tiết'
    }).subscribe({
      next: (res) => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu dữ liệu' });
        if (res?.data) {
          this.dossier.set(res.data);
          const meta = normalizeDossierDetail(res.data);
          if (meta) {
            this.pendingFormData = parseFormDataJson(meta.formDataJson);
            this.detailFormData = pickFormDataForSchema(this.dynamicFields(), this.pendingFormData);
          }
        }
        this.savingForm.set(false);
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể lưu dữ liệu' });
        this.savingForm.set(false);
      }
    });
  }

  onConfirmSubmit() {
    this.submitting.set(true);
    this.service.submitForApproval(this.dossierId).subscribe({
      next: (res) => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã gửi duyệt thành công' });
        this.showSubmitConfirm.set(false);
        this.dossier.set(res.data);
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

  onCancel() {
    this.cancel.emit();
  }

  /** Event-based setters — tránh ngModel bleeding trong *ngFor+*ngSwitch */
  setDetailField(key: string, event: Event) {
    if (this.isFormReadOnly()) return;
    this.detailFormData[key] = (event.target as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement).value;
  }

  setDetailFieldNumber(key: string, event: Event) {
    if (this.isFormReadOnly()) return;
    const raw = (event.target as HTMLInputElement).value;
    this.detailFormData[key] = raw === '' ? null : Number(raw);
  }

  setDetailCheckbox(key: string, event: Event) {
    if (this.isFormReadOnly()) return;
    this.detailFormData[key] = (event.target as HTMLInputElement).checked;
  }

  /** Mở tab phiên bản và load dữ liệu nếu chưa có */
  onOpenVersionsTab() {
    this.activeTab.set('versions');
    if (this.versions().length === 0 && !this.loadingVersions()) {
      this.loadVersions();
    }
  }

  loadVersions() {
    this.loadingVersions.set(true);
    this.service.getVersions(this.dossierId).pipe(
      catchError(() => of([] as any[])),
      finalize(() => this.loadingVersions.set(false))
    ).subscribe({
      next: (res) => {
        this.versions.set(Array.isArray(res) ? res : []);
      }
    });
  }

  trackByFieldKey(_index: number, field: EavField): string {
    return field.key;
  }

  parseDocumentSnapshotCount(json?: string | null): string {
    if (!json?.trim()) return '0 tài liệu';
    try {
      const arr = JSON.parse(json) as unknown[];
      const count = Array.isArray(arr) ? arr.length : 0;
      return `${count} tài liệu`;
    } catch {
      return '—';
    }
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
