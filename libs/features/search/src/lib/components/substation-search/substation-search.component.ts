import { Component, OnInit, signal, computed, inject, effect, HostListener } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer } from '@angular/platform-browser';
import { ToastModule } from 'primeng/toast';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { PaginatorModule } from 'primeng/paginator';
import { MessageService } from 'primeng/api';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService, APP_CONFIG } from '@sohoa.frontend/shared/core';
import { EquipmentService } from '@sohoa.frontend/features/equipment';
import { DossierDocumentService, DossierManagementService } from '@sohoa.frontend/features/dossier-management';
import { HttpClient, HttpParams } from '@angular/common/http';
import { forkJoin, of } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';
import { LookupTrackingService } from '../../data-access/lookup-tracking.service';

@Component({
  selector: 'app-substation-search',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, SelectModule, DialogModule, PaginatorModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './substation-search.component.html',
  styleUrl: './substation-search.component.scss'
})
export class SubstationSearchComponent implements OnInit {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
  private equipmentService = inject(EquipmentService);
  private authService = inject(AuthService);
  private messageService = inject(MessageService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private dossierService = inject(DossierManagementService);
  private dossierDocumentService = inject(DossierDocumentService);
  private sanitizer = inject(DomSanitizer);
  private lookupTrackingService = inject(LookupTrackingService);

  // States
  pageTitle = signal<string>('Tra cứu tìm kiếm Trạm biến áp');
  items = signal<any[]>([]);
  orgUnits = signal<any[]>([]);
  gridTypes = signal<any[]>([]);
  searchKeyword = signal<string>('');
  searchStatus = signal<string>(''); // '', '1', '0'
  searchUnitId = signal<number | null>(null);
  searchGridTypeId = signal<number | null>(null);
  searchFromDate = signal<string>('');
  searchToDate = signal<string>('');
  searchDateError = signal<string>('');
  totalCount = signal<number>(0);

  currentView = signal<'list' | 'detail'>('list');
  currentItem = signal<any>({});

  // Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  // ── DETAIL VIEW SIGNALS ────────────────────────────────────────────────────
  activeTab = signal<number>(0);

  // Hồ sơ liên quan trong tab chi tiết
  relatedDossiers = signal<any[]>([]);
  relatedDossiersTotalCount = signal<number>(0);
  relatedDossiersPage = signal<number>(1);
  relatedDossiersPageSize = signal<number>(10);
  isLoadingRelatedDossiers = signal<boolean>(false);
  relatedDossiersSearchKeyword = signal<string>('');
  relatedDossiersTotalPages = computed(() =>
    Math.ceil(this.relatedDossiersTotalCount() / this.relatedDossiersPageSize())
  );

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
  showAttachmentDocumentPreview = signal<boolean>(false);
  attachmentPreviewTarget = signal<{ dossierId: string; document: any } | null>(null);
  attachmentPreviewUrl = signal<string | null>(null);
  loadingAttachmentDocumentPreview = signal<boolean>(false);
  technicalFolderSearchKeyword = signal<string>('');
  technicalDossiers = signal<any[]>([]);
  loadingTechnicalDossiers = signal<boolean>(false);
  expandedTechnicalFolders = signal<Set<string>>(new Set<string>());
  technicalDocumentsByDossier = signal<Record<string, any[]>>({});
  loadingTechnicalFolders = signal<Set<string>>(new Set<string>());
  selectedTechnicalFolderId = signal<string | null>(null);
  technicalStationFolderExpanded = signal<boolean>(true);
  technicalDocumentKeyword = signal<string>('');
  technicalSelectedDocumentTypeId = signal<string>('');
  technicalDocumentPage = signal<number>(1);
  technicalDocumentPageSize = signal<number>(10);
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

  attachmentPreviewSrc = computed(() => {
    const url = this.attachmentPreviewUrl();
    return url ? this.sanitizer.bypassSecurityTrustResourceUrl(url) : null;
  });

  selectedAttachmentFolder = computed(() =>
    this.attachmentDocumentFolders().find(folder => folder.id === this.selectedAttachmentFolderId()) ?? null
  );

  selectedAttachmentDocuments = computed(() => {
    const keyword = this.attachmentDocumentKeyword().trim().toLocaleLowerCase();
    const equipmentId = this.attachmentSelectedEquipmentId();
    return (this.selectedAttachmentFolder()?.documents ?? []).filter(item => {
      const name = (item.document.name || item.document.fileName || '').toLocaleLowerCase();
      const itemEquipmentId = String(item.document.equipmentId || item.dossier.equipmentId || '');
      return (!keyword || name.includes(keyword)) && (!equipmentId || itemEquipmentId === equipmentId);
    });
  });

  pagedAttachmentDocuments = computed(() => {
    const start = (this.attachmentDocumentPage() - 1) * this.attachmentDocumentPageSize();
    return this.selectedAttachmentDocuments().slice(start, start + this.attachmentDocumentPageSize());
  });

  technicalDossierFolders = computed(() => {
    const keyword = this.technicalFolderSearchKeyword().trim().toLocaleLowerCase();
    const groups = new Map<string, { id: string; name: string; dossiers: any[] }>();

    this.technicalDossiers().forEach(dossier => {
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
      const name = (item.document.name || item.document.fileName || '').toLocaleLowerCase();
      return (!keyword || name.includes(keyword))
        && (!documentTypeId || String(item.document.documentTypeId || '') === documentTypeId);
    });
  });

  pagedTechnicalDocuments = computed(() => {
    const start = (this.technicalDocumentPage() - 1) * this.technicalDocumentPageSize();
    return this.selectedTechnicalDocuments().slice(start, start + this.technicalDocumentPageSize());
  });

  technicalDocumentTypeOptions = computed(() => {
    const types = new Map<string, { id: string; name: string }>();
    this.attachmentDossierDocuments().forEach(item => {
      if (item.document.documentTypeId && item.document.documentTypeName) {
        types.set(String(item.document.documentTypeId), { id: String(item.document.documentTypeId), name: item.document.documentTypeName });
      }
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

  // Equipment Detail Dialog Signals
  showEquipmentDetail = signal<boolean>(false);
  selectedEquipment = signal<any>(null);
  equipmentFormSchema = signal<any[]>([]);
  equipmentFormValues = signal<any>({});

  // Pagination Computeds
  paginatedItems = computed(() => {
    return this.items();
  });

  constructor() {
    effect(() => {
      // Re-trigger load when page, pageSize, or search state changes
      this.currentPage();
      this.pageSize();
      if (this.currentView() === 'list') {
        this.loadItems();
      }
    }, { allowSignalWrites: true });

    effect(() => {
      if (this.currentView() === 'detail' && this.activeTab() === 1) {
        this.loadAttachmentDocuments();
      }
    }, { allowSignalWrites: true });

    effect(() => {
      if (this.currentView() === 'detail' && this.activeTab() === 2 && this.currentItem()?.id) {
        this.loadTechnicalDossiers();
      }
    }, { allowSignalWrites: true });
  }

  ngOnInit() {
    this.authService.loadPermissions();
    this.loadOrgUnits();
    this.loadGridTypes();
    this.loadEquipmentTypes();

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
        this.loadSubstationById(id);
      } else {
        this.currentView.set('list');
        this.loadItems();
      }
    });
  }

  loadOrgUnits() {
    this.http.get<any>(`${this.config.apiGatewayUrl}/api/v1/equipment/get-organization-units`).subscribe({
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
    this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/v1/equipment/get-grid-types`).subscribe({
      next: (data) => {
        this.gridTypes.set(data || []);
      },
      error: () => {
        console.error('Không thể tải danh sách loại lưới điện');
      }
    });
  }

  loadEquipmentTypes() {
    this.equipmentService.getEquipmentTypes().subscribe({
      next: (data) => {
        this.equipmentTypes.set(data || []);
      },
      error: () => {
        console.error('Không thể tải danh sách loại thiết bị');
      }
    });
  }

  loadItems() {
    let params = new HttpParams()
      .set('page', this.currentPage().toString())
      .set('pageSize', this.pageSize().toString());

    if (this.searchKeyword() && this.searchKeyword().trim()) {
      params = params.set('keyword', this.searchKeyword().trim());
    }

    if (this.searchStatus() !== '') {
      params = params.set('status', this.searchStatus());
    }

    if (this.searchUnitId() !== null) {
      params = params.set('unitId', this.searchUnitId()!.toString());
    }

    if (this.searchGridTypeId() !== null) {
      params = params.set('gridTypeId', this.searchGridTypeId()!.toString());
    }

    if (this.searchFromDate()) {
      params = params.set('fromDate', this.searchFromDate());
    }

    if (this.searchToDate()) {
      params = params.set('toDate', this.searchToDate());
    }

    this.http.get<any>(`${this.config.apiGatewayUrl}/api/catalog/substation-search`, { params }).subscribe({
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
          detail: 'Không thể tải danh sách trạm biến áp'
        });
      }
    });
  }

  loadSubstationById(id: string) {
    this.http.get<any>(`${this.config.apiGatewayUrl}/api/catalog/substation-search/${id}`).subscribe({
      next: (res) => {
        this.currentItem.set(res || {});
        this.loadEquipments();
        this.loadRelatedDossiers();
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải thông tin chi tiết.' });
      }
    });
  }

  onSearch() {
    if (!this.validateOperationDateRange()) {
      return;
    }
    this.currentPage.set(1);
    this.loadItems();
  }

  onResetSearch() {
    this.searchKeyword.set('');
    this.searchStatus.set('');
    this.searchUnitId.set(null);
    this.searchGridTypeId.set(null);
    this.searchFromDate.set('');
    this.searchToDate.set('');
    this.searchDateError.set('');
    this.currentPage.set(1);
    this.loadItems();
  }

  onOperationDateChange() {
    this.validateOperationDateRange();
  }

  private validateOperationDateRange(): boolean {
    const fromDate = this.searchFromDate();
    const toDate = this.searchToDate();

    if (fromDate && toDate && fromDate > toDate) {
      this.searchDateError.set('Từ ngày không được lớn hơn Đến ngày.');
      return false;
    }

    this.searchDateError.set('');
    return true;
  }

  onListPageChange(event: { first?: number; rows?: number }) {
    const rows = Number(event.rows) || this.pageSize();
    const first = Number(event.first) || 0;
    this.pageSize.set(rows);
    this.currentPage.set(Math.floor(first / rows) + 1);
  }

  onViewDetail(item: any) {
    this.router.navigate(['/search/substation', item.id]);
  }

  goBack() {
    this.router.navigate(['/search/substation']);
  }

  // ── DETAIL VIEW METHODS ────────────────────────────────────────────────────

  loadEquipments() {
    const item = this.currentItem();
    if (!item?.id) return;

    this.isLoadingEquipments.set(true);
    const keyword = this.equipmentSearchKeyword();
    const typeId = this.equipmentSearchTypeId();

    let params = new HttpParams()
      .set('page', this.equipmentPage().toString())
      .set('pageSize', this.equipmentPageSize().toString());

    if (keyword && keyword.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    if (typeId) {
      params = params.set('equipmentTypeId', typeId);
    }

    this.http.get<any>(`${this.config.apiGatewayUrl}/api/catalog/substation-search/${item.id}/equipments`, { params })
      .pipe(finalize(() => this.isLoadingEquipments.set(false)))
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

  onResetEquipmentSearch() {
    this.equipmentSearchKeyword.set('');
    this.equipmentSearchTypeId.set('');
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

  // Xem chi tiết thiết bị chỉ đọc qua Dialog
  onViewEquipment(equipment: any) {
    this.http.get<any>(`${this.config.apiGatewayUrl}/api/catalog/substation-search/equipments/${equipment.id}`).subscribe({
      next: (res) => {
        this.selectedEquipment.set(res);
        // Parse FormValues (EAV thông số kỹ thuật)
        let values = {};
        if (res.formValues) {
          try {
            values = typeof res.formValues === 'string' ? JSON.parse(res.formValues) : res.formValues;
          } catch (e) {
            console.error('Lỗi parse formValues', e);
          }
        }
        this.equipmentFormValues.set(values);

        // Lấy form template schema để biết tên hiển thị của các thông số kỹ thuật EAV
        this.equipmentFormSchema.set([]);
        this.http.get<any>(`${this.config.apiGatewayUrl}/api/catalog/substation-search/equipments/${equipment.id}/form-template`).subscribe({
          next: (tpl) => {
            if (tpl?.formSchema) {
              try {
                const schema = JSON.parse(tpl.formSchema);
                // Lấy danh sách các fields từ schema
                if (schema.fields) {
                  this.equipmentFormSchema.set(schema.fields);
                } else if (Array.isArray(schema)) {
                  this.equipmentFormSchema.set(schema);
                }
              } catch {
                // Fallback: Nếu không có schema hoặc lỗi parse
              }
            }
            this.showEquipmentDetail.set(true);
          },
          error: () => {
            // Vẫn mở dialog hiển thị thông tin chung nếu lỗi template
            this.showEquipmentDetail.set(true);
          }
        });
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải thông tin thiết bị.' });
      }
    });
  }

  getEavFields(): { label: string, value: string }[] {
    const schema = this.equipmentFormSchema();
    const values = this.equipmentFormValues();
    const fields: { label: string, value: string }[] = [];

    if (schema.length > 0) {
      schema.forEach(field => {
        const key = field.key || field.id || field.name;
        const label = field.label || field.title || key;
        const val = values[key];
        if (val !== undefined && val !== null && String(val).trim() !== '') {
          fields.push({ label, value: String(val) });
        }
      });
    } else {
      // Fallback: hiển thị tất cả các key-value trong formValues nếu không có schema
      Object.keys(values).forEach(key => {
        const val = values[key];
        if (val !== undefined && val !== null && String(val).trim() !== '') {
          fields.push({ label: key, value: String(val) });
        }
      });
    }

    return fields;
  }

  getEquipmentTypeName(typeId: any): string {
    const et = this.equipmentTypes().find(t => t.id == typeId);
    return et ? et.name : '-';
  }

  loadAttachmentDocuments() {
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
    this.loadingAttachmentDocuments.set(true);

    this.dossierService.getCatalogDossiers({
      infrastructureId: String(item.id),
      page: 1,
      pageSize: 500
    }).pipe(
      catchError(() => of({ items: [] }))
    ).subscribe(res => {
      const dossiers: any[] = res?.items || [];
      if (!dossiers.length) {
        this.loadingAttachmentDocuments.set(false);
        return;
      }

      const documentRequests = dossiers.map((dossier: any) =>
        this.dossierDocumentService.getDocuments(String(dossier.id), { page: 1, pageSize: 1000 }, true).pipe(
          catchError(() => of({ items: [] }))
        )
      );

      forkJoin(documentRequests).pipe(finalize(() => this.loadingAttachmentDocuments.set(false))).subscribe((results: Array<{ items?: any[] }>) => {
        const documents = dossiers.flatMap((dossier: any, index: number) =>
          (results[index]?.items || []).map((document: any) => ({ dossier, document }))
        );
        this.attachmentDossierDocuments.set(documents);
      });
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
    this.attachmentStationFolderExpanded.set(true);
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
    return item.document.equipmentName || item.dossier.equipmentName || '-';
  }

  downloadAttachmentDocument(dossierId: string | undefined, document: any) {
    if (!dossierId || !document?.latestVersionId) return;
    void this.dossierDocumentService.downloadFile(dossierId, document.latestVersionId, document.name, true);
  }

  openAttachmentDocumentPreview(dossierId: string, document: any) {
    if (!dossierId || !document?.latestVersionId) {
      this.messageService.add({ severity: 'warn', summary: 'Xem trước', detail: 'Tài liệu chưa có phiên bản để xem.' });
      return;
    }

    this.cleanupAttachmentDocumentPreview();
    this.attachmentPreviewTarget.set({ dossierId, document });
    this.showAttachmentDocumentPreview.set(true);
    this.loadingAttachmentDocumentPreview.set(true);

    const versionId = document.latestVersionId;
    void this.dossierDocumentService.getPreviewBlobUrl(dossierId, versionId, true)
      .then(url => {
        if (this.attachmentPreviewTarget()?.document?.latestVersionId === versionId) {
          this.attachmentPreviewUrl.set(url);
        } else {
          this.dossierDocumentService.revokePreviewBlobUrl(url);
        }
      })
      .catch(() => {
        this.messageService.add({ severity: 'error', summary: 'Xem trước', detail: 'Không thể tải tài liệu để xem trước.' });
      })
      .finally(() => this.loadingAttachmentDocumentPreview.set(false));
  }

  closeAttachmentDocumentPreview() {
    this.showAttachmentDocumentPreview.set(false);
    this.loadingAttachmentDocumentPreview.set(false);
    this.attachmentPreviewTarget.set(null);
    this.cleanupAttachmentDocumentPreview();
  }

  isAttachmentPreviewImage(): boolean {
    const document = this.attachmentPreviewTarget()?.document;
    return document?.mimeType?.startsWith('image/')
      || /\.(png|jpe?g|gif|webp|bmp)$/i.test(document?.name || document?.fileName || '');
  }

  private cleanupAttachmentDocumentPreview() {
    const url = this.attachmentPreviewUrl();
    if (url) {
      this.dossierDocumentService.revokePreviewBlobUrl(url);
      this.attachmentPreviewUrl.set(null);
    }
  }

  loadTechnicalDossiers() {
    const item = this.currentItem();
    if (!item?.id) return;

    this.loadingTechnicalDossiers.set(true);
    this.technicalDocumentsByDossier.set({});
    this.expandedTechnicalFolders.set(new Set<string>());
    this.dossierService.getCatalogDossiers({
      infrastructureId: String(item.id),
      page: 1,
      pageSize: 500
    }).pipe(finalize(() => this.loadingTechnicalDossiers.set(false))).subscribe({
      next: res => {
        this.technicalDossiers.set(res?.items || []);
        this.selectedTechnicalFolderId.set(null);
        this.technicalStationFolderExpanded.set(true);
        this.technicalDocumentKeyword.set('');
        this.technicalSelectedDocumentTypeId.set('');
        this.technicalDocumentPage.set(1);
        this.loadAttachmentDocuments();
      },
      error: () => {
        this.technicalDossiers.set([]);
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải hồ sơ kỹ thuật của trạm biến áp.' });
      }
    });
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

    const documentRequests = missingDossiers.map((dossier: any) =>
      this.dossierDocumentService.getDocuments(String(dossier.id), { page: 1, pageSize: 1000 }, true).pipe(
        catchError(() => of({ items: [] }))
      )
    );

    forkJoin(documentRequests).pipe(finalize(() => {
      const next = new Set(this.loadingTechnicalFolders());
      next.delete(folder.id);
      this.loadingTechnicalFolders.set(next);
    })).subscribe((results: Array<{ items?: any[] }>) => {
      this.technicalDocumentsByDossier.update(current => {
        const next = { ...current };
        missingDossiers.forEach((dossier: any, index: number) => next[dossier.id] = results[index]?.items || []);
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

  onAttachmentDocumentPageChange(event: { first?: number; rows?: number }) {
    const rows = Number(event.rows) || this.attachmentDocumentPageSize();
    const first = Number(event.first) || 0;
    this.attachmentDocumentPageSize.set(rows);
    this.attachmentDocumentPage.set(Math.floor(first / rows) + 1);
  }

  onTechnicalDocumentPageChange(event: { first?: number; rows?: number }) {
    const rows = Number(event.rows) || this.technicalDocumentPageSize();
    const first = Number(event.first) || 0;
    this.technicalDocumentPageSize.set(rows);
    this.technicalDocumentPage.set(Math.floor(first / rows) + 1);
  }

  getTechnicalFolderDocuments(folder: { dossiers: any[] }): Array<{ dossier: any; document: any }> {
    return folder.dossiers.flatMap((dossier: any) =>
      (this.technicalDocumentsByDossier()[dossier.id] || []).map((document: any) => ({ dossier, document }))
    );
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
      page: this.relatedDossiersPage(),
      pageSize: this.relatedDossiersPageSize()
    }).pipe(finalize(() => this.isLoadingRelatedDossiers.set(false)))
      .subscribe({
        next: (res) => {
          this.relatedDossiers.set(res?.items || []);
          this.relatedDossiersTotalCount.set(res?.totalCount || 0);
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách hồ sơ liên quan.' });
        }
      });
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
    if (dossier?.id) {
      this.lookupTrackingService.recordView('DOSSIER', dossier.id);
    }
    this.router.navigate(['/search/dossier/detail', dossier.id]);
  }

  getDossierCode(doc: any): string {
    const data = doc?.catalogData ?? doc?.CatalogData ?? {};
    return data['Mã hồ sơ'] ?? data['ma_ho_so'] ?? doc?.code ?? '-';
  }

  getDossierTitle(doc: any): string {
    const data = doc?.catalogData ?? doc?.CatalogData ?? {};
    return data['Tiêu đề hồ sơ'] ?? data['tieude_hoso'] ?? data['tieude'] ?? doc?.title ?? '-';
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
