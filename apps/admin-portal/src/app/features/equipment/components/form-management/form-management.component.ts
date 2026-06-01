import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { CardModule } from 'primeng/card';
import { TextareaModule } from 'primeng/textarea';
import { EavFormService, EavFormTemplate } from '../../../../core/services/eav-form.service';

interface FormField {
  id: string;
  name: string;
  label: string;
  type: string;
  placeholder?: string;
  required: boolean;
  options?: string[];
  helpText?: string;
  width: number;
}

interface ToolboxItem {
  type: string;
  label: string;
  icon: string;
  description: string;
}

@Component({
  selector: 'app-form-management',
  standalone: true,
  imports: [
    CommonModule, 
    FormsModule, 
    ToastModule, 
    ButtonModule,
    InputTextModule,
    Select,
    CheckboxModule,
    CardModule,
    TextareaModule
  ],
  providers: [MessageService],
  template: `
    <div class="wf-page">
      <p-toast></p-toast>
      
      <!-- ────────────────── VIEW 1: DANH SÁCH BIỂU MẪU ────────────────── -->
      <div class="wf-card" *ngIf="viewState === 'list'">
        <!-- Breadcrumb -->
        <div class="breadcrumb">
          <i class="pi pi-home bc-icon"></i>
          <span class="bc-text">Trang chủ</span>
          <span class="bc-sep">/</span>
          <span class="bc-text">Quản trị hệ thống</span>
          <span class="bc-sep">/</span>
          <span class="bc-current">Quản lý biểu mẫu</span>
        </div>

        <p class="text-muted mb-4">
          Quản lý danh sách các biểu mẫu cấu hình thuộc tính EAV động cho thiết bị kỹ thuật điện của EVNHANOI.
        </p>

        <!-- Toolbar -->
        <div class="list-toolbar">
          <div class="toolbar-left">
            <input type="text" class="wf-search-input"
              placeholder="Tìm kiếm biểu mẫu..."
              [(ngModel)]="searchKeyword"
              (keyup.enter)="onSearch()" />
            <button class="btn-tim" (click)="onSearch()">
              <i class="pi pi-search"></i> Tìm
            </button>
          </div>
          <div class="toolbar-right">
            <button class="btn-green" (click)="onAddNew()">
              <i class="pi pi-plus"></i> Tạo biểu mẫu mới
            </button>
          </div>
        </div>

        <!-- Table -->
        <div class="wf-table-wrap">
          <table class="wf-table">
            <thead>
              <tr>
                <th style="width: 200px;">Mã số</th>
                <th>Tên biểu mẫu thuộc tính thiết bị</th>
                <th style="width: 120px; text-align: center;">Phiên bản</th>
                <th class="col-tt">Trạng thái</th>
                <th>Cập nhật lần cuối</th>
                <th class="col-hd" style="width: 250px;">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngIf="loading">
                <td colspan="6" class="skeleton-row" style="padding: 30px; text-align: center;">
                  <i class="pi pi-spin pi-spinner mr-2" style="font-size: 1.5rem; color: #002D72;"></i>
                  <span>Đang tải danh sách biểu mẫu...</span>
                </td>
              </tr>
              <tr *ngFor="let form of filteredForms; let i = index">
                <td><code class="text-muted">{{ form.id }}</code></td>
                <td><b class="wf-name-link" (click)="onEdit(form)">{{ form.name }}</b></td>
                <td style="text-align: center; font-family: monospace; font-weight: bold;">v{{ form.version }}.0</td>
                <td class="col-tt">
                  <span class="status-pill"
                    [class.status-active]="form.isActive"
                    [class.status-inactive]="!form.isActive">
                    <i class="pi pi-clock"></i>
                    {{ form.isActive ? 'Đang hoạt động' : 'Ngưng hoạt động' }}
                  </span>
                </td>
                <td>{{ form.createdAt | date:'dd/MM/yyyy HH:mm' }}</td>
                <td class="col-hd">
                  <button class="btn-outlined btn-small mr-1" (click)="onPreview(form)">
                    <i class="pi pi-eye mr-1"></i> Xem trước
                  </button>
                  <button class="act-btn act-edit" (click)="onEdit(form)" title="Chỉnh sửa">
                    <i class="pi pi-pencil"></i>
                  </button>
                  <button class="act-btn act-delete" 
                    *ngIf="form.isActive"
                    (click)="deactivateForm(form)" 
                    title="Vô hiệu hóa">
                    <i class="pi pi-times-circle"></i>
                  </button>
                </td>
              </tr>
              <tr *ngIf="filteredForms.length === 0 && !loading">
                <td colspan="6" class="empty-row">
                  <i class="pi pi-inbox"></i>
                  <div>Chưa có biểu mẫu động nào. Nhấp tạo biểu mẫu để thiết lập!</div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer" *ngIf="filteredForms.length > 0">
          <span class="record-count">Tổng số: <b>{{ filteredForms.length }}</b> bản ghi.</span>
        </div>
      </div>

      <!-- ────────────────── VIEW 2: CHI TIẾT BIỂU MẪU (THÊM / SỬA / XEM) ────────────────── -->
      <div *ngIf="viewState !== 'list'">
        <!-- Breadcrumb -->
        <div class="breadcrumb" style="margin-left: 12px; margin-bottom: 12px;">
          <i class="pi pi-home bc-icon"></i>
          <span class="bc-text" (click)="goToList()" style="cursor: pointer;">Trang chủ</span>
          <span class="bc-sep">/</span>
          <span class="bc-text" (click)="goToList()" style="cursor: pointer;">Quản lý biểu mẫu</span>
          <span class="bc-sep">/</span>
          <span class="bc-current">{{ detailTitle }}</span>
        </div>

        <!-- Builder & Editor Workspace -->
        <div class="form-builder-wrapper" *ngIf="viewState === 'add' || viewState === 'edit'">
          <!-- Top Header Bar -->
          <div class="builder-header shadow-1">
            <div class="header-left">
              <i class="pi pi-sliders-h header-icon" style="color: #002D72; background: rgba(0, 45, 114, 0.1); padding: 10px; border-radius: 8px;"></i>
              <div>
                <h2 class="m-0 text-lg font-bold" style="color: #002D72;">{{ detailTitle }}</h2>
                <p class="m-0 text-xs text-muted">Kéo thả các trường thông tin kỹ thuật để thiết lập biểu mẫu chuẩn hóa</p>
              </div>
            </div>
            <div class="header-right" style="display: flex; gap: 8px;">
              <p-button label="Quay lại" icon="pi pi-arrow-left" severity="secondary" [rounded]="true" (onClick)="goToList()"></p-button>
              <p-button label="Xem JSON" icon="pi pi-code" severity="secondary" [rounded]="true" (onClick)="showJson = !showJson"></p-button>
              <p-button [label]="isEditMode ? 'Cập nhật cấu hình' : 'Lưu biểu mẫu'" icon="pi pi-save" [rounded]="true" styleClass="btn-evn-blue" (onClick)="saveForm()"></p-button>
            </div>
          </div>

          <div class="builder-main-layout">
            <!-- Left Column: Toolbox -->
            <div class="toolbox-column">
              <p-card header="Thành phần biểu mẫu" styleClass="card-custom">
                <p class="text-xs text-muted mb-4">Nhấp hoặc kéo thả các loại trường dưới đây vào vùng thiết kế.</p>
                
                <div class="toolbox-list">
                  <div *ngFor="let item of toolboxItems" 
                       class="toolbox-item" 
                       draggable="true" 
                       (dragstart)="onToolboxDragStart($event, item.type)"
                       (click)="addNewField(item.type)">
                    <div class="item-icon-wrapper">
                      <i class="pi" [ngClass]="item.icon"></i>
                    </div>
                    <div class="item-text">
                      <span class="item-label font-bold text-sm" style="color: #374151;">{{ item.label }}</span>
                      <span class="item-desc text-xs text-muted">{{ item.description }}</span>
                    </div>
                  </div>
                </div>
              </p-card>
            </div>

            <!-- Center Column: Canvas Workspace -->
            <div class="canvas-column" (dragover)="onDragOver($event)" (drop)="onDrop($event)">
              <!-- General Info Card -->
              <div class="general-info-card card p-4 mb-4">
                <div class="form-group mb-3">
                  <label class="block text-xs font-bold text-muted uppercase tracking-wider mb-2">Tên biểu mẫu kỹ thuật <span class="required">*</span></label>
                  <input type="text" pInputText [(ngModel)]="formName" class="w-full form-title-input" placeholder="Nhập tên biểu mẫu..." />
                </div>
                <div class="form-group">
                  <label class="block text-xs font-bold text-muted uppercase tracking-wider mb-2">Mô tả chi tiết</label>
                  <textarea pTextarea [(ngModel)]="formDescription" rows="2" class="w-full form-desc-input" placeholder="Nhập mô tả về biểu mẫu này..."></textarea>
                </div>
              </div>

              <!-- Main Fields Area -->
              <div class="canvas-workspace card p-4">
                <div class="workspace-header mb-3 flex justify-between items-center" style="display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid #e2e8f0; padding-bottom: 10px;">
                  <span class="font-bold text-sm text-muted uppercase tracking-wider">Khu vực thiết kế giao diện (Vùng kéo thả)</span>
                  <span class="badge" style="background: #002D72; color: white; padding: 2px 8px; border-radius: 12px; font-size: 0.8rem;">{{ fields.length }} trường</span>
                </div>

                <div *ngIf="fields.length === 0" class="empty-workspace-state" style="display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 60px 0; border: 2px dashed #cbd5e1; border-radius: 8px;">
                  <i class="pi pi-clone text-4xl mb-3 text-muted"></i>
                  <p class="font-bold text-lg m-0">Vùng thiết kế đang trống</p>
                  <p class="text-xs text-muted mt-1">Kéo thả các trường từ cột trái vào đây, hoặc click trực tiếp để thêm mới.</p>
                </div>

                <div class="fields-list-container" *ngIf="fields.length > 0" style="display: flex; flex-wrap: wrap; gap: 14px;">
                  <div *ngFor="let field of fields; let i = index; trackBy: trackByFn" 
                       class="canvas-field-card"
                       [class.selected]="selectedFieldIndex === i"
                       [class.width-50]="field.width === 50"
                       [class.width-100]="field.width === 100"
                       draggable="true"
                       (dragstart)="onCanvasDragStart($event, i)"
                       (dragover)="onDragOver($event)"
                       (drop)="onCanvasDrop($event, i)"
                       (click)="selectField(i)">
                    
                    <div class="field-card-header" style="display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid #e2e8f0; padding-bottom: 6px; margin-bottom: 8px;">
                      <span class="drag-handle-indicator"><i class="pi pi-ellipsis-v"></i><i class="pi pi-ellipsis-v"></i></span>
                      <span class="field-type-badge text-xs uppercase font-bold" style="background: #f1f5f9; padding: 2px 6px; border-radius: 4px;">{{ field.type }}</span>
                      <div class="field-actions" style="display: flex; gap: 4px;">
                        <button class="act-btn act-edit" (click)="cloneField(i, $event)" title="Nhân bản" style="border: none; padding: 4px 6px;"><i class="pi pi-copy"></i></button>
                        <button class="act-btn act-delete" (click)="removeField(i, $event)" title="Xóa" style="border: none; padding: 4px 6px;"><i class="pi pi-trash"></i></button>
                      </div>
                    </div>

                    <div class="field-preview-body">
                      <label class="preview-label font-bold text-sm block mb-2" style="color: #002D72;">
                        {{ field.label || '(Trường chưa đặt tên)' }}
                        <span *ngIf="field.required" class="text-danger">*</span>
                      </label>

                      <div class="preview-input-container">
                        <input *ngIf="field.type === 'text'" type="text" pInputText class="w-full preview-input" [placeholder]="field.placeholder || ''" disabled />
                        <input *ngIf="field.type === 'number'" type="number" pInputText class="w-full preview-input" [placeholder]="field.placeholder || ''" disabled />
                        <input *ngIf="field.type === 'date'" type="date" pInputText class="w-full preview-input" disabled />
                        
                        <p-select *ngIf="field.type === 'dropdown'" 
                                  [options]="field.options || []" 
                                  [placeholder]="field.placeholder || 'Chọn giá trị...'"
                                  styleClass="w-full preview-input" 
                                  disabled>
                        </p-select>

                        <textarea *ngIf="field.type === 'textarea'" pTextarea class="w-full preview-input" rows="2" [placeholder]="field.placeholder || ''" disabled></textarea>

                        <div *ngIf="field.type === 'checkbox'" class="flex items-center gap-2 py-1" style="display: flex; align-items: center; gap: 8px;">
                          <input type="checkbox" disabled />
                          <span class="text-sm">{{ field.placeholder || 'Đồng ý / Xác nhận' }}</span>
                        </div>
                      </div>

                      <div *ngIf="field.helpText" class="text-xs text-muted mt-2">
                        <i class="pi pi-info-circle mr-1"></i> {{ field.helpText }}
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Right Column: Properties Configuration -->
            <div class="properties-column">
              <p-card header="Thuộc tính cấu hình" styleClass="card-custom">
                <div *ngIf="selectedFieldIndex === null" class="empty-properties-state" style="display: flex; flex-direction: column; align-items: center; justify-content: center; height: 200px; border: 2px dashed #cbd5e1; border-radius: 8px; color: #64748b; padding: 20px; text-align: center;">
                  <i class="pi pi-cog text-3xl mb-2"></i>
                  <p class="text-sm m-0">Chọn một trường trong vùng thiết kế để cấu hình chi tiết.</p>
                </div>

                <div *ngIf="selectedFieldIndex !== null" class="properties-form" style="display: flex; flex-direction: column; gap: 14px;">
                  <div class="property-section-title font-bold text-xs uppercase mb-2" style="border-bottom: 1px solid #cbd5e1; padding-bottom: 4px; color: #002D72;">Thông tin cơ bản</div>
                  
                  <div class="field">
                    <label class="block font-bold text-xs text-muted mb-1">Nhãn hiển thị (Label)</label>
                    <input type="text" pInputText [(ngModel)]="fields[selectedFieldIndex].label" class="w-full p-inputtext-sm" />
                  </div>

                  <div class="field">
                    <label class="block font-bold text-xs text-muted mb-1">Mã trường kỹ thuật (Key/Name)</label>
                    <input type="text" pInputText [(ngModel)]="fields[selectedFieldIndex].name" class="w-full p-inputtext-sm" />
                  </div>

                  <div class="field" *ngIf="fields[selectedFieldIndex].type !== 'checkbox'">
                    <label class="block font-bold text-xs text-muted mb-1">Gợi ý nhập liệu (Placeholder)</label>
                    <input type="text" pInputText [(ngModel)]="fields[selectedFieldIndex].placeholder" class="w-full p-inputtext-sm" />
                  </div>

                  <div class="field">
                    <label class="block font-bold text-xs text-muted mb-1">Dòng giải thích phụ (Help Text)</label>
                    <input type="text" pInputText [(ngModel)]="fields[selectedFieldIndex].helpText" class="w-full p-inputtext-sm" placeholder="Hiển thị dưới trường nhập liệu..." />
                  </div>

                  <div class="property-section-title font-bold text-xs uppercase my-2" style="border-bottom: 1px solid #cbd5e1; padding-bottom: 4px; color: #002D72;">Trình bày & Ràng buộc</div>

                  <div class="field">
                    <label class="block font-bold text-xs text-muted mb-1">Độ rộng hiển thị (Width)</label>
                    <p-select [options]="[{label: 'Chiều rộng đầy đủ (100%)', value: 100}, {label: 'Một nửa cột (50%)', value: 50}]" 
                              [(ngModel)]="fields[selectedFieldIndex].width" 
                              optionLabel="label" 
                              optionValue="value" 
                              styleClass="w-full p-inputtext-sm" 
                              appendTo="body">
                    </p-select>
                  </div>

                  <div class="field" style="display: flex; align-items: center; gap: 8px; margin-top: 10px;">
                    <p-checkbox [(ngModel)]="fields[selectedFieldIndex].required" [binary]="true" id="prop-required"></p-checkbox>
                    <label for="prop-required" class="font-bold text-sm cursor-pointer select-none">Bắt buộc nhập (Required)</label>
                  </div>

                  <!-- Options builder for Dropdown -->
                  <div *ngIf="fields[selectedFieldIndex].type === 'dropdown'" class="dropdown-options-builder" style="margin-top: 10px;">
                    <div class="property-section-title font-bold text-xs uppercase mb-2 flex justify-between items-center" style="display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid #cbd5e1; padding-bottom: 4px; color: #002D72;">
                      <span>Các lựa chọn</span>
                      <p-button label="Thêm" icon="pi pi-plus" size="small" [text]="true" (onClick)="addOption()"></p-button>
                    </div>

                    <div class="options-list" style="display: flex; flex-direction: column; gap: 8px; max-height: 150px; overflow-y: auto;">
                      <div *ngFor="let opt of fields[selectedFieldIndex].options; let oIdx = index; trackBy: trackByFn" style="display: flex; gap: 6px; align-items: center;">
                        <input type="text" pInputText [(ngModel)]="fields[selectedFieldIndex].options![oIdx]" class="w-full p-inputtext-sm" />
                        <p-button icon="pi pi-times" severity="danger" [rounded]="true" [text]="true" size="small" (onClick)="removeOption(oIdx)"></p-button>
                      </div>
                    </div>
                  </div>
                </div>
              </p-card>
            </div>
          </div>
        </div>

        <!-- Render & Preview Workspace -->
        <div class="wf-card" *ngIf="viewState === 'preview'">
          <div class="builder-header shadow-1" style="display: flex; justify-content: space-between; align-items: center; background: white; padding: 16px; border-radius: 8px; margin-bottom: 16px; border: 1px solid #e2e8f0;">
            <div class="header-left">
              <i class="pi pi-eye header-icon" style="color: #002D72; background: rgba(0, 45, 114, 0.1); padding: 10px; border-radius: 8px; font-size: 1.25rem;"></i>
              <div>
                <h2 class="m-0 text-lg font-bold" style="color: #002D72;">{{ detailTitle }}</h2>
                <p class="m-0 text-xs text-muted">{{ formDescription || 'Biểu mẫu thuộc tính EAV dành cho thiết bị số hóa EVNHANOI.' }}</p>
              </div>
            </div>
            <div class="header-right">
              <p-button label="Quay lại danh sách" icon="pi pi-arrow-left" [rounded]="true" styleClass="btn-evn-blue" (onClick)="goToList()"></p-button>
            </div>
          </div>

          <!-- Beautiful Interactive rendered fields -->
          <div class="card p-5" style="background: white; border-radius: 12px; border: 1px solid #e2e8f0;">
            <h3 class="font-bold text-lg mb-4" style="color: #002D72; border-bottom: 2px solid #FF6B00; padding-bottom: 8px; display: inline-block;">Thông số kỹ thuật thiết bị</h3>
            
            <div style="display: flex; flex-wrap: wrap; gap: 20px;">
              <div *ngFor="let field of fields" 
                   [style.width]="field.width === 50 ? 'calc(50% - 10px)' : '100%'" 
                   style="display: flex; flex-direction: column; gap: 6px;">
                
                <label class="font-semibold text-sm" style="color: #374151;">
                  {{ field.label }}
                  <span *ngIf="field.required" class="text-danger">*</span>
                </label>

                <div>
                  <input *ngIf="field.type === 'text'" type="text" pInputText class="w-full" [placeholder]="field.placeholder || ''" [(ngModel)]="simulatedValues[field.name]" />
                  <input *ngIf="field.type === 'number'" type="number" pInputText class="w-full" [placeholder]="field.placeholder || ''" [(ngModel)]="simulatedValues[field.name]" />
                  <input *ngIf="field.type === 'date'" type="date" pInputText class="w-full" [(ngModel)]="simulatedValues[field.name]" />
                  
                  <p-select *ngIf="field.type === 'dropdown'" 
                            [options]="field.options || []" 
                            [placeholder]="field.placeholder || 'Chọn giá trị...'"
                            [(ngModel)]="simulatedValues[field.name]"
                            styleClass="w-full" 
                            appendTo="body">
                  </p-select>

                  <textarea *ngIf="field.type === 'textarea'" pTextarea class="w-full" rows="3" [placeholder]="field.placeholder || ''" [(ngModel)]="simulatedValues[field.name]"></textarea>

                  <div *ngIf="field.type === 'checkbox'" style="display: flex; align-items: center; gap: 8px; padding: 8px 0;">
                    <input type="checkbox" [id]="'sim_' + field.id" [(ngModel)]="simulatedValues[field.name]" style="scale: 1.1; cursor: pointer;" />
                    <label [for]="'sim_' + field.id" class="text-sm select-none" style="cursor: pointer;">{{ field.placeholder || 'Đồng ý / Xác nhận' }}</label>
                  </div>
                </div>

                <div *ngIf="field.helpText" class="text-xs text-muted">
                  <i class="pi pi-info-circle mr-1"></i> {{ field.helpText }}
                </div>
              </div>
            </div>

            <!-- Submit button simulation -->
            <div style="margin-top: 30px; display: flex; justify-content: flex-end; border-top: 1px solid #e2e8f0; padding-top: 20px;">
              <p-button label="Kiểm nghiệm nhập liệu (Simulate)" icon="pi pi-check-circle" [rounded]="true" styleClass="btn-evn-blue" (onClick)="onSimulateSubmit()"></p-button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- JSON Modal/Viewer if clicked -->
    <div *ngIf="showJson" class="json-preview-overlay" (click)="showJson = false">
      <div class="json-preview-modal card p-4 shadow-4" (click)="$event.stopPropagation()">
        <div class="flex justify-between items-center mb-3" style="display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid #cbd5e1; padding-bottom: 10px;">
          <h3 class="m-0 font-bold" style="color: #002D72;">Cấu trúc JSON Biểu mẫu</h3>
          <p-button icon="pi pi-times" [rounded]="true" [text]="true" (onClick)="showJson = false"></p-button>
        </div>
        <pre class="json-content" style="background: #f8fafc; padding: 10px; border-radius: 6px; font-family: monospace; font-size: 0.8rem; overflow: auto; max-height: 400px; border: 1px solid #e2e8f0;">{{ { name: formName, description: formDescription, fields: fields } | json }}</pre>
      </div>
    </div>
  `,
  styles: [`
    .form-builder-wrapper {
      display: flex;
      flex-direction: column;
      height: calc(100vh - 120px);
      gap: 1.5rem;
    }
    .builder-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      background-color: white;
      padding: 1rem 1.5rem;
      border-radius: 12px;
      border: 1px solid #e2e8f0;
    }
    .builder-main-layout {
      display: grid;
      grid-template-columns: 280px 1fr 300px;
      gap: 1.5rem;
      flex: 1;
      min-height: 0;
    }
    .toolbox-column {
      min-height: 0;
      display: flex;
      flex-direction: column;
    }
    .toolbox-list {
      display: flex;
      flex-direction: column;
      gap: 10px;
      max-height: calc(100vh - 300px);
      overflow-y: auto;
    }
    .toolbox-item {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 10px 14px;
      border-radius: 8px;
      border: 1px solid #cbd5e1;
      background-color: #f8fafc;
      cursor: grab;
      transition: all 0.2s ease-in-out;
      user-select: none;
    }
    .toolbox-item:hover {
      border-color: #002D72;
      background-color: rgba(0, 45, 114, 0.05);
      transform: translateY(-2px);
    }
    .item-icon-wrapper {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 32px;
      height: 32px;
      border-radius: 6px;
      background-color: white;
      border: 1px solid #cbd5e1;
      color: #002D72;
    }
    .item-text {
      display: flex;
      flex-direction: column;
      flex: 1;
    }
    .canvas-column {
      display: flex;
      flex-direction: column;
      min-height: 0;
      overflow-y: auto;
    }
    .general-info-card {
      background-color: white;
      border-radius: 12px;
      border: 1px solid #e2e8f0;
    }
    .form-title-input {
      font-size: 1.15rem;
      font-weight: 700;
      border: none !important;
      border-bottom: 2px dashed #cbd5e1 !important;
      border-radius: 0;
      background: transparent;
      padding: 6px 0;
    }
    .form-title-input:focus {
      border-color: #002D72 !important;
      box-shadow: none !important;
    }
    .canvas-workspace {
      background-color: white;
      border-radius: 12px;
      border: 1px solid #e2e8f0;
      flex: 1;
      min-height: 400px;
    }
    .canvas-field-card {
      background-color: #f8fafc;
      border: 1px solid #cbd5e1;
      border-radius: 8px;
      padding: 14px;
      position: relative;
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .canvas-field-card.width-100 {
      width: 100%;
    }
    .canvas-field-card.width-50 {
      width: calc(50% - 7px);
    }
    .canvas-field-card.selected {
      border-color: #002D72;
      background-color: rgba(0, 45, 114, 0.02);
      box-shadow: 0 0 0 2px rgba(0, 45, 114, 0.15);
    }
    .canvas-field-card.selected::before {
      content: '';
      position: absolute;
      left: 0;
      top: 0;
      bottom: 0;
      width: 4px;
      background-color: #002D72;
      border-radius: 8px 0 0 8px;
    }
    .drag-handle-indicator {
      cursor: move;
      color: #94a3b8;
      margin-right: 6px;
    }
    .field-preview-body {
      pointer-events: none;
    }
    .preview-input {
      background-color: white !important;
      border: 1px solid #cbd5e1 !important;
      opacity: 0.85;
    }
    .properties-column {
      min-height: 0;
      display: flex;
      flex-direction: column;
    }
    ::ng-deep .btn-evn-blue {
      background-color: #002D72 !important;
      border-color: #002D72 !important;
      color: white !important;
    }
    ::ng-deep .btn-evn-blue:hover {
      background-color: #001e4e !important;
      border-color: #001e4e !important;
    }
    .text-danger {
      color: #ef4444;
      margin-left: 2px;
    }
    .json-preview-overlay {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background-color: rgba(0, 0, 0, 0.4);
      backdrop-filter: blur(2px);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 1200;
    }
    .json-preview-modal {
      width: 500px;
      background: white;
      border-radius: 8px;
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }
  `]
})
export class FormManagementComponent implements OnInit {
  // Navigation & States
  viewState: 'list' | 'add' | 'edit' | 'preview' = 'list';
  detailTitle = '';
  isEditMode = false;
  
