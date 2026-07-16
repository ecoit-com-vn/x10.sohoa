import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
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
import { ActivatedRoute, Router } from '@angular/router';
import { FormTemplateService, EavFormTemplate } from '../../data-access/form-template.service';
import { EquipmentTypeService } from '../../data-access/equipment-type.service';
import { ToggleSwitch } from 'primeng/toggleswitch';

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
  dataSourceType?: 'manual' | 'catalog';
  catalogType?: string;
  description?: string;
  selectAll?: boolean;
  active?: boolean;
}

interface ToolboxItem {
  type: string;
  label: string;
  icon: string;
}

@Component({
  selector: 'app-form-builder',
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
    TextareaModule,
    ToggleSwitch,
    WfBreadcrumbComponent,
  ],
  providers: [MessageService],
  templateUrl: './form-builder.component.html',
  styleUrl: './form-builder.component.scss'
})
export class FormBuilderComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private formTemplateService = inject(FormTemplateService);
  private equipmentTypeService = inject(EquipmentTypeService);
  private messageService = inject(MessageService);

  templateId = signal<string | null>(null);
  isEditMode = signal<boolean>(false);
  loading = signal<boolean>(false);
  activeTab = signal<'info' | 'builder'>('info');

  formName = signal<string>('');
  formCode = signal<string>('');
  formCategory = signal<string>('');
  formDescription = signal<string>('');
  formDescriptionInfo = signal<string>('');
  extractionProcess = signal<string>('');
  fields = signal<FormField[]>([]);
  selectedFieldIndex = signal<number | null>(null);
  showJson = signal<boolean>(false);
  fieldSearchQuery = signal<string>('');

  isFieldSearchMatched(field: FormField): boolean {
    const query = this.fieldSearchQuery().trim().toLowerCase();
    if (!query) return false;
    return (field.label || '').toLowerCase().includes(query) || 
           (field.name || '').toLowerCase().includes(query) || 
           (field.type || '').toLowerCase().includes(query);
  }
  gridTypes = signal<any[]>([]);
  selectedGridTypeId = signal<number | null>(null);
  equipmentTypeId = signal<string>('');
  equipmentTypes = signal<any[]>([]);

  filteredEquipmentTypes = computed(() => {
    const gridId = this.selectedGridTypeId();
    if (!gridId) {
      return [];
    }
    return this.equipmentTypes().filter(et => et.gridTypeId === Number(gridId));
  });

  categories = [
    { code: 'MAY_BIEN_AP', name: 'Máy biến áp' },
    { code: 'MAY_CAT', name: 'Máy cắt' },
    { code: 'DAO_CACH_LY', name: 'Dao cách ly' },
    { code: 'BIEN_DIEN_AP', name: 'Biến điện áp (TU)' },
    { code: 'BIEN_DONG_DIEN', name: 'Biến dòng điện (TI)' },
    { code: 'CAP_DIEN_LUC', name: 'Cáp điện lực' },
    { code: 'TU_TRUNG_THE', name: 'Tủ trung thế' },
    { code: 'THIET_BI_DO_LUONG', name: 'Thiết bị đo lường' },
    { code: 'KHAC', name: 'Hạng mục khác' }
  ];

  toolboxItems: ToolboxItem[] = [
    { type: 'text', label: 'Trường Văn bản (Text)', icon: 'pi-align-left' },
    { type: 'number', label: 'Số liệu kỹ thuật (Number)', icon: 'pi-percentage' },
    { type: 'date', label: 'Ngày kiểm định (Date)', icon: 'pi-calendar' },
    { type: 'dropdown', label: 'Danh sách Lựa chọn (Dropdown)', icon: 'pi-chevron-down' },
    { type: 'radio', label: 'Lựa chọn duy nhất (Radio)', icon: 'pi-circle-fill' },
    { type: 'textarea', label: 'Mô tả / Ghi chú (Textarea)', icon: 'pi-align-justify' },
    { type: 'checkbox', label: 'Hộp kiểm xác nhận (Checkbox)', icon: 'pi-check-square' }
  ];

  draggedType: string | null = null;
  draggedIndex: number | null = null;
  catalogTypes = signal<any[]>([]);

  ngOnInit() {
    this.loadCatalogTypes();
    this.loadGridTypes();
    this.loadEquipmentTypes();
    this.route.queryParams.subscribe(params => {
      this.activeTab.set('info');
      this.fieldSearchQuery.set('');
      if (params['id']) {
        const id = params['id'];
        this.templateId.set(id);
        this.isEditMode.set(true);
        this.loadTemplate(id);
      } else {
        this.isEditMode.set(false);
        this.templateId.set(null);
        this.resetForm();
      }
    });
  }

  loadEquipmentTypes() {
    this.equipmentTypeService.getEquipmentTypes(1, 1000, undefined, undefined, undefined, true).subscribe({
      next: (res) => {
        if (res && res.items) {
          this.equipmentTypes.set(res.items);
          // Backwards compatibility mapping if template is already loaded but equipmentTypeId is empty
          if (!this.equipmentTypeId() && this.formCategory()) {
            const matched = res.items.find((t: any) => t.code === this.formCategory());
            if (matched) {
              this.equipmentTypeId.set(matched.id);
            }
          }
        }
      },
      error: (err) => {
        console.error('Failed to load equipment types', err);
      }
    });
  }

  loadTemplate(id: string) {
    this.loading.set(true);
    this.formTemplateService.getTemplateById(id).subscribe({
      next: (form) => {
        this.formName.set(form.name);
        this.formCode.set(form.code || '');
        this.formCategory.set(form.category || '');
        this.formDescription.set(form.description || '');
        this.formDescriptionInfo.set(form.descriptionInfo || '');
        this.extractionProcess.set(form.extractionProcess || '');
        this.equipmentTypeId.set(form.equipmentTypeId || '');
        
        // Load gridTypeId if available, else fall back to equipment type mapping
        this.selectedGridTypeId.set(form.gridTypeId || null);
        
        // Backwards compatibility mapping if equipment types are already loaded
        if (!form.equipmentTypeId && form.category && this.equipmentTypes().length > 0) {
          const matched = this.equipmentTypes().find(t => t.code === form.category);
          if (matched) {
            this.equipmentTypeId.set(matched.id);
          }
        }

        try {
          const parsedFields = JSON.parse(form.formSchema || '[]') || [];
          this.fields.set(parsedFields);
          this.selectedFieldIndex.set(parsedFields.length > 0 ? 0 : null);
        } catch (e) {
          console.error('Failed to parse form schema', e);
          this.fields.set([]);
          this.selectedFieldIndex.set(null);
        }
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load template', err);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải chi tiết cấu hình form.'
        });
        this.loading.set(false);
      }
    });
  }

  loadCatalogTypes() {
    this.formTemplateService.getCatalogTypes().subscribe({
      next: (types) => {
        this.catalogTypes.set(types || []);
      },
      error: (err) => {
        console.error('Failed to load catalog types', err);
        this.catalogTypes.set([
          { code: 'HANG_SAN_XUAT', name: 'Hãng sản xuất' },
          { code: 'CAP_DIEN_AP', name: 'Cấp điện áp' },
          { code: 'TINH_TRANG_VH', name: 'Tình trạng vận hành' },
          { code: 'DON_VI', name: 'Đơn vị quản lý' },
          { code: 'CHUC_VU', name: 'Chức vụ' }
        ]);
      }
    });
  }

  resetForm() {
    this.formName.set('');
    this.formCode.set('');
    this.formCategory.set('');
    this.formDescription.set('');
    this.formDescriptionInfo.set('');
    this.extractionProcess.set('');
    this.equipmentTypeId.set('');
    this.fields.set([]);
    this.selectedFieldIndex.set(null);
    this.showJson.set(false);
    this.selectedGridTypeId.set(null);
  }

  goToList() {
    this.router.navigate(['/equipment/form-template']);
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
      const sourceIndex = this.draggedIndex;
      this.fields.update(currentFields => {
        const updated = [...currentFields];
        const movedField = updated.splice(sourceIndex, 1)[0];
        updated.splice(targetIndex, 0, movedField);
        return updated;
      });
      this.selectedFieldIndex.set(targetIndex);
      this.draggedIndex = null;
    }
  }

  addNewField(type: string) {
    const newField: FormField = this.createDefaultField(type);
    this.fields.update(currentFields => [...currentFields, newField]);
    this.selectedFieldIndex.set(this.fields().length - 1);
  }

  addNewFieldAtIndex(type: string, index: number) {
    const newField: FormField = this.createDefaultField(type);
    this.fields.update(currentFields => {
      const updated = [...currentFields];
      updated.splice(index, 0, newField);
      return updated;
    });
    this.selectedFieldIndex.set(index);
  }

  createDefaultField(type: string): FormField {
    const id = 'f_' + Math.random().toString(36).substring(2, 9);
    return {
      id,
      name: '',
      label: '',
      type,
      placeholder: '',
      required: false,
      options: (type === 'dropdown' || type === 'radio' || type === 'checkbox') ? [] : undefined,
      width: 100,
      dataSourceType: 'manual',
      selectAll: false,
      active: true
    };
  }

  onDataSourceTypeChange(newType: 'manual' | 'catalog') {
    const idx = this.selectedFieldIndex();
    if (idx !== null) {
      this.fields.update(currentFields => {
        const updated = [...currentFields];
        const updatedField = { ...updated[idx], dataSourceType: newType };
        if (newType === 'catalog') {
          updatedField.options = [];
          if (!updatedField.catalogType && this.catalogTypes().length > 0) {
            updatedField.catalogType = this.catalogTypes()[0].code;
          }
        } else {
          if (!updatedField.options || updatedField.options.length === 0) {
            updatedField.options = ['Lựa chọn A', 'Lựa chọn B'];
          }
          updatedField.catalogType = undefined;
        }
        updated[idx] = updatedField;
        return updated;
      });
    }
  }

  selectField(index: number) {
    this.selectedFieldIndex.set(index);
  }

  removeField(index: number, event: Event) {
    event.stopPropagation();
    this.fields.update(currentFields => {
      const updated = [...currentFields];
      updated.splice(index, 1);
      return updated;
    });
    const currentSelected = this.selectedFieldIndex();
    if (currentSelected === index) {
      this.selectedFieldIndex.set(this.fields().length > 0 ? 0 : null);
    } else if (currentSelected !== null && currentSelected > index) {
      this.selectedFieldIndex.set(currentSelected - 1);
    }
  }

  cloneField(index: number, event: Event) {
    event.stopPropagation();
    const sourceField = this.fields()[index];
    const cloned: FormField = {
      ...sourceField,
      id: 'f_' + Math.random().toString(36).substring(2, 9),
      name: sourceField.name + '_copy',
      label: sourceField.label + ' (Bản sao)'
    };
    if (sourceField.options) {
      cloned.options = [...sourceField.options];
    }
    this.fields.update(currentFields => {
      const updated = [...currentFields];
      updated.splice(index + 1, 0, cloned);
      return updated;
    });
    this.selectedFieldIndex.set(index + 1);
  }

  addOption() {
    const idx = this.selectedFieldIndex();
    if (idx !== null) {
      this.fields.update(currentFields => {
        const updated = [...currentFields];
        const field = { ...updated[idx] };
        if (!field.options) {
          field.options = [];
        }
        field.options = [...field.options, 'Lựa chọn mới ' + (field.options.length + 1)];
        updated[idx] = field;
        return updated;
      });
    }
  }

  removeOption(optIndex: number) {
    const idx = this.selectedFieldIndex();
    if (idx !== null) {
      this.fields.update(currentFields => {
        const updated = [...currentFields];
        const field = { ...updated[idx] };
        if (field.options) {
          const opts = [...field.options];
          opts.splice(optIndex, 1);
          field.options = opts;
        }
        updated[idx] = field;
        return updated;
      });
    }
  }

  trackByFn(index: number, item: any) {
    return item.id;
  }

  updateSelectedField(key: keyof FormField, value: any) {
    const idx = this.selectedFieldIndex();
    if (idx !== null) {
      this.fields.update(currentFields => {
        const updated = [...currentFields];
        updated[idx] = { ...updated[idx], [key]: value };
        return updated;
      });
    }
  }

  updateSelectedFieldOption(optIndex: number, value: string) {
    const idx = this.selectedFieldIndex();
    if (idx !== null) {
      this.fields.update(currentFields => {
        const updated = [...currentFields];
        const field = { ...updated[idx] };
        if (field.options) {
          const opts = [...field.options];
          opts[optIndex] = value;
          field.options = opts;
        }
        updated[idx] = field;
        return updated;
      });
    }
  }

  saveForm() {
    const fName = this.formName().trim();
    const fCode = this.formCode().trim();
    const eqTypeId = this.equipmentTypeId();
    const gridId = this.selectedGridTypeId();

    if (!fName) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập tên form.' });
      return;
    }
    if (!fCode) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập mã form.' });
      return;
    }
    if (!gridId) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng chọn loại lưới điện.' });
      return;
    }
    if (!eqTypeId) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng chọn loại thiết bị.' });
      return;
    }

    // Resolve the category code from the selected equipment type ID
    const selectedEqType = this.equipmentTypes().find(t => t.id === eqTypeId);
    const fCategory = selectedEqType ? selectedEqType.code : '';

    if (!fCategory) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng chọn loại thiết bị hợp lệ.' });
      return;
    }

    const currentFields = this.fields();
    if (currentFields.length === 0) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng thêm ít nhất một trường vào form.' });
      return;
    }

    const schemaStr = JSON.stringify(currentFields);
    const desc = this.formDescription();
    const fDescInfo = this.formDescriptionInfo();
    const extractProc = this.extractionProcess();
    const isEdit = this.isEditMode();
    const tId = this.templateId();
    
    if (isEdit && tId) {
      this.formTemplateService.updateTemplate(tId, fName, fCode, fCategory, desc, fDescInfo, schemaStr, 'admin', eqTypeId, gridId || undefined, extractProc).subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: 'Đã cập nhật cấu hình form EAV thành công!'
          });
          setTimeout(() => {
            this.goToList();
          }, 800);
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể nâng cấp cấu hình form.'
          });
        }
      });
    } else {
      this.formTemplateService.createTemplate(fName, fCode, fCategory, desc, fDescInfo, schemaStr, 'admin', eqTypeId, gridId || undefined, extractProc).subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: 'Đã lưu form động mới thành công!'
          });
          setTimeout(() => {
            this.goToList();
          }, 800);
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể tạo mới form.'
          });
        }
      });
    }
  }

  loadGridTypes() {
    this.equipmentTypeService.getGridTypesLookup().subscribe({
      next: (types) => {
        this.gridTypes.set(types || []);
      },
      error: (err) => {
        console.error('Failed to load grid types', err);
      }
    });
  }

}
