import { Component, OnInit, signal, inject, Output, EventEmitter, Input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { DialogModule } from 'primeng/dialog';
import { DossierManagementService } from '../../data-access/dossier-management.service';
import { DossierDocumentsTabComponent } from '../dossier-documents/dossier-documents-tab.component';
import { DossierVersionsTabComponent } from '../dossier-versions-tab/dossier-versions-tab.component';
import { DossierWorkflowTabComponent } from '../dossier-workflow-tab/dossier-workflow-tab.component';
import {
  EavField,
  guidsEqual,
  normalizeField,
  parseFormDataJson,
  pickFormDataForSchema,
  readFormSchemaJson,
  serializeFormDataForSchema,
} from '../../utils/dossier-form-schema.util';
import { forkJoin } from 'rxjs';
import { DatePickerModule } from 'primeng/datepicker';
import { AuthService } from '../../../../../../shared/core/src/lib/services/auth.service';
import { EavFormService } from '../../../../../../shared/core/src/lib/services/eav-form.service';
import {
  isApproveWorkflowLabel,
  isRejectWorkflowLabel,
  parseWorkflowActionButtons,
} from '../../utils/dossier-workflow-bpmn.util';

@Component({
  selector: 'app-dossier-form',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, DialogModule, DossierDocumentsTabComponent, DossierVersionsTabComponent, DossierWorkflowTabComponent, DatePickerModule],
  template: `
    <div class="wf-card" style="position: relative;">
      <!-- Header -->
      <div class="edit-header">
        <div style="display: flex; align-items: center; gap: 10px;">
          <button (click)="onCancel()" class="btn-back btn-small" title="Quay lại">
            <i class="pi pi-arrow-left"></i>
          </button>
          <h2 class="edit-title">{{ isEditMode() ? 'Cập nhật Thông tin Hồ sơ' : 'Tạo Hồ sơ mới' }}</h2>
        </div>
        <div class="edit-actions">
          <button (click)="onCancel()" class="btn-cancel"><i class="pi pi-times"></i> Hủy</button>
          <button *ngIf="showCompleteInputButton()"
                  (click)="onCompleteInput()" class="btn-green" [disabled]="completingInput()">
            <i class="pi pi-check" *ngIf="!completingInput()"></i>
            <i class="pi pi-spin pi-spinner" *ngIf="completingInput()"></i>
            Hoàn thành nhập liệu
          </button>
          <button *ngIf="showSubmitForApprovalButton()"
                  (click)="openSubmitWorkflowDialog()" class="btn-green" [disabled]="submitting()">
            <i class="pi pi-send" *ngIf="!submitting()"></i>
            <i class="pi pi-spin pi-spinner" *ngIf="submitting()"></i>
            Gửi duyệt
          </button>

          <!-- Workflow action buttons for Returned (statusId = 5) -->
          <ng-container *ngIf="formPendingTask() && isUserAuthorizedForFormAction">
            <button *ngFor="let btn of formDynamicButtons()"
                    class="btn-small"
                    [style.padding]="'8px 12px'"
                    [style.border-radius]="'4px'"
                    [class.btn-cancel]="isRejectLabel(btn.label)"
                    [class.btn-save]="isApproveLabel(btn.label)"
                    [class.btn-green]="!isRejectLabel(btn.label) && !isApproveLabel(btn.label)"
                    (click)="openFormActionDialog(btn)">
              <i class="pi"
                 [class.pi-check]="!isRejectLabel(btn.label)"
                 [class.pi-times]="isRejectLabel(btn.label)"
                 style="margin-right: 4px;"></i>
              {{ btn.label }}
            </button>
          </ng-container>

          <button (click)="onSave()" class="btn-save" [disabled]="isSaving() || !isValid()">
            <i class="pi pi-save" *ngIf="!isSaving()"></i>
            <i class="pi pi-spin pi-spinner" *ngIf="isSaving()"></i>
            Lưu thông tin
          </button>
        </div>
      </div>

      <!-- Tabs — chỉ hiện khi sửa hồ sơ -->
      <div class="tab-bar" *ngIf="isEditMode()">
        <button *ngIf="isFormTabVisible('info')" class="tab-item" [class.tab-active]="activeTab() === 'info'" (click)="activeTab.set('info')">
          <i class="pi pi-info-circle" style="margin-right: 6px;"></i>
          Thông tin hồ sơ
        </button>
        <button *ngIf="isFormTabVisible('documents')" class="tab-item" [class.tab-active]="activeTab() === 'documents'" (click)="activeTab.set('documents')">
          <i class="pi pi-file" style="margin-right: 6px;"></i>
          Tài liệu đính kèm
        </button>
        <button *ngIf="isFormTabVisible('versions')" class="tab-item" [class.tab-active]="activeTab() === 'versions'" (click)="activeTab.set('versions')">
          <i class="pi pi-history" style="margin-right: 6px;"></i>
          Lịch sử phiên bản
        </button>
        <button *ngIf="isFormTabVisible('workflow')" class="tab-item" [class.tab-active]="activeTab() === 'workflow'" (click)="activeTab.set('workflow')">
          <i class="pi pi-sitemap" style="margin-right: 6px;"></i>
          Quy trình & Lịch sử
        </button>
      </div>

      <div *ngIf="!isEditMode() || activeTab() === 'info'">
      <!-- Thông tin vị trí + Thiết bị liên quan -->
      <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 24px; margin-bottom: 24px;">

        <!-- Cột trái: Thông tin vị trí -->
        <div style="display: flex; flex-direction: column; gap: 16px;">
          <h3 style="font-size: 0.95rem; font-weight: 700; color: #002D72; padding-bottom: 8px; border-bottom: 1px solid #e2e8f0; margin: 0;">Thông tin vị trí</h3>

          <div class="form-group">
            <label class="form-label">Loại lưới điện</label>
            <select class="wf-select w-full" [(ngModel)]="dossier.gridTypeId" (change)="onGridTypeChange(dossier.gridTypeId)">
              <option [ngValue]="null">-- Chọn loại lưới điện --</option>
              <option *ngFor="let item of gridTypes()" [value]="item.id">{{ item.name }}</option>
            </select>
          </div>

          <div class="form-group">
            <label class="form-label">Trạm / Đường dây</label>
            <select class="wf-select w-full" [(ngModel)]="dossier.infrastructureId">
              <option [ngValue]="null">-- Chọn trạm/đường dây --</option>
              <option *ngFor="let item of formInfrastructures()" [value]="item.id">{{ item.name }}</option>
            </select>
          </div>
        </div>

        <!-- Cột phải: Thiết bị liên quan -->
        <div style="display: flex; flex-direction: column; gap: 16px;">
          <div style="display: flex; justify-content: space-between; align-items: center; padding-bottom: 8px; border-bottom: 1px solid #e2e8f0;">
            <h3 style="font-size: 0.95rem; font-weight: 700; color: #002D72; margin: 0;">Thiết bị liên quan</h3>
            <button (click)="openAddEquipmentDialog()" class="btn-outlined btn-small">
              <i class="pi pi-plus"></i> Thêm
            </button>
          </div>

          <div *ngIf="selectedEquipments().length === 0" style="padding: 24px; background: #f8fafc; border: 1px dashed #e2e8f0; border-radius: 8px; text-align: center; color: #9ca3af; font-size: 0.85rem;">
            Chưa có thiết bị nào được gắn vào hồ sơ.
          </div>

          <div *ngIf="selectedEquipments().length > 0" class="wf-table-wrap" style="max-height: 280px; overflow-y: auto;">
            <table class="wf-table">
              <thead>
                <tr>
                  <th>Mã TB</th>
                  <th>Tên TB</th>
                  <th style="width: 50px; text-align: center;"></th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let eq of selectedEquipments()">
                  <td>{{ eq.equipmentCode || eq.code }}</td>
                  <td>{{ eq.equipmentName || eq.name }}</td>
                  <td style="text-align: center;">
                    <button (click)="removeEquipment(eq)" class="act-btn act-delete" title="Bỏ thiết bị"><i class="pi pi-times"></i></button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- ================================================================ -->
      <!-- Box chọn Loại hồ sơ — đặt ở dưới để gen form động bên dưới      -->
      <!-- ================================================================ -->
      <div style="padding: 16px; background: #f0f5ff; border: 1.5px solid #bfdbfe; border-radius: 8px; margin-bottom: 20px;">
        <div class="form-group" style="margin-bottom: 0;">
          <label class="form-label" style="font-weight: 700; color: #002D72;">
            Loại hồ sơ <span class="required">*</span>
          </label>
          <select class="wf-select w-full" [(ngModel)]="dossier.dossierTypeId"
                  (ngModelChange)="onDossierTypeChange($event)"
                  [disabled]="isEditMode()">
            <option value="">-- Chọn loại hồ sơ --</option>
            <option *ngFor="let item of dossierTypes()" [value]="item.id">{{ item.name }}</option>
          </select>
          <p style="font-size: 0.75rem; color: #6b7280; margin: 6px 0 0 0;">
            <i class="pi pi-info-circle"></i>
            Chọn loại hồ sơ để hiển thị các trường thông tin tương ứng cần nhập bên dưới
          </p>
        </div>
      </div>

      <!-- ================================================================ -->
      <!-- Dynamic form fields — sinh ra từ FormSchema khi chọn loại hồ sơ  -->
      <!-- ================================================================ -->
      <div *ngIf="loadingForm()">
        <div style="display: flex; align-items: center; gap: 8px; color: #6b7280; padding: 12px 0;">
          <i class="pi pi-spin pi-spinner"></i> Đang tải biểu mẫu...
        </div>
      </div>

      <div *ngIf="!loadingForm() && dynamicFields().length > 0">
        <div style="margin-bottom: 12px; display: flex; align-items: center; gap: 8px;">
          <h3 style="font-size: 0.95rem; font-weight: 700; color: #002D72; margin: 0;">Thông tin chi tiết hồ sơ</h3>
          <span style="font-size: 0.78rem; color: #6b7280;">({{ selectedTypeName() }})</span>
        </div>
        <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 16px;">
          <ng-container *ngFor="let field of dynamicFields(); trackBy: trackByFieldKey">
            <div class="form-group" [style.grid-column]="field.type === 'textarea' ? '1 / -1' : 'auto'">
              <label class="form-label">
                {{ field.label }}
                <span class="required" *ngIf="field.required">*</span>
              </label>

              <ng-container [ngSwitch]="field.type">
                <input *ngSwitchCase="'text'" type="text" class="wf-input w-full"
                       autocomplete="off"
                       [name]="'dyn_' + field.key"
                       [placeholder]="field.placeholder || ''"
                       [(ngModel)]="formData[field.key]">

                <div *ngSwitchCase="'number'" style="display: flex; gap: 6px; align-items: center;">
                  <input type="number" class="wf-input" style="flex: 1;"
                         autocomplete="off"
                         [name]="'dyn_' + field.key"
                         [placeholder]="field.placeholder || ''"
                         [(ngModel)]="formData[field.key]">
                  <span *ngIf="field.unit" style="font-size: 0.85rem; color: #6b7280; white-space: nowrap;">{{ field.unit }}</span>
                </div>

                <p-datepicker *ngSwitchCase="'date'"
                              [name]="'dyn_' + field.key"
                              [(ngModel)]="formData[field.key]"
                              dateFormat="dd/mm/yy"
                              [showIcon]="true"
                              icon="pi pi-calendar"
                              appendTo="body"
                              styleClass="w-full"
                              [placeholder]="field.placeholder || 'dd/mm/yyyy'">
                </p-datepicker>

                <textarea *ngSwitchCase="'textarea'" class="wf-textarea w-full" rows="3"
                          autocomplete="off"
                          [name]="'dyn_' + field.key"
                          [placeholder]="field.placeholder || ''"
                          [(ngModel)]="formData[field.key]"></textarea>

                <!-- Dropdown (type = select, luưu trong schema là dropdown → được normalize thành select) -->
                <select *ngSwitchCase="'select'" class="wf-select w-full"
                        [name]="'dyn_' + field.key"
                        [(ngModel)]="formData[field.key]">
                  <option value="">-- Chọn --</option>
                  <ng-container *ngIf="field.dataSourceType === 'catalog' && field.catalogItems?.length; else manualOptions">
                    <option *ngFor="let opt of field.catalogItems" [value]="opt.value">{{ opt.label }}</option>
                  </ng-container>
                  <ng-template #manualOptions>
                    <option *ngFor="let opt of field.options" [value]="opt.value">{{ opt.label }}</option>
                  </ng-template>
                </select>

                <!-- Radio group -->
                <div *ngSwitchCase="'radio'" style="display: flex; flex-direction: column; gap: 8px; margin-top: 4px;">
                  <ng-container *ngIf="field.dataSourceType === 'catalog' && field.catalogItems?.length; else manualRadioOptions">
                    <label *ngFor="let opt of field.catalogItems"
                           style="display: flex; align-items: center; gap: 8px; cursor: pointer; font-weight: normal;">
                      <input type="radio"
                             [name]="'dyn_' + field.key"
                             [value]="opt.value"
                             [(ngModel)]="formData[field.key]"
                             style="width: 16px; height: 16px; accent-color: #002D72; cursor: pointer;">
                      <span style="font-size: 0.9rem;">{{ opt.label }}</span>
                    </label>
                  </ng-container>
                  <ng-template #manualRadioOptions>
                    <label *ngFor="let opt of (field.options || [])"
                           style="display: flex; align-items: center; gap: 8px; cursor: pointer; font-weight: normal;">
                      <input type="radio"
                             [name]="'dyn_' + field.key"
                             [value]="opt.value"
                             [(ngModel)]="formData[field.key]"
                             style="width: 16px; height: 16px; accent-color: #002D72; cursor: pointer;">
                      <span style="font-size: 0.9rem;">{{ opt.label }}</span>
                    </label>
                  </ng-template>
                </div>

                <!-- Checkbox (có thể là đơn hoặc nhóm nhiều lựa chọn) -->
                <ng-container *ngSwitchCase="'checkbox'">
                  <!-- TH 1: Nhóm checkbox (nếu có options hoặc catalog) -->
                  <div *ngIf="field.dataSourceType === 'catalog' || (field.options && field.options.length > 0); else singleCheckbox"
                       style="display: flex; flex-direction: column; gap: 8px; margin-top: 4px;">
                    
                    <!-- Nút "Chọn tất cả" nếu có selectAll -->
                    <label *ngIf="field.selectAll" style="display: flex; align-items: center; gap: 8px; cursor: pointer; font-weight: 600; border-bottom: 1px dashed #cbd5e1; padding-bottom: 4px; margin-bottom: 4px;">
                      <input type="checkbox"
                             [checked]="isAllCheckboxesChecked(field)"
                             (change)="toggleSelectAllCheckboxes(field, $any($event.target).checked)"
                             style="width: 16px; height: 16px; accent-color: #002D72; cursor: pointer;">
                      <span style="font-size: 0.9rem; color: #002D72;">Chọn tất cả</span>
                    </label>

                    <!-- Catalog hoặc Manual options -->
                    <ng-container *ngIf="field.dataSourceType === 'catalog' && field.catalogItems?.length; else manualCheckboxGroup">
                      <label *ngFor="let opt of field.catalogItems"
                             style="display: flex; align-items: center; gap: 8px; cursor: pointer; font-weight: normal;">
                        <input type="checkbox"
                               [name]="'dyn_' + field.key + '_' + opt.value"
                               [checked]="isCheckboxChecked(field.key, opt.value)"
                               (change)="onCheckboxGroupChange(field.key, opt.value, $any($event.target).checked)"
                               style="width: 16px; height: 16px; accent-color: #002D72; cursor: pointer;">
                        <span style="font-size: 0.9rem;">{{ opt.label }}</span>
                      </label>
                    </ng-container>
                    <ng-template #manualCheckboxGroup>
                      <label *ngFor="let opt of (field.options || [])"
                             style="display: flex; align-items: center; gap: 8px; cursor: pointer; font-weight: normal;">
                        <input type="checkbox"
                               [name]="'dyn_' + field.key + '_' + opt.value"
                               [checked]="isCheckboxChecked(field.key, opt.value)"
                               (change)="onCheckboxGroupChange(field.key, opt.value, $any($event.target).checked)"
                               style="width: 16px; height: 16px; accent-color: #002D72; cursor: pointer;">
                        <span style="font-size: 0.9rem;">{{ opt.label }}</span>
                      </label>
                    </ng-template>
                  </div>

                  <!-- TH 2: Checkbox đơn -->
                  <ng-template #singleCheckbox>
                    <label style="display: flex; align-items: center; gap: 8px; cursor: pointer; margin-top: 4px;">
                      <input type="checkbox"
                             [name]="'dyn_' + field.key"
                             [(ngModel)]="formData[field.key]"
                             style="width: 16px; height: 16px; accent-color: #002D72; cursor: pointer;">
                      <span style="font-size: 0.9rem;">{{ field.placeholder || field.label }}</span>
                    </label>
                  </ng-template>
                </ng-container>

                <!-- Checkbox Group (Giữ lại để tương thích ngược nếu có template cũ dùng checkboxGroup) -->
                <div *ngSwitchCase="'checkboxGroup'" style="display: flex; flex-direction: column; gap: 8px; margin-top: 4px;">
                  <!-- Nút "Chọn tất cả" nếu có selectAll -->
                  <label *ngIf="field.selectAll" style="display: flex; align-items: center; gap: 8px; cursor: pointer; font-weight: 600; border-bottom: 1px dashed #cbd5e1; padding-bottom: 4px; margin-bottom: 4px;">
                    <input type="checkbox"
                           [checked]="isAllCheckboxesChecked(field)"
                           (change)="toggleSelectAllCheckboxes(field, $any($event.target).checked)"
                           style="width: 16px; height: 16px; accent-color: #002D72; cursor: pointer;">
                    <span style="font-size: 0.9rem; color: #002D72;">Chọn tất cả</span>
                  </label>

                  <ng-container *ngIf="field.dataSourceType === 'catalog' && field.catalogItems?.length; else manualCheckboxGroupOld">
                    <label *ngFor="let opt of field.catalogItems"
                           style="display: flex; align-items: center; gap: 8px; cursor: pointer; font-weight: normal;">
                      <input type="checkbox"
                             [name]="'dyn_' + field.key + '_' + opt.value"
                             [checked]="isCheckboxChecked(field.key, opt.value)"
                             (change)="onCheckboxGroupChange(field.key, opt.value, $any($event.target).checked)"
                             style="width: 16px; height: 16px; accent-color: #002D72; cursor: pointer;">
                      <span style="font-size: 0.9rem;">{{ opt.label }}</span>
                    </label>
                  </ng-container>
                  <ng-template #manualCheckboxGroupOld>
                    <label *ngFor="let opt of (field.options || [])"
                           style="display: flex; align-items: center; gap: 8px; cursor: pointer; font-weight: normal;">
                      <input type="checkbox"
                             [name]="'dyn_' + field.key + '_' + opt.value"
                             [checked]="isCheckboxChecked(field.key, opt.value)"
                             (change)="onCheckboxGroupChange(field.key, opt.value, $any($event.target).checked)"
                             style="width: 16px; height: 16px; accent-color: #002D72; cursor: pointer;">
                      <span style="font-size: 0.9rem;">{{ opt.label }}</span>
                    </label>
                  </ng-template>
                </div>

                <input *ngSwitchDefault type="text" class="wf-input w-full"
                       autocomplete="off"
                       [name]="'dyn_' + field.key"
                       [(ngModel)]="formData[field.key]">
              </ng-container>
            </div>
          </ng-container>
        </div>
      </div>

      <!-- Placeholder khi chưa chọn loại hồ sơ -->
      <div *ngIf="!loadingForm() && !dossier.dossierTypeId" style="padding: 28px; background: #f8fafc; border: 1px dashed #cbd5e1; border-radius: 8px; text-align: center; color: #94a3b8;">
        <i class="pi pi-file-edit" style="font-size: 2rem; display: block; margin-bottom: 8px;"></i>
        Chọn Loại hồ sơ ở trên để hiển thị các trường thông tin chi tiết
      </div>

      <!-- Thông báo khi loại hồ sơ chưa có biểu mẫu -->
      <div *ngIf="!loadingForm() && dossier.dossierTypeId && dynamicFields().length === 0 && selectedFormId()"
           style="padding: 16px; background: #fffbeb; border: 1px solid #fde68a; border-radius: 8px; color: #92400e; font-size: 0.88rem;">
        <i class="pi pi-exclamation-triangle"></i>
        Biểu mẫu cho loại hồ sơ này chưa có trường thông tin nào. Liên hệ quản trị viên để cấu hình.
      </div>
      </div>

      <!-- Tab Tài liệu đính kèm (chỉ khi sửa) -->
      <div *ngIf="isEditMode() && activeTab() === 'documents'">
        <app-dossier-documents-tab
          [dossierId]="dossierId!"
          [canEdit]="true"
          [hasFormTemplate]="!!selectedFormId()"
          [formId]="selectedFormId()"
          (formDataSaved)="loadDossierDetail(dossierId!)"
        ></app-dossier-documents-tab>
      </div>

      <div *ngIf="isEditMode() && activeTab() === 'versions'">
        <app-dossier-versions-tab [dossierId]="dossierId!" />
      </div>

      <div *ngIf="isEditMode() && activeTab() === 'workflow'">
        <app-dossier-workflow-tab [dossierId]="dossierId!" />
      </div>

      <!-- Loading Overlay -->
      <div *ngIf="loading()" style="position: absolute; inset: 0; background: rgba(255,255,255,0.6); display: flex; align-items: center; justify-content: center; z-index: 50; border-radius: 12px;">
        <i class="pi pi-spin pi-spinner" style="font-size: 2rem; color: #002D72;"></i>
      </div>
    </div>

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

    <!-- Dialog thực hiện hành động workflow cho Form -->
    <p-dialog [visible]="showFormActionDialog()" 
              (visibleChange)="$event ? null : showFormActionDialog.set(false)"
              [header]="'Xác nhận hành động: ' + (pendingActionBtn()?.label || '')" 
              [modal]="true" 
              [style]="{ width: '450px' }"
              styleClass="evn-dialog-no-modal"
              [closable]="!formActionSubmitting()">
      <div style="display: flex; flex-direction: column; gap: 16px; padding: 8px 0 16px;">
        <div class="form-group" style="display: flex; flex-direction: column; gap: 6px;">
          <label class="form-label required">Ý kiến xử lý</label>
          <textarea class="wf-textarea" [ngModel]="formActionComment()" (ngModelChange)="formActionComment.set($event)" rows="3" placeholder="Nhập ý kiến xử lý..."></textarea>
        </div>
        
        <div class="form-group" *ngIf="pendingActionBtn()?.requiresUser && !isRejectLabel(pendingActionBtn()?.label || '')" style="display: flex; flex-direction: column; gap: 6px;">
          <label class="form-label required">Người xử lý tiếp theo</label>
          <select class="wf-select w-full" [ngModel]="selectedNextUserId()" (ngModelChange)="selectedNextUserId.set($event)">
            <option value="" disabled selected>-- Chọn người xử lý --</option>
            <option *ngFor="let u of filteredFormNextUsers()" [value]="u.id">{{ u.fullName || u.name }} ({{ u.username }})</option>
          </select>
        </div>
      </div>
      <ng-template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9; padding-top: 12px;">
          <button (click)="showFormActionDialog.set(false)" class="btn-cancel btn-small" [disabled]="formActionSubmitting()">Hủy</button>
          <button (click)="confirmFormAction()" class="btn-save btn-small" [disabled]="formActionSubmitting() || (pendingActionBtn()?.requiresUser && !isRejectLabel(pendingActionBtn()?.label || '') && !selectedNextUserId())">
            <i class="pi pi-spin pi-spinner" *ngIf="formActionSubmitting()"></i>
            <i class="pi pi-check" *ngIf="!formActionSubmitting()"></i>
            Đồng ý
          </button>
        </div>
      </ng-template>
    </p-dialog>

    <!-- Dialog Thêm Thiết Bị -->
    <p-dialog [(visible)]="showEquipmentDialog" header="Chọn thiết bị" [modal]="true" [style]="{width: '800px'}" styleClass="evn-dialog-no-modal" appendTo="body">
      <div style="display: flex; gap: 8px; margin-bottom: 16px;">
        <input type="text" class="wf-input" style="flex: 1;" placeholder="Tìm theo mã, tên thiết bị..." [(ngModel)]="equipmentKeyword" (keyup.enter)="searchEquipments()">
        <button class="btn-tim" (click)="searchEquipments()"><i class="pi pi-search"></i> Tìm</button>
      </div>

      <div class="wf-table-wrap" style="max-height: 384px; overflow-y: auto;">
        <table class="wf-table">
          <thead>
            <tr>
              <th class="col-chk">Chọn</th>
              <th>Mã TB</th>
              <th>Tên TB</th>
              <th>Trạm/ĐZ</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngIf="searchingEquipments()">
              <td colspan="4" class="empty-cell"><i class="pi pi-spin pi-spinner"></i> Đang tìm...</td>
            </tr>
            <ng-container *ngFor="let eq of equipmentSearchResults()">
              <tr style="cursor: pointer;" (click)="toggleDialogEquipmentSelection(eq)">
                <td class="col-chk">
                  <input type="checkbox" [checked]="isDialogEquipmentSelected(eq)" (click)="$event.stopPropagation()"
                         (change)="toggleDialogEquipmentSelection(eq)"
                         style="width: 15px; height: 15px; accent-color: #002D72; cursor: pointer;">
                </td>
                <td>{{ eq.code }}</td>
                <td>{{ eq.name }}</td>
                <td>{{ eq.infrastructureName || '-' }}</td>
              </tr>
            </ng-container>
            <tr *ngIf="!searchingEquipments() && equipmentSearchResults().length === 0">
              <td colspan="4" class="empty-cell">Không tìm thấy thiết bị phù hợp.</td>
            </tr>
          </tbody>
        </table>
      </div>

      <ng-template pTemplate="footer">
        <button class="btn-cancel btn-small" (click)="closeEquipmentDialog()"><i class="pi pi-times"></i> Đóng</button>
        <button class="btn-save btn-small" (click)="confirmEquipmentSelection()"><i class="pi pi-check"></i> Lưu</button>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .w-full { width: 100%; }
    .tab-bar { margin-bottom: 16px; }
  `]
})
export class DossierFormComponent implements OnInit {
  @Input() dossierId: string | null = null;
  @Output() cancel = new EventEmitter<void>();
  @Output() saved = new EventEmitter<string>();