  // Forms list state
  forms: EavFormTemplate[] = [];
  filteredForms: EavFormTemplate[] = [];
  searchKeyword = '';
  loading = false;

  // Active builder/preview states
  templateId: string | null = null;
  formName = '';
  formDescription = '';
  fields: FormField[] = [];
  selectedFieldIndex: number | null = null;
  showJson = false;

  // Simulation Preview values
  simulatedValues: { [key: string]: any } = {};

  // Drag & drop status
  draggedType: string | null = null;
  draggedIndex: number | null = null;

  // Toolbox configuration
  toolboxItems: ToolboxItem[] = [
    { type: 'text', label: 'Trường Văn bản (Text)', icon: 'pi-align-left', description: 'Tên thiết bị, số seri, hãng sản xuất...' },
    { type: 'number', label: 'Số liệu kỹ thuật (Number)', icon: 'pi-percentage', description: 'Điện áp định mức, công suất, dòng điện...' },
    { type: 'date', label: 'Ngày kiểm định (Date)', icon: 'pi-calendar', description: 'Ngày đưa vào vận hành, ngày thí nghiệm...' },
    { type: 'dropdown', label: 'Danh sách Lựa chọn (Dropdown)', icon: 'pi-chevron-down', description: 'Loại cách điện, cấp điện áp...' },
    { type: 'textarea', label: 'Mô tả / Ghi chú (Textarea)', icon: 'pi-align-justify', description: 'Tình trạng kỹ thuật, ghi chú khác...' },
    { type: 'checkbox', label: 'Hộp kiểm xác nhận (Checkbox)', icon: 'pi-check-square', description: 'Đã nghiệm thu, đạt tiêu chuẩn...' }
  ];

