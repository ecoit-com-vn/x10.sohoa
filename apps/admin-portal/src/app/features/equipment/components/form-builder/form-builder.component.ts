import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { CardModule } from 'primeng/card';
import { TextareaModule } from 'primeng/textarea';
import { Tooltip } from 'primeng/tooltip';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { ActivatedRoute, Router } from '@angular/router';
import { EavFormService } from '../../../../core/services/eav-form.service';

interface FormField {
  id: string;
  name: string;
  label: string;
  type: string;
  placeholder?: string;
  required: boolean;
  options?: string[]; // for dropdown/select type
  helpText?: string;
  width: number; // grid width percentage (50% or 100%)
}

interface ToolboxItem {
  type: string;
  label: string;
  icon: string;
  description: string;
}

@Component({
  selector: 'app-form-builder',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    InputTextModule,
    Select,
    CheckboxModule,
    CardModule,
    TextareaModule,
    Tooltip,
    ToastModule
  ],
  providers: [MessageService],
  templateUrl: './form-builder.component.html',
  styleUrls: ['./form-builder.component.scss']
})
export class FormBuilderComponent implements OnInit {
  templateId: string | null = null;
  loading = false;

  formName: string = 'Biểu mẫu thiết bị mới';
  formDescription: string = 'Định nghĩa các thông số kỹ thuật số hóa thiết bị';
  showJson: boolean = false;
  
  fields: FormField[] = [];
  selectedFieldIndex: number | null = null;
  
  toolboxItems: ToolboxItem[] = [
    { type: 'text', label: 'Trường Văn bản (Text)', icon: 'pi-align-left', description: 'Tên thiết bị, số seri, hãng sản xuất...' },
    { type: 'number', label: 'Số liệu kỹ thuật (Number)', icon: 'pi-percentage', description: 'Điện áp định mức, công suất, dòng điện...' },
    { type: 'date', label: 'Ngày kiểm định (Date)', icon: 'pi-calendar', description: 'Ngày đưa vào vận hành, ngày thí nghiệm...' },
    { type: 'dropdown', label: 'Danh sách Lựa chọn (Dropdown)', icon: 'pi-chevron-down', description: 'Loại cách điện, cấp điện áp...' },
    { type: 'textarea', label: 'Mô tả / Ghi chú (Textarea)', icon: 'pi-align-justify', description: 'Tình trạng kỹ thuật, ghi chú khác...' },
    { type: 'checkbox', label: 'Hộp kiểm xác nhận (Checkbox)', icon: 'pi-check-square', description: 'Đã nghiệm thu, đạt tiêu chuẩn...' }
  ];

  fieldTypes = [
    { label: 'Văn bản (Text)', value: 'text' },
    { label: 'Số (Number)', value: 'number' },
    { label: 'Ngày (Date)', value: 'date' },
    { label: 'Lựa chọn (Dropdown)', value: 'dropdown' },
    { label: 'Đoạn văn (Textarea)', value: 'textarea' },
    { label: 'Hộp kiểm (Checkbox)', value: 'checkbox' }
  ];