  private service = inject(DossierManagementService);
  private messageService = inject(MessageService);
  private authService = inject(AuthService);
  private eavFormService = inject(EavFormService);

  isEditMode = computed(() => !!this.dossierId);
  activeTab = signal<'info' | 'documents' | 'versions' | 'workflow'>('info');
  loading = signal<boolean>(false);
  isSaving = signal<boolean>(false);
  completingInput = signal<boolean>(false);
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

  filteredFormNextUsers = computed(() => {
    const btn = this.pendingActionBtn();
    if (!btn || !btn.requiredRole) return [];
    const roles = btn.requiredRole.split(',').map((r: string) => r.trim().toUpperCase());
    return this.formWorkflowUsers().filter((u: any) => {
      const uRoles: string[] = (u.roles || u.Roles || []).map((r: string) => r.toUpperCase());
      return uRoles.some(r => roles.includes(r));
    });
  });

  submitting = signal<boolean>(false);
  showSubmitConfirm = signal<boolean>(false);
  nextStepInfo = signal<any>(null);
  selectedNextUser = signal<string>('');
  users = signal<any[]>([]);

  filteredSubmitNextUsers = computed(() => {
    const info = this.nextStepInfo();
    if (!info || !info.requiredRole) return [];
    const roles = info.requiredRole.split(',').map((r: string) => r.trim().toUpperCase());
    return this.users().filter((u: any) => {
      const uRoles: string[] = (u.roles || u.Roles || []).map((r: string) => r.toUpperCase());
      return uRoles.some(r => roles.includes(r));
    });
  });

