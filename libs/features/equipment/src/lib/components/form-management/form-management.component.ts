import { Component, OnInit, inject, signal, computed, effect } from '@angular/core';
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
import { Paginator } from 'primeng/paginator';
import { EavFormService, EavFormTemplate, LoadingService, AuthService } from '@sohoa.frontend/shared/core';
import { finalize } from 'rxjs';
import { Dialog } from 'primeng/dialog';
import { EquipmentTypeService } from '../../data-access/equipment-type.service';
import { ActivatedRoute, Router } from '@angular/router';
import {
  canCreateForm,
  canDeleteForm,
  canEditForm,
  canSubmitForm,
} from '../../utils/eav-form-permission.util';

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
}

interface ToolboxItem {
  type: string;
  label: string;
  icon: string;
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
    TextareaModule,
    Paginator,
    Dialog,
    WfBreadcrumbComponent,
  ],
  providers: [MessageService],
  templateUrl: './form-management.component.html',
  styleUrl: './form-management.component.scss'
})
export class FormManagementComponent implements OnInit {
  private loadingService = inject(LoadingService);
  // Confirm dialog state variables
  showConfirmDelete = signal<boolean>(false);
  showConfirmSubmit = signal<boolean>(false);
  targetForm: EavFormTemplate | null = null;
  showVersionsDialog = signal<boolean>(false);
  versionList = signal<EavFormTemplate[]>([]);
  selectedTemplate = signal<EavFormTemplate | null>(null);

  // Navigation & States
  viewState = signal<'list' | 'add' | 'edit' | 'preview'>('list');
  detailTitle = signal<string>('');
  isEditMode = signal<boolean>(false);
  isFromCompletedForms = signal<boolean>(false);
  activeTab = signal<'info' | 'builder'>('info');

  // Forms list state
  forms = signal<EavFormTemplate[]>([]);
  searchKeyword = signal<string>('');
  loading = signal<boolean>(false);

  // Active builder/preview states
  templateId = signal<string | null>(null);
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

  // Pagination states
  first = signal<number>(0);
  rows = signal<number>(10);

  catalogOptionsMap = signal<{ [catalogCode: string]: string[] }>({});

  loadCatalogOptions(catalogCode: string) {
    if (!catalogCode || this.catalogOptionsMap()[catalogCode]) return;
    this.eavFormService.getCatalogTypeByCode(catalogCode).subscribe({
      next: (catalogType) => {
        if (catalogType && catalogType.id) {
          this.eavFormService.getCatalogsLookup(catalogType.id).subscribe({
            next: (items) => {
              const options = (items || []).map((item: any) => item.name || item.code);
              this.catalogOptionsMap.update(prev => ({
                ...prev,
                [catalogCode]: options
              }));
            },
            error: (err) => console.error(`Failed to load catalogs lookup for ${catalogCode}`, err)
          });
        }
      },
      error: (err) => console.error(`Failed to load catalog type for ${catalogCode}`, err)
    });
  }

  constructor() {
    effect(() => {
      this.searchKeyword();
      this.first.set(0);
    });

    effect(() => {
      const currentFields = this.fields();
      if (!currentFields) return;
      currentFields.forEach((f: FormField) => {
        if (f.dataSourceType === 'catalog' && f.catalogType) {
          this.loadCatalogOptions(f.catalogType);
        }
      });
    });
  }

  categories = signal<any[]>([]);

  // Simulation Preview values
  simulatedValues = signal<{ [key: string]: any }>({});

  // Drag & drop status
  draggedType: string | null = null;
  draggedIndex: number | null = null;

  // Toolbox configuration
  toolboxItems: ToolboxItem[] = [
    { type: 'text', label: 'Trường Văn bản (Text)', icon: 'pi-align-left' },
    { type: 'number', label: 'Số liệu kỹ thuật (Number)', icon: 'pi-percentage' },
    { type: 'date', label: 'Ngày kiểm định (Date)', icon: 'pi-calendar' },
    { type: 'dropdown', label: 'Danh sách Lựa chọn (Dropdown)', icon: 'pi-chevron-down' },
    { type: 'radio', label: 'Lựa chọn duy nhất (Radio)', icon: 'pi-circle-fill' },
    { type: 'textarea', label: 'Mô tả / Ghi chú (Textarea)', icon: 'pi-align-justify' },
    { type: 'checkbox', label: 'Hộp kiểm xác nhận (Checkbox)', icon: 'pi-check-square' }
  ];