  draggedType: string | null = null;
  draggedIndex: number | null = null;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private eavFormService = inject(EavFormService);
  private messageService = inject(MessageService);

  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      if (params['id']) {
        this.templateId = params['id'];
        this.loadTemplate(this.templateId!);
      } else {
        this.loadDefaultFields();
      }
    });
  }

  loadTemplate(id: string) {
    this.loading = true;
    this.eavFormService.getTemplateById(id).subscribe({
      next: (data) => {
        this.formName = data.name;
        this.formDescription = data.description;
        try {
          this.fields = JSON.parse(data.schema) || [];
          if (this.fields.length > 0) {
            this.selectedFieldIndex = 0;
          }
        } catch (e) {
          console.error('Failed to parse template schema JSON', e);
          this.fields = [];
        }
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading template details', err);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải chi tiết biểu mẫu từ máy chủ.'
        });
        this.loading = false;
      }
    });
  }

  loadDefaultFields() {
    this.fields = [
      {
        id: 'f_' + Date.now() + '_1',
        name: 'ten_thiet_bi',
        label: 'Tên thiết bị',
        type: 'text',
        placeholder: 'Nhập tên thiết bị...',
        required: true,
        width: 100
      },
      {
        id: 'f_' + Date.now() + '_2',
        name: 'dien_ap_dinh_muc',
        label: 'Điện áp định mức (kV)',
        type: 'number',
        placeholder: 'Ví dụ: 110, 220...',
        required: true,
        width: 50
      },
      {
        id: 'f_' + Date.now() + '_3',
        name: 'cap_dien_ap',
        label: 'Cấp điện áp',
        type: 'dropdown',
        required: true,
        options: ['110kV', '220kV', '500kV', 'Trung thế'],
        width: 50
      }
    ];
    this.selectedFieldIndex = 0;
  }

  // --- HTML5 Drag & Drop for Toolbox -> Canvas ---
  onToolboxDragStart(event: DragEvent, type: string) {
    this.draggedType = type;
    this.draggedIndex = null;
    if (event.dataTransfer) {
      event.dataTransfer.setData('text/plain', type);
      event.dataTransfer.effectAllowed = 'copy';
    }
  }

  // --- HTML5 Drag & Drop for Canvas reordering ---
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
      // Add new field from toolbox
      this.addNewField(this.draggedType);
    } else if (this.draggedIndex !== null) {
      // Reorder fields
      // Drop index determination is handled inside dragover target, or appended to end here
      this.draggedIndex = null;
    }
    this.draggedType = null;
  }

  onCanvasDrop(event: DragEvent, targetIndex: number) {
    event.preventDefault();
    event.stopPropagation();
    
    if (this.draggedType) {
      // Add new field at specific index
      this.addNewFieldAtIndex(this.draggedType, targetIndex);
      this.draggedType = null;
    } else if (this.draggedIndex !== null && this.draggedIndex !== targetIndex) {
      // Move field from draggedIndex to targetIndex
      const movedField = this.fields.splice(this.draggedIndex, 1)[0];
      this.fields.splice(targetIndex, 0, movedField);
      this.selectedFieldIndex = targetIndex;
      this.draggedIndex = null;
    }
  }

  // --- Core functions ---
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
    const id = 'f_' + Math.random().toString(36).substr(2, 9);
    let label = 'Trường mới';
    let name = 'truong_moi';
    let options: string[] | undefined = undefined;

    switch (type) {
      case 'text':
        label = 'Trường Văn bản';
        name = 'truong_van_ban';
        break;
      case 'number':
        label = 'Số liệu Kỹ thuật';
        name = 'so_lieu_ky_thuat';
        break;
      case 'date':
        label = 'Ngày tháng';
        name = 'ngay_thang';
        break;
      case 'dropdown':
        label = 'Danh mục Lựa chọn';
        name = 'danh_muc_lua_chon';
        options = ['Lựa chọn 1', 'Lựa chọn 2'];
        break;
      case 'textarea':
        label = 'Đoạn văn mô tả';
        name = 'doan_van_mo_ta';
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
      id: 'f_' + Math.random().toString(36).substr(2, 9),
      name: sourceField.name + '_copy',
      label: sourceField.label + ' (Bản sao)'
    };
    if (sourceField.options) {
      cloned.options = [...sourceField.options];
    }
    this.fields.splice(index + 1, 0, cloned);
    this.selectedFieldIndex = index + 1;
  }

  // --- Option builders for Dropdown type ---
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
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng kéo thả ít nhất một trường vào biểu mẫu.' });
      return;
    }

    const schemaStr = JSON.stringify(this.fields);
    
    if (this.templateId) {
      // Upgrade existing version
      this.eavFormService.updateTemplate(this.templateId, this.formName, this.formDescription, schemaStr).subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: 'Đã nâng cấp phiên bản biểu mẫu thành công!'
          });
          setTimeout(() => {
            this.router.navigate(['/equipment/form-management']);
          }, 1000);
        },
        error: (err) => {
          console.error('Error upgrading form template', err);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể nâng cấp phiên bản biểu mẫu.'
          });
        }
      });
    } else {
      // Create new template
      this.eavFormService.createTemplate(this.formName, this.formDescription, schemaStr).subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: 'Đã lưu biểu mẫu động mới thành công!'
          });
          setTimeout(() => {
            this.router.navigate(['/equipment/form-management']);
          }, 1000);
        },
        error: (err) => {
          console.error('Error creating form template', err);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể tạo mới biểu mẫu.'
          });
        }
      });
    }
  }
}