  dossier = {
    id: '',
    dossierTypeId: '',
    gridTypeId: null as number | null,
    infrastructureId: null as string | null,
    dossierSetId: null as string | null,
    rowVersion: 1
  };

  selectedEquipments = signal<any[]>([]);
  formGridTypeId = signal<number | null>(null);

  formInfrastructures = computed(() => {
    const gtId = this.formGridTypeId();
    if (!gtId) return this.infrastructures(); // Nếu chưa chọn lưới điện, hiển thị tất cả
    return this.infrastructures().filter(inf => {
      const itemGridType = Number(inf.gridTypeId ?? inf.GridTypeId);
      return itemGridType === Number(gtId);
    });
  });

  // Lookups
  dossierTypes = signal<any[]>([]);
  gridTypes = signal<any[]>([]);
  infrastructures = signal<any[]>([]);
  dossierSets = signal<any[]>([]);

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

  // Dialog State
  showEquipmentDialog = false;
  equipmentKeyword = '';
  equipmentSearchResults = signal<any[]>([]);
  searchingEquipments = signal<boolean>(false);
  /** Lựa chọn tạm trong popup — chỉ áp dụng vào hồ sơ khi bấm Lưu. */
  dialogSelectedEquipments = signal<any[]>([]);

  ngOnInit() {
    this.loadLookups();
    if (this.dossierId) {
      this.loadDossierDetail(this.dossierId);
    }
  }

