import { Component, OnInit, signal, computed, inject, effect, HostListener } from '@angular/core';
import {
  DeleteConfirmDialogComponent,
  EcoPaginatorComponent,
  WfBreadcrumbComponent
} from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer } from '@angular/platform-browser';
import { ToastModule } from 'primeng/toast';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { Menu, MenuModule } from 'primeng/menu';
import { MenuItem, MessageService } from 'primeng/api';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService, BreadcrumbTrailItem } from '@sohoa.frontend/shared/core';
import { InfrastructureService } from '../../data-access/infrastructure.service';
import { EquipmentService } from '@sohoa.frontend/features/equipment';
import { DossierDocumentService, DossierManagementService } from '@sohoa.frontend/features/dossier-management';
import { forkJoin, of } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';

@Component({
  selector: 'app-infrastructure',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    SelectModule,
    DatePickerModule,
    DialogModule,
    MenuModule,
    EcoPaginatorComponent,
    WfBreadcrumbComponent,
    DeleteConfirmDialogComponent
  ],
  providers: [MessageService],
  templateUrl: './infrastructure.component.html',
  styleUrl: './infrastructure.component.scss'
})
export class InfrastructureComponent implements OnInit {
  private infraService = inject(InfrastructureService);
  private equipmentService = inject(EquipmentService);
  private authService = inject(AuthService);
  private messageService = inject(MessageService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private dossierService = inject(DossierManagementService);
  private dossierDocumentService = inject(DossierDocumentService);
  private sanitizer = inject(DomSanitizer);

  protected readonly Math = Math;

  // Dynamic Route Data
  infraTypeId = signal<number>(1);
  pageTitle = signal<string>('Danh mục cơ sở hạ tầng');

  // States
  items = signal<any[]>([]);
  orgUnits = signal<any[]>([]);
  gridTypes = signal<any[]>([]);
  searchKeyword = signal<string>('');
  searchStatus = signal<string>(''); // '', '1', '0'
  searchUnitId = signal<number | null>(null);
  totalCount = signal<number>(0);
  // Personal catalog only toggle
  searchPersonalOnly = signal<boolean>(false);

  // Lock/Unlock Confirmation Dialog Signals
  showLockUnlockConfirm = signal<boolean>(false);
  lockUnlockTarget = signal<any>(null);
  lockUnlockLoading = signal<boolean>(false);

  // Equipment Lock/Unlock Confirmation Signals
  showEquipmentLockConfirm = signal<boolean>(false);
  equipmentLockTarget = signal<any>(null);
  equipmentLockLoading = signal<boolean>(false);

  // Equipment transfer signals in infrastructure detail
  showTransferDialog = signal<boolean>(false);
  transferForm = signal<{ unitId: number | null; infrastructureId: string | null; note: string }>({
    unitId: null,
    infrastructureId: null,
    note: ''
  });
  transferSubmitted = signal<boolean>(false);
  transferLoading = signal<boolean>(false);
  transferTarget = signal<any>(null);
  transferInfrastructuresSource = signal<any[]>([]);
  transferOrganizationUnits = signal<any[]>([]);

  showTransferDossierConfirm = signal<boolean>(false);
  transferDossierTarget = signal<any>(null);
  transferDossierLoading = signal<boolean>(false);

  currentView = signal<'list' | 'add' | 'edit' | 'detail'>('list');
  currentItem = signal<any>({});
  isSaving = signal<boolean>(false);

  // Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  // Form Validation
  formSubmitted = signal<boolean>(false);
  serverErrors = signal<any>({});

  codeError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().code) return 'Mã là bắt buộc';
    return this.serverErrors().code || this.serverErrors().Code || '';
  });

  nameError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().name) return 'Tên là bắt buộc';
    return this.serverErrors().name || this.serverErrors().Name || '';
  });

  gridTypeIdError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().gridTypeId) return 'Loại lưới điện là bắt buộc';
    return this.serverErrors().gridTypeId || this.serverErrors().GridTypeId || '';
  });

  // Delete Confirmation Dialog Signals
  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<any>(null);
  deleting = signal<boolean>(false);
  // Chuẩn hóa loại hạ tầng theo route để popup xóa dùng đúng ngữ cảnh.
  readonly deleteEntityLabel = computed(() =>
    this.infraTypeId() === 1 ? 'Trạm biến áp' : 'Đường dây'
  );
  // Chuẩn hóa tên hạ tầng hiển thị trong popup xóa dùng chung.
  readonly deleteTargetLabel = computed(() => this.deleteTarget()?.name ?? '');

  // Org-unit tree picker signals
  orgUnitTree = computed(() => this.buildOrgTree(this.orgUnits()));
  expandedUnitNodes = signal<Set<any>>(new Set<any>());
  orgTreePickerOpen = signal<boolean>(false);
  orgTreeSearchKeyword = signal<string>('');
  filteredOrgUnitTree = computed(() => this.filterOrgTree(this.orgUnitTree(), this.orgTreeSearchKeyword()));
  searchOrgTreeOpen = signal<boolean>(false);
  searchOrgSearchKeyword = signal<string>('');
  expandedSearchUnitNodes = signal<Set<any>>(new Set<any>());
  searchOrgUnitTree = computed(() => this.filterOrgTree(this.orgUnitTree(), this.searchOrgSearchKeyword()));
  transferOrgTreeOpen = signal<boolean>(false);
  transferOrgSearchKeyword = signal<string>('');
  expandedTransferUnitNodes = signal<Set<any>>(new Set<any>());
  transferOrgUnitTree = computed(() =>
    this.filterOrgTree(this.buildOrgTree(this.transferOrganizationUnits()), this.transferOrgSearchKeyword())
  );

  onSearchKeywordChange(value: string) {
    this.searchKeyword.set(value);
    this.currentPage.set(1);
  }

  onSearchStatusChange(value: string) {
    this.searchStatus.set(value);
    this.currentPage.set(1);
  }

  // Pagination Computeds
  paginatedItems = computed(() => {
    return this.items();
  });

  totalPages = computed(() => {
    return Math.ceil(this.totalCount() / this.pageSize());
  });

  /**
   * Breadcrumb tường minh cho màn chi tiết — chỉ 2 cấp: "Trạm biến áp/Đường dây" (link) + pageTitle() (leaf).
   * Không dùng trail tự resolve từ menu (tránh nhảy thêm cấp cha) + không bind viewMode (tránh lặp "Chi tiết").
   */
  detailBreadcrumbItems = computed<BreadcrumbTrailItem[]>(() => {
    const isSubstation = this.infraTypeId() === 1;
    return [
      { label: isSubstation ? 'Trạm biến áp' : 'Đường dây', url: isSubstation ? '/catalog/substation' : '/catalog/transmission-line' },
      { label: this.pageTitle() }
    ];
  });

  // ── DETAIL VIEW SIGNALS ────────────────────────────────────────────────────
  activeTab = signal<number>(0);

  // Hồ sơ liên quan trong tab chi tiết
  relatedDossiers = signal<any[]>([]);
  relatedDossiersTotalCount = signal<number>(0);
  relatedDossiersPage = signal<number>(1);
  relatedDossiersPageSize = signal<number>(10);
  isLoadingRelatedDossiers = signal<boolean>(false);
  relatedDossiersSearchKeyword = signal<string>('');
  technicalFolderSearchKeyword = signal<string>('');
  expandedTechnicalFolders = signal<Set<string>>(new Set<string>());
  technicalDocumentsByDossier = signal<Record<string, any[]>>({});
  loadingTechnicalFolders = signal<Set<string>>(new Set<string>());
  showTechnicalDocumentPreview = signal<boolean>(false);
  technicalPreviewTarget = signal<{ dossierId: string; document: any } | null>(null);
  technicalPreviewUrl = signal<string | null>(null);
  loadingTechnicalDocumentPreview = signal<boolean>(false);
  attachmentFolderSearchKeyword = signal<string>('');
  attachmentDossierDocuments = signal<Array<{ dossier: any; document: any }>>([]);
  loadingAttachmentDocuments = signal<boolean>(false);
  expandedAttachmentFolders = signal<Set<string>>(new Set<string>());
  selectedAttachmentFolderId = signal<string | null>(null);
  attachmentStationFolderExpanded = signal<boolean>(true);
  attachmentDocumentKeyword = signal<string>('');
  attachmentSelectedEquipmentId = signal<string>('');
  attachmentEquipmentOptions = signal<any[]>([]);
  attachmentDocumentPage = signal<number>(1);
  attachmentDocumentPageSize = signal<number>(10);
  selectedTechnicalFolderId = signal<string | null>(null);
  technicalStationFolderExpanded = signal<boolean>(true);
  technicalDocumentKeyword = signal<string>('');
  technicalSelectedDocumentTypeId = signal<string>('');
  technicalDocumentPage = signal<number>(1);
  technicalDocumentPageSize = signal<number>(10);

  technicalPreviewSrc = computed(() => {
    const url = this.technicalPreviewUrl();
    return url ? this.sanitizer.bypassSecurityTrustResourceUrl(url) : null;
  });

  attachmentDocumentFolders = computed(() => {
    const keyword = this.attachmentFolderSearchKeyword().trim().toLocaleLowerCase();
    const groups = new Map<string, { id: string; name: string; documents: Array<{ dossier: any; document: any }> }>();

    this.attachmentDossierDocuments().forEach(item => {
      const name = item.document.documentTypeName || 'Chưa phân loại';
      const id = String(item.document.documentTypeId || name);
      if (!groups.has(id)) groups.set(id, { id, name, documents: [] });
      groups.get(id)!.documents.push(item);
    });

    return Array.from(groups.values())
      .filter(folder => !keyword || folder.name.toLocaleLowerCase().includes(keyword))
      .sort((a, b) => a.name.localeCompare(b.name, 'vi'));
  });

  selectedAttachmentFolder = computed(() =>
    this.attachmentDocumentFolders().find(folder => folder.id === this.selectedAttachmentFolderId()) ?? null
  );

  selectedAttachmentDocuments = computed(() => {
    const keyword = this.attachmentDocumentKeyword().trim().toLocaleLowerCase();
    const equipmentId = this.attachmentSelectedEquipmentId();
    const equipment = this.attachmentEquipmentOptions().find(item => String(item.id) === equipmentId);
    const equipmentName = (equipment?.name || equipment?.equipmentName || '').toLocaleLowerCase();

    return (this.selectedAttachmentFolder()?.documents ?? []).filter(item => {
      const documentName = (item.document.name || item.document.fileName || '').toLocaleLowerCase();
      const itemEquipmentId = String(
        item.document.equipmentId
        || item.document.equipment?.id
        || item.dossier.equipmentId
        || item.dossier.equipment?.id
        || ''
      );
      const itemEquipmentName = this.getAttachmentEquipmentName(item).toLocaleLowerCase();
      return (!keyword || documentName.includes(keyword))
        && (!equipmentId || itemEquipmentId === equipmentId || (!!equipmentName && itemEquipmentName === equipmentName));
    });
  });

  attachmentDocumentTotalPages = computed(() =>
    Math.max(1, Math.ceil(this.selectedAttachmentDocuments().length / this.attachmentDocumentPageSize()))
  );

  pagedAttachmentDocuments = computed(() => {
    const start = (this.attachmentDocumentPage() - 1) * this.attachmentDocumentPageSize();
    return this.selectedAttachmentDocuments().slice(start, start + this.attachmentDocumentPageSize());
  });
  relatedDossiersTotalPages = computed(() =>
    Math.ceil(this.relatedDossiersTotalCount() / this.relatedDossiersPageSize())
  );

  technicalDossierFolders = computed(() => {
    const keyword = this.technicalFolderSearchKeyword().trim().toLocaleLowerCase();
    const groups = new Map<string, { id: string; name: string; dossiers: any[] }>();

    this.relatedDossiers().forEach(dossier => {
      const name = dossier.dossierTypeName || 'Chưa phân loại';
      const id = String(dossier.dossierTypeId || name);
      if (!groups.has(id)) groups.set(id, { id, name, dossiers: [] });
      groups.get(id)!.dossiers.push(dossier);
    });

    return Array.from(groups.values())
      .filter(folder => !keyword || folder.name.toLocaleLowerCase().includes(keyword))
      .sort((a, b) => a.name.localeCompare(b.name, 'vi'));
  });

  selectedTechnicalFolder = computed(() =>
    this.technicalDossierFolders().find(folder => folder.id === this.selectedTechnicalFolderId()) ?? null
  );

  selectedTechnicalDocuments = computed(() => {
    const keyword = this.technicalDocumentKeyword().trim().toLocaleLowerCase();
    const documentTypeId = this.technicalSelectedDocumentTypeId();

    return this.getTechnicalFolderDocuments(this.selectedTechnicalFolder() ?? { dossiers: [] }).filter(item => {
      const documentName = (item.document.name || item.document.fileName || '').toLocaleLowerCase();
      return (!keyword || documentName.includes(keyword))
        && (!documentTypeId || String(item.document.documentTypeId || '') === documentTypeId);
    });
  });

  technicalDocumentTotalPages = computed(() =>
    Math.max(1, Math.ceil(this.selectedTechnicalDocuments().length / this.technicalDocumentPageSize()))
  );

  pagedTechnicalDocuments = computed(() => {
    const start = (this.technicalDocumentPage() - 1) * this.technicalDocumentPageSize();
    return this.selectedTechnicalDocuments().slice(start, start + this.technicalDocumentPageSize());
  });

  technicalDocumentTypeOptions = computed(() => {
    const types = new Map<string, { id: string; name: string }>();
    this.attachmentDossierDocuments().forEach(item => {
      const id = item.document.documentTypeId;
      const name = item.document.documentTypeName;
      if (id && name) types.set(String(id), { id: String(id), name });
    });
    return Array.from(types.values()).sort((a, b) => a.name.localeCompare(b.name, 'vi'));
  });

  // Danh sách thiết bị trong tab chi tiết
  equipmentItems = signal<any[]>([]);
  equipmentTotalCount = signal<number>(0);
  equipmentPage = signal<number>(1);
  equipmentPageSize = signal<number>(10);
  equipmentTypes = signal<any[]>([]);
  isLoadingEquipments = signal<boolean>(false);

  // Search thiết bị
  equipmentSearchKeyword = signal<string>('');
  equipmentSearchTypeId = signal<string>('');

  equipmentTotalPages = computed(() =>
    Math.ceil(this.equipmentTotalCount() / this.equipmentPageSize())
  );

  equipmentTypesForCurrentInfrastructure = computed(() => {
    const gridTypeId = this.currentItem()?.gridTypeId;
    if (!gridTypeId) return [];
    return this.equipmentTypes().filter(type => this.matchesGridTypeId(type, gridTypeId));
  });

  transferInfrastructures = computed(() => {
    const unitId = this.transferForm().unitId;
    const gridTypeId = this.transferTarget()?.gridTypeId ?? this.currentItem().gridTypeId;
    if (!unitId) return [];

    return this.transferInfrastructuresSource().filter(inf => {
      const matchUnit = inf.unitId === Number(unitId);
      const matchGridType = !gridTypeId || this.matchesGridTypeId(inf, gridTypeId);
      return matchUnit && matchGridType;
    });
  });

  // More-menu 3 chấm cho bảng thiết bị
  activeEquipmentMenu = signal<string | null>(null);
  actionMenuItems: MenuItem[] = [];

  @HostListener('document:click')
  closeActionMenus() {
    this.activeEquipmentMenu.set(null);
    this.searchOrgTreeOpen.set(false);
    this.transferOrgTreeOpen.set(false);
  }

  openActionMenu(item: any, event: Event, menu: Menu): void {
    event.stopPropagation();
    this.actionMenuItems = [
      { label: 'Xem chi tiết', title: 'Xem chi tiết', icon: 'pi pi-eye color-teal', command: () => this.onViewDetail(item) },
      ...(this.canEdit() ? [{ label: 'Chỉnh sửa', title: 'Chỉnh sửa', icon: 'pi pi-pencil color-blue', command: () => this.onEdit(item) }] : []),
      ...(this.canManage() 
      ? [{ label: (item.isActive === 1 || item.isActive === true) 
        ? 'Khóa' 
        : 'Mở khóa', title: (item.isActive === 1 || item.isActive === true) 
        ? 'Khóa bản ghi' : 'Mở khóa bản ghi', icon: (item.isActive === 1 || item.isActive === true) 
        ? 'pi pi-lock color-red' 
        : 'pi pi-lock-open color-teal', command: () => this.onToggleStatus(item) }] : []),
      ...(this.canDelete() ? [{ label: 'Xóa', title: 'Xóa', icon: 'pi pi-trash color-red', command: () => this.onDelete(item) }] : []),
    ];
    menu.toggle(event);
  }

  toggleEquipmentMenu(item: any, event: Event) {
    event.stopPropagation();
    if (this.activeEquipmentMenu() === item.id) {
      this.activeEquipmentMenu.set(null);
    } else {
      this.activeEquipmentMenu.set(item.id);
    }
  }
  // ── END DETAIL VIEW SIGNALS ────────────────────────────────────────────────

  // Dynamic Permissions check per catalog type
  canCreate = computed(() => {
    const perm = this.infraTypeId() === 1 ? 'SUBSTATION_CREATE' : 'TRANSMISSION_LINE_CREATE';
    return this.authService.hasPermission(perm) || this.authService.hasPermission('SUPER_ADMIN');
  });

  canEdit = computed(() => {
    const perm = this.infraTypeId() === 1 ? 'SUBSTATION_EDIT' : 'TRANSMISSION_LINE_EDIT';
    return this.authService.hasPermission(perm) || this.authService.hasPermission('SUPER_ADMIN');
  });

  canDelete = computed(() => {
    const perm = this.infraTypeId() === 1 ? 'SUBSTATION_DELETE' : 'TRANSMISSION_LINE_DELETE';
    return this.authService.hasPermission(perm) || this.authService.hasPermission('SUPER_ADMIN');
  });

  canManage = computed(() => {
    const perm = this.infraTypeId() === 1 ? 'SUBSTATION_MANAGE' : 'TRANSMISSION_LINE_MANAGE';
    return this.authService.hasPermission(perm) || this.authService.hasPermission('SUPER_ADMIN');
  });

  canCreateEquipment = computed(() =>
    this.authService.hasPermission('EQUIPMENT_CREATE') || this.authService.hasPermission('SUPER_ADMIN')
  );

  constructor() {
    effect(() => {
      // Re-trigger load when page, pageSize, or infraTypeId changes
      this.currentPage();
      this.pageSize();
      this.infraTypeId();
      if (this.currentView() === 'list') {
        this.loadItems();
      }
    }, { allowSignalWrites: true });

    effect(() => {
      if (this.currentView() === 'detail' && this.activeTab() === 2) {
        this.loadRelatedDossiers();
      }
    }, { allowSignalWrites: true });

    effect(() => {
      if (this.currentView() === 'detail' && this.activeTab() === 1 && this.currentItem()?.id) {
        this.loadInfrastructureAttachmentDocuments();
      }
    }, { allowSignalWrites: true });

    if (typeof window !== 'undefined') {
      window.addEventListener('click', () => {
        this.orgTreePickerOpen.set(false);
        this.searchOrgTreeOpen.set(false);
      });
    }
  }

  ngOnInit() {
    this.authService.loadPermissions();
    this.loadOrgUnits();
    this.loadGridTypes();
    this.loadEquipmentTypes();

    // Listen to route data changes to adapt dynamically
    this.route.data.subscribe(data => {
      if (data) {
        this.infraTypeId.set(data['infraTypeId'] || 1);
        this.pageTitle.set(data['title'] || 'Danh mục cơ sở hạ tầng');
        // Reset state on route switch
        this.currentPage.set(1);
        this.searchKeyword.set('');
        this.searchStatus.set('');
        this.searchUnitId.set(null);
        this.searchOrgSearchKeyword.set('');
      }
    });

    // Detect detail route (has :id param)
    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.currentView.set('detail');
        const tabParam = this.route.snapshot.queryParamMap.get('tab');
        this.activeTab.set(tabParam ? Number(tabParam) : 0);
        this.equipmentPage.set(1);
        this.equipmentSearchKeyword.set('');
        this.equipmentSearchTypeId.set('');
        // Load item detail
        this.infraService.getInfrastructureById(this.infraTypeId(), id).subscribe({
          next: (res) => {
            this.currentItem.set(res || {});
            this.loadEquipments();
            this.loadRelatedDossiers();
          },
          error: () => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải thông tin chi tiết.' });
          }
        });
      } else {
        this.currentView.set('list');
        this.loadItems();
      }
    });
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

  loadOrgUnits() {
    this.infraService.getOrganizationUnits().subscribe({
      next: (data) => {
        const rawUnits = Array.isArray(data) ? data : (data && Array.isArray((data as any).items) ? (data as any).items : (data && Array.isArray((data as any).value) ? (data as any).value : []));
        this.orgUnits.set(rawUnits);
      },
      error: () => {
        console.error('Không thể tải danh sách đơn vị');
      }
    });
  }

  loadGridTypes() {
    this.infraService.getGridTypes().subscribe({
      next: (data) => {
        this.gridTypes.set(data || []);
      },
      error: () => {
        console.error('Không thể tải danh sách loại lưới điện');
      }
    });
  }

  loadEquipmentTypes() {
    this.infraService.getEquipmentTypes().subscribe({
      next: (data) => {
        this.equipmentTypes.set(data || []);
      },
      error: () => {
        console.error('Không thể tải danh sách loại thiết bị');
      }
    });
  }

  // ── Org-unit Tree Picker methods ──────────────────────────────────────────
  buildOrgTree(units: any[]): any[] {
    const map = new Map<any, any>();
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

  toggleUnitNode(unitId: any, event?: Event) {
    if (event) event.stopPropagation();
    const current = new Set(this.expandedUnitNodes());
    if (current.has(unitId)) {
      current.delete(unitId);
    } else {
      current.add(unitId);
    }
    this.expandedUnitNodes.set(current);
  }

  isNodeExpanded(unitId: any): boolean {
    return this.expandedUnitNodes().has(unitId);
  }

  selectOrgUnit(unitId: any) {
    this.currentItem.update(u => ({ ...u, unitId: unitId }));
    this.orgTreePickerOpen.set(false);
    this.orgTreeSearchKeyword.set('');
    this.onFieldChange('unitId');
  }

  toggleOrgTreePicker(event?: Event) {
    if (event) event.stopPropagation();
    this.orgTreePickerOpen.update(open => {
      const nextOpen = !open;
      if (!nextOpen) this.orgTreeSearchKeyword.set('');
      return nextOpen;
    });
  }

  toggleSearchOrgTree(event?: Event) {
    if (event) event.stopPropagation();
    this.searchOrgTreeOpen.update(open => !open);
  }

  toggleSearchUnitNode(unitId: any, event?: Event) {
    if (event) event.stopPropagation();
    const expanded = new Set(this.expandedSearchUnitNodes());
    expanded.has(unitId) ? expanded.delete(unitId) : expanded.add(unitId);
    this.expandedSearchUnitNodes.set(expanded);
  }

  isSearchNodeExpanded(unitId: any): boolean {
    return this.expandedSearchUnitNodes().has(unitId);
  }

  selectSearchOrgUnit(unitId: any) {
    this.searchUnitId.set(Number(unitId));
    this.searchOrgTreeOpen.set(false);
    this.searchOrgSearchKeyword.set('');
    this.onSearch();
  }

  clearSearchOrgUnit(event: Event) {
    event.stopPropagation();
    this.searchUnitId.set(null);
    this.searchOrgTreeOpen.set(false);
    this.searchOrgSearchKeyword.set('');
    this.onSearch();
  }

  getUnitLabel(unitId: any): string {
    if (!unitId) return '';
    const u = (this.orgUnits() || []).find(x => x.id == unitId);
    return u ? u.name : '';
  }

  getTransferUnitLabel(unitId: any): string {
    if (!unitId) return '';
    const u = (this.transferOrganizationUnits() || []).find(x => Number(x.id) === Number(unitId));
    return u ? u.name : '';
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

  private matchesGridTypeId(item: any, gridTypeId: any): boolean {
    if (gridTypeId == null || gridTypeId === '') return false;
    const selectedGridType = Number(gridTypeId);
    const itemGridType = Number(item?.gridTypeId ?? item?.GridTypeId);
    return !Number.isNaN(selectedGridType)
      && !Number.isNaN(itemGridType)
      && itemGridType === selectedGridType;
  }

  clearOrgUnit(event: Event) {
    event.stopPropagation();
    this.currentItem.update(u => ({ ...u, unitId: null }));
    this.onFieldChange('unitId');
  }

  loadItems() {
    this.infraService.getInfrastructures(
      this.infraTypeId(),
      this.currentPage(),
      this.pageSize(),
      this.searchKeyword(),
      this.searchStatus(),
      this.searchUnitId(),
      this.searchPersonalOnly()
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
          detail: 'Không thể tải danh sách cơ sở hạ tầng'
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
    this.searchUnitId.set(null);
    this.searchOrgSearchKeyword.set('');
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

  onInfrastructurePageChange(event: { first?: number; rows?: number }) {
    const rows = Number(event.rows) || this.pageSize();
    const first = Number(event.first) || 0;
    this.pageSize.set(rows);
    this.currentPage.set(Math.floor(first / rows) + 1);
  }

  onAddNew() {
    this.currentItem.set({
      isActive: true,
      infraTypeId: this.infraTypeId(),
      unitId: null,
      gridTypeId: null,
      address: '',
      operationDate: null,
      organization: null
    });
    this.orgTreePickerOpen.set(false);
    this.orgTreeSearchKeyword.set('');
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.currentView.set('add');
  }

  onEdit(item: any) {
    this.currentItem.set({
      ...item,
      operationDate: item.operationDate ? new Date(item.operationDate) : null
    });
    this.orgTreePickerOpen.set(false);
    this.orgTreeSearchKeyword.set('');
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.currentView.set('edit');
  }

  // Điều hướng vào màn hình chi tiết
  onViewDetail(item: any) {
    const segment = this.infraTypeId() === 1 ? 'substation' : 'transmission-line';
    this.router.navigate(['/catalog', segment, item.id]);
  }

  // Quay lại danh sách
  goBack() {
    const segment = this.infraTypeId() === 1 ? 'substation' : 'transmission-line';
    this.router.navigate(['/catalog', segment]);
  }

  isGridTypeLocked(item?: any): boolean {
    if (this.currentView() !== 'edit') return false;
    const target = item ?? this.currentItem();
    const count = Number(target?.equipmentCount ?? target?.EquipmentCount ?? 0);
    return count > 0;
  }

  /**
   * Chuyển Date của p-datepicker thành chuỗi 'yyyy-MM-dd' theo giờ local trước khi gửi lên BE.
   * Nếu gửi thẳng đối tượng Date, HttpClient JSON.stringify sẽ gọi Date.toISOString() (quy về UTC),
   * khiến ngày bị lùi 1 ngày với múi giờ dương (vd. UTC+7) do Oracle TIMESTAMP lưu nguyên giá trị nhận được.
   */
  private toDateOnlyString(value: any): string | null {
    if (!value) return null;
    const d = value instanceof Date ? value : new Date(value);
    if (isNaN(d.getTime())) return null;
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  onSaveItem() {
    this.formSubmitted.set(true);
    this.serverErrors.set({});
    const item = this.currentItem();

    if (!item.code || !item.name || !item.gridTypeId) {
      return;
    }

    this.isSaving.set(true);

    const payload = {
      id: item.id,
      code: item.code.trim(),
      name: item.name.trim(),
      address: item.address ? item.address.trim() : null,
      infraTypeId: this.infraTypeId(),
      unitId: item.unitId || null,
      gridTypeId: item.gridTypeId,
      operationDate: this.toDateOnlyString(item.operationDate),
      isActive: item.isActive
    };

    const request$ = this.currentView() === 'add'
      ? this.infraService.createInfrastructure(this.infraTypeId(), payload)
      : this.infraService.updateInfrastructure(this.infraTypeId(), item.id, payload);

    request$.pipe(
      finalize(() => this.isSaving.set(false))
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: this.currentView() === 'add' ? 'Đã thêm mới thành công!' : 'Đã cập nhật thành công!'
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
    this.lockUnlockTarget.set(item);
    this.showLockUnlockConfirm.set(true);
  }

  onCancelLockUnlock() {
    this.showLockUnlockConfirm.set(false);
    this.lockUnlockTarget.set(null);
  }

  onConfirmLockUnlock() {
    const item = this.lockUnlockTarget();
    if (!item) return;

    const isLocking = item.isActive === 1 || item.isActive === true;
    this.lockUnlockLoading.set(true);
    this.infraService.toggleStatus(this.infraTypeId(), item.id, isLocking)
      .pipe(
        finalize(() => {
          this.lockUnlockLoading.set(false);
          this.showLockUnlockConfirm.set(false);
          this.lockUnlockTarget.set(null);
        })
      ).subscribe({
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
            detail: err?.error?.message || 'Không thể cập nhật trạng thái.'
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
    this.infraService.deleteInfrastructure(this.infraTypeId(), item.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Xóa thành công',
            detail: `Đã xóa "${item.name}" thành công!`
          });
          this.showDeleteConfirm.set(false);
          this.deleteTarget.set(null);
          this.loadItems();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể xóa bản ghi.'
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

  // ── DETAIL VIEW METHODS ────────────────────────────────────────────────────

  loadEquipments() {
    const item = this.currentItem();
    if (!item?.id) return;

    this.isLoadingEquipments.set(true);
    const keyword = this.equipmentSearchKeyword();
    const typeId = this.equipmentSearchTypeId();

    this.equipmentService.getEquipments(
      this.equipmentPage(),
      this.equipmentPageSize(),
      undefined, // code
      undefined, // name
      undefined, // unitId
      String(item.id), // infrastructureId
      undefined, // gridTypeId
      typeId || undefined, // equipmentTypeId
      undefined, // isActive
      keyword || undefined // keyword
    ).pipe(finalize(() => this.isLoadingEquipments.set(false)))
     .subscribe({
       next: (res) => {
         this.equipmentItems.set(res?.items || []);
         this.equipmentTotalCount.set(res?.totalCount || 0);
       },
       error: () => {
         this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách thiết bị.' });
       }
     });
  }

   onEquipmentFilterChange() {
    this.equipmentPage.set(1);
    this.loadEquipments();
  }

  onSearchEquipments() {
    this.equipmentPage.set(1);
    this.loadEquipments();
  }

  onResetEquipmentSearch() {
    this.equipmentSearchKeyword.set('');
    this.equipmentSearchTypeId.set('');
    this.equipmentPage.set(1);
    this.loadEquipments();
  }

  equipmentPrevPage() {
    if (this.equipmentPage() > 1) {
      this.equipmentPage.update(p => p - 1);
      this.loadEquipments();
    }
  }

  equipmentNextPage() {
    if (this.equipmentPage() < this.equipmentTotalPages()) {
      this.equipmentPage.update(p => p + 1);
      this.loadEquipments();
    }
  }

  goToEquipmentPage(page: any) {
    const p = Number(page);
    if (p >= 1 && p <= this.equipmentTotalPages()) {
      this.equipmentPage.set(p);
      this.loadEquipments();
    }
  }

  onEquipmentPageSizeChange(event: any) {
    this.equipmentPageSize.set(Number(event.target.value));
    this.equipmentPage.set(1);
    this.loadEquipments();
  }

  onEquipmentPageChange(event: { first?: number; rows?: number }) {
    const rows = Number(event.rows) || this.equipmentPageSize();
    const first = Number(event.first) || 0;
    this.equipmentPageSize.set(rows);
    this.equipmentPage.set(Math.floor(first / rows) + 1);
    this.loadEquipments();
  }

  // Thêm thiết bị mới gắn với trạm/đường dây này
  onAddEquipment() {
    const segment = this.infraTypeId() === 1 ? 'substation' : 'transmission-line';
    this.router.navigate(['/catalog', segment, this.currentItem().id, 'device-add']);
  }

  // Xem chi tiết thiết bị
  onViewEquipment(equipment: any) {
    const segment = this.infraTypeId() === 1 ? 'substation' : 'transmission-line';
    this.router.navigate(['/catalog', segment, this.currentItem().id, 'device-detail', equipment.id]);
  }

  // Chỉnh sửa thiết bị (vào thẳng chế độ sửa)
  onEditEquipment(equipment: any) {
    const segment = this.infraTypeId() === 1 ? 'substation' : 'transmission-line';
    this.router.navigate(['/catalog', segment, this.currentItem().id, 'device-detail', equipment.id], {
      queryParams: { mode: 'edit' }
    });
  }

  // Khóa / Mở khóa thiết bị (Mở popup xác nhận)
  onToggleEquipmentStatus(equipment: any) {
    this.equipmentLockTarget.set(equipment);
    this.showEquipmentLockConfirm.set(true);
  }

  onCancelEquipmentLock() {
    this.showEquipmentLockConfirm.set(false);
    this.equipmentLockTarget.set(null);
  }

  onConfirmEquipmentLock() {
    const equipment = this.equipmentLockTarget();
    if (!equipment) return;

    const isLocking = equipment.isActive === 1 || equipment.isActive === true;
    this.equipmentLockLoading.set(true);
    this.equipmentService.toggleStatus(equipment.id, isLocking)
      .pipe(
        finalize(() => {
          this.equipmentLockLoading.set(false);
          this.showEquipmentLockConfirm.set(false);
          this.equipmentLockTarget.set(null);
        })
      )
      .subscribe({
        next: (res) => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: res.message || (isLocking ? 'Khóa thiết bị thành công!' : 'Mở khóa thiết bị thành công!')
          });
          this.loadEquipments();
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
      return Number(statusTransition) === 0 || Number(statusTransition) === 1 ? 'Đã chuyển TBA' : String(statusTransition);
    }
    return item?.isActive === 1 || item?.isActive === true ? 'Hoạt động' : 'Ngừng hoạt động';
  }

  getEquipmentStatusClass(item: any): string {
    if (this.hasEquipmentStatusTransition(item)) return 'status-inactive';
    return item?.isActive === 1 || item?.isActive === true ? 'status-active' : 'status-inactive';
  }

  get transferUnitError(): string {
    if (this.transferSubmitted() && !this.transferForm().unitId) return 'Đơn vị quản lý là bắt buộc';
    return '';
  }

  get transferInfrastructureError(): string {
    if (this.transferSubmitted() && !this.transferForm().infrastructureId) return 'Trạm/Đường dây là bắt buộc';
    return '';
  }

  toggleTransferOrgTree(event?: Event) {
    if (event) event.stopPropagation();
    if (this.transferLoading()) return;
    this.transferOrgTreeOpen.update(open => !open);
  }

  toggleTransferUnitNode(unitId: any, event?: Event) {
    if (event) event.stopPropagation();
    const expanded = new Set(this.expandedTransferUnitNodes());
    expanded.has(unitId) ? expanded.delete(unitId) : expanded.add(unitId);
    this.expandedTransferUnitNodes.set(expanded);
  }

  isTransferNodeExpanded(unitId: any): boolean {
    return this.expandedTransferUnitNodes().has(unitId);
  }

  selectTransferOrgUnit(unitId: any) {
    this.transferForm.update(form => ({
      ...form,
      unitId: Number(unitId),
      infrastructureId: null
    }));
    this.transferOrgTreeOpen.set(false);
    this.transferOrgSearchKeyword.set('');
  }

  onTransferInfrastructureChange(value: string | null) {
    this.transferForm.update(form => ({ ...form, infrastructureId: value }));
  }

  onTransferNoteChange(value: string) {
    this.transferForm.update(form => ({ ...form, note: value || '' }));
  }

  openTransferDialog(equipment: any) {
    this.transferTarget.set(equipment);
    this.transferForm.set({
      unitId: equipment?.unitId ? Number(equipment.unitId) : (this.currentItem()?.unitId ? Number(this.currentItem().unitId) : null),
      infrastructureId: equipment?.infrastructureId ?? this.currentItem()?.id ?? null,
      note: ''
    });
    this.transferSubmitted.set(false);
    this.transferOrgTreeOpen.set(false);
    this.transferOrgSearchKeyword.set('');
    this.showTransferDialog.set(true);

    if (this.transferOrganizationUnits().length === 0 || this.transferInfrastructuresSource().length === 0) {
      forkJoin({
        organizationUnits: this.transferOrganizationUnits().length === 0
          ? this.equipmentService.getAllOrganizationUnits().pipe(catchError(() => of([])))
          : of(this.transferOrganizationUnits()),
        infrastructures: this.transferInfrastructuresSource().length === 0
          ? this.equipmentService.getAllInfrastructures().pipe(catchError(() => of([])))
          : of(this.transferInfrastructuresSource())
      }).subscribe(data => {
        this.transferOrganizationUnits.set(this.getAvailableOrganizationUnits(data.organizationUnits));
        this.transferInfrastructuresSource.set(Array.isArray(data.infrastructures) ? data.infrastructures : []);
      });
    }
  }

  closeTransferDialog() {
    if (this.transferLoading()) return;
    this.showTransferDialog.set(false);
    this.transferSubmitted.set(false);
    this.transferOrgTreeOpen.set(false);
    this.transferOrgSearchKeyword.set('');
    this.transferTarget.set(null);
  }

  confirmTransferEquipment() {
    this.transferSubmitted.set(true);
    const item = this.transferTarget();
    const form = this.transferForm();
    if (!item?.id || !form.unitId || !form.infrastructureId) return;

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
          this.loadEquipments();
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
          this.loadEquipments();
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

  // Lấy tên loại thiết bị
  getEquipmentTypeName(typeId: any): string {
    const et = this.equipmentTypes().find(t => t.id == typeId);
    return et ? et.name : '-';
  }

  getTransmissionLineLength(): string {
    const item = this.currentItem();
    const value = item?.length
      ?? item?.lineLength
      ?? item?.lengthKm
      ?? item?.Length
      ?? item?.LineLength
      ?? item?.LengthKm
      ?? item?.address;

    if (value === null || value === undefined || value === '') return '-';
    const displayValue = String(value);
    return /\bkm\b/i.test(displayValue) ? displayValue : `${displayValue} km`;
  }

  // Tải danh sách hồ sơ liên quan (đã xuất bản của cùng trạm/đường dây)
  loadRelatedDossiers() {
    const item = this.currentItem();
    if (!item?.id) return;

    this.isLoadingRelatedDossiers.set(true);
    const keyword = this.relatedDossiersSearchKeyword();

    this.dossierService.getCatalogDossiers({
      keyword: keyword || undefined,
      infrastructureId: String(item.id),
      page: 1,
      pageSize: 500
    }).pipe(finalize(() => this.isLoadingRelatedDossiers.set(false)))
      .subscribe({
        next: (res) => {
          this.relatedDossiers.set(res?.items || []);
          this.relatedDossiersTotalCount.set(res?.totalCount || 0);
          this.technicalDocumentsByDossier.set({});
          this.expandedTechnicalFolders.set(new Set<string>());
          this.selectedTechnicalFolderId.set(null);
          this.technicalStationFolderExpanded.set(true);
          this.technicalDocumentKeyword.set('');
          this.technicalSelectedDocumentTypeId.set('');
          if (this.activeTab() === 1 || this.activeTab() === 2) {
            this.loadInfrastructureAttachmentDocuments(res?.items || []);
          }
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách hồ sơ liên quan.' });
        }
      });
  }

  loadInfrastructureAttachmentDocuments(dossiers = this.relatedDossiers()) {
    const item = this.currentItem();
    if (!item?.id) return;

    this.attachmentDossierDocuments.set([]);
    this.expandedAttachmentFolders.set(new Set<string>());
    this.selectedAttachmentFolderId.set(null);
    this.attachmentStationFolderExpanded.set(true);
    this.attachmentDocumentKeyword.set('');
    this.attachmentSelectedEquipmentId.set('');
    this.attachmentDocumentPage.set(1);
    this.loadAttachmentEquipmentOptions();
    if (!dossiers.length) return;

    this.loadingAttachmentDocuments.set(true);
    forkJoin(dossiers.map(dossier =>
      this.dossierDocumentService.getDocuments(String(dossier.id), { page: 1, pageSize: 1000 }, true, true).pipe(
        catchError(() => of({ items: [] }))
      )
    )).pipe(finalize(() => this.loadingAttachmentDocuments.set(false))).subscribe(results => {
      const documents = dossiers.flatMap((dossier, index) =>
        (results[index]?.items || []).map(document => ({ dossier, document }))
      );
      this.attachmentDossierDocuments.set(documents);
      this.selectedAttachmentFolderId.set(null);
    });
  }

  toggleAttachmentFolder(folderId: string) {
    const expanded = new Set(this.expandedAttachmentFolders());
    if (expanded.has(folderId)) {
      expanded.delete(folderId);
    } else {
      expanded.add(folderId);
    }
    this.expandedAttachmentFolders.set(expanded);
  }

  isAttachmentFolderExpanded(folderId: string): boolean {
    return this.expandedAttachmentFolders().has(folderId);
  }

  selectAttachmentFolder(folderId: string) {
    this.selectedAttachmentFolderId.set(folderId);
    this.attachmentDocumentPage.set(1);
    const expanded = new Set(this.expandedAttachmentFolders());
    expanded.add(folderId);
    this.expandedAttachmentFolders.set(expanded);
  }

  toggleAttachmentStationFolder() {
    this.attachmentStationFolderExpanded.update(expanded => !expanded);
  }

  private loadAttachmentEquipmentOptions() {
    const item = this.currentItem();
    if (!item?.id) return;

    this.equipmentService.getEquipments(1, 1000, undefined, undefined, undefined, String(item.id)).pipe(
      catchError(() => of({ items: [] }))
    ).subscribe(res => this.attachmentEquipmentOptions.set(res?.items || []));
  }

  getAttachmentEquipmentName(item: { dossier: any; document: any }): string {
    const document = item?.document ?? {};
    const dossier = item?.dossier ?? {};
    return document.equipmentName
      || document.equipment?.name
      || dossier.equipmentName
      || dossier.equipment?.name
      || '-';
  }

  toggleTechnicalFolder(folder: { id: string; dossiers: any[] }) {
    const expanded = new Set(this.expandedTechnicalFolders());
    if (expanded.has(folder.id)) {
      expanded.delete(folder.id);
      this.expandedTechnicalFolders.set(expanded);
      return;
    }

    expanded.add(folder.id);
    this.expandedTechnicalFolders.set(expanded);

    const missingDossiers = folder.dossiers.filter(dossier => !this.technicalDocumentsByDossier()[dossier.id]);
    if (!missingDossiers.length) return;

    const loading = new Set(this.loadingTechnicalFolders());
    loading.add(folder.id);
    this.loadingTechnicalFolders.set(loading);

    forkJoin(missingDossiers.map(dossier =>
      this.dossierDocumentService.getDocuments(String(dossier.id), { page: 1, pageSize: 1000 }, true, true).pipe(
        catchError(() => of({ items: [] })),
        finalize(() => undefined)
      )
    )).pipe(finalize(() => {
      const next = new Set(this.loadingTechnicalFolders());
      next.delete(folder.id);
      this.loadingTechnicalFolders.set(next);
    })).subscribe(results => {
      this.technicalDocumentsByDossier.update(current => {
        const next = { ...current };
        missingDossiers.forEach((dossier, index) => next[dossier.id] = results[index]?.items || []);
        return next;
      });
    });
  }

  selectTechnicalFolder(folder: { id: string; dossiers: any[] }) {
    this.selectedTechnicalFolderId.set(folder.id);
    this.technicalDocumentPage.set(1);
    this.technicalStationFolderExpanded.set(true);
    if (!this.isTechnicalFolderExpanded(folder.id)) {
      this.toggleTechnicalFolder(folder);
    }
  }

  toggleTechnicalStationFolder() {
    this.technicalStationFolderExpanded.update(expanded => !expanded);
  }

  isTechnicalFolderExpanded(folderId: string): boolean {
    return this.expandedTechnicalFolders().has(folderId);
  }

  isTechnicalFolderLoading(folderId: string): boolean {
    return this.loadingTechnicalFolders().has(folderId);
  }

  onAttachmentDocumentFiltersChange() {
    this.attachmentDocumentPage.set(1);
  }

  onTechnicalDocumentFiltersChange() {
    this.technicalDocumentPage.set(1);
  }

  changeAttachmentDocumentPage(page: number) {
    if (page >= 1 && page <= this.attachmentDocumentTotalPages()) {
      this.attachmentDocumentPage.set(page);
    }
  }

  changeTechnicalDocumentPage(page: number) {
    if (page >= 1 && page <= this.technicalDocumentTotalPages()) {
      this.technicalDocumentPage.set(page);
    }
  }

  onAttachmentDocumentPageSizeChange(event: Event) {
    this.attachmentDocumentPageSize.set(Number((event.target as HTMLSelectElement).value) || 10);
    this.attachmentDocumentPage.set(1);
  }

  onTechnicalDocumentPageSizeChange(event: Event) {
    this.technicalDocumentPageSize.set(Number((event.target as HTMLSelectElement).value) || 10);
    this.technicalDocumentPage.set(1);
  }

  getVisibleDocumentPages(currentPage: number, totalPages: number): number[] {
    const start = Math.max(1, Math.min(currentPage - 2, totalPages - 4));
    const end = Math.min(totalPages, start + 4);
    return Array.from({ length: end - start + 1 }, (_, index) => start + index);
  }

  getTechnicalFolderDocuments(folder: { dossiers: any[] }): Array<{ dossier: any; document: any }> {
    return folder.dossiers.flatMap(dossier =>
      (this.technicalDocumentsByDossier()[dossier.id] || []).map(document => ({ dossier, document }))
    );
  }

  downloadTechnicalDocument(dossierId: string | undefined, document: any) {
    if (!dossierId || !document?.latestVersionId) return;
    void this.dossierDocumentService.downloadFile(dossierId, document.latestVersionId, document.name, true);
  }

  openTechnicalDocumentPreview(dossierId: string, document: any) {
    if (!document?.latestVersionId) {
      this.messageService.add({ severity: 'warn', summary: 'Xem trước', detail: 'Tài liệu chưa có phiên bản để xem.' });
      return;
    }

    this.cleanupTechnicalDocumentPreview();
    this.technicalPreviewTarget.set({ dossierId, document });
    this.showTechnicalDocumentPreview.set(true);
    this.loadingTechnicalDocumentPreview.set(true);

    const versionId = document.latestVersionId;
    void this.dossierDocumentService.getPreviewBlobUrl(dossierId, versionId, true)
      .then(url => {
        if (this.technicalPreviewTarget()?.document?.latestVersionId === versionId) {
          this.technicalPreviewUrl.set(url);
        } else {
          this.dossierDocumentService.revokePreviewBlobUrl(url);
        }
      })
      .catch(() => {
        this.messageService.add({ severity: 'error', summary: 'Xem trước', detail: 'Không thể tải tài liệu để xem trước.' });
      })
      .finally(() => this.loadingTechnicalDocumentPreview.set(false));
  }

  closeTechnicalDocumentPreview() {
    this.showTechnicalDocumentPreview.set(false);
    this.loadingTechnicalDocumentPreview.set(false);
    this.technicalPreviewTarget.set(null);
    this.cleanupTechnicalDocumentPreview();
  }

  isTechnicalPreviewImage(): boolean {
    const document = this.technicalPreviewTarget()?.document;
    return document?.mimeType?.startsWith('image/')
      || /\.(png|jpe?g|gif|webp|bmp)$/i.test(document?.name || document?.fileName || '');
  }

  private cleanupTechnicalDocumentPreview() {
    const url = this.technicalPreviewUrl();
    if (url) {
      this.dossierDocumentService.revokePreviewBlobUrl(url);
      this.technicalPreviewUrl.set(null);
    }
  }

  onRelatedDossierFilterChange() {
    this.relatedDossiersPage.set(1);
    this.loadRelatedDossiers();
  }

  relatedDossiersPrevPage() {
    if (this.relatedDossiersPage() > 1) {
      this.relatedDossiersPage.update(p => p - 1);
      this.loadRelatedDossiers();
    }
  }

  relatedDossiersNextPage() {
    if (this.relatedDossiersPage() < this.relatedDossiersTotalPages()) {
      this.relatedDossiersPage.update(p => p + 1);
      this.loadRelatedDossiers();
    }
  }

  goToRelatedDossiersPage(page: any) {
    const p = Number(page);
    if (p >= 1 && p <= this.relatedDossiersTotalPages()) {
      this.relatedDossiersPage.set(p);
      this.loadRelatedDossiers();
    }
  }

  onRelatedDossiersPageSizeChange(event: any) {
    this.relatedDossiersPageSize.set(Number(event.target.value));
    this.relatedDossiersPage.set(1);
    this.loadRelatedDossiers();
  }

  onViewDossier(dossier: any) {
    const segment = this.infraTypeId() === 1 ? 'substation' : 'transmission-line';
    const parentId = this.currentItem()?.id;
    this.router.navigate(['/catalog', segment, parentId, 'dossier-detail', dossier.id]);
  }

  getDossierCode(doc: any): string {
    const data = doc?.catalogData ?? doc?.CatalogData ?? {};
    return data['Mã hồ sơ'] ?? data['ma_ho_so'] ?? doc?.code ?? '-';
  }

  getDossierTitle(doc: any): string {
    const data = doc?.catalogData ?? doc?.CatalogData ?? {};
    return data['Tiêu đề hồ sơ'] ?? data['tieude_hoso'] ?? data['tieude'] ?? doc?.title ?? '-';
  }

  getDossierBox(doc: any): string {
    return doc?.dossierSetName ?? doc?.boxCode ?? '-';
  }

  getDossierCreator(doc: any): string {
    return doc?.creator ?? doc?.createdByName ?? doc?.createdBy ?? '-';
  }

  getDossierDate(doc: any): any {
    return doc?.createdDate ?? doc?.createdAt;
  }

  exportToExcel() {
    const item = this.currentItem();
    if (!item?.id) return;

    this.messageService.add({ severity: 'info', summary: 'Thông báo', detail: 'Đang chuẩn bị tệp Excel...' });

    import('xlsx').then(XLSX => {
      const workbook = XLSX.utils.book_new();
      
      const dataRows = this.relatedDossiers().map((doc, index) => ({
        'STT': index + 1,
        'Mã hồ sơ': this.getDossierCode(doc),
        'Tiêu đề hồ sơ': this.getDossierTitle(doc),
        'Loại hồ sơ': doc.dossierTypeName || '-',
        'Số tài liệu': doc.documentCount ?? 0
      }));

      const worksheet = XLSX.utils.json_to_sheet(dataRows);

      worksheet['!cols'] = [
        { wch: 6 },  // STT
        { wch: 20 }, // Mã hồ sơ
        { wch: 45 }, // Tiêu đề hồ sơ
        { wch: 25 }, // Loại hồ sơ
        { wch: 12 }  // Số tài liệu
      ];

      XLSX.utils.book_append_sheet(workbook, worksheet, 'Hồ sơ liên quan');

      const workbookBlob = new Blob([XLSX.write(workbook, { bookType: 'xlsx', type: 'array' })], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
      });

      const url = URL.createObjectURL(workbookBlob);
      const link = document.createElement('a');
      link.setAttribute('href', url);
      const fileName = `HoSoLienQuan_${item.code || 'Tram'}_${new Date().getTime()}.xlsx`;
      link.setAttribute('download', fileName);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);

      this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xuất file Excel thành công!' });
    }).catch(() => {
      this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể xuất file Excel.' });
    });
  }
}
