import { Component, OnInit, signal, inject, Output, EventEmitter, Input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
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
import { finalize, forkJoin } from 'rxjs';
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
        <button type="button" *ngIf="isFormTabVisible('info')" class="tab-item" [class.tab-active]="activeTab() === 'info'" (click)="activeTab.set('info')">
          <i class="pi pi-info-circle" style="margin-right: 6px;"></i>
          Thông tin hồ sơ
        </button>
        <button type="button" *ngIf="isFormTabVisible('documents')" class="tab-item" [class.tab-active]="activeTab() === 'documents'" (click)="activeTab.set('documents')">
          <i class="pi pi-file" style="margin-right: 6px;"></i>
          Tài liệu đính kèm
        </button>
        <button type="button" *ngIf="isFormTabVisible('versions')" class="tab-item" [class.tab-active]="activeTab() === 'versions'" (click)="activeTab.set('versions')">
          <i class="pi pi-history" style="margin-right: 6px;"></i>
          Lịch sử phiên bản
        </button>
        <button type="button" *ngIf="isFormTabVisible('workflow')" class="tab-item" [class.tab-active]="activeTab() === 'workflow'" (click)="activeTab.set('workflow')">
          <i class="pi pi-sitemap" style="margin-right: 6px;"></i>
          Quy trình & Lịch sử
        </button>
      </div>

      <div class="tab-content" style="position: relative;">
      <div *ngIf="!isEditMode() || activeTab() === 'info'">
      <!-- Thông tin vị trí: Nhóm / Trạm-ĐZ / Hộp lưu trữ (cùng 1 hàng) → (Thiết bị) -->
      <div style="display: flex; flex-direction: column; gap: 16px; margin-bottom: 24px;">
        <h3 style="font-size: 0.95rem; font-weight: 700; color: #002D72; padding-bottom: 8px; border-bottom: 1px solid #e2e8f0; margin: 0;">Thông tin vị trí</h3>

        <div style="display: flex; gap: 16px; flex-wrap: wrap;">
          <div class="form-group" style="flex: 1 1 240px; min-width: 220px;">
            <label class="form-label">Nhóm hồ sơ <span class="required">*</span></label>
            <p-select
              [options]="dossierGroups()"
              [(ngModel)]="dossier.dossierGroupId"
              (ngModelChange)="onDossierGroupChange($event)"
              optionLabel="name"
              optionValue="id"
              [filter]="true"
              filterBy="name,code"
              [showClear]="false"
              placeholder="-- Chọn nhóm hồ sơ --"
              appendTo="body"
              styleClass="w-full"
              [style]="{'width':'100%'}">
            </p-select>
          </div>

          <div class="form-group" style="flex: 1 1 240px; min-width: 220px;">
            <label class="form-label">Trạm / Đường dây</label>
            <p-multiSelect
              [options]="formInfrastructures()"
              [(ngModel)]="dossier.infrastructureIds"
              (ngModelChange)="onInfrastructureChange($event)"
              optionLabel="displayLabel"
              optionValue="id"
              [filter]="true"
              filterBy="name,code,displayLabel"
              [showClear]="true"
              placeholder="-- Chọn trạm / đường dây --"
              appendTo="body"
              styleClass="w-full"
              [style]="{'width':'100%'}">
            </p-multiSelect>
          </div>

          <div class="form-group" style="flex: 1 1 240px; min-width: 220px;">
            <label class="form-label">Hộp lưu trữ</label>
            <div class="storage-tree-picker">
              <div class="storage-tree-trigger"
                [class.has-value]="!!selectedStorageBoxId()"
                [class.open]="storageTreeOpen()"
                (click)="toggleStorageTree($event)">
                <span class="storage-tree-selected-label">
                  <i class="pi pi-box" style="margin-right: 6px; color: #6b7280;"></i>
                  {{ storageSelectionLabel() || '-- Chọn hộp (kệ / tầng / hộp) --' }}
                </span>
                <span style="display: inline-flex; align-items: center; gap: 4px; flex-shrink: 0;">
                  <button *ngIf="selectedStorageBoxId()" type="button" class="storage-tree-clear"
                    title="Bỏ chọn" (click)="clearStorageSelection($event)">
                    <i class="pi pi-times"></i>
                  </button>
                  <i class="pi" [class.pi-chevron-down]="!storageTreeOpen()"
                    [class.pi-chevron-up]="storageTreeOpen()"></i>
                </span>
              </div>
              <div class="storage-tree-dropdown" *ngIf="storageTreeOpen()" (click)="$event.stopPropagation()">
                <div *ngIf="storageTree().length === 0" class="storage-tree-empty">
                  Chưa có kệ/tầng/hộp theo đơn vị hiện tại.
                </div>
                <ng-container *ngFor="let shelf of storageTree()">
                  <div class="storage-tree-node" [style.padding-left.px]="8">
                    <button type="button" class="storage-tree-expand-btn"
                      *ngIf="shelf.floors?.length"
                      (click)="toggleStorageNode('s-' + shelf.id, $event)">
                      <i class="pi"
                        [class.pi-chevron-right]="!isStorageNodeExpanded('s-' + shelf.id)"
                        [class.pi-chevron-down]="isStorageNodeExpanded('s-' + shelf.id)"></i>
                    </button>
                    <span class="storage-tree-node-spacer" *ngIf="!shelf.floors?.length"></span>
                    <span class="storage-tree-node-label storage-tree-level">
                      {{ shelf.name }} <code>({{ shelf.code }})</code>
                    </span>
                  </div>
                  <ng-container *ngIf="isStorageNodeExpanded('s-' + shelf.id)">
                    <ng-container *ngFor="let floor of shelf.floors || []">
                      <div class="storage-tree-node" [style.padding-left.px]="22">
                        <button type="button" class="storage-tree-expand-btn"
                          *ngIf="floor.boxes?.length"
                          (click)="toggleStorageNode('f-' + floor.id, $event)">
                          <i class="pi"
                            [class.pi-chevron-right]="!isStorageNodeExpanded('f-' + floor.id)"
                            [class.pi-chevron-down]="isStorageNodeExpanded('f-' + floor.id)"></i>
                        </button>
                        <span class="storage-tree-node-spacer" *ngIf="!floor.boxes?.length"></span>
                        <span class="storage-tree-node-label storage-tree-level">
                          {{ floor.name }} <code>({{ floor.code }})</code>
                        </span>
                      </div>
                      <ng-container *ngIf="isStorageNodeExpanded('f-' + floor.id)">
                        <div class="storage-tree-node storage-tree-leaf"
                          *ngFor="let box of floor.boxes || []"
                          [style.padding-left.px]="36"
                          [class.selected]="selectedStorageBoxId() == box.id"
                          (click)="selectStorageBox(shelf, floor, box)">
                          <span class="storage-tree-node-spacer"></span>
                          <span class="storage-tree-node-label">
                            {{ box.name }} <code>({{ box.code }})</code>
                          </span>
                        </div>
                      </ng-container>
                    </ng-container>
                  </ng-container>
                </ng-container>
              </div>
            </div>
            <p style="font-size: 0.75rem; color: #6b7280; margin: 6px 0 0 0;">
              Không bắt buộc. Chỉ lưu khi chọn đến hộp.
            </p>
          </div>
        </div>

        <div class="form-group" *ngIf="isEquipmentDossier()">
          <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
            <label class="form-label" style="margin: 0;">Thiết bị liên quan <span class="required">*</span></label>
            <button type="button" (click)="openAddEquipmentDialog()" class="btn-outlined btn-small">
              <i class="pi pi-plus"></i> Thêm
            </button>
          </div>

          <div *ngIf="selectedEquipments().length === 0" style="padding: 16px; background: #f8fafc; border: 1px dashed #e2e8f0; border-radius: 8px; text-align: center; color: #9ca3af; font-size: 0.85rem;">
            Bắt buộc chọn ít nhất một thiết bị.
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
                    <button type="button" (click)="removeEquipment(eq)" class="act-btn act-delete" title="Bỏ thiết bị"><i class="pi pi-times"></i></button>
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
          [kindId]="kindIdSignal()"
          [menuScope]="'creator'"
          [hasFormTemplate]="!!selectedFormId()"
          [formId]="selectedFormId()"
          (formDataSaved)="loadDossierDetail(dossierId!)"
        ></app-dossier-documents-tab>
      </div>

      <div *ngIf="isEditMode() && activeTab() === 'versions'">
        <app-dossier-versions-tab [dossierId]="dossierId!" />
      </div>

      <div *ngIf="isEditMode() && activeTab() === 'workflow'">
        <app-dossier-workflow-tab [dossierId]="dossierId!" [kindId]="kindIdSignal()" />
      </div>

      <!-- Loading — chỉ che tab thông tin, không chặn tab Tài liệu -->
      <div *ngIf="loading() && (!isEditMode() || activeTab() === 'info')" style="position: absolute; inset: 0; background: rgba(255,255,255,0.6); display: flex; align-items: center; justify-content: center; z-index: 10; border-radius: 8px; pointer-events: none;">
        <i class="pi pi-spin pi-spinner" style="font-size: 2rem; color: #002D72;"></i>
      </div>
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
          <select class="wf-select" [value]="selectedNextUser()" (change)="onNextUserChange($event)"
            [disabled]="loadingEligibleSubmitUsers()">
            <option value="">-- Chọn người phê duyệt --</option>
            <option *ngFor="let u of filteredSubmitNextUsers()" [value]="u.id || u.Id || u.userId || u.username">
              {{ u.fullName || u.FullName || u.name || u.username }}
            </option>
          </select>
          <div style="margin-top: 4px; font-size: 0.8rem; color: #64748b;" *ngIf="nextStepInfo()?.staticAssigneeId">Bước này có cấu hình giao việc đích danh — đã chọn sẵn, có thể đổi người khác nếu cần.</div>
          <div style="margin-top: 4px; font-size: 0.8rem; color: #64748b;" *ngIf="loadingEligibleSubmitUsers()">Đang tải danh sách người đủ điều kiện...</div>
          <div style="margin-top: 4px; font-size: 0.8rem; color: #64748b;" *ngIf="!loadingEligibleSubmitUsers() && filteredSubmitNextUsers().length === 0">
            Không có người dùng nào đủ điều kiện xử lý bước này.
          </div>
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
          <select class="wf-select w-full" [ngModel]="selectedNextUserId()" (ngModelChange)="selectedNextUserId.set($event)"
            [disabled]="loadingEligibleFormNextUsers()">
            <option value="" disabled selected>-- Chọn người xử lý --</option>
            <option *ngFor="let u of filteredFormNextUsers()" [value]="u.id || u.Id || u.userId || u.username">{{ u.fullName || u.name }} ({{ u.username }})</option>
          </select>
          <div style="margin-top: 4px; font-size: 0.8rem; color: #64748b;" *ngIf="pendingActionBtn()?.staticAssigneeId">Bước này có cấu hình giao việc đích danh — đã chọn sẵn, có thể đổi người khác nếu cần.</div>
          <div style="margin-top: 4px; font-size: 0.8rem; color: #64748b;" *ngIf="loadingEligibleFormNextUsers()">Đang tải danh sách người đủ điều kiện...</div>
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
    .storage-tree-picker { position: relative; width: 100%; }
    .storage-tree-trigger {
      display: flex; align-items: center; justify-content: space-between;
      height: 34px; padding: 0 10px;
      border: 1px solid #d1d5db; border-radius: 5px;
      cursor: pointer; background: #ffffff;
      font-size: 0.85rem; color: #94a3b8; user-select: none; outline: none;
    }
    .storage-tree-trigger.has-value { color: #374151; }
    .storage-tree-trigger.open, .storage-tree-trigger:hover { border-color: #9ca3af; }
    .storage-tree-selected-label {
      overflow: hidden; text-overflow: ellipsis; white-space: nowrap; flex: 1;
    }
    .storage-tree-clear {
      border: none; background: transparent; width: 18px; height: 18px;
      cursor: pointer; color: #6b7280; display: inline-flex; align-items: center; justify-content: center;
      font-size: 0.75rem;
    }
    .storage-tree-dropdown {
      position: absolute; top: calc(100% + 4px); left: 0; right: 0; z-index: 40;
      background: #fff; border: 1px solid #d1d5db; border-radius: 5px;
      box-shadow: 0 4px 12px rgba(0,0,0,.08); max-height: 240px; overflow-y: auto;
    }
    .storage-tree-empty { padding: 12px; text-align: center; color: #6b7280; font-size: 0.85rem; }
    .storage-tree-node {
      display: flex; align-items: center; gap: 4px; padding: 6px 8px; margin: 1px 4px;
      border-radius: 4px; cursor: default;
    }
    .storage-tree-node.selected { background: #eff6ff; cursor: pointer; }
    .storage-tree-node.selected .storage-tree-node-label { color: #002D72; font-weight: 600; }
    .storage-tree-node:not(.selected) .storage-tree-node-label.storage-tree-level { color: #6b7280; font-weight: 500; }
    .storage-tree-leaf { cursor: pointer; }
    .storage-tree-node:hover { background: #f8fafc; }
    .storage-tree-expand-btn {
      display: inline-flex; align-items: center; justify-content: center;
      width: 18px; height: 18px; border: none; background: transparent; cursor: pointer; flex-shrink: 0;
    }
    .storage-tree-expand-btn .pi { font-size: 0.65rem; color: #6b7280; }
    .storage-tree-node-spacer { display: inline-block; width: 18px; height: 18px; flex-shrink: 0; }
    .storage-tree-node-label {
      font-size: 0.85rem; color: #374151; flex: 1;
      overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
    }
    .storage-tree-node-label code { font-size: 0.85rem; color: #6b7280; font-family: inherit; }
  `]
})
export class DossierFormComponent implements OnInit {
  @Input() dossierId: string | null = null;
  @Input() set kindId(value: number | undefined) {
    const id = normalizeDossierKindId(value, this.kindIdSignal());
    this.kindIdSignal.set(id);
    this.service.setKindContext(id);
  }
  kindIdSignal = signal<number>(2);
  @Output() cancel = new EventEmitter<void>();
  @Output() saved = new EventEmitter<string>();

  private service = inject(DossierManagementService);
  private messageService = inject(MessageService);
  private authService = inject(AuthService);
  private eavFormService = inject(EavFormService);
  private workflowSvc = inject(WorkflowService);

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
  eligibleFormNextUsers = signal<any[]>([]);
  loadingEligibleFormNextUsers = signal<boolean>(false);

  // Ưu tiên cấu hình bước ĐÍCH: Nhóm quyền đơn vị > Nhóm quyền hệ thống > requiredRole cũ > toàn bộ user.
  // Giao việc đích danh không giới hạn danh sách — chỉ chọn sẵn mặc định (xem openFormActionDialog).
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
    this.workflowSvc.getEligibleAssignees(groupParams.systemGroupIds, groupParams.unitGroupIds, unitId)
      .pipe(finalize(() => this.loadingEligibleFormNextUsers.set(false)))
      .subscribe({
        next: (list) => this.eligibleFormNextUsers.set(Array.isArray(list) ? list : []),
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

  // Ưu tiên cấu hình bước tiếp theo: Nhóm quyền đơn vị > Nhóm quyền hệ thống > (cũ) requiredRole > toàn bộ user.
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
    const unitId = info.requireSameUnit ? (this.authService.getUserUnitId() ?? undefined) : undefined;
    this.loadingEligibleSubmitUsers.set(true);
    this.workflowSvc.getEligibleAssignees(groupParams.systemGroupIds, groupParams.unitGroupIds, unitId)
      .pipe(finalize(() => this.loadingEligibleSubmitUsers.set(false)))
      .subscribe({
        next: (list) => this.eligibleSubmitUsers.set(Array.isArray(list) ? list : []),
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

  selectedEquipments = signal<any[]>([]);
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

  selectedDossierGroup = computed(() => {
    const id = this.dossierGroupIdSignal();
    if (id == null) return null;
    return this.dossierGroups().find(g => Number(g.id ?? g.Id) === Number(id)) ?? null;
  });

  isEquipmentDossier = computed(() => {
    const g = this.selectedDossierGroup();
    if (!g) return false;
    const flag = g.isEquipmentDossier ?? g.IsEquipmentDossier;
    return flag === true || flag === 1 || flag === '1';
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

  // Dialog State
  showEquipmentDialog = false;
  equipmentKeyword = '';
  equipmentSearchResults = signal<any[]>([]);
  searchingEquipments = signal<boolean>(false);
  /** Lựa chọn tạm trong popup — chỉ áp dụng vào hồ sơ khi bấm Lưu. */
  dialogSelectedEquipments = signal<any[]>([]);

  constructor() {
    if (typeof window !== 'undefined') {
      window.addEventListener('click', () => this.storageTreeOpen.set(false));
    }
  }

  ngOnInit() {
    this.loadLookups();
    if (this.dossierId) {
      this.loadDossierDetail(this.dossierId);
    }
  }

  loadLookups() {
    this.service.getDossierTypeLookup().subscribe(res => this.dossierTypes.set(res || []));
    this.service.getDossierGroupLookup().subscribe(res => this.dossierGroups.set(res || []));
    this.service.getGridTypeLookup().subscribe(res => this.gridTypes.set(res || []));
    this.service.getDossierSets().subscribe(res => this.dossierSets.set(res || []));
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
      const items = (res || []).map((inf: any) => this.enrichInfrastructureOption(inf));
      const selectedId = this.dossier.infrastructureId;
      if (selectedId && !items.some((inf: any) => (inf.id ?? inf.Id) === selectedId)) {
        const existing = this.infrastructures().find((inf) => (inf.id ?? inf.Id) === selectedId);
        if (existing) {
          items.push(this.enrichInfrastructureOption(existing));
        }
      }
      this.infrastructures.set(items);
    });
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

  /** Giữ option trạm/đường dây hiện tại khi sửa hồ sơ (tránh mất giá trị đã lưu). */
  private ensureInfrastructureOption(detail: Record<string, unknown>) {
    const infraId = (detail['infrastructureId'] ?? detail['InfrastructureId']) as string | null | undefined;
    if (!infraId) return;

    const exists = this.infrastructures().some(
      (inf) => (inf.id ?? inf.Id) === infraId
    );
    if (exists) return;

    const group = this.selectedDossierGroup();
    const fallbackInfraType = group
      ? Number(group.infraTypeId ?? group.InfraTypeId)
      : Number(detail['infraTypeId'] ?? detail['InfraTypeId'] ?? 0) || null;

    this.infrastructures.update((list) => [
      ...list,
      this.enrichInfrastructureOption({
        id: infraId,
        name: (detail['infrastructureName'] ?? detail['InfrastructureName'] ?? infraId) as string,
        code: detail['infrastructureCode'] ?? detail['InfrastructureCode'],
        gridTypeId: detail['gridTypeId'] ?? detail['GridTypeId'],
        infraTypeId: fallbackInfraType,
      }),
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
          this.dossierStatus.set(String(res.status ?? res.Status ?? ''));
          this.dossierStatusId.set(Number(res.statusId ?? res.StatusId ?? 0));
          this.workflowInstanceId.set(res.workflowInstanceId ?? res.WorkflowInstanceId ?? null);
          if (res.workflowInstanceId ?? res.WorkflowInstanceId) {
            this.loadWorkflow();
          }
          this.formGridTypeId.set(this.dossier.gridTypeId);
          this.dossierGroupIdSignal.set(this.dossier.dossierGroupId);
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

    if (this.dossier.infrastructureIds && this.dossier.infrastructureIds.length > 0) {
      const allowed = this.infrastructures()
        .filter(inf => numericId == null || Number(inf.gridTypeId ?? inf.GridTypeId) === numericId)
        .map(inf => inf.id ?? inf.Id);
      this.dossier.infrastructureIds = this.dossier.infrastructureIds.filter(id => allowed.includes(id));
      this.dossier.infrastructureId = this.dossier.infrastructureIds[0] ?? null;
      this.selectedEquipments.set([]);
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

    if (!this.isEquipmentDossier()) {
      this.selectedEquipments.set([]);
    }

    this.tryAutoGenerateDossierCode();
  }

  onInfrastructureChange(selectedIds: any) {
    if (this.loading()) return;
    const ids: string[] = Array.isArray(selectedIds) ? selectedIds : (selectedIds ? [selectedIds] : []);
    this.dossier.infrastructureIds = ids;
    this.dossier.infrastructureId = ids[0] ?? null;

    // Đổi trạm/đường dây -> clear danh sách thiết bị đã chọn
    this.selectedEquipments.set([]);
    this.tryAutoGenerateDossierCode();
  }

  /**
   * Tự sinh mã hồ sơ theo công thức: <Mã nhóm hs>.<Mã trạm/ĐZ đầu tiên>.<Mã loại hồ sơ>
   * Điền vào trường có mã "CODE" trong EAV form nếu ở chế độ Tạo mới (hoặc khi đổi tiêu chí).
   */
  private tryAutoGenerateDossierCode() {
    if (this.isEditMode()) return;
    const fields = this.dynamicFields();
    if (!fields.length) return;

    const codeField = fields.find(
      (f) => f.key?.toUpperCase() === 'CODE' || f.name?.toUpperCase() === 'CODE' || f.id?.toUpperCase() === 'CODE'
    );
    if (!codeField) return;

    // 1. Mã nhóm hồ sơ
    const group = this.selectedDossierGroup();
    const groupCode = group ? String(group.code ?? group.Code ?? '').trim() : '';

    // 2. Mã trạm/đường dây phần tử đầu tiên
    const firstInfraId = this.dossier.infrastructureIds?.[0] || this.dossier.infrastructureId;
    const firstInfra = this.infrastructures().find((inf) => guidsEqual(inf.id ?? inf.Id, firstInfraId));
    const infraCode = firstInfra ? String(firstInfra.code ?? firstInfra.Code ?? '').trim() : '';

    // 3. Mã loại hồ sơ
    const typeId = this.dossier.dossierTypeId;
    const dossierType = this.dossierTypes().find((t) => guidsEqual(t.id ?? t.Id, typeId));
    const typeCode = dossierType ? String(dossierType.code ?? dossierType.Code ?? '').trim() : '';

    if (groupCode && infraCode && typeCode) {
      const generatedCode = `${groupCode}.${infraCode}.${typeCode}`;
      this.formData[codeField.key] = generatedCode;
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
           this.tryAutoGenerateDossierCode();
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
    if (this.isEquipmentDossier() && this.selectedEquipments().length === 0) return false;
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

  onCompleteInput(): void {
    if (!this.dossier.id || this.completingInput()) return;

    this.completingInput.set(true);
    this.service.completeInput(this.dossier.id).subscribe({
      next: () => {
        this.dossierStatus.set('CompletedInput');
        this.dossierStatusId.set(2);
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
      } else if (this.isEquipmentDossier() && this.selectedEquipments().length === 0) {
        detail = 'Hồ sơ thiết bị bắt buộc chọn ít nhất một thiết bị';
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
      equipmentIds: this.isEquipmentDossier()
        ? this.selectedEquipments().map(e => e.equipmentId || e.id)
        : [],
      formDataJson: this.dynamicFields().length > 0
        ? serializeFormDataForSchema(this.dynamicFields(), this.toSerializableFormData())
        : undefined,
      // Chỉ gửi vị trí khi đã chọn đến hộp; ngược lại null để BE clear.
      shelfId: hasBox ? this.dossier.shelfId : null,
      floorId: hasBox ? this.dossier.floorId : null,
      boxId: hasBox ? this.dossier.boxId : null,
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
    this.selectedNextUserId.set(resolveDefaultNextAssignee(btn));

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
