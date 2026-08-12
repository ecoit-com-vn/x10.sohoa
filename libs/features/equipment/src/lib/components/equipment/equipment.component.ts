import { Component, OnInit, signal, computed, inject, effect, HostListener } from '@angular/core';
import {
  DeleteConfirmDialogComponent,
  EcoPaginatorComponent,
  WfBreadcrumbComponent
} from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { ToggleSwitch } from 'primeng/toggleswitch';
import { MenuItem, MessageService } from 'primeng/api';
import { Menu, MenuModule } from 'primeng/menu';
import { DatePickerModule } from 'primeng/datepicker';
import { Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '@sohoa.frontend/shared/core';
import { EquipmentService } from '../../data-access/equipment.service';
import { FormTemplateService } from '../../data-access/form-template.service';
import { DossierManagementService } from '@sohoa.frontend/features/dossier-management';
import { EMPTY, forkJoin, of } from 'rxjs';
import { catchError, finalize, switchMap, map } from 'rxjs/operators';
import { EquipmentDocumentsComponent } from '../equipment-documents/equipment-documents.component';
import { EavFormService } from '../../../../../../shared/core/src/lib/services/eav-form.service';

@Component({
  selector: 'app-equipment-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    MenuModule,
    SelectModule,
    DialogModule,
    ToggleSwitch,
    WfBreadcrumbComponent,
    EcoPaginatorComponent,
    DeleteConfirmDialogComponent,
    EquipmentDocumentsComponent,
    DatePickerModule
  ],
  providers: [MessageService],
  templateUrl: './equipment.component.html',
  styleUrls: ['./equipment.component.css']
})
export class EquipmentComponent implements OnInit {
  public equipmentService = inject(EquipmentService);
  public formTemplateService = inject(FormTemplateService);
  public dossierService = inject(DossierManagementService);
  public authService = inject(AuthService);
  private eavFormService = inject(EavFormService);
  public messageService = inject(MessageService);
  public router = inject(Router);
  public route = inject(ActivatedRoute);

  public readonly Math = Math;
  public readonly currentYear = new Date().getFullYear();

  // More menu state
  activeRowMenu = signal<string | null>(null);
  actionMenuItems: MenuItem[] = [];

  openActionMenu(item: any, event: Event, menu: Menu) {
    event.stopPropagation();
    this.actionMenuItems = [
      { label: 'Xem chi tiết', title: 'Xem chi tiết', icon: 'pi pi-eye color-teal', command: () => this.onViewSpecs(item) },
      ...(this.canEdit() && item.equipmentTypeId ? [{ label: 'Cấu hình', title: 'Cấu hình', icon: 'pi pi-cog color-blue', command: () => this.onEditSpecs(item) }] : []),
      ...(this.canEdit() ? [{ label: 'Sửa', title: 'Sửa', icon: 'pi pi-pencil color-blue', command: () => this.onEdit(item) }] : []),
      ...(this.canManage() && this.canShowTransferEquipment(item) ? [{ label: 'Chuyển thiết bị', title: 'Chuyển thiết bị', icon: 'pi pi-send color-blue', command: () => this.openTransferDialog(item) }] : []),
      ...(this.canManage() && this.canShowTransferDossier(item) ? [{ label: 'Chuyển hồ sơ', title: 'Chuyển hồ sơ', icon: 'pi pi-folder-open color-blue', command: () => this.onTransferDossier(item) }] : []),
      ...(this.canManage() ? [{
        label: item.isActive === 1 || item.isActive === true ? 'Khóa thiết bị' : 'Mở khóa',
        title: item.isActive === 1 || item.isActive === true ? 'Khóa thiết bị' : 'Mở khóa',
        icon: item.isActive === 1 || item.isActive === true ? 'pi pi-lock color-red' : 'pi pi-lock-open color-teal',
        command: () => this.onToggleStatus(item)
      }] : []),
      ...(this.canDelete() ? [{ label: 'Xóa thiết bị', title: 'Xóa thiết bị', icon: 'pi pi-trash color-red', command: () => this.onDelete(item) }] : []),
    ];
    menu.toggle(event);
  }

