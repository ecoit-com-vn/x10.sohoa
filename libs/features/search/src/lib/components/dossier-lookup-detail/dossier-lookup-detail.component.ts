import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { MessageService } from 'primeng/api';
import { PaginatorModule } from 'primeng/paginator';
import { ToastModule } from 'primeng/toast';
import { of, switchMap, finalize, catchError } from 'rxjs';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { getDossierStatusLabel } from '../../utils/dossier-status.util';
import {
  DossierManagementService,
  EavField,
  normalizeDossierDetail,
  readApiField,
  parseFormDataJson,
  readFormSchemaJson,
  normalizeField,
  DossierDocumentEditDialogComponent,
} from '@sohoa.frontend/features/dossier-management';

@Component({
  selector: 'app-dossier-lookup-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    PaginatorModule,
    ToastModule,
    WfBreadcrumbComponent,
    RouterLink,
    DossierDocumentEditDialogComponent,
  ],
  providers: [MessageService],
  templateUrl: './dossier-lookup-detail.component.html',
  styleUrl: './dossier-lookup-detail.component.scss',
})
export class DossierLookupDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
  private dossierService = inject(DossierManagementService);
  private messageService = inject(MessageService);

  private get apiBase(): string {
    return `${this.config.apiGatewayUrl}/api/v1/dossiers-by-equipment`;
  }

  // ===== SIGNALS =====
  dossierId = signal<string>('');
  dossier = signal<any | null>(null);
  activeDetailTab = signal<'info' | 'documents' | 'related'>('info');
  dossierDocuments = signal<any[]>([]);
  loadingDossierDocuments = signal<boolean>(false);
  relatedEquipments = signal<any[]>([]);
  loadingEquipments = signal<boolean>(false);
  dynamicFields = signal<EavField[]>([]);
  detailFormData = signal<Record<string, any>>({});
  loadingForm = signal<boolean>(false);
  gridTypes = signal<any[]>([]);
  dossierTypes = signal<any[]>([]);
  showViewDocument = signal<boolean>(false);
  viewTarget = signal<any | null>(null);

  // Equipment pagination signals
  equipmentFirst = signal<number>(0);
  equipmentRows = signal<number>(10);
  paginatedEquipments = computed(() => {
    const start = this.equipmentFirst();
    const end = start + this.equipmentRows();
    return this.relatedEquipments().slice(start, end);
  });

  catalogColumnsMap = signal<Record<string, string>>({});

  formatKeyToLabel(key: string): string {
    if (!key) return '';
    return key
      .split('_')
      .map(word => word.charAt(0).toUpperCase() + word.slice(1))
      .join(' ');
  }

  // Phục hồi và hiển thị đầy đủ các trường từ formDataJson
  allFields = computed(() => {
    const fields = [...this.dynamicFields()];
    const data = this.detailFormData();
    const colMap = this.catalogColumnsMap();

    const existingKeys = new Set(fields.map(f => f.key));

    Object.keys(data).forEach(key => {
      if (!existingKeys.has(key)) {
        const label = colMap[key] || this.formatKeyToLabel(key);
        fields.push({
          key: key,
          label: label,
          type: 'text' as const
        });
      }
    });

    return fields;
  });

  // Chia đôi thuộc tính động EAV để hiển thị 2 cột sạch đẹp dạng văn bản
  leftDynamicFields = computed(() => {
    const fields = this.allFields();
    return fields.slice(0, Math.ceil(fields.length / 2));
  });

  rightDynamicFields = computed(() => {
    const fields = this.allFields();
    return fields.slice(Math.ceil(fields.length / 2));
  });

  // Related dossiers signals & filters
  relatedDossiers = signal<any[]>([]);
  loadingRelated = signal<boolean>(false);
  relatedFirst = signal<number>(0);
  relatedRows = signal<number>(10);
  totalRelatedDossiers = signal<number>(0);
  paginatedRelatedDossiers = computed(() => {
    const start = this.relatedFirst();
    const end = start + this.relatedRows();
    return this.relatedDossiers().slice(start, end);
  });

  // Filters for related dossiers
  filterKeyword = '';
  filterEquipmentId = '';
  filterDossierTypeId = '';

  dossierMeta = computed(() => normalizeDossierDetail(this.dossier()));
  dossierViewMeta = computed(() => {
    const meta = this.dossierMeta();
    const raw = meta?.raw ?? {};

    const shelfName = readApiField<string>(raw, 'shelfName', 'ShelfName');
    const shelfCode = readApiField<string>(raw, 'shelfCode', 'ShelfCode');
    const floorName = readApiField<string>(raw, 'floorName', 'FloorName');
    const floorCode = readApiField<string>(raw, 'floorCode', 'FloorCode');
    const boxName = readApiField<string>(raw, 'boxName', 'BoxName');
    const boxCode = readApiField<string>(raw, 'boxCode', 'BoxCode');
    const boxId = readApiField<string>(raw, 'boxId', 'BoxId');
    const directStorageLabel = readApiField<string>(raw, 'storageLabel', 'StorageLabel');
    const storageParts = [shelfName || shelfCode, floorName || floorCode, boxName || boxCode].filter(Boolean);

    return {
      dossierGroupName: readApiField<string>(raw, 'dossierGroupName', 'DossierGroupName') ?? '',
      gridTypeName: readApiField<string>(raw, 'gridTypeName', 'GridTypeName')
        ?? this.getGridTypeName(meta?.gridTypeId ?? null),
      infrastructureName: meta?.infrastructureName
        || readApiField<string>(raw, 'infrastructureName', 'InfrastructureName')
        || '',
      infrastructureCode: readApiField<string>(raw, 'infrastructureCode', 'InfrastructureCode') ?? '',
      storageLabel: directStorageLabel
        || storageParts.join(' / ')
        || (boxId ? `Hộp #${boxId}` : ''),
      dossierTypeName: meta?.dossierTypeName ?? '',
    };
  });

  loadCatalogColumns() {
    this.dossierService.getBhsCatalogColumns().subscribe({
      next: (cols) => {
        const map: Record<string, string> = {};
        if (Array.isArray(cols)) {
          cols.forEach(c => {
            if (c.code) map[c.code] = c.label || c.key;
          });
        }
        this.catalogColumnsMap.set(map);
      }
    });
  }

  ngOnInit() {
    this.loadCatalogColumns();

    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.dossierId.set(id);
        this.loadDossierDetail(id);
      }
    });

    // Load lookups for related dossier filter
    this.dossierService.getGridTypeLookup().subscribe({
      next: (types) => this.gridTypes.set(types || []),
      error: () => console.error('Failed to load grid types'),
    });

    this.dossierService.getDossierTypeLookup().subscribe({
      next: (types) => this.dossierTypes.set(types || []),
      error: () => console.error('Failed to load dossier types'),
    });
  }

  getStatusText(status?: string | number, statusName?: string): string {
    return getDossierStatusLabel(status, statusName);
  }

  onBack(): void {
    if (window.history.length > 1) {
      window.history.back();
    } else {
      void this.router.navigate(['/search/dossier-by-equipment']);
    }
  }

  loadDossierDetail(id: string) {
    this.loadingForm.set(true);
    this.http.get<any>(`${this.apiBase}/${id}`).pipe(
      switchMap((fullDossier) => {
        const normalized = normalizeDossierDetail(fullDossier);
        this.dossier.set(normalized || fullDossier);

        // Load danh sách tài liệu, thiết bị liên quan và hồ sơ liên quan
        this.loadDossierDocuments(id);
        this.loadRelatedEquipments(id);
        this.loadRelatedDossiers(id);

        if (normalized) {
          const parsedData = parseFormDataJson(normalized.formDataJson);
          this.detailFormData.set(parsedData);
          return this.resolveFormTemplate(normalized.formId, normalized.dossierTypeId);
        }
        return of(null);
      }),
      finalize(() => this.loadingForm.set(false))
    ).subscribe({
      next: (template) => {
        this.applyFormTemplate(template);
      },
      error: (err) => {
        console.error('Error loading dossier details', err);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải thông tin chi tiết hồ sơ',
        });
      }
    });
  }

  private applyFormTemplate(template: any) {
    if (!template) {
      this.dynamicFields.set([]);
      return;
    }

    const schemaJson = readFormSchemaJson(template);
    if (!schemaJson) {
      this.dynamicFields.set([]);
      return;
    }

    try {
      const raw = JSON.parse(schemaJson);
      const fields: EavField[] = Array.isArray(raw) ? raw.map((f) => normalizeField(f)) : [];
      this.dynamicFields.set(fields);
    } catch {
      this.dynamicFields.set([]);
    }
  }

  private resolveFormTemplate(formId: string | null, dossierTypeId: string | null) {
    if (formId) {
      return this.dossierService.getFormTemplate(formId);
    }
    if (!dossierTypeId) {
      return of(null);
    }
    return this.dossierService.getDossierTypeLookup().pipe(
      catchError(() => of([] as any[])),
      switchMap((types) => {
        const found = Array.isArray(types)
          ? types.find((t: any) => (t.id ?? t.Id) === dossierTypeId)
          : null;
        const resolvedFormId = found ? (found.formId ?? found.FormId) : null;
        if (!resolvedFormId) return of(null);
        return this.dossierService.getFormTemplate(resolvedFormId);
      })
    );
  }

  loadRelatedEquipments(dossierId: string) {
    this.loadingEquipments.set(true);
    this.http.get<any[]>(`${this.apiBase}/${dossierId}/equipments`).pipe(
      finalize(() => this.loadingEquipments.set(false))
    ).subscribe({
      next: (data) => {
        this.relatedEquipments.set(data || []);
      },
      error: (err) => {
        console.error('Error loading related equipments', err);
        this.relatedEquipments.set([]);
      }
    });
  }

  loadDossierDocuments(dossierId: string) {
    this.loadingDossierDocuments.set(true);
    this.http.get<any>(`${this.apiBase}/${dossierId}/documents`, {
      params: { page: '1', pageSize: '50' }
    }).pipe(
      finalize(() => this.loadingDossierDocuments.set(false))
    ).subscribe({
      next: (response) => {
        this.dossierDocuments.set(response?.items || []);
      },
      error: (err) => {
        console.error('Error loading dossier documents', err);
        this.dossierDocuments.set([]);
      }
    });
  }

  loadRelatedDossiers(dossierId: string) {
    this.loadingRelated.set(true);
    const params: Record<string, string> = {
      page: '1',
      pageSize: '100',
    };
    if (this.filterKeyword.trim()) params['keyword'] = this.filterKeyword.trim();
    if (this.filterDossierTypeId) params['dossierTypeId'] = this.filterDossierTypeId;

    this.http.get<any>(`${this.apiBase}/${dossierId}/related`, { params }).pipe(
      finalize(() => this.loadingRelated.set(false))
    ).subscribe({
      next: (response) => {
        let items = response?.items || [];

        // Client-side filter by selected equipment if any
        if (this.filterEquipmentId) {
          items = items.filter((item: any) => {
            const equipments = item.equipments || item.Equipments || [];
            return equipments.some((e: any) => (e.id || e.Id) === this.filterEquipmentId);
          });
        }

        this.relatedDossiers.set(items);
        this.totalRelatedDossiers.set(items.length);
      },
      error: (err) => {
        console.error('Error loading related dossiers', err);
        this.relatedDossiers.set([]);
        this.totalRelatedDossiers.set(0);
      }
    });
  }

  onRelatedFilterChange() {
    this.relatedFirst.set(0);
    this.loadRelatedDossiers(this.dossierId());
  }

  onEquipmentPageChange(event: any) {
    this.equipmentFirst.set(event.first);
    this.equipmentRows.set(event.rows);
  }

  onRelatedPageChange(event: any) {
    this.relatedFirst.set(event.first);
    this.relatedRows.set(event.rows);
  }

  getEquipmentId(equipment: any): string {
    const id = equipment?.equipmentId ?? equipment?.EquipmentId ?? equipment?.id ?? equipment?.Id;
    return id == null ? '' : String(id).trim();
  }

  openRelatedDetail(rel: any) {
    window.open(`/#/search/dossier-by-equipment/${rel.id}`, '_blank');
  }

  getDossierTitle(dossier: unknown): string {
    if (!dossier || typeof dossier !== 'object') return '-';

    const dossierRecord = dossier as Record<string, unknown>;
    const catalogDataValue = dossierRecord['catalogData'] ?? dossierRecord['CatalogData'];
    const catalogData = catalogDataValue && typeof catalogDataValue === 'object'
      ? catalogDataValue as Record<string, unknown>
      : {};
    const candidates = [
      catalogData['Tiêu đề hồ sơ'],
      catalogData['tieude_hoso'],
      catalogData['tieu_de_ho_so'],
      catalogData['tieude'],
      dossierRecord['title'],
      dossierRecord['Title'],
    ];
    const title = candidates.find(value => typeof value === 'string' && value.trim().length > 0);

    return typeof title === 'string' ? title.trim() : '-';
  }

  getGridTypeName(gridTypeId: number | null): string {
    if (gridTypeId == null) return '-';
    const found = this.gridTypes().find(t => t.id === gridTypeId);
    return found ? found.name : `Lưới điện ${gridTypeId}`;
  }

  getFieldValueText(field: EavField): string {
    const value = this.detailFormData()[field.key];
    if (value === null || value === undefined || value === '') {
      return '-';
    }
    if (field.type === 'select') {
      const option = field.options?.find(opt => opt.value === value);
      return option ? option.label : value;
    }
    if (field.type === 'checkbox') {
      return value ? 'Có' : 'Không';
    }
    if (field.type === 'date') {
      try {
        const date = new Date(value);
        if (!isNaN(date.getTime())) {
          return date.toLocaleDateString('vi-VN');
        }
      } catch (e) {
        // ignore
      }
    }
    return value;
  }

  formatDate(value?: Date | string): string {
    if (!value) return '-';
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) return '-';
    return date.toLocaleDateString('vi-VN');
  }

  formatSize(bytes?: number): string {
    if (bytes == null || isNaN(bytes)) return '-';
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  // ===== PREVIEW & DOWNLOAD =====
  onViewDocumentDetail(doc: any) {
    this.viewTarget.set(doc);
    this.showViewDocument.set(true);
  }

  onCloseDocumentDetail() {
    this.showViewDocument.set(false);
    this.viewTarget.set(null);
  }

  downloadDocument(doc: any) {
    if (!doc.latestVersionId) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Không thể tải',
        detail: 'Tài liệu chưa có phiên bản file để tải xuống.',
      });
      return;
    }

    this.http.get<any>(`${this.apiBase}/${this.dossierId()}/documents/${doc.latestVersionId}/download-url`)
      .subscribe({
        next: (res) => {
          const token = res?.token;
          if (token) {
            const downloadUrl = `${this.config.apiGatewayUrl}/api/v1/files/download?token=${encodeURIComponent(token)}`;
            window.open(downloadUrl, '_blank');
          } else {
            const url = res?.downloadUrl || res?.url;
            if (url) {
              window.open(url, '_blank');
            } else {
              this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không lấy được đường dẫn tải tài liệu' });
            }
          }
        },
        error: (err) => {
          const message = err?.error?.message || 'Không thể tải file';
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi tải file',
            detail: `${doc.name}: ${message}`,
          });
        }
      });
  }
}