  loadLookups() {
    this.service.getDossierTypeLookup().subscribe(res => this.dossierTypes.set(res || []));
    this.service.getGridTypeLookup().subscribe(res => this.gridTypes.set(res || []));
    this.service.getDossierSets().subscribe(res => this.dossierSets.set(res || []));
    this.loadInfrastructures();
    this.service.getUsersLookup().subscribe({
      next: (users) => this.users.set(Array.isArray(users) ? users : []),
      error: () => this.users.set([])
    });
  }

  loadInfrastructures() {
    this.service.getInfrastructureLookup().subscribe(res => {
      const items = [...(res || [])];
      const selectedId = this.dossier.infrastructureId;
      if (selectedId && !items.some((inf) => (inf.id ?? inf.Id) === selectedId)) {
        const existing = this.infrastructures().find((inf) => (inf.id ?? inf.Id) === selectedId);
        if (existing) {
          items.push(existing);
        }
      }
      this.infrastructures.set(items);
    });
  }

  /** Giữ option trạm/đường dây hiện tại khi sửa hồ sơ (tránh mất giá trị đã lưu). */
  private ensureInfrastructureOption(detail: Record<string, unknown>) {
    const infraId = (detail['infrastructureId'] ?? detail['InfrastructureId']) as string | null | undefined;
    if (!infraId) return;

    const exists = this.infrastructures().some(
      (inf) => (inf.id ?? inf.Id) === infraId
    );
    if (exists) return;

    this.infrastructures.update((list) => [
      ...list,
      {
        id: infraId,
        name: (detail['infrastructureName'] ?? detail['InfrastructureName'] ?? infraId) as string,
        code: detail['infrastructureCode'] ?? detail['InfrastructureCode'],
        gridTypeId: detail['gridTypeId'] ?? detail['GridTypeId'],
      },
    ]);
  }