  private eavFormService = inject(EavFormService);
  private equipmentTypeService = inject(EquipmentTypeService);
  private messageService = inject(MessageService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private authService = inject(AuthService);

  canCreate = computed(() => canCreateForm(this.authService));
  canEdit = computed(() => canEditForm(this.authService));
  canSubmit = computed(() => canSubmitForm(this.authService));
  canDelete = computed(() => canDeleteForm(this.authService));

  filteredForms = computed(() => {
    const keyword = this.searchKeyword().trim().toLowerCase();
    const allForms = this.forms().filter(f => !f.isDeleted);

    // Group forms by code and select the latest version for each unique code
    const latestFormsMap = new Map<string, EavFormTemplate>();
    for (const f of allForms) {
      const code = f.code || '';
      const existing = latestFormsMap.get(code);
      if (!existing || f.version > existing.version) {
        latestFormsMap.set(code, f);
      }
    }
    let result = Array.from(latestFormsMap.values());

    if (keyword) {
      result = result.filter(f =>
        (f.name?.toLowerCase().includes(keyword) ?? false) ||
        (f.code?.toLowerCase().includes(keyword) ?? false) ||
        (f.id?.toLowerCase().includes(keyword) ?? false)
      );
    }
    return result;
  });

  paginatedForms = computed(() => {
    const start = this.first();
    const end = start + this.rows();
    return this.filteredForms().slice(start, end);
  });

  onPageChange(event: any) {
    this.first.set(event.first);
    this.rows.set(event.rows);
  }

  catalogTypes = signal<any[]>([]);

  loadCatalogTypes() {
    this.eavFormService.getCatalogTypes().subscribe({
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

  loadHmadCategories() {
    this.eavFormService.getCatalogTypeByCode('HMAD').subscribe({
      next: (catalogType) => {
        if (catalogType && catalogType.id) {
          this.eavFormService.getCatalogsLookup(catalogType.id).subscribe({
            next: (catalogs) => {
              this.categories.set(catalogs || []);
            },
            error: (err) => {
              console.error('Failed to load catalogs for HMAD', err);
            }
          });
        }
      },
      error: (err) => {
        console.error('Failed to load CatalogType HMAD', err);
      }
    });
  }

  ngOnInit() {
    this.isFromCompletedForms.set(this.router.url.includes('/completed-forms/edit'));
    this.authService.loadPermissions();
    this.loadCatalogTypes();
    this.loadHmadCategories();
    if (!this.route.snapshot.queryParamMap.get('id')) {
      this.loadForms();
    }

    this.route.queryParams.subscribe(params => {
      if (params['id']) {
        const id = params['id'];
        this.loadingService.show();
        this.eavFormService.getTemplateById(id)
          .pipe(finalize(() => this.loadingService.hide()))
          .subscribe({
            next: (matched) => {
              if (matched) {
                this.onEdit(matched);
              } else {
                this.messageService.add({
                  severity: 'error',
                  summary: 'Lỗi',
                  detail: 'Không tìm thấy cấu hình form.'
                });
              }
            },
            error: (err) => {
              console.error('Error loading form', err);
            }
          });
      }
    });
  }

  loadForms() {
    this.loadingService.show();
    const request$ = this.isFromCompletedForms()
      ? this.eavFormService.getCompletedTemplatesForm()
      : this.eavFormService.getDesignTemplates();
    request$
      .pipe(finalize(() => this.loadingService.hide()))
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
    if (this.isFromCompletedForms()) {
      this.router.navigate(['/equipment/completed-forms']);
      return;
    }
    this.viewState.set('list');
    this.templateId.set(null);
    this.targetForm = null;
    this.loadForms();
  }

  onAddNew() {
    this.viewState.set('add');
    this.isEditMode.set(false);
    this.activeTab.set('info');
    this.detailTitle.set('Thêm mới form');
    this.formName.set('');
    this.formCode.set('');
    this.formCategory.set('');
    this.formDescription.set('');
    this.formDescriptionInfo.set('');
    this.extractionProcess.set('');
    this.fields.set([]);
    this.selectedFieldIndex.set(null);
    this.showJson.set(false);
    this.fieldSearchQuery.set('');
  }

  onEdit(form: EavFormTemplate) {
    if (form.status === 'Chờ duyệt') {
      this.onPreview(form);
      return;
    }
    this.targetForm = form;
    this.viewState.set('edit');
    this.isEditMode.set(true);
    this.activeTab.set('info');
    this.templateId.set(form.id);
    this.fieldSearchQuery.set('');
    this.detailTitle.set(`Chỉnh sửa cấu hình form: ${form.name}`);
    this.formName.set(form.name);
    this.formCode.set(form.code || '');
    this.formCategory.set(form.category || '');
    this.formDescription.set(form.description);
    this.formDescriptionInfo.set(form.descriptionInfo || '');
    this.extractionProcess.set(form.extractionProcess || '');
    this.showJson.set(false);

    try {
      const parsedFields = JSON.parse(form.formSchema) || [];
      this.fields.set(parsedFields);
      this.selectedFieldIndex.set(parsedFields.length > 0 ? 0 : null);
    } catch (e) {
      console.error('Failed to parse form schema', e);
      this.fields.set([]);
      this.selectedFieldIndex.set(null);
    }
  }

  onPreview(form: EavFormTemplate) {
    this.targetForm = form;
    this.viewState.set('preview');
    this.templateId.set(form.id);
    this.detailTitle.set(`Xem trước form: ${form.name}`);
    this.formName.set(form.name);
    this.formCode.set(form.code || '');
    this.formCategory.set(form.category || '');
    this.formDescription.set(form.description);
    this.formDescriptionInfo.set(form.descriptionInfo || '');
    this.extractionProcess.set(form.extractionProcess || '');

    const initialSimulated: { [key: string]: any } = {};

    try {
      const parsedFields = JSON.parse(form.formSchema) || [];
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
    this.targetForm = form;
    this.showConfirmDelete.set(true);
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
      selectAll: false
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

  updateSimulatedValue(name: string, value: any) {
    this.simulatedValues.update(prev => ({
      ...prev,
      [name]: value
    }));
  }

  isAllChecked(field: any): boolean {
    if (!field.options || field.options.length === 0) return false;
    return field.options.every((opt: string) => this.simulatedValues()[field.name + '_' + opt] === true);
  }

  toggleSelectAll(field: any, checked: boolean) {
    if (!field.options) return;
    field.options.forEach((opt: string) => {
      this.updateSimulatedValue(field.name + '_' + opt, checked);
    });
  }

  saveForm() {
    const fName = this.formName().trim();
    const fCode = this.formCode().trim();
    const fCategory = this.formCategory().trim();

    if (!fName) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập tên biểu mẫu.' });
      return;
    }
    if (!fCode) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập mã biểu mẫu.' });
      return;
    }
    if (!fCategory) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng chọn hạng mục áp dụng.' });
      return;
    }

    const currentFields = this.fields();
    if (currentFields.length === 0) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng thêm ít nhất một trường vào biểu mẫu.' });
      return;
    }

    const schemaStr = JSON.stringify(currentFields);
    const desc = this.formDescription();
    const fDescInfo = this.formDescriptionInfo();
    const extractProc = this.extractionProcess();
    const isEdit = this.isEditMode();
    const tId = this.templateId();

    this.loadingService.show();
    if (isEdit && tId) {
      this.eavFormService.updateTemplate(tId, fName, fCode, fCategory, desc, fDescInfo, schemaStr, 'admin', undefined, undefined, extractProc)
        .pipe(finalize(() => this.loadingService.hide()))
        .subscribe({
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
      this.eavFormService.createTemplate(fName, fCode, fCategory, desc, fDescInfo, schemaStr, 'admin', undefined, undefined, extractProc)
        .pipe(finalize(() => this.loadingService.hide()))
        .subscribe({
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

  submitForm(form: EavFormTemplate) {
    this.targetForm = form;
    this.showConfirmSubmit.set(true);
  }

  onConfirmDelete() {
    if (!this.targetForm) return;
    if (this.targetForm.status === 'Chờ duyệt' || this.targetForm.status === 'Hoàn thành') {
      this.messageService.add({
        severity: 'error',
        summary: 'Không được phép',
        detail: 'Không thể xóa biểu mẫu ở trạng thái Chờ duyệt hoặc Hoàn thành.'
      });
      this.showConfirmDelete.set(false);
      this.targetForm = null;
      return;
    }
    this.loadingService.show();
    this.eavFormService.deleteTemplate(this.targetForm.id)
      .pipe(finalize(() => this.loadingService.hide()))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `Đã xóa biểu mẫu thành công!`
          });
          this.showConfirmDelete.set(false);
          this.targetForm = null;
          this.loadForms();
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể xóa biểu mẫu.'
          });
          this.showConfirmDelete.set(false);
          this.targetForm = null;
        }
      });
  }

  onConfirmSubmit() {
    if (!this.targetForm) return;
    this.loadingService.show();
    this.eavFormService.submitTemplate(this.targetForm.id)
      .pipe(finalize(() => this.loadingService.hide()))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `Gửi duyệt biểu mẫu thành công!`
          });
          this.showConfirmSubmit.set(false);
          this.targetForm = null;
          this.loadForms();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.Message || 'Không thể gửi duyệt biểu mẫu.'
          });
          this.showConfirmSubmit.set(false);
          this.targetForm = null;
        }
      });
  }

  getCategoryName(code: string): string {
    const cat = this.categories().find(c => c.code === code || c.id === code || c.id?.toString() === code?.toString());
    return cat ? cat.name : code || '';
  }

  viewVersions(form: EavFormTemplate) {
    this.loadingService.show();
    this.eavFormService.getTemplateVersions(form.code)
      .pipe(finalize(() => this.loadingService.hide()))
      .subscribe({
        next: (versions) => {
          this.versionList.set(versions || []);
          this.selectedTemplate.set(form);
          this.showVersionsDialog.set(true);
        },
        error: (err) => {
          console.error('Failed to load template versions', err);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể tải danh sách phiên bản của biểu mẫu.'
          });
        }
      });
  }

  viewVersionDetail(ver: EavFormTemplate) {
    this.showVersionsDialog.set(false);
    this.onPreview(ver);
  }
}
