import { Component, OnInit, signal, computed, inject, effect, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '@sohoa.frontend/shared/core';
import { EquipmentService } from '../../data-access/equipment.service';
import { FormTemplateService } from '../../data-access/form-template.service';
import { DossierManagementService } from '@sohoa.frontend/features/dossier-management';
import { EMPTY, forkJoin, of } from 'rxjs';
import { catchError, finalize, switchMap } from 'rxjs/operators';

@Component({
  selector: 'app-equipment-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, SelectModule, DialogModule],
  providers: [MessageService],
  templateUrl: './equipment.component.html',
  styleUrls: ['./equipment.component.css']
})
export class EquipmentComponent implements OnInit {
  private equipmentService = inject(EquipmentService);
  private formTemplateService = inject(FormTemplateService);
  private dossierService = inject(DossierManagementService);
  private authService = inject(AuthService);
  private messageService = inject(MessageService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  protected readonly Math = Math;

  // More menu state
  activeRowMenu = signal<string | null>(null);

  @HostListener('document:click')
  closeMoreMenu() {
    this.activeRowMenu.set(null);
  }

  toggleMoreMenu(item: any, event: Event) {
    event.stopPropagation();
    if (this.activeRowMenu() === item.id) {
      this.activeRowMenu.set(null);
    } else {
      this.activeRowMenu.set(item.id);
    }
  }

  // Tab and Detail View States
  activeTab = signal<number>(0);
  eavTemplate = signal<any>(null);
  eavFields = signal<any[]>([]);
  formValuesObj = signal<any>({});
  isEditingGeneral = signal<boolean>(false);
  isEditingFormValues = signal<boolean>(false);
  isLoadingTemplate = signal<boolean>(false);
  isSavingFormValues = signal<boolean>(false);

  // Dossiers Tab States
  dossierItems = signal<any[]>([]);
  dossierTotalCount = signal<number>(0);
  dossierPage = signal<number>(1);
  dossierPageSize = signal<number>(10);
  dossierColumns = signal<any[]>([]);
  isLoadingDossiers = signal<boolean>(false);

  // Lists from lookup
  organizationUnits = signal<any[]>([]);
  infrastructures = signal<any[]>([]);
  gridTypes = signal<any[]>([]);
  equipmentTypes = signal<any[]>([]);
  countries = signal<any[]>([]);

  // Tree computed and states
  orgUnitTree = computed(() => this.buildOrgTree(this.organizationUnits()));
  
  // Search Tree Picker State
  searchOrgTreeOpen = signal<boolean>(false);
  expandedSearchUnitNodes = signal<Set<number>>(new Set<number>());

  // Form Tree Picker State
  formOrgTreeOpen = signal<boolean>(false);
  expandedFormUnitNodes = signal<Set<number>>(new Set<number>());

  // Search Filters
  searchCode = signal<string>('');
  searchName = signal<string>('');
  searchUnitId = signal<string>(''); 
  searchInfrastructureId = signal<string>('');
  searchGridTypeId = signal<string>('');
  searchEquipmentTypeId = signal<string>('');
  searchStatus = signal<string>(''); // '', '1', '0'

  // Cascading lists for Search
  searchInfrastructures = computed(() => {
    const uId = this.searchUnitId();
    const gtId = this.searchGridTypeId();
    return this.infrastructures().filter(inf => {
      const matchUnit = !uId || inf.unitId === Number(uId);
      const matchGridType = !gtId || this.matchesGridTypeId(inf, gtId);
      return matchUnit && matchGridType;
    });
  });

  searchEquipmentTypes = computed(() => {
    const gtId = this.searchGridTypeId();
    return this.equipmentTypes().filter(et => {
      return !gtId || this.matchesGridTypeId(et, gtId);
    });
  });

  // Cascading lists for Form
  formInfrastructures = computed(() => {
    const uId = this.currentItem().unitId;
    const gtId = this.currentItem().gridTypeId;
    if (!uId) return [];
    return this.infrastructures().filter(inf => {
      const matchUnit = inf.unitId === Number(uId);
      const matchGridType = !gtId || this.matchesGridTypeId(inf, gtId);
      return matchUnit && matchGridType;
    });
  });

  formEquipmentTypes = computed(() => {
    const gtId = this.currentItem().gridTypeId;
    if (!gtId) return [];
    const selectedId = this.currentItem().equipmentTypeId;
    return this.equipmentTypes().filter(et => {
      if (!this.matchesGridTypeId(et, gtId)) return false;
      const isActive = et.isActive === true || et.isActive === 1;
      const isSelected = !!selectedId && String(et.id) === String(selectedId);
      return isActive || isSelected;
    });
  });

  /** Buộc p-select render lại khi đổi lưới điện hoặc danh mục loại thiết bị vừa tải xong */
  equipmentTypeSelectKey = computed(() => {
    const gtId = this.currentItem().gridTypeId;
    if (!gtId) return 'none';
    return `${gtId}-${this.equipmentTypes().length}`;
  });

  // State lists
  items = signal<any[]>([]);
  totalCount = signal<number>(0);

  currentView = signal<'list' | 'add' | 'edit'>('list');
  currentItem = signal<any>({});
  isSaving = signal<boolean>(false);

  // Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  // Form Validation
  formSubmitted = signal<boolean>(false);
  serverErrors = signal<any>({});

  codeError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().code) return 'Mã thiết bị là bắt buộc';
    return this.serverErrors().code || this.serverErrors().Code || '';
  });

  nameError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().name) return 'Tên thiết bị là bắt buộc';
    return this.serverErrors().name || this.serverErrors().Name || '';
  });

  unitError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().unitId) return 'Đơn vị quản lý là bắt buộc';
    return this.serverErrors().unitId || this.serverErrors().UnitId || '';
  });

  gridTypeError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().gridTypeId) return 'Loại lưới điện là bắt buộc';
    return this.serverErrors().gridTypeId || this.serverErrors().GridTypeId || '';
  });

  infrastructureError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().infrastructureId) return 'Trạm/Đường dây là bắt buộc';
    return this.serverErrors().infrastructureId || this.serverErrors().InfrastructureId || '';
  });

  equipmentTypeError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().equipmentTypeId) return 'Loại thiết bị là bắt buộc';
    return this.serverErrors().equipmentTypeId || this.serverErrors().EquipmentTypeId || '';
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
  canCreate = computed(() => this.authService.hasPermission('EQUIPMENT_CREATE') || this.authService.hasPermission('SUPER_ADMIN'));
  canEdit = computed(() => this.authService.hasPermission('EQUIPMENT_EDIT') || this.authService.hasPermission('SUPER_ADMIN'));
  canDelete = computed(() => this.authService.hasPermission('EQUIPMENT_DELETE') || this.authService.hasPermission('SUPER_ADMIN'));
  canManage = computed(() => this.authService.hasPermission('EQUIPMENT_MANAGE') || this.authService.hasPermission('SUPER_ADMIN'));

  constructor() {
    effect(() => {
      this.currentPage();
      this.pageSize();
      if (this.currentView() === 'list') {
        this.loadItems();
      }
    }, { allowSignalWrites: true });

    effect(() => {
      const tab = this.activeTab();
      const page = this.dossierPage();
      const pageSize = this.dossierPageSize();
      const item = this.currentItem();
      if (tab === 1 && item?.id) {
        this.loadDossiers();
      }
    }, { allowSignalWrites: true });

    if (typeof window !== 'undefined') {
      window.addEventListener('click', () => {
        this.searchOrgTreeOpen.set(false);
        this.formOrgTreeOpen.set(false);
      });
    }
  }

  ngOnInit() {
    this.authService.loadPermissions();
    
    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      const url = this.router.url;

      if (url.includes('/device-list/add')) {
        this.currentView.set('add');
        this.currentItem.set({
          isActive: true,
          unitId: null,
          gridTypeId: null,
          infrastructureId: null,
          equipmentTypeId: null,
          countryId: null,
          code: '',
          name: '',
          serialNumber: ''
        });
        this.formSubmitted.set(false);
        this.serverErrors.set({});
        this.loadLookupData();
      } else if (id) {
        this.currentView.set('edit');
        this.activeTab.set(0);
        
        let mode = '';
        this.route.queryParams.subscribe(qParams => {
          mode = qParams['mode'] || '';
          if (mode === 'edit-specs') {
            this.isEditingFormValues.set(true);
          } else {
            this.isEditingFormValues.set(false);
          }
        });

        this.isEditingGeneral.set(false);
        this.formSubmitted.set(false);
        this.serverErrors.set({});
        this.loadLookupData();

        this.equipmentService.getById(id).subscribe({
          next: (res) => {
            if (res) {
              this.currentItem.set({
                id: res.id,
                equipmentTypeId: res.equipmentTypeId,
                name: res.name,
                code: res.code,
                serialNumber: res.serialNumber,
                unitId: res.unitId,
                infrastructureId: res.infrastructureId,
                countryId: res.countryId,
                gridTypeId: res.gridTypeId,
                isActive: res.isActive === 1 || res.isActive === true,
                formValues: res.formValues,
                equipmentTypeName: res.equipmentTypeName,
                equipmentTypeCode: res.equipmentTypeCode,
                gridTypeName: res.gridTypeName,
                infrastructureName: res.infrastructureName,
                unitName: res.unitName,
                countryName: res.countryName,
                creator: res.creator,
                createdBy: res.createdBy
              });

              // Load form template directly from response
              let parsedFields: any[] = [];
              if (res.formSchema) {
                this.eavTemplate.set({ name: res.formTemplateName, formSchema: res.formSchema });
                try {
                  parsedFields = JSON.parse(res.formSchema) || [];
                } catch (e) {
                  parsedFields = [];
                }
              } else {
                this.eavTemplate.set(null);
              }
              this.eavFields.set(parsedFields);

              // Parse form values và khởi tạo đủ key theo schema (tránh trùng binding khi name rỗng)
              try {
                const parsed = res.formValues ? JSON.parse(res.formValues) : {};
                this.formValuesObj.set(this.initEavFormValues(parsedFields, parsed));
              } catch (e) {
                this.formValuesObj.set(this.initEavFormValues(parsedFields, {}));
              }
              
              // Load dossier columns (for dossier tab)
              this.loadBhsColumns();

              // Nếu mode là view-specs hoặc edit-specs, tự động scroll xuống specs section
              if (mode === 'view-specs' || mode === 'edit-specs') {
                setTimeout(() => {
                  const el = document.getElementById('specs-section');
                  if (el) el.scrollIntoView({ behavior: 'smooth' });
                }, 300);
              }
            }
          },
          error: () => {
            this.messageService.add({
              severity: 'error',
              summary: 'Lỗi',
              detail: 'Không thể tải chi tiết thiết bị'
            });
            this.goBack();
          }
        });
      } else {
        this.currentView.set('list');
        this.loadItems();
      }
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

  loadLookupData() {
    forkJoin({
      organizationUnits: this.equipmentService.getOrganizationUnits().pipe(catchError(() => of([]))),
      infrastructures: this.equipmentService.getInfrastructures().pipe(catchError(() => of([]))),
      gridTypes: this.equipmentService.getGridTypes().pipe(catchError(() => of([]))),
      equipmentTypes: this.equipmentService.getEquipmentTypes().pipe(catchError(() => of([]))),
      countries: this.equipmentService.getCountries().pipe(catchError(() => of([])))
    }).subscribe({
      next: (data) => {
        this.organizationUnits.set(Array.isArray(data.organizationUnits) ? data.organizationUnits : []);
        this.infrastructures.set(Array.isArray(data.infrastructures) ? data.infrastructures : []);
        this.gridTypes.set(Array.isArray(data.gridTypes) ? data.gridTypes : []);
        this.equipmentTypes.set(Array.isArray(data.equipmentTypes) ? data.equipmentTypes : []);
        this.countries.set(Array.isArray(data.countries) ? data.countries : []);
      },
      error: () => {
        console.error('Không thể tải dữ liệu danh mục');
      }
    });
  }

  private matchesGridTypeId(item: any, gridTypeId: any): boolean {
    if (gridTypeId == null || gridTypeId === '') return false;
    const selectedGridType = Number(gridTypeId);
    const itemGridType = Number(item?.gridTypeId ?? item?.GridTypeId);
    return !Number.isNaN(selectedGridType)
      && !Number.isNaN(itemGridType)
      && itemGridType === selectedGridType;
  }

  loadItems() {
    const unitId = this.searchUnitId() ? Number(this.searchUnitId()) : undefined;
    const gridTypeId = this.searchGridTypeId() ? Number(this.searchGridTypeId()) : undefined;
    const isActive = this.searchStatus() !== '' ? this.searchStatus() === '1' : undefined;

    this.equipmentService.getEquipments(
      this.currentPage(),
      this.pageSize(),
      this.searchCode(),
      this.searchName(),
      unitId,
      this.searchInfrastructureId(),
      gridTypeId,
      this.searchEquipmentTypeId(),
      isActive
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
          detail: 'Không thể tải danh sách thiết bị'
        });
      }
    });
  }

  // Tree Helpers
  buildOrgTree(units: any[]): any[] {
    const map = new Map<number, any>();
    const roots: any[] = [];
    units.forEach(u => map.set(u.id, { ...u, children: [] }));
    map.forEach(node => {
      if (node.parentId && map.has(node.parentId)) {
        map.get(node.parentId)!.children.push(node);
      } else {
        roots.push(node);
      }
    });
    return roots;
  }

  getUnitLabel(unitId: any): string {
    if (!unitId) return '';
    const u = (this.organizationUnits() || []).find(x => x.id == unitId);
    return u ? u.name : `Đơn vị #${unitId}`;
  }

  loadSearchInfrastructuresOnDemand() {
    if (this.infrastructures().length === 0) {
      this.equipmentService.getInfrastructures().pipe(catchError(() => of([]))).subscribe(data => {
        this.infrastructures.set(Array.isArray(data) ? data : []);
      });
    }
  }

  loadGridTypesOnDemand() {
    if (this.gridTypes().length === 0) {
      this.equipmentService.getGridTypes().pipe(catchError(() => of([]))).subscribe(data => {
        this.gridTypes.set(Array.isArray(data) ? data : []);
      });
    }
  }

  loadEquipmentTypesOnDemand() {
    if (this.equipmentTypes().length === 0) {
      this.equipmentService.getEquipmentTypes().pipe(catchError(() => of([]))).subscribe(data => {
        this.equipmentTypes.set(Array.isArray(data) ? data : []);
      });
    }
  }

  // Search tree picker actions
  toggleSearchOrgTree(event?: Event) {
    if (event) event.stopPropagation();
    if (this.organizationUnits().length === 0) {
      this.equipmentService.getOrganizationUnits().pipe(catchError(() => of([]))).subscribe(data => {
        this.organizationUnits.set(Array.isArray(data) ? data : []);
        this.searchOrgTreeOpen.update(v => !v);
        this.formOrgTreeOpen.set(false);
      });
    } else {
      this.searchOrgTreeOpen.update(v => !v);
      this.formOrgTreeOpen.set(false);
    }
  }

  toggleSearchUnitNode(unitId: number, event?: Event) {
    if (event) event.stopPropagation();
    const current = new Set(this.expandedSearchUnitNodes());
    if (current.has(unitId)) {
      current.delete(unitId);
    } else {
      current.add(unitId);
    }
    this.expandedSearchUnitNodes.set(current);
  }

  isSearchNodeExpanded(unitId: number): boolean {
    return this.expandedSearchUnitNodes().has(unitId);
  }

  selectSearchOrgUnit(unitId: number) {
    this.searchUnitId.set(unitId.toString());
    this.searchInfrastructureId.set('');
    this.searchOrgTreeOpen.set(false);
    this.onSearch();
  }

  clearSearchOrgUnit(event: Event) {
    event.stopPropagation();
    this.searchUnitId.set('');
    this.searchInfrastructureId.set('');
    this.searchOrgTreeOpen.set(false);
    this.onSearch();
  }

  // Form tree picker actions
  toggleFormOrgTree(event?: Event) {
    if (event) event.stopPropagation();
    if (this.organizationUnits().length === 0) {
      this.equipmentService.getOrganizationUnits().pipe(catchError(() => of([]))).subscribe(data => {
        this.organizationUnits.set(Array.isArray(data) ? data : []);
        this.formOrgTreeOpen.update(v => !v);
        this.searchOrgTreeOpen.set(false);
      });
    } else {
      this.formOrgTreeOpen.update(v => !v);
      this.searchOrgTreeOpen.set(false);
    }
  }

  toggleFormUnitNode(unitId: number, event?: Event) {
    if (event) event.stopPropagation();
    const current = new Set(this.expandedFormUnitNodes());
    if (current.has(unitId)) {
      current.delete(unitId);
    } else {
      current.add(unitId);
    }
    this.expandedFormUnitNodes.set(current);
  }

  isFormNodeExpanded(unitId: number): boolean {
    return this.expandedFormUnitNodes().has(unitId);
  }

  selectFormOrgUnit(unitId: number) {
    this.currentItem.update(item => ({
      ...item,
      unitId: unitId,
      infrastructureId: null
    }));
    this.formOrgTreeOpen.set(false);
    this.onFieldChange('unitId');
  }

  onGridTypeChange(value: any) {
    this.currentItem.update(item => ({ ...item, gridTypeId: value, equipmentTypeId: null, infrastructureId: null }));
    this.onFieldChange('gridTypeId');
    this.onFieldChange('equipmentTypeId');
    this.onFieldChange('infrastructureId');
  }

  onEquipmentTypeChange(value: any) {
    this.currentItem.update(item => ({ ...item, equipmentTypeId: value }));
    this.onFieldChange('equipmentTypeId');
  }

  onSearch() {
    this.currentPage.set(1);
    this.loadItems();
  }

  onResetSearch() {
    this.searchCode.set('');
    this.searchName.set('');
    this.searchUnitId.set('');
    this.searchInfrastructureId.set('');
    this.searchGridTypeId.set('');
    this.searchEquipmentTypeId.set('');
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
    this.router.navigate(['/equipment/device-list/add']);
  }

  onEdit(item: any) {
    this.router.navigate(['/equipment/device-list', item.id]);
  }

  onSaveItem() {
    this.formSubmitted.set(true);
    const item = this.currentItem();

    if (!item.code || !item.name || !item.unitId || !item.gridTypeId || !item.infrastructureId || !item.equipmentTypeId) {
      return;
    }

    this.isSaving.set(true);

    const payload = {
      equipmentTypeId: item.equipmentTypeId,
      infrastructureId: item.infrastructureId,
      countryId: item.countryId || null,
      code: item.code.trim(),
      name: item.name.trim(),
      serialNumber: item.serialNumber ? item.serialNumber.trim() : '',
      unitId: Number(item.unitId),
      isActive: item.isActive === true || item.isActive === 1
    };

    const isAdd = this.currentView() === 'add';
    const excludeId = isAdd ? undefined : item.id;

    this.equipmentService.checkCodeExists(payload.code, excludeId).pipe(
      switchMap(exists => {
        if (exists) {
          const duplicateMessage = isAdd
            ? `Mã thiết bị '${payload.code}' đã tồn tại trong hệ thống.`
            : `Mã thiết bị '${payload.code}' đã được sử dụng bởi bản ghi khác.`;
          this.serverErrors.set({ code: duplicateMessage });
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: duplicateMessage
          });
          return EMPTY;
        }

        return isAdd
          ? this.equipmentService.create(payload)
          : this.equipmentService.update(item.id, payload);
      }),
      finalize(() => this.isSaving.set(false))
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: isAdd ? 'Đã thêm mới thiết bị thành công!' : 'Đã cập nhật thiết bị thành công!'
        });
        this.goBack();
      },
      error: (err) => {
        this.handleSaveError(err);
      }
    });
  }

  private handleSaveError(err: any) {
    let errorsObj = {};
    if (err?.error) {
      if (typeof err.error === 'object') {
        errorsObj = err.error.errors || err.error;
      } else if (typeof err.error === 'string') {
        try {
          const parsed = JSON.parse(err.error);
          errorsObj = parsed.errors || parsed;
        } catch (e) {
          // Ignore
        }
      }
    } else if (err?.errors) {
      errorsObj = err.errors;
    }
    this.serverErrors.set(errorsObj);

    const errMsg = err?.error?.message || 'Có lỗi xảy ra khi lưu thông tin thiết bị.';
    this.messageService.add({
      severity: 'error',
      summary: 'Lỗi',
      detail: errMsg
    });
  }

  onToggleStatus(item: any) {
    const isLocking = item.isActive === 1 || item.isActive === true;
    this.equipmentService.toggleStatus(item.id, isLocking).subscribe({
      next: (res) => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: res.message || (isLocking ? 'Khóa thiết bị thành công!' : 'Mở khóa thiết bị thành công!')
        });
        this.loadItems();
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err?.error?.message || 'Không thể cập nhật trạng thái thiết bị.'
        });
      }
    });
  }

  onDelete(item: any) {
    this.deleteTarget.set(item);
    this.showDeleteConfirm.set(true);
  }

  onConfirmDelete() {
    const item = this.deleteTarget();
    if (!item) return;

    this.deleting.set(true);
    this.equipmentService.delete(item.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Xóa thành công',
            detail: `Đã xóa thiết bị "${item.name}" thành công!`
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
            detail: err?.error?.message || 'Không thể xóa thiết bị.'
          });
        }
      });
  }

  onCancelDelete() {
    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }

  // EAV Form & Template Loaders
  loadEavTemplate(equipmentTypeId: string) {
    // Không cần gọi riêng lẻ ở client nữa, FormSchema đã được LEFT JOIN trả về cùng thông tin chi tiết thiết bị
  }

  loadBhsColumns() {
    // Không cần gọi riêng lẻ ở client nữa, API by-equipment đã tích hợp trả về columns
  }

  loadDossiers() {
    const item = this.currentItem();
    if (!item?.id) return;

    this.isLoadingDossiers.set(true);
    this.dossierService.getDossiersByEquipment(item.id, this.dossierPage(), this.dossierPageSize()).subscribe({
      next: (res) => {
        if (res) {
          this.dossierItems.set(res.items || []);
          this.dossierTotalCount.set(res.totalCount || 0);
          this.dossierColumns.set(res.columns || []);
        }
        this.isLoadingDossiers.set(false);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải danh sách hồ sơ liên quan'
        });
        this.isLoadingDossiers.set(false);
      }
    });
  }

  onSaveFormValues() {
    const item = this.currentItem();
    if (!item?.id) return;

    this.isSavingFormValues.set(true);
    const payload = JSON.stringify(this.formValuesObj());
    this.equipmentService.updateFormValues(item.id, payload).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: 'Cập nhật thông số thiết bị thành công!'
        });
        this.currentItem.update(curr => ({ ...curr, formValues: payload }));
        this.isSavingFormValues.set(false);
        this.router.navigate(['/equipment/device-list', item.id], { queryParams: { mode: 'view-specs' } });
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err?.error?.message || 'Không thể cập nhật thông số thiết bị.'
        });
        this.isSavingFormValues.set(false);
      }
    });
  }

  onCancelConfigureParams() {
    const item = this.currentItem();
    if (item?.id) {
      this.router.navigate(['/equipment/device-list', item.id], { queryParams: { mode: 'view-specs' } });
    } else {
      this.isEditingFormValues.set(false);
    }
    // Reset values to original
    try {
      const parsed = this.currentItem().formValues ? JSON.parse(this.currentItem().formValues) : {};
      this.formValuesObj.set(this.initEavFormValues(this.eavFields(), parsed));
    } catch (e) {
      this.formValuesObj.set(this.initEavFormValues(this.eavFields(), {}));
    }
  }

  /** Khóa lưu giá trị EAV: ưu tiên name, fallback id để tránh trùng khi name rỗng */
  getEavFieldKey(field: any): string {
    const name = (field?.name ?? '').trim();
    return name || field?.id || '';
  }

  trackEavField(_index: number, field: any): string {
    return field?.id || this.getEavFieldKey(field) || String(_index);
  }

  initEavFormValues(fields: any[], existing: Record<string, any> = {}): Record<string, any> {
    const values: Record<string, any> = { ...existing };
    for (const field of fields) {
      const key = this.getEavFieldKey(field);
      if (!key) continue;
      if (values[key] === undefined) {
        values[key] = field.type === 'checkbox' ? false : '';
      }
    }
    return values;
  }

  getEavFieldValue(field: any): any {
    const key = this.getEavFieldKey(field);
    if (!key) return field.type === 'checkbox' ? false : '';
    const val = this.formValuesObj()[key];
    if (val === undefined || val === null) {
      return field.type === 'checkbox' ? false : '';
    }
    return val;
  }

  setEavFieldValue(field: any, value: any): void {
    const key = this.getEavFieldKey(field);
    if (!key) return;
    this.formValuesObj.update(current => ({ ...current, [key]: value }));
  }

  viewDossierDetail(dossier: any) {
    const id = dossier?.id ?? dossier?.Id;
    if (!id) return;
    const serialized = this.router.serializeUrl(
      this.router.createUrlTree(['/search/dossier-by-equipment', id])
    );
    window.open(`/#${serialized}`, '_blank');
  }

  onDossierPageChange(page: number) {
    this.dossierPage.set(page);
  }

  // --- View Doc Helpers ---
  isNumberField(field: any): boolean {
    if (field.type === 'number') return true;
    const val = this.getEavFieldValue(field);
    if (val === null || val === undefined || val === '') return false;
    return !isNaN(Number(val));
  }

  hasValue(field: any): boolean {
    const val = this.getEavFieldValue(field);
    return val !== null && val !== undefined && val !== '';
  }

  isImportantField(field: any): boolean {
    return field.required === true || field.Required === true;
  }

  getFormattedValue(field: any): string {
    const val = this.getEavFieldValue(field);
    if (val === null || val === undefined) return '';
    if (field.type === 'checkbox') {
      return val === true || val === 'true' ? 'Có' : 'Không';
    }
    return String(val);
  }

  onViewSpecs(item: any) {
    this.router.navigate(['/equipment/device-list', item.id], { queryParams: { mode: 'view-specs' } });
  }

  onEditSpecs(item: any) {
    this.router.navigate(['/equipment/device-list', item.id], { queryParams: { mode: 'edit-specs' } });
  }

  goBack() {
    this.router.navigate(['/equipment/device-list']);
  }
}
