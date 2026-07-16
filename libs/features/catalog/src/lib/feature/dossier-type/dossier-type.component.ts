import { Component, OnInit, signal, computed, inject, effect } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { Menu, MenuModule } from 'primeng/menu';
import { MenuItem, MessageService } from 'primeng/api';
import { AuthService, EavFormService } from '@sohoa.frontend/shared/core';
import { DossierTypeService } from '../../data-access/dossier-type.service';
import { DocumentTypeService } from '../../data-access/document-type.service';
import { MultiSelectModule } from 'primeng/multiselect';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-dossier-type',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, MenuModule, SelectModule, DialogModule, MultiSelectModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './dossier-type.component.html',
  styleUrl: './dossier-type.component.scss'
})
export class DossierTypeComponent implements OnInit {
  private dossierTypeService = inject(DossierTypeService);
  private documentTypeService = inject(DocumentTypeService);
  private authService = inject(AuthService);
  private messageService = inject(MessageService);

  // States
  items = signal<any[]>([]);
  documentTypes = signal<any[]>([]);
  searchKeyword = signal<string>('');
  searchStatus = signal<string>(''); // '', '1', '0'
  totalCount = signal<number>(0);

  currentView = signal<'list' | 'add' | 'edit' | 'configure'>('list');
  currentItem = signal<any>({});
  isSaving = signal<boolean>(false);

  // Configure EAV states & signals
  private eavFormService = inject(EavFormService);
  lookupFormTemplates = signal<any[]>([]);
  selectedFormId = signal<string | null>(null);
  fields = signal<any[]>([]);
  selectedFieldIndex = signal<number | null>(null);
  isSavingEav = signal<boolean>(false);
  catalogTypes = signal<any[]>([]);
  draggedType: string | null = null;
  draggedIndex: number | null = null;

  formName: string = '';
  formCode: string = '';
  formCategory: string = '';
  formDescriptionInfo: string = '';
  formDescription: string = '';

  toolboxItems = [
    { type: 'text', label: 'Trường Văn bản (Text)', icon: 'pi-align-left' },
    { type: 'number', label: 'Số liệu kỹ thuật (Number)', icon: 'pi-percentage' },
    { type: 'date', label: 'Ngày kiểm định (Date)', icon: 'pi-calendar' },
    { type: 'dropdown', label: 'Danh sách Lựa chọn (Dropdown)', icon: 'pi-chevron-down' },
    { type: 'radio', label: 'Lựa chọn một (Radio)', icon: 'pi-circle' },
    { type: 'textarea', label: 'Mô tả / Ghi chú (Textarea)', icon: 'pi-align-justify' },
    { type: 'checkbox', label: 'Hộp kiểm (Checkbox)', icon: 'pi-check-square' }
  ];

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

  // Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  // Form Validation
  formSubmitted = signal<boolean>(false);
  serverErrors = signal<any>({});