  @HostListener('document:click')
  closeMoreMenu() {
    this.activeRowMenu.set(null);
    this.formOrgTreeOpen.set(false);
    this.transferOrgTreeOpen.set(false);
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
  activeTab = signal<'info' | 'related' | 'profileDocs'>('info');
  searchReadOnly = signal<boolean>(false);
  dossierLookupMode = signal<boolean>(false);
  lookupDossierId = signal<string | null>(null);
  eavTemplate = signal<any>(null);
  eavFields = signal<any[]>([]);
  formValuesObj = signal<any>({});
  isEditingGeneral = signal<boolean>(false);
  isEditingFormValues = signal<boolean>(false);
  isLoadingTemplate = signal<boolean>(false);
  isSavingFormValues = signal<boolean>(false);

  // Dossiers Tab States
  public dossierItems = signal<any[]>([]);
  public dossierTotalCount = signal<number>(0);
  public dossierPage = signal<number>(1);
  public dossierPageSize = signal<number>(10);
  public dossierColumns = signal<any[]>([]);
  public isLoadingDossiers = signal<boolean>(false);

  // Lists from lookup
  organizationUnits = signal<any[]>([]);
  transferOrganizationUnits = signal<any[]>([]);
  transferInfrastructuresList = signal<any[]>([]);
  infrastructures = signal<any[]>([]);
  gridTypes = signal<any[]>([]);
  equipmentTypes = signal<any[]>([]);
  equipmentStatuses = signal<any[]>([]);

  orgUnitTree = computed(() => this.buildOrgTree(this.organizationUnits()));
  formOrgTreeOpen = signal<boolean>(false);
  formOrgSearchKeyword = signal<string>('');
  expandedFormUnitNodes = signal<Set<number>>(new Set<number>());
  transferOrgTreeOpen = signal<boolean>(false);
  transferOrgSearchKeyword = signal<string>('');
  expandedTransferUnitNodes = signal<Set<number>>(new Set<number>());

  formOrgUnitTree = computed(() => this.filterOrgTree(this.orgUnitTree(), this.formOrgSearchKeyword()));
  transferOrgUnitTree = computed(() =>
    this.filterOrgTree(this.buildOrgTree(this.transferOrganizationUnits()), this.transferOrgSearchKeyword())
  );

  // Transfer Equipment Dialog State
  showTransferDialog = signal<boolean>(false);
  transferForm = signal<{ unitId: number | null; infrastructureId: string | null; note: string }>({
    unitId: null,
    infrastructureId: null,
    note: ''
  });
  transferSubmitted = signal<boolean>(false);
  transferLoading = signal<boolean>(false);
  transferTarget = signal<any>(null);
  showTransferDossierConfirm = signal<boolean>(false);
  transferDossierTarget = signal<any>(null);
  transferDossierLoading = signal<boolean>(false);

  // Search Filters
  searchKeyword = signal<string>('');
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

  transferInfrastructures = computed(() => {
    const unitId = this.transferForm().unitId;
    const gridTypeId = this.transferTarget()?.gridTypeId ?? this.currentItem().gridTypeId;
    if (!unitId) return [];
    return this.transferInfrastructuresList().filter(inf => {
      const matchUnit = inf.unitId === Number(unitId);
      const matchGridType = !gridTypeId || this.matchesGridTypeId(inf, gridTypeId);
      return matchUnit && matchGridType;
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

  manufactureYearError = computed(() => {
    if (this.formSubmitted()) {
      const year = this.currentItem().manufactureYear;
      if (year && !this.isManufactureYearValid(year)) return `Năm sản xuất phải trong khoảng 1900 - ${this.currentYear}`;
    }
    return this.serverErrors().manufactureYear || this.serverErrors().ManufactureYear || '';
  });

  transferUnitError = computed(() => {
    if (this.transferSubmitted() && !this.transferForm().unitId) return 'Đơn vị quản lý là bắt buộc';
    return '';
  });

  transferInfrastructureError = computed(() => {
    if (this.transferSubmitted() && !this.transferForm().infrastructureId) return 'Trạm/Đường dây là bắt buộc';
    return '';
  });

  // Delete Confirmation Dialog Signals
  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<any>(null);
  deleting = signal<boolean>(false);
  // Chuẩn hóa tên thiết bị hiển thị trong popup xóa dùng chung.
  readonly deleteTargetLabel = computed(() => this.deleteTarget()?.name ?? '');

  // Lock/Unlock Confirmation Dialog Signals
  showLockUnlockConfirm = signal<boolean>(false);
  lockUnlockTarget = signal<any>(null);
  lockUnlockLoading = signal<boolean>(false);

  // Pagination Computeds
  paginatedItems = computed(() => {
    return this.items();
  });

  totalPages = computed(() => {
    return Math.ceil(this.totalCount() / this.pageSize());
  });

  // Permission Computeds
  canCreate = computed(() => this.authService.hasPermission('EQUIPMENT_CREATE') || this.authService.hasPermission('SUPER_ADMIN'));
  canEdit = computed(() =>
    this.authService.hasPermission('EQUIPMENT_EDIT') || this.authService.hasPermission('SUPER_ADMIN')
  );
  canDelete = computed(() => this.authService.hasPermission('EQUIPMENT_DELETE') || this.authService.hasPermission('SUPER_ADMIN'));
  canManage = computed(() => this.authService.hasPermission('EQUIPMENT_MANAGE') || this.authService.hasPermission('SUPER_ADMIN'));

  constructor() {
    effect(() => {
      this.currentPage();
      this.pageSize();
      if (this.currentView() === 'list') {
        this.loadItems();
      }
    });

    // Load hồ sơ liên quan chỉ khi mở tab related.
    effect(() => {
      const tab = this.activeTab();
      const page = this.dossierPage();
      const pageSize = this.dossierPageSize();
      const item = this.currentItem();
      if (tab === 'related' && item?.id) {
        this.loadDossiers();
      }
    });

  }

  ngOnInit() {
    this.searchReadOnly.set(this.route.snapshot.data['searchReadOnly'] === true);
    const routeDossierId = this.route.snapshot.paramMap.get('dossierId');
    this.lookupDossierId.set(routeDossierId);
    this.dossierLookupMode.set(this.searchReadOnly() && !!routeDossierId);
    this.authService.loadPermissions();

    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      const url = this.router.url;

      if (url.includes('/device-list/add') || url.includes('/device-add')) {
        this.currentView.set('add');
        this.currentItem.set({
          isActive: true,
          unitId: null,
          gridTypeId: null,
          infrastructureId: null,
          equipmentTypeId: null,
          manufactureYear: null,
          equipmentStatusId: null,
          code: '',
          name: ''
        });
        this.formSubmitted.set(false);
        this.serverErrors.set({});
        this.loadLookupData();
      } else if (id) {
        this.currentView.set('edit');
        this.activeTab.set('info');

        let mode = '';
        this.route.queryParams.subscribe(qParams => {
          mode = qParams['mode'] || '';
          if (mode === 'edit-specs') {
            this.isEditingFormValues.set(true);
            this.isEditingGeneral.set(false);
          } else if (mode === 'edit') {
            this.isEditingGeneral.set(true);
            this.isEditingFormValues.set(false);
          } else {
            this.isEditingFormValues.set(false);
            this.isEditingGeneral.set(false);
          }
        });

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
                unitId: res.unitId,
                infrastructureId: res.infrastructureId,
                manufactureYear: res.manufactureYear,
                equipmentStatusId: res.equipmentStatusId,
                gridTypeId: res.gridTypeId,
                isActive: res.isActive === 1 || res.isActive === true,
                formValues: res.formValues,
                equipmentTypeName: res.equipmentTypeName,
                equipmentTypeCode: res.equipmentTypeCode,
                gridTypeName: res.gridTypeName,
                infrastructureName: res.infrastructureName,
                unitName: res.unitName,
                equipmentStatusName: res.equipmentStatusName,
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
        this.applyLoggedInUserUnitFilter();
        this.loadItems();
      }
    });
  }

  isManufactureYearValid(year: any): boolean {
    const n = Number(year);
    return !Number.isNaN(n) && n >= 1900 && n <= this.currentYear;
  }

  onFieldChange(field: string) {
    this.currentItem.update(item => ({ ...item }));
    this.serverErrors.update(errs => {
      const copy = { ...errs };
      delete copy[field];
      const capitalized = field.charAt(0).toUpperCase() + field.slice(1);
      delete copy[capitalized];
      return copy;
    });
  }
  /** Danh mục Tình trạng thiết bị (catalogType = EQUIPMENT_STATUS) — tái dùng API Catalog chung (1 lần gọi theo code). */
  private loadEquipmentStatusesLookup() {
    return this.eavFormService.getCatalogsLookupByCode('EQUIPMENT_STATUS').pipe(
      map((items: any[]) => (items || []).map((item: any) => ({
        id: item.id ?? item.Id,
        code: item.code ?? item.Code,
        name: item.name ?? item.Name
      }))),
      catchError(() => of([]))
    );
  }

  loadLookupData() {
    forkJoin({
      organizationUnits: this.equipmentService.getOrganizationUnits().pipe(catchError(() => of([]))),
      infrastructures: this.equipmentService.getInfrastructures().pipe(catchError(() => of([]))),
      gridTypes: this.equipmentService.getGridTypes().pipe(catchError(() => of([]))),
      equipmentTypes: this.equipmentService.getEquipmentTypes().pipe(catchError(() => of([]))),
      equipmentStatuses: this.loadEquipmentStatusesLookup()
    }).subscribe({
      next: (data) => {
        this.organizationUnits.set(this.getAvailableOrganizationUnits(data.organizationUnits));
        this.applyLoggedInUserUnitForAdd();
        this.infrastructures.set(Array.isArray(data.infrastructures) ? data.infrastructures : []);
        this.gridTypes.set(Array.isArray(data.gridTypes) ? data.gridTypes : []);
        this.equipmentTypes.set(Array.isArray(data.equipmentTypes) ? data.equipmentTypes : []);
        this.equipmentStatuses.set(Array.isArray(data.equipmentStatuses) ? data.equipmentStatuses : []);

        // Tự động điền dữ liệu nếu có parentId từ route hoặc infrastructureId truyền qua queryParams
        const parentId = this.route.snapshot.paramMap.get('parentId');
        const queryParams = this.route.snapshot.queryParams;
        const preInfraId = parentId || queryParams['infrastructureId'];
        if (preInfraId && this.currentView() === 'add') {
          const infra = this.infrastructures().find(x => String(x.id) === String(preInfraId));
          if (infra) {
            this.currentItem.update(item => ({
              ...item,
              infrastructureId: infra.id,
              unitId: infra.unitId,
              gridTypeId: infra.gridTypeId
            }));
          }
        }
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

  getEquipmentStatusTransition(item: any): any {
    return item?.statusTransition ?? item?.StatusTransition;
  }

  hasEquipmentStatusTransition(item: any): boolean {
    const statusTransition = this.getEquipmentStatusTransition(item);
    return statusTransition !== null && statusTransition !== undefined;
  }

  isEquipmentStatusTransition(item: any, value: number): boolean {
    const statusTransition = this.getEquipmentStatusTransition(item);
    return statusTransition !== null
      && statusTransition !== undefined
      && Number(statusTransition) === value;
  }

  canShowTransferEquipment(item: any): boolean {
    return !this.hasEquipmentStatusTransition(item);
  }

  canShowTransferDossier(item: any): boolean {
    return this.isEquipmentStatusTransition(item, 0);
  }

  getEquipmentStatusLabel(item: any): string {
    const statusTransition = this.getEquipmentStatusTransition(item);
    if (statusTransition !== null && statusTransition !== undefined) {
      return Number(statusTransition) == 0 || Number(statusTransition) == 1 ? 'Đã chuyển TBA' : String(statusTransition);
    }
    return item?.isActive === 1 || item?.isActive === true ? 'Hoạt động' : 'Ngừng hoạt động';
  }

  getEquipmentStatusClass(item: any): string {
    if (this.hasEquipmentStatusTransition(item)) return 'status-inactive';
    return item?.isActive === 1 || item?.isActive === true ? 'status-active' : 'status-inactive';
  }

  loadItems() {
    const unitId = this.getEquipmentListUnitId();
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
      isActive,
      this.searchKeyword()
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

  private buildOrgTree(units: any[]): any[] {
    const map = new Map<number, any>();
    const roots: any[] = [];
    units.forEach(unit => map.set(unit.id, { ...unit, children: [] }));
    map.forEach(node => {
      if (node.parentId && map.has(node.parentId)) {
        map.get(node.parentId)!.children.push(node);
      } else {
        roots.push(node);
      }
    });
    return roots;
  }

  private getAvailableOrganizationUnits(data: unknown): any[] {
    const source = Array.isArray(data)
      ? data
      : (data as any)?.items ?? (data as any)?.Items ?? (data as any)?.data ?? (data as any)?.Data ?? [];
    const units = Array.isArray(source)
      ? source.map(unit => ({
        ...unit,
        id: unit?.id ?? unit?.Id,
        code: unit?.code ?? unit?.Code,
        name: unit?.name ?? unit?.Name,
        parentId: unit?.parentId ?? unit?.ParentId ?? null,
      }))
      : [];

    return units.filter(unit => {
      const deleted = unit?.isDeleted ?? unit?.IsDeleted;
      const status = unit?.isActive ?? unit?.IsActive ?? unit?.status ?? unit?.Status;
      const isDeleted = deleted === true || deleted === 1 || String(deleted).toLowerCase() === 'true';
      const isInactive = status === false || status === 0
        || ['0', 'false', 'inactive', 'deleted'].includes(String(status).toLowerCase());

      return !isDeleted && !isInactive;
    });
  }

  private applyLoggedInUserUnitForAdd(): void {
    if (this.currentView() !== 'add' || this.currentItem().unitId) return;

    const userUnitId = this.authService.getUserUnitId();
    if (!userUnitId) return;

    const hasUserUnit = this.organizationUnits().some(unit => Number(unit.id) === Number(userUnitId));
    if (hasUserUnit) {
      this.currentItem.update(item => ({ ...item, unitId: userUnitId }));
    }
  }

  private applyLoggedInUserUnitFilter(): void {
    const userUnitId = this.authService.getUserUnitId();
    if (userUnitId) {
      this.searchUnitId.set(String(userUnitId));
    }
  }

  private getEquipmentListUnitId(): number | undefined {
    const userUnitId = this.authService.getUserUnitId();
    if (userUnitId) return userUnitId;

    const selectedUnitId = this.searchUnitId();
    return selectedUnitId ? Number(selectedUnitId) : undefined;
  }

  private filterOrgTree(nodes: any[], value: string): any[] {
    const keyword = value.trim().toLocaleLowerCase();
    if (!keyword) return nodes;

    return nodes.reduce<any[]>((filtered, node) => {
      const children = this.filterOrgTree(node.children || [], value);
      const label = `${node.name || ''} ${node.code || ''}`.toLocaleLowerCase();
      if (label.includes(keyword) || children.length > 0) {
        filtered.push({ ...node, children });
      }
      return filtered;
    }, []);
  }

  getUnitLabel(unitId: number | null): string {
    return this.organizationUnits().find(unit => Number(unit.id) === Number(unitId))?.name || '';
  }

  getTransferUnitLabel(unitId: number | null): string {
    return this.transferOrganizationUnits().find(unit => Number(unit.id) === Number(unitId))?.name || '';
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

  toggleFormOrgTree(event?: Event) {
    if (event) event.stopPropagation();
    if (this.isSaving()) return;

    if (this.organizationUnits().length === 0) {
      this.equipmentService.getOrganizationUnits().pipe(catchError(() => of([]))).subscribe(data => {
        this.organizationUnits.set(this.getAvailableOrganizationUnits(data));
        this.formOrgTreeOpen.set(true);
      });
      return;
    }
    this.formOrgTreeOpen.update(open => !open);
  }

  toggleFormUnitNode(unitId: number, event?: Event) {
    if (event) event.stopPropagation();
    const expanded = new Set(this.expandedFormUnitNodes());
    expanded.has(unitId) ? expanded.delete(unitId) : expanded.add(unitId);
    this.expandedFormUnitNodes.set(expanded);
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
    this.formOrgSearchKeyword.set('');
    this.onFieldChange('unitId');
  }

  toggleTransferOrgTree(event?: Event) {
    if (event) event.stopPropagation();
    if (this.transferLoading()) return;

    if (this.transferOrganizationUnits().length === 0) {
      this.equipmentService.getAllOrganizationUnits().pipe(catchError(() => of([]))).subscribe(data => {
        this.transferOrganizationUnits.set(this.getAvailableOrganizationUnits(data));
        this.transferOrgTreeOpen.set(true);
      });
      return;
    }
    this.transferOrgTreeOpen.update(open => !open);
  }

  toggleTransferUnitNode(unitId: number, event?: Event) {
    if (event) event.stopPropagation();
    const expanded = new Set(this.expandedTransferUnitNodes());
    expanded.has(unitId) ? expanded.delete(unitId) : expanded.add(unitId);
    this.expandedTransferUnitNodes.set(expanded);
  }

  isTransferNodeExpanded(unitId: number): boolean {
    return this.expandedTransferUnitNodes().has(unitId);
  }

  selectTransferOrgUnit(unitId: number) {
    this.transferForm.set({
      unitId,
      infrastructureId: null,
      note: this.transferForm().note
    });
    this.transferOrgTreeOpen.set(false);
    this.transferOrgSearchKeyword.set('');
  }

  onTransferInfrastructureChange(value: string | null) {
    this.transferForm.update(form => ({ ...form, infrastructureId: value }));
  }

  onTransferNoteChange(value: string) {
    this.transferForm.update(form => ({ ...form, note: value || '' }));
  }

  onTransferDossier(item: any) {
    this.transferDossierTarget.set(item);
    this.showTransferDossierConfirm.set(true);
  }

  onCancelTransferDossier() {
    if (this.transferDossierLoading()) return;
    this.showTransferDossierConfirm.set(false);
    this.transferDossierTarget.set(null);
  }

  onConfirmTransferDossier() {
    const item = this.transferDossierTarget();
    if (!item?.id) return;

    this.transferDossierLoading.set(true);
    this.equipmentService.copyDetailById(item.id)
      .pipe(finalize(() => this.transferDossierLoading.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: 'Chuyển hồ sơ thành công!'
          });
          this.showTransferDossierConfirm.set(false);
          this.transferDossierTarget.set(null);
          this.loadItems();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể chuyển hồ sơ.'
          });
        }
      });
  }

  openTransferDialog(targetItem?: any) {
    const item = targetItem || this.currentItem();
    this.transferTarget.set(item);
    this.transferForm.set({
      unitId: null,
      infrastructureId: null,
      note: ''
    });
    this.transferSubmitted.set(false);
    this.transferOrgSearchKeyword.set('');
    this.showTransferDialog.set(true);

    if (this.transferOrganizationUnits().length === 0 || this.transferInfrastructuresList().length === 0) {
      forkJoin({
        organizationUnits: this.transferOrganizationUnits().length === 0
          ? this.equipmentService.getAllOrganizationUnits().pipe(catchError(() => of([])))
          : of(this.transferOrganizationUnits()),
        infrastructures: this.transferInfrastructuresList().length === 0
          ? this.equipmentService.getAllInfrastructures().pipe(catchError(() => of([])))
          : of(this.transferInfrastructuresList())
      }).subscribe(data => {
        this.transferOrganizationUnits.set(this.getAvailableOrganizationUnits(data.organizationUnits));
        this.transferInfrastructuresList.set(Array.isArray(data.infrastructures) ? data.infrastructures : []);
      });
    }
  }

  closeTransferDialog() {
    if (this.transferLoading()) return;
    this.showTransferDialog.set(false);
    this.transferSubmitted.set(false);
    this.transferOrgSearchKeyword.set('');
    this.transferTarget.set(null);
  }

  confirmTransferEquipment() {
    this.transferSubmitted.set(true);
    const item = this.transferTarget() || this.currentItem();
    const form = this.transferForm();
    if (!item?.id || !form.unitId || !form.infrastructureId) return;

    if (String(item.infrastructureId || '').toLowerCase() === String(form.infrastructureId).toLowerCase()) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Chuyển TBA',
        detail: 'Vui lòng chọn Trạm/Đường dây đích khác với Trạm/Đường dây hiện tại.'
      });
      return;
    }

    this.transferLoading.set(true);
    this.equipmentService.copyById(item.id, form.infrastructureId, form.note.trim())
      .pipe(finalize(() => this.transferLoading.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: 'Chuyển TBA thành công!'
          });
          this.showTransferDialog.set(false);
          this.transferSubmitted.set(false);
          this.transferTarget.set(null);
          if (this.currentView() === 'edit') {
            this.reloadDetail(item.id);
          } else {
            this.loadItems();
          }
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể chuyển thiết bị.'
          });
        }
      });
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
    this.searchKeyword.set('');
    this.searchCode.set('');
    this.searchName.set('');
    this.searchUnitId.set('');
    this.searchInfrastructureId.set('');
    this.searchGridTypeId.set('');
    this.searchEquipmentTypeId.set('');
    this.searchStatus.set('');
    this.applyLoggedInUserUnitFilter();
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

  onEquipmentListPaginatorChange(event: { first: number; rows: number }): void {
    this.pageSize.set(event.rows);
    this.currentPage.set(Math.floor(event.first / event.rows) + 1);
  }

  onAddNew() {
    this.router.navigate(['/equipment/device-list/add']);
  }

  onEdit(item: any) {
    this.router.navigate(['/equipment/device-list', item.id]);
  }

  onSaveItem() {
    this.formSubmitted.set(true);
    this.serverErrors.set({});
    const item = this.currentItem();

    if (!item.code || !item.name || !item.unitId || !item.gridTypeId || !item.infrastructureId || !item.equipmentTypeId
      || (item.manufactureYear && !this.isManufactureYearValid(item.manufactureYear))) {
      return;
    }

    this.isSaving.set(true);

    const payload = {
      equipmentTypeId: item.equipmentTypeId,
      infrastructureId: item.infrastructureId,
      code: item.code.trim(),
      name: item.name.trim(),
      manufactureYear: item.manufactureYear ? Number(item.manufactureYear) : null,
      equipmentStatusId: item.equipmentStatusId || null,
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
          ? this.equipmentService.create({ ...payload, statusTransition: null })
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

  onToggleStatusRequest(item: any) {
    console.log('onToggleStatusRequest was called with item:', item);
    this.lockUnlockTarget.set(item);
    this.showLockUnlockConfirm.set(true);
    console.log('showLockUnlockConfirm is now:', this.showLockUnlockConfirm());
  }

  onToggleStatus(item: any) {
    console.log('onToggleStatus (alias) was called with item:', item);
    this.onToggleStatusRequest(item);
  }

  onCancelLockUnlock() {
    console.log('onCancelLockUnlock was called');
    this.showLockUnlockConfirm.set(false);
    this.lockUnlockTarget.set(null);
  }

  onConfirmLockUnlock() {
    console.log('onConfirmLockUnlock was called');
    const item = this.lockUnlockTarget();
    if (!item) {
      console.log('No lockUnlockTarget item found!');
      return;
    }

    this.lockUnlockLoading.set(true);
    const isLocking = item.isActive === 1 || item.isActive === true;
    console.log('Calling equipmentService.toggleStatus with id:', item.id, 'isLocking:', isLocking);
    this.equipmentService.toggleStatus(item.id, isLocking)
      .pipe(
        finalize(() => {
          this.lockUnlockLoading.set(false);
          console.log('toggleStatus API call completed');
        })
      )
      .subscribe({
        next: (res) => {
          console.log('toggleStatus success response:', res);
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: res.message || (isLocking ? 'Khóa thiết bị thành công!' : 'Mở khóa thiết bị thành công!')
          });
          this.showLockUnlockConfirm.set(false);
          this.lockUnlockTarget.set(null);
          this.loadItems();
        },
        error: (err) => {
          console.error('toggleStatus error response:', err);
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
    // Chặn target không hợp lệ hoặc request xóa bị gửi trùng.
    if (!item || this.deleting()) return;

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
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể xóa thiết bị.'
          });
        }
      });
  }

  onCancelDelete() {
    // Không đóng popup khi request xóa đang được xử lý.
    if (this.deleting()) return;

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
    if (val === undefined || val === null || val === '') {
      return field.type === 'checkbox' ? false : '';
    }
    if (field.type === 'date') {
      const d = val instanceof Date ? val : new Date(val);
      return isNaN(d.getTime()) ? null : d;
    }
    return val;
  }

  setEavFieldValue(field: any, value: any): void {
    const key = this.getEavFieldKey(field);
    if (!key) return;
    let stored = value;
    if (field.type === 'date' && value instanceof Date && !isNaN(value.getTime())) {
      const y = value.getFullYear();
      const m = String(value.getMonth() + 1).padStart(2, '0');
      const dd = String(value.getDate()).padStart(2, '0');
      stored = `${y}-${m}-${dd}`;
    }
    this.formValuesObj.update(current => ({ ...current, [key]: stored }));
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

  onRelatedDossierPageChange(event: { page: number; rows: number }) {
    this.dossierPage.set(event.page + 1);
    this.dossierPageSize.set(event.rows);
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
    if (val === null || val === undefined || val === '') return '';
    if (field.type === 'checkbox') {
      return val === true || val === 'true' ? 'Có' : 'Không';
    }
    if (field.type === 'date') {
      const d = val instanceof Date ? val : new Date(val);
      return isNaN(d.getTime()) ? String(val) : d.toLocaleDateString('vi-VN');
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
    const url = this.router.url;
    if (url.includes('/catalog/substation/')) {
      const parentId = this.route.snapshot.paramMap.get('parentId');
      this.router.navigate(['/catalog/substation', parentId]);
    } else if (url.includes('/catalog/transmission-line/')) {
      const parentId = this.route.snapshot.paramMap.get('parentId');
      this.router.navigate(['/catalog/transmission-line', parentId]);
    } else {
      this.router.navigate(['/equipment/device-list']);
    }
  }

  onEquipmentSpecsSaved(): void {
    const item = this.currentItem();
    if (!item?.id) return;
    this.equipmentService.getById(item.id).subscribe({
      next: (res) => {
        if (!res) return;
        try {
          const parsed = res.formValues ? JSON.parse(res.formValues) : {};
          this.formValuesObj.set(this.initEavFormValues(this.eavFields(), parsed));
          this.currentItem.update((current) => ({ ...current, formValues: res.formValues }));
        } catch {
          // ignore
        }
      },
    });
  }

  reloadDetail(id: string | null | undefined): void {
    if (!id) return;
    this.equipmentService.getById(id).subscribe({
      next: (res) => {
        if (res) {
          this.currentItem.set({
            id: res.id,
            equipmentTypeId: res.equipmentTypeId,
            name: res.name,
            code: res.code,
            unitId: res.unitId,
            infrastructureId: res.infrastructureId,
            manufactureYear: res.manufactureYear,
            equipmentStatusId: res.equipmentStatusId,
            gridTypeId: res.gridTypeId,
            isActive: res.isActive === 1 || res.isActive === true,
            formValues: res.formValues,
            equipmentTypeName: res.equipmentTypeName,
            equipmentTypeCode: res.equipmentTypeCode,
            gridTypeName: res.gridTypeName,
            infrastructureName: res.infrastructureName,
            unitName: res.unitName,
            equipmentStatusName: res.equipmentStatusName,
            creator: res.creator,
            createdBy: res.createdBy
          });

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

          try {
            const parsed = res.formValues ? JSON.parse(res.formValues) : {};
            this.formValuesObj.set(this.initEavFormValues(parsedFields, parsed));
          } catch (e) {
            this.formValuesObj.set(this.initEavFormValues(parsedFields, {}));
          }
        }
      }
    });
  }
}
