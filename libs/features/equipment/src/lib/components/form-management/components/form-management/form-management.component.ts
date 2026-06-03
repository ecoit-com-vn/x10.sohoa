import { Component, OnInit, inject, signal, computed } from '@angular/core';
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
import { EavFormService, EavFormTemplate } from '@sohoa.frontend/shared/core';
import { finalize } from 'rxjs';

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
  templateUrl: './form-management.component.html',
  styleUrl: './form-management.component.scss'
})
export class FormManagementComponent implements OnInit {
  // Navigation & States
  viewState = signal<'list' | 'add' | 'edit' | 'preview'>('list');
  detailTitle = signal<string>('');
  isEditMode = signal<boolean>(false);
  
  // Forms list state
  forms = signal<EavFormTemplate[]>([]);
  searchKeyword = signal<string>('');
  loading = signal<boolean>(false);

  // Active builder/preview states
  templateId = signal<string | null>(null);
  formName = signal<string>('');
  formDescription = signal<string>('');
  fields = signal<FormField[]>([]);
  selectedFieldIndex = signal<number | null>(null);
  showJson = signal<boolean>(false);

  // Simulation Preview values
  simulatedValues = signal<{ [key: string]: any }>({});

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

  filteredForms = computed(() => {
    const keyword = this.searchKeyword().trim().toLowerCase();
    const allForms = this.forms();
    if (!keyword) {
      return allForms;
    }
    return allForms.filter(f =>
      (f.name?.toLowerCase().includes(keyword) ?? false) ||
      (f.id?.toLowerCase().includes(keyword) ?? false)
    );
  });

  ngOnInit() {
    this.loadForms();
  }

  loadForms() {
    this.loading.set(true);
    this.eavFormService.getTemplates()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (data) => {
          this.forms.set(data || []);
        },
        error: (err) => {
          console.error('Error loading forms', err);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi tải dữ liệu',
            detail: 'Không thể kết nối đến API Gateway để tải biểu mẫu.'
          });
          this.forms.set([]);
        }
      });
  }

  onSearch() {
    // Search is handled reactively by computed signal filteredForms
  }

  goToList() {
    this.viewState.set('list');
    this.templateId.set(null);
    this.loadForms();
  }

  // --- ACTIONS ---
  onAddNew() {
    this.viewState.set('add');
    this.isEditMode.set(false);
    this.detailTitle.set('Thêm mới Biểu mẫu thuộc tính thiết bị');
    this.formName.set('Biểu mẫu thiết bị mới');
    this.formDescription.set('Định nghĩa thông số kỹ thuật EAV cho thiết bị điện');
    this.fields.set([
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
    ]);
    this.selectedFieldIndex.set(0);
    this.showJson.set(false);
  }

  onEdit(form: EavFormTemplate) {
    this.viewState.set('edit');
    this.isEditMode.set(true);
    this.templateId.set(form.id);
    this.detailTitle.set(`Chỉnh sửa cấu hình Biểu mẫu: ${form.name}`);
    this.formName.set(form.name);
    this.formDescription.set(form.description);
    this.showJson.set(false);

    try {
      const parsedFields = JSON.parse(form.schema) || [];
      this.fields.set(parsedFields);
      this.selectedFieldIndex.set(parsedFields.length > 0 ? 0 : null);
    } catch (e) {
      console.error('Failed to parse form schema', e);
      this.fields.set([]);
      this.selectedFieldIndex.set(null);
    }
  }

  onPreview(form: EavFormTemplate) {
    this.viewState.set('preview');
    this.templateId.set(form.id);
    this.detailTitle.set(`Xem trước Biểu mẫu: ${form.name}`);
    this.formName.set(form.name);
    this.formDescription.set(form.description);
    
    const initialSimulated: { [key: string]: any } = {};

    try {
      const parsedFields = JSON.parse(form.schema) || [];
      this.fields.set(parsedFields);
      // Initialize simulated checkboxes to false
      parsedFields.forEach((f: FormField) => {
        if (f.type === 'checkbox') {
          initialSimulated[f.name] = false;
        } else {
          initialSimulated[f.name] = '';
        }
      });
    } catch {
      this.fields.set([]);
    }
    this.simulatedValues.set(initialSimulated);
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
        error: () => {
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

  updateSimulatedValue(name: string, value: any) {
    this.simulatedValues.update(prev => ({
      ...prev,
      [name]: value
    }));
  }

  saveForm() {
    const fName = this.formName().trim();
    if (!fName) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập tên biểu mẫu.' });
      return;
    }

    const currentFields = this.fields();
    if (currentFields.length === 0) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng thêm ít nhất một trường vào biểu mẫu.' });
      return;
    }

    const schemaStr = JSON.stringify(currentFields);
    const desc = this.formDescription();
    const isEdit = this.isEditMode();
    const tId = this.templateId();
    
    if (isEdit && tId) {
      this.eavFormService.updateTemplate(tId, fName, desc, schemaStr).subscribe({
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
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể nâng cấp cấu hình biểu mẫu.'
          });
        }
      });
    } else {
      this.eavFormService.createTemplate(fName, desc, schemaStr).subscribe({
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
        error: () => {
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
    const vals = this.simulatedValues();
    this.fields().forEach(f => {
      if (f.required) {
        const val = vals[f.name];
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