  private eavFormService = inject(EavFormService);
  private messageService = inject(MessageService);

  ngOnInit() {
    this.loadForms();
  }

  loadForms() {
    this.loading = true;
    this.eavFormService.getTemplates().subscribe({
      next: (data) => {
        this.forms = data || [];
        this.filteredForms = [...this.forms];
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading forms', err);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi tải dữ liệu',
          detail: 'Không thể kết nối đến API Gateway để tải biểu mẫu.'
        });
        this.loading = false;
      }
    });
  }

  onSearch() {
    if (this.searchKeyword) {
      const term = this.searchKeyword.toLowerCase();
      this.filteredForms = this.forms.filter(f =>
        f.name.toLowerCase().includes(term) ||
        f.id.toLowerCase().includes(term)
      );
    } else {
      this.filteredForms = [...this.forms];
    }
  }

  goToList() {
    this.viewState = 'list';
    this.templateId = null;
    this.loadForms();
  }

  // --- ACTIONS ---
  onAddNew() {
    this.viewState = 'add';
    this.isEditMode = false;
    this.detailTitle = 'Thêm mới Biểu mẫu thuộc tính thiết bị';
    this.formName = 'Biểu mẫu thiết bị mới';
    this.formDescription = 'Định nghĩa thông số kỹ thuật EAV cho thiết bị điện';
    this.fields = [
      {
        id: 'f_1',
        name: 'ten_thiet_bi',
        label: 'Tên thiết bị kỹ thuật',
        type: 'text',
        placeholder: 'Nhập tên thiết bị...',
        required: true,
        width: 100
      },
      {
        id: 'f_2',
        name: 'dien_ap_dinh_muc',
        label: 'Điện áp định mức (kV)',
        type: 'number',
        placeholder: 'Ví dụ: 110, 220, 500...',
        required: true,
        width: 50
      }
    ];
    this.selectedFieldIndex = 0;
    this.showJson = false;
  }

  onEdit(form: EavFormTemplate) {
    this.viewState = 'edit';
    this.isEditMode = true;
    this.templateId = form.id;
    this.detailTitle = `Chỉnh sửa cấu hình Biểu mẫu: ${form.name}`;
    this.formName = form.name;
    this.formDescription = form.description;
    this.showJson = false;

    try {
      this.fields = JSON.parse(form.schema) || [];
      this.selectedFieldIndex = this.fields.length > 0 ? 0 : null;
    } catch (e) {
      console.error('Failed to parse form schema', e);
      this.fields = [];
      this.selectedFieldIndex = null;
    }
  }

  onPreview(form: EavFormTemplate) {
    this.viewState = 'preview';
    this.templateId = form.id;
    this.detailTitle = `Xem trước Biểu mẫu: ${form.name}`;
    this.formName = form.name;
    this.formDescription = form.description;
    this.simulatedValues = {};

    try {
      this.fields = JSON.parse(form.schema) || [];
      // Initialize simulated checkboxes to false
      this.fields.forEach(f => {
        if (f.type === 'checkbox') {
          this.simulatedValues[f.name] = false;
        } else {
          this.simulatedValues[f.name] = '';
        }
      });
    } catch (e) {
      this.fields = [];
    }
  }

  deactivateForm(form: EavFormTemplate) {
    if (confirm(`Bạn có chắc chắn muốn vô hiệu hóa biểu mẫu: ${form.name}?`)) {
      this.eavFormService.deleteTemplate(form.id).subscribe({
        next: () => {
          this.messageService.add({ 
            severity: 'success', 
            summary: 'Thành công', 
            detail: `Đã vô hiệu hóa biểu mẫu thành công!` 
          });
          this.loadForms();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể vô hiệu hóa biểu mẫu.'
          });
        }
      });
    }
  }

  // --- HTML5 Drag & Drop ---
  onToolboxDragStart(event: DragEvent, type: string) {
    this.draggedType = type;
    this.draggedIndex = null;
    if (event.dataTransfer) {
      event.dataTransfer.setData('text/plain', type);
      event.dataTransfer.effectAllowed = 'copy';
    }
  }

  onCanvasDragStart(event: DragEvent, index: number) {
    this.draggedIndex = index;
    this.draggedType = null;
    if (event.dataTransfer) {
      event.dataTransfer.setData('text/plain', index.toString());
      event.dataTransfer.effectAllowed = 'move';
    }
  }

  onDragOver(event: DragEvent) {
    event.preventDefault();
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    if (this.draggedType) {
      this.addNewField(this.draggedType);
    }
    this.draggedType = null;
  }

  onCanvasDrop(event: DragEvent, targetIndex: number) {
    event.preventDefault();
    event.stopPropagation();
    
    if (this.draggedType) {
      this.addNewFieldAtIndex(this.draggedType, targetIndex);
      this.draggedType = null;
    } else if (this.draggedIndex !== null && this.draggedIndex !== targetIndex) {
      const movedField = this.fields.splice(this.draggedIndex, 1)[0];
      this.fields.splice(targetIndex, 0, movedField);
      this.selectedFieldIndex = targetIndex;
      this.draggedIndex = null;
    }
  }

  addNewField(type: string) {
    const newField: FormField = this.createDefaultField(type);
    this.fields.push(newField);
    this.selectedFieldIndex = this.fields.length - 1;
  }

  addNewFieldAtIndex(type: string, index: number) {
    const newField: FormField = this.createDefaultField(type);
    this.fields.splice(index, 0, newField);
    this.selectedFieldIndex = index;
  }

  createDefaultField(type: string): FormField {
    const id = 'f_' + Math.random().toString(36).substring(2, 9);
    let label = 'Trường mới';
    let name = 'truong_moi';
    let options: string[] | undefined = undefined;

    switch (type) {
      case 'text':
        label = 'Trường Văn bản';
        name = 'truong_van_ban';
        break;
      case 'number':
        label = 'Thông số kỹ thuật';
        name = 'thong_so_ky_thuat';
        break;
      case 'date':
        label = 'Ngày tháng';
        name = 'ngay_thang';
        break;
      case 'dropdown':
        label = 'Danh mục lựa chọn';
        name = 'danh_muc_lua_chon';
        options = ['Lựa chọn A', 'Lựa chọn B'];
        break;
      case 'textarea':
        label = 'Đoạn mô tả ngắn';
        name = 'doan_mo_ta';
        break;
      case 'checkbox':
        label = 'Xác nhận kiểm tra';
        name = 'xac_nhan_kiem_tra';
        break;
    }

    return {
      id,
      name: name + '_' + Math.floor(Math.random() * 1000),
      label,
      type,
      placeholder: 'Nhập giá trị...',
      required: false,
      options,
      width: 100
    };
  }

  selectField(index: number) {
    this.selectedFieldIndex = index;
  }

  removeField(index: number, event: Event) {
    event.stopPropagation();
    this.fields.splice(index, 1);
    if (this.selectedFieldIndex === index) {
      this.selectedFieldIndex = this.fields.length > 0 ? 0 : null;
    } else if (this.selectedFieldIndex !== null && this.selectedFieldIndex > index) {
      this.selectedFieldIndex--;
    }
  }

  cloneField(index: number, event: Event) {
    event.stopPropagation();
    const sourceField = this.fields[index];
    const cloned: FormField = {
      ...sourceField,
      id: 'f_' + Math.random().toString(36).substring(2, 9),
      name: sourceField.name + '_copy',
      label: sourceField.label + ' (Bản sao)'
    };
    if (sourceField.options) {
      cloned.options = [...sourceField.options];
    }
    this.fields.splice(index + 1, 0, cloned);
    this.selectedFieldIndex = index + 1;
  }

  addOption() {
    if (this.selectedFieldIndex !== null) {
      const field = this.fields[this.selectedFieldIndex];
      if (!field.options) {
        field.options = [];
      }
      field.options.push('Lựa chọn mới ' + (field.options.length + 1));
    }
  }

  removeOption(optIndex: number) {
    if (this.selectedFieldIndex !== null) {
      const field = this.fields[this.selectedFieldIndex];
      if (field.options) {
        field.options.splice(optIndex, 1);
      }
    }
  }

  trackByFn(index: number, item: any) {
    return item.id;
  }

  saveForm() {
    if (!this.formName.trim()) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập tên biểu mẫu.' });
      return;
    }

    if (this.fields.length === 0) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng thêm ít nhất một trường vào biểu mẫu.' });
      return;
    }

    const schemaStr = JSON.stringify(this.fields);
    
    if (this.isEditMode && this.templateId) {
      this.eavFormService.updateTemplate(this.templateId, this.formName, this.formDescription, schemaStr).subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: 'Đã cập nhật cấu hình biểu mẫu EAV thành công!'
          });
          setTimeout(() => {
            this.goToList();
          }, 800);
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể nâng cấp cấu hình biểu mẫu.'
          });
        }
      });
    } else {
      this.eavFormService.createTemplate(this.formName, this.formDescription, schemaStr).subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: 'Đã lưu biểu mẫu động mới thành công!'
          });
          setTimeout(() => {
            this.goToList();
          }, 800);
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể tạo mới biểu mẫu.'
          });
        }
      });
    }
  }

  // --- PREVIEW SIMULATOR ACTION ---
  onSimulateSubmit() {
    // Check required fields
    const missingFields: string[] = [];
    this.fields.forEach(f => {
      if (f.required) {
        const val = this.simulatedValues[f.name];
        if (val === undefined || val === null || val === '') {
          missingFields.push(f.label);
        }
      }
    });

    if (missingFields.length > 0) {
      this.messageService.add({
        severity: 'error',
        summary: 'Kiểm nghiệm lỗi',
        detail: `Vui lòng điền các trường bắt buộc: ${missingFields.join(', ')}`
      });
    } else {
      this.messageService.add({
        severity: 'success',
        summary: 'Kiểm nghiệm thành công',
        detail: 'Dữ liệu nhập liệu mô phỏng hoàn toàn đạt chuẩn cấu trúc!'
      });
    }
  }
}