  codeError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().code) return 'Mã loại hồ sơ là bắt buộc';
    return this.serverErrors().code || this.serverErrors().Code || '';
  });

  nameError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().name) return 'Tên loại hồ sơ là bắt buộc';
    return this.serverErrors().name || this.serverErrors().Name || '';
  });

  // Delete Confirmation Dialog Signals
  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<any>(null);
  deleting = signal<boolean>(false);

  // Pagination Computeds
  paginatedItems = computed(() => {
    return this.items();
  });

  totalPages = computed(() => {
    return Math.ceil(this.totalCount() / this.pageSize());
  });

  // Permission Computeds
  canCreate = computed(() => this.authService.hasPermission('DOSSIER_TYPE_CREATE') || this.authService.hasPermission('SUPER_ADMIN'));
  canEdit = computed(() => this.authService.hasPermission('DOSSIER_TYPE_EDIT') || this.authService.hasPermission('SUPER_ADMIN'));
  canDelete = computed(() => this.authService.hasPermission('DOSSIER_TYPE_DELETE') || this.authService.hasPermission('SUPER_ADMIN'));
  canManage = computed(() => this.authService.hasPermission('DOSSIER_TYPE_MANAGE') || this.authService.hasPermission('SUPER_ADMIN'));
  actionMenuItems: MenuItem[] = [];

  openActionMenu(item: any, event: Event, menu: Menu): void {
    event.stopPropagation();
    const active = item.isActive === 1 || item.isActive === true;
    this.actionMenuItems = [
      ...(this.canManage() ? [{
        label: active ? 'Khóa loại hồ sơ' : 'Mở khóa loại hồ sơ',
        title: active ? 'Khóa loại hồ sơ' : 'Mở khóa loại hồ sơ',
        icon: active ? 'pi pi-lock color-red' : 'pi pi-lock-open color-teal',
        command: () => this.onToggleStatus(item)
      }] : []),
      ...(this.canEdit() ? [{ label: 'Cấu hình biểu mẫu EAV', title: 'Cấu hình biểu mẫu EAV', icon: 'pi pi-cog color-teal', command: () => this.onConfigureEav(item) }] : []),
      ...(this.canEdit() ? [{ label: 'Chỉnh sửa', title: 'Chỉnh sửa', icon: 'pi pi-pencil color-blue', command: () => this.onEdit(item) }] : []),
      ...(this.canDelete() ? [{ label: 'Xóa', title: 'Xóa', icon: 'pi pi-trash color-red', command: () => this.onDelete(item) }] : []),
    ];
    menu.toggle(event);
  }

  constructor() {
    effect(() => {
      // Re-trigger load when page or pageSize changes
      this.currentPage();
      this.pageSize();
      this.loadItems();
    }, { allowSignalWrites: true });
  }

  ngOnInit() {
    this.authService.loadPermissions();
    this.loadDocumentTypes();
    this.loadItems();
  }

  loadDocumentTypes() {
    this.documentTypeService.getDocumentTypes(1, 1000, '', '1').subscribe({
      next: (res) => {
        this.documentTypes.set(res?.items || []);
      },
      error: () => this.documentTypes.set([])
    });
  }

  onFieldChange(field: string) {
    this.serverErrors.update(errs => {
      const copy = { ...errs };
      delete copy[field];
      const capitalized = field.charAt(0).toUpperCase() + field.slice(1);
      delete copy[capitalized];
      return copy;
    });
  }

  loadItems() {
    this.dossierTypeService.getDossierTypes(
      this.currentPage(),
      this.pageSize(),
      this.searchKeyword(),
      this.searchStatus()
    ).subscribe({
      next: (res) => {
        if (res) {
          this.items.set(res.items || []);
          this.totalCount.set(res.totalCount || 0);
        }
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải danh sách loại hồ sơ'
        });
      }
    });
  }

  onSearch() {
    this.currentPage.set(1);
    this.loadItems();
  }

  onResetSearch() {
    this.searchKeyword.set('');
    this.searchStatus.set('');
    this.currentPage.set(1);
    this.loadItems();
  }

  nextPage() {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update(p => p + 1);
    }
  }

  prevPage() {
    if (this.currentPage() > 1) {
      this.currentPage.update(p => p - 1);
    }
  }

  goToPage(page: any) {
    const p = Number(page);
    if (p >= 1 && p <= this.totalPages()) {
      this.currentPage.set(p);
    }
  }

  onPageSizeChange(event: any) {
    this.pageSize.set(Number(event.target.value));
    this.currentPage.set(1);
  }

  onAddNew() {
    this.currentItem.set({
      isActive: true,
      piority: 1,
      formId: null,
      documentTypeIds: []
    });
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.currentView.set('add');
  }

  onEdit(item: any) {
    this.currentItem.set({ 
      ...item,
      documentTypeIds: item.documentTypeIds || []
    });
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.currentView.set('edit');
  }

  onSaveItem() {
    this.formSubmitted.set(true);
    const item = this.currentItem();

    if (!item.code || !item.name) {
      return;
    }

    this.isSaving.set(true);

    const payload = {
      id: item.id,
      code: item.code.trim(),
      name: item.name.trim(),
      formId: item.formId || null,
      piority: item.piority || 1,
      isActive: item.isActive,
      documentTypeIds: item.documentTypeIds || []
    };

    const request$ = this.currentView() === 'add'
      ? this.dossierTypeService.createDossierType(payload)
      : this.dossierTypeService.updateDossierType(item.id, payload);

    request$.pipe(
      finalize(() => this.isSaving.set(false))
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: this.currentView() === 'add' ? 'Đã thêm mới loại hồ sơ thành công!' : 'Đã cập nhật loại hồ sơ thành công!'
        });
        this.currentView.set('list');
        this.loadItems();
      },
      error: (err) => {
        let errorsObj = {};
        if (err?.error) {
          if (typeof err.error === 'object') {
            errorsObj = err.error.errors || err.error;
          } else if (typeof err.error === 'string') {
            try {
              const parsed = JSON.parse(err.error);
              errorsObj = parsed.errors || parsed;
            } catch (e) {
              // Ignore parse error
            }
          }
        } else if (err?.errors) {
          errorsObj = err.errors;
        }
        this.serverErrors.set(errorsObj);
        
        // Show validation/error message
        const errMsg = err?.error?.message || 'Có lỗi xảy ra khi lưu thông tin.';
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: errMsg
        });
      }
    });
  }

  onToggleStatus(item: any) {
    const isLocking = item.isActive === 1 || item.isActive === true;
    this.dossierTypeService.toggleStatus(item.id, isLocking).subscribe({
      next: (res) => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: res.message || (isLocking ? 'Khóa thành công!' : 'Mở khóa thành công!')
        });
        this.loadItems();
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err?.error?.message || 'Không thể cập nhật trạng thái loại hồ sơ.'
        });
      }
    });
  }

  // Custom Delete Confirm Logic
  onDelete(item: any) {
    this.deleteTarget.set(item);
    this.showDeleteConfirm.set(true);
  }

  onConfirmDelete() {
    const item = this.deleteTarget();
    if (!item) return;

    this.deleting.set(true);
    this.dossierTypeService.deleteDossierType(item.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Xóa thành công',
            detail: `Đã xóa loại hồ sơ "${item.name}" thành công!`
          });
          this.showDeleteConfirm.set(false);
          this.deleteTarget.set(null);
          this.loadItems();
        },
        error: (err) => {
          this.showDeleteConfirm.set(false);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể xóa loại hồ sơ.'
          });
        }
      });
  }

  onCancelDelete() {
    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }

  onConfigureEav(item: any) {
    this.currentItem.set({ ...item });
    this.selectedFieldIndex.set(null);
    
    if (item.formId) {
      this.selectedFormId.set(item.formId);
      this.eavFormService.getTemplateById(item.formId).subscribe({
        next: (form: any) => {
          this.formName = form.name;
          this.formCode = form.code || '';
          this.formCategory = form.category || '';
          this.formDescription = form.description || '';
          this.formDescriptionInfo = form.descriptionInfo || '';
          try {
            const parsedFields = JSON.parse(form.formSchema) || [];
            this.fields.set(parsedFields);
            this.selectedFieldIndex.set(parsedFields.length > 0 ? 0 : null);
          } catch {
            this.fields.set([]);
          }
        },
        error: () => {
          this.formName = '';
          this.formCode = '';
          this.formCategory = '';
          this.formDescription = '';
          this.formDescriptionInfo = '';
          this.fields.set([]);
        }
      });
    } else {
      this.selectedFormId.set(null);
      this.formName = '';
      this.formCode = '';
      this.formCategory = '';
      this.formDescription = '';
      this.formDescriptionInfo = '';
      this.fields.set([]);
    }

    this.loadFormTemplatesLookup();
    this.loadCatalogTypes();
    this.currentView.set('configure');
  }

  loadFormTemplatesLookup() {
    this.dossierTypeService.getEavFormTemplatesLookup().subscribe({
      next: (data) => {
        this.lookupFormTemplates.set(Array.isArray(data) ? data : []);
      },
      error: () => {
        this.lookupFormTemplates.set([]);
      }
    });
  }

  loadCatalogTypes() {
    if (this.catalogTypes().length === 0) {
      this.eavFormService.getCatalogTypes().subscribe({
        next: (types) => this.catalogTypes.set(types || []),
        error: () => this.catalogTypes.set([])
      });
    }
  }

  onSelectFormTemplate(formId: any) {
    this.selectedFormId.set(formId);
    this.selectedFieldIndex.set(null);
    if (!formId) {
      this.formName = '';
      this.formCode = '';
      this.formCategory = '';
      this.formDescription = '';
      this.formDescriptionInfo = '';
      this.fields.set([]);
      return;
    }

    const template = this.lookupFormTemplates().find(t => t.id === formId);
    if (template) {
      this.formName = template.name;
      this.formCode = template.code || '';
      this.formCategory = template.category || '';
      this.formDescription = template.description || '';
      this.formDescriptionInfo = template.descriptionInfo || '';
      try {
        const parsedFields = JSON.parse(template.formSchema) || [];
        this.fields.set(parsedFields);
        this.selectedFieldIndex.set(parsedFields.length > 0 ? 0 : null);
      } catch {
        this.fields.set([]);
      }
    }
  }

  // --- EAV Form Builder canvas interactions ---
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
    const newField = this.createDefaultField(type);
    this.fields.update(currentFields => [...currentFields, newField]);
    this.selectedFieldIndex.set(this.fields().length - 1);
  }

  addNewFieldAtIndex(type: string, index: number) {
    const newField = this.createDefaultField(type);
    this.fields.update(currentFields => {
      const updated = [...currentFields];
      updated.splice(index, 0, newField);
      return updated;
    });
    this.selectedFieldIndex.set(index);
  }

  createDefaultField(type: string): any {
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
      case 'radio':
        label = 'Lựa chọn một';
        name = 'lua_chon_mot';
        options = ['Tùy chọn 1', 'Tùy chọn 2'];
        break;
      case 'textarea':
        label = 'Đoạn mô tả ngắn';
        name = 'doan_mo_ta';
        break;
      case 'checkbox':
        label = 'Hộp kiểm';
        name = 'hop_kiem';
        options = []; // Khởi tạo mảng rỗng, nếu có options thì là checkboxGroup, nếu rỗng thì là checkbox đơn
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
    const cloned = {
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

  updateSelectedField(key: string, value: any) {
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

  onSaveEavConfiguration() {
    const activeDossierType = this.currentItem();
    if (!activeDossierType) return;

    const formId = this.selectedFormId();

    const fName = this.formName.trim();
    const fCode = this.formCode.trim();
    const fCategory = this.formCategory.trim();

    if (formId) {
      if (!fName || !fCode || !fCategory) {
        this.messageService.add({
          severity: 'warn',
          summary: 'Thiếu thông tin',
          detail: 'Mã biểu mẫu, tên biểu mẫu và hạng mục áp dụng là bắt buộc.'
        });
        return;
      }

      const currentFields = this.fields();
      if (currentFields.length === 0) {
        this.messageService.add({
          severity: 'warn',
          summary: 'Thiếu thông tin',
          detail: 'Cần thiết kế ít nhất một trường thông tin cho biểu mẫu EAV.'
        });
        return;
      }
    }

    this.isSavingEav.set(true);

    const payload = {
      formId: formId,
      name: formId ? fName : null,
      code: formId ? fCode : null,
      category: formId ? fCategory : null,
      description: formId ? this.formDescription : null,
      descriptionInfo: formId ? this.formDescriptionInfo : null,
      formSchema: formId ? JSON.stringify(this.fields()) : null
    };

    this.dossierTypeService.updateEav(activeDossierType.id, payload)
      .pipe(finalize(() => this.isSavingEav.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: formId ? 'Đã cập nhật cấu hình biểu mẫu EAV thành công!' : 'Đã hủy liên kết biểu mẫu EAV thành công!'
          });
          this.currentView.set('list');
          this.loadItems();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể cập nhật cấu hình biểu mẫu EAV.'
          });
        }
      });
  }
}