  loadDossierDetail(id: string) {
    this.loading.set(true);
    forkJoin({
      detail: this.service.getDossierById(id),
      types: this.service.getDossierTypeLookup(),
    }).subscribe({
      next: ({ detail: res, types }) => {
        if (types?.length) {
          this.dossierTypes.set(types);
        }
        if (res) {
          this.dossier = {
            id: res.id ?? res.Id,
            dossierTypeId: res.dossierTypeId ?? res.DossierTypeId,
            gridTypeId: res.gridTypeId != null ? Number(res.gridTypeId ?? res.GridTypeId) : null,
            infrastructureId: res.infrastructureId ?? res.InfrastructureId,
            dossierSetId: res.dossierSetId ?? res.DossierSetId,
            rowVersion: res.rowVersion ?? res.RowVersion,
          };
          this.dossierStatus.set(String(res.status ?? res.Status ?? ''));
          this.dossierStatusId.set(Number(res.statusId ?? res.StatusId ?? 0));
          this.workflowInstanceId.set(res.workflowInstanceId ?? res.WorkflowInstanceId ?? null);
          if (res.workflowInstanceId ?? res.WorkflowInstanceId) {
            this.loadWorkflow();
          }
          this.formGridTypeId.set(this.dossier.gridTypeId);
          this.selectedEquipments.set(res.equipments ?? res.Equipments ?? []);

          const typeId = res.dossierTypeId ?? res.DossierTypeId;
          const formId = res.formId ?? res.FormId;
          const formDataJson = res.formDataJson ?? res.FormDataJson;
          if (typeId) {
            this.loadFormForType(typeId, formDataJson, formId);
          }
          this.ensureInfrastructureOption(res);
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
    this.dynamicFields.set([]);
    this.formData = {};
    this.selectedFormId.set(null);

    if (!typeId) return;
    this.loadFormForType(typeId);
  }

  onGridTypeChange(gtId: any) {
    const numericId = gtId != null && gtId !== '' ? Number(gtId) : null;
    this.dossier.gridTypeId = numericId;
    this.formGridTypeId.set(numericId);

    // Reset trạm nếu trạm cũ không thuộc lưới điện mới
    if (this.dossier.infrastructureId) {
      const match = this.infrastructures().find(inf => inf.id === this.dossier.infrastructureId);
      const itemGridType = match ? Number(match.gridTypeId ?? match.GridTypeId) : null;
      if (itemGridType !== numericId) {
        this.dossier.infrastructureId = null;
      }
    }
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
    return !!this.dossier.dossierTypeId;
  }

  showCompleteInputButton(): boolean {
    return this.isEditMode() && this.dossierStatusId() === 1;
  }

  showSubmitForApprovalButton(): boolean {
    return this.isEditMode() && (this.dossierStatusId() === 2 || this.dossierStatusId() === 5);
  }

  openSubmitWorkflowDialog() {
    this.submitting.set(true);
    this.service.getNextStepInfo().subscribe({
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
    const isReturned = this.dossierStatusId() === 5;
    const call$ = isReturned
      ? this.service.resubmitWorkflow(this.dossier.id, {
          nextNodeId: info.nextNodeId,
          actionLabel: 'Trình duyệt',
          nextAssigneeUserId: this.selectedNextUser() || undefined,
          comment: 'Kính trình phê duyệt lại hồ sơ.'
        })
      : this.service.submitForApproval(this.dossier.id, {
          nextNodeId: info.nextNodeId,
          actionLabel: 'Trình duyệt',
          nextAssigneeUserId: this.selectedNextUser() || undefined,
          comment: 'Kính trình phê duyệt hồ sơ.'
        });

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

  onCompleteInput(): void {
    if (!this.dossier.id || this.completingInput()) return;

    this.completingInput.set(true);
    this.service.completeInput(this.dossier.id).subscribe({
      next: () => {
        this.dossierStatus.set('CompletedInput');
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: 'Đã chuyển trạng thái sang Hoàn thành nhập liệu',
        });
        this.completingInput.set(false);
      },
      error: (err: any) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err.error?.message || 'Không thể hoàn thành nhập liệu',
        });
        this.completingInput.set(false);
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

  onSave() {
    if (!this.isValid()) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Vui lòng chọn loại hồ sơ' });
      return;
    }

    this.isSaving.set(true);
    const dto = {
      ...this.dossier,
      gridTypeId: this.dossier.gridTypeId != null ? Number(this.dossier.gridTypeId) : null,
      equipmentIds: this.selectedEquipments().map(e => e.equipmentId || e.id),
      formDataJson: this.dynamicFields().length > 0
        ? serializeFormDataForSchema(this.dynamicFields(), this.formData)
        : undefined
    };

    const req$ = this.isEditMode()
      ? this.service.updateDossier(this.dossier.id, dto)
      : this.service.createDossier(dto);

    req$.subscribe({
      next: (res: any) => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu thông tin hồ sơ' });
        this.saved.emit(this.isEditMode() ? this.dossier.id : (res.id || res));
        this.isSaving.set(false);
      },
      error: (err: any) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể lưu hồ sơ' });
        this.isSaving.set(false);
      }
    });
  }

  onCancel() {
    this.cancel.emit();
  }

  // ===== Equipment Logic =====

  openAddEquipmentDialog() {
    this.dialogSelectedEquipments.set(this.selectedEquipments().map(eq => ({ ...eq })));
    this.showEquipmentDialog = true;
    this.equipmentKeyword = '';
    this.searchEquipments();
  }

  closeEquipmentDialog() {
    this.showEquipmentDialog = false;
    this.dialogSelectedEquipments.set([]);
  }

  confirmEquipmentSelection() {
    this.selectedEquipments.set(this.dialogSelectedEquipments().map(eq => ({ ...eq })));
    this.closeEquipmentDialog();
  }

  searchEquipments() {
    this.searchingEquipments.set(true);
    this.service.getEquipmentLookup({
      keyword: this.equipmentKeyword,
      infrastructureId: this.dossier.infrastructureId || undefined,
      gridTypeId: this.dossier.gridTypeId || undefined,
      pageSize: 50
    }).subscribe({
      next: (res: any) => {
        this.equipmentSearchResults.set(res.items || []);
        this.searchingEquipments.set(false);
      },
      error: (err: any) => {
        this.searchingEquipments.set(false);
      }
    });
  }

  isDialogEquipmentSelected(eq: any): boolean {
    return this.dialogSelectedEquipments().some(s => (s.equipmentId || s.id) === eq.id);
  }

  toggleDialogEquipmentSelection(eq: any) {
    const currentList = [...this.dialogSelectedEquipments()];
    const index = currentList.findIndex(s => (s.equipmentId || s.id) === eq.id);

    if (index >= 0) {
      currentList.splice(index, 1);
    } else {
      currentList.push({
        equipmentId: eq.id,
        id: eq.id,
        equipmentCode: eq.code,
        code: eq.code,
        equipmentName: eq.name,
        name: eq.name,
        infrastructureName: eq.infrastructureName
      });
    }
    this.dialogSelectedEquipments.set(currentList);
  }

  removeEquipment(eq: any) {
    const eqId = eq.equipmentId || eq.id;
    this.selectedEquipments.set(this.selectedEquipments().filter(s => (s.equipmentId || s.id) !== eqId));
  }

  trackByFieldKey(_index: number, field: EavField): string {
    return field.key;
  }

  loadWorkflow() {
    if (!this.dossierId) return;
    this.service.getWorkflowDetail(this.dossierId).subscribe({
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
    const roles = this.authService.getUserRoles?.() ?? [];
    if (roles.includes('ADMIN') || roles.includes('OPERATOR')) return true;

    const assigneeId = task.assigneeUserId ?? task.AssigneeUserId;
    const currentUserId = this.authService.getUserId();

    if (assigneeId && currentUserId && String(assigneeId) === String(currentUserId)) return true;
    if (assigneeId) return false;

    const statusId = this.dossierStatusId();
    if (statusId === 5) return true; // Returned, creator được phép gửi

    return false;
  }

  openFormActionDialog(btn: any) {
    this.pendingActionBtn.set(btn);
    this.formActionComment.set('');
    this.selectedNextUserId.set('');

    if (btn.requiresUser && !this.isRejectLabel(btn.label)) {
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
      ? this.service.resubmitWorkflow(this.dossierId, payload)
      : this.service.moveWorkflow(this.dossierId, payload);

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
