import { Component, OnInit, signal, inject, Output, EventEmitter, Input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { DialogModule } from 'primeng/dialog';
import { DossierManagementService } from '../../data-access/dossier-management.service';
import {
  EavField,
  normalizeField,
  pickFormDataForSchema,
  serializeFormDataForSchema,
} from '../../utils/dossier-form-schema.util';

@Component({
  selector: 'app-dossier-form',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, DialogModule],
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
          <button (click)="onSave()" class="btn-save" [disabled]="isSaving() || !isValid()">
            <i class="pi pi-save" *ngIf="!isSaving()"></i>
            <i class="pi pi-spin pi-spinner" *ngIf="isSaving()"></i>
            Lưu thông tin
          </button>
        </div>
      </div>

      <!-- Thông tin vị trí + Thiết bị liên quan -->
      <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 24px; margin-bottom: 24px;">

        <!-- Cột trái: Thông tin vị trí -->
        <div style="display: flex; flex-direction: column; gap: 16px;">
          <h3 style="font-size: 0.95rem; font-weight: 700; color: #002D72; padding-bottom: 8px; border-bottom: 1px solid #e2e8f0; margin: 0;">Thông tin vị trí</h3>

          <div class="form-group">
            <label class="form-label">Loại lưới điện</label>
            <select class="wf-select w-full" [(ngModel)]="dossier.gridTypeId" (change)="loadInfrastructures()">
              <option [ngValue]="null">-- Chọn loại lưới điện --</option>
              <option *ngFor="let item of gridTypes()" [value]="item.id">{{ item.name }}</option>
            </select>
          </div>

          <div class="form-group">
            <label class="form-label">Trạm / Đường dây</label>
            <select class="wf-select w-full" [(ngModel)]="dossier.infrastructureId">
              <option [ngValue]="null">-- Chọn trạm/đường dây --</option>
              <option *ngFor="let item of infrastructures()" [value]="item.id">{{ item.name }}</option>
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

                <input *ngSwitchCase="'date'" type="date" class="wf-input w-full"
                       [name]="'dyn_' + field.key"
                       [(ngModel)]="formData[field.key]">

                <textarea *ngSwitchCase="'textarea'" class="wf-textarea w-full" rows="3"
                          autocomplete="off"
                          [name]="'dyn_' + field.key"
                          [placeholder]="field.placeholder || ''"
                          [(ngModel)]="formData[field.key]"></textarea>

                <select *ngSwitchCase="'select'" class="wf-select w-full"
                        [name]="'dyn_' + field.key"
                        [(ngModel)]="formData[field.key]">
                  <option value="">-- Chọn --</option>
                  <option *ngFor="let opt of field.options" [value]="opt.value">{{ opt.label }}</option>
                </select>

                <label *ngSwitchCase="'checkbox'" style="display: flex; align-items: center; gap: 8px; cursor: pointer; margin-top: 4px;">
                  <input type="checkbox"
                         [name]="'dyn_' + field.key"
                         [(ngModel)]="formData[field.key]"
                         style="width: 16px; height: 16px; accent-color: #002D72; cursor: pointer;">
                  <span style="font-size: 0.9rem;">{{ field.placeholder || field.label }}</span>
                </label>

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

      <!-- Loading Overlay -->
      <div *ngIf="loading()" style="position: absolute; inset: 0; background: rgba(255,255,255,0.6); display: flex; align-items: center; justify-content: center; z-index: 50; border-radius: 12px;">
        <i class="pi pi-spin pi-spinner" style="font-size: 2rem; color: #002D72;"></i>
      </div>
    </div>

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
              <tr style="cursor: pointer;" (click)="toggleEquipmentSelection(eq)">
                <td class="col-chk">
                  <input type="checkbox" [checked]="isEquipmentSelected(eq)" (change)="toggleEquipmentSelection(eq)"
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
        <button class="btn-cancel btn-small" (click)="showEquipmentDialog = false"><i class="pi pi-times"></i> Đóng</button>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .w-full { width: 100%; }
  `]
})
export class DossierFormComponent implements OnInit {
  @Input() dossierId: string | null = null;
  @Output() cancel = new EventEmitter<void>();
  @Output() saved = new EventEmitter<string>();

  private service = inject(DossierManagementService);
  private messageService = inject(MessageService);

  isEditMode = computed(() => !!this.dossierId);
  loading = signal<boolean>(false);
  isSaving = signal<boolean>(false);
  loadingForm = signal<boolean>(false);

  dossier = {
    id: '',
    dossierTypeId: '',
    gridTypeId: null as number | null,
    infrastructureId: null as string | null,
    dossierSetId: null as string | null,
    rowVersion: 1
  };

  selectedEquipments = signal<any[]>([]);

  // Lookups
  dossierTypes = signal<any[]>([]);
  gridTypes = signal<any[]>([]);
  infrastructures = signal<any[]>([]);
  dossierSets = signal<any[]>([]);

  // Dynamic form state
  dynamicFields = signal<EavField[]>([]);
  formData: Record<string, any> = {};
  selectedFormId = signal<string | null>(null);
  selectedTypeName = computed(() => {
    const found = this.dossierTypes().find(t => t.id === this.dossier.dossierTypeId);
    return found?.name ?? '';
  });

  // Dialog State
  showEquipmentDialog = false;
  equipmentKeyword = '';
  equipmentSearchResults = signal<any[]>([]);
  searchingEquipments = signal<boolean>(false);

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
  }

  loadInfrastructures() {
    this.service.getInfrastructureLookup().subscribe(res => this.infrastructures.set(res || []));
  }

  loadDossierDetail(id: string) {
    this.loading.set(true);
    this.service.getDossierById(id).subscribe({
      next: (res) => {
        if (res) {
          this.dossier = {
            id: res.id,
            dossierTypeId: res.dossierTypeId,
            gridTypeId: res.gridTypeId,
            infrastructureId: res.infrastructureId,
            dossierSetId: res.dossierSetId,
            rowVersion: res.rowVersion
          };
          this.selectedEquipments.set(res.equipments || []);

          // Load form và điền dữ liệu đã lưu
          if (res.dossierTypeId) {
            this.loadFormForType(res.dossierTypeId, res.formDataJson);
          }
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

  /** Tìm formId từ dossierType rồi gọi API lấy form template */
  private loadFormForType(typeId: string, existingFormDataJson?: string) {
    const found = this.dossierTypes().find(t => t.id === typeId);
    const formId: string | null = found?.formId ?? null;

    if (!formId) {
      this.selectedFormId.set('');
      this.dynamicFields.set([]);
      return;
    }

    this.selectedFormId.set(formId);
    this.loadingForm.set(true);

    this.service.getFormTemplate(formId).subscribe({
      next: (template) => {
        this.loadingForm.set(false);
        if (!template?.formSchema) {
          this.dynamicFields.set([]);
          return;
        }

        try {
          const raw = JSON.parse(template.formSchema);
          const fields: EavField[] = Array.isArray(raw) ? raw.map((f) => normalizeField(f)) : [];
          this.dynamicFields.set(fields);

          if (existingFormDataJson) {
            try {
              const saved = JSON.parse(existingFormDataJson) as Record<string, unknown>;
              this.formData = pickFormDataForSchema(fields, saved);
            } catch { /* bỏ qua nếu JSON lỗi */ }
          }
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

  isValid() {
    return !!this.dossier.dossierTypeId;
  }

  onSave() {
    if (!this.isValid()) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Vui lòng chọn loại hồ sơ' });
      return;
    }

    this.isSaving.set(true);
    const dto = {
      ...this.dossier,
      equipmentIds: this.selectedEquipments().map(e => e.equipmentId || e.id),
      formDataJson: this.dynamicFields().length > 0
        ? serializeFormDataForSchema(this.dynamicFields(), this.formData)
        : undefined
    };

    const req$ = this.isEditMode()
      ? this.service.updateDossier(this.dossier.id, dto)
      : this.service.createDossier(dto);

    req$.subscribe({
      next: (res) => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu thông tin hồ sơ' });
        this.saved.emit(this.isEditMode() ? this.dossier.id : (res.id || res));
        this.isSaving.set(false);
      },
      error: (err) => {
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
    this.showEquipmentDialog = true;
    this.equipmentKeyword = '';
    this.searchEquipments();
  }

  searchEquipments() {
    this.searchingEquipments.set(true);
    this.service.getEquipmentLookup({
      keyword: this.equipmentKeyword,
      infrastructureId: this.dossier.infrastructureId || undefined,
      gridTypeId: this.dossier.gridTypeId || undefined,
      pageSize: 50
    }).subscribe({
      next: (res) => {
        this.equipmentSearchResults.set(res.items || []);
        this.searchingEquipments.set(false);
      },
      error: () => {
        this.searchingEquipments.set(false);
      }
    });
  }

  isEquipmentSelected(eq: any): boolean {
    return this.selectedEquipments().some(s => (s.equipmentId || s.id) === eq.id);
  }

  toggleEquipmentSelection(eq: any) {
    const currentList = [...this.selectedEquipments()];
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
    this.selectedEquipments.set(currentList);
  }

  removeEquipment(eq: any) {
    const eqId = eq.equipmentId || eq.id;
    this.selectedEquipments.set(this.selectedEquipments().filter(s => (s.equipmentId || s.id) !== eqId));
  }

  trackByFieldKey(_index: number, field: EavField): string {
    return field.key;
  }
}
