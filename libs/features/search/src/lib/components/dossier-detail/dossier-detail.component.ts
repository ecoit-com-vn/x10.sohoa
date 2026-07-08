import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DomSanitizer } from '@angular/platform-browser';
import { MessageService } from 'primeng/api';
import { TableModule } from 'primeng/table';
import { PaginatorModule } from 'primeng/paginator';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { of, switchMap, finalize, catchError } from 'rxjs';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { getDossierStatusLabel } from '../../utils/dossier-status.util';
import { DocumentManagementService } from '../../data-access/document-management.service';
import { FileDownloadService } from '../../data-access/file-download.service';
import { DocumentFilter } from '../../models/document.models';
import {
  DossierManagementService,
  DossierDocumentService,
  EavField,
  normalizeDossierDetail,
  parseFormDataJson,
  readFormSchemaJson,
  normalizeField,
  DossierDocumentEditDialogComponent,
} from '@sohoa.frontend/features/dossier-management';

@Component({
  selector: 'app-dossier-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    PaginatorModule,
    DialogModule,
    ToastModule,
    WfBreadcrumbComponent,
    DossierDocumentEditDialogComponent,
  ],
  providers: [MessageService],
  templateUrl: './dossier-detail.component.html',
  styleUrl: './dossier-detail.component.scss',
})
export class DossierDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private documentService = inject(DocumentManagementService);
  private dossierService = inject(DossierManagementService);
  private dossierDocumentService = inject(DossierDocumentService);
  private fileDownloadService = inject(FileDownloadService);
  private messageService = inject(MessageService);
  private sanitizer = inject(DomSanitizer);

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

    // Load lookups
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

  goBack() {
    // If opened in a new tab, we can try window.close()
    if (window.history.length > 1) {
      window.history.back();
    } else {
      window.close();
    }
  }

  loadDossierDetail(id: string) {
    this.loadingForm.set(true);
    this.dossierService.getWarehouseSearchDossierById(id).pipe(
      switchMap((fullDossier) => {
        const normalized = normalizeDossierDetail(fullDossier);
        this.dossier.set(normalized || fullDossier);

        // Load document list and related equipments
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
    this.dossierService.getWarehouseSearchEquipments(dossierId).pipe(
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
    const filter: DocumentFilter = {
      folderId: 'dossier_' + dossierId,
      page: 1,
      pageSize: 50,
    };
    this.documentService.getDocuments(filter, null).pipe(
      finalize(() => this.loadingDossierDocuments.set(false))
    ).subscribe({
      next: (response) => {
        this.dossierDocuments.set(response.items || []);
      },
      error: (err) => {
        console.error('Error loading dossier documents', err);
        this.dossierDocuments.set([]);
      }
    });
  }

  loadRelatedDossiers(dossierId: string) {
    this.loadingRelated.set(true);
    const filter = {
      keyword: this.filterKeyword.trim(),
      dossierTypeId: this.filterDossierTypeId || undefined,
      page: 1,
      pageSize: 100, // Load enough for local client-side paging or display
    };
    this.documentService.getRelatedDossiers(dossierId, filter).pipe(
      finalize(() => this.loadingRelated.set(false))
    ).subscribe({
      next: (response) => {
        let items = response.items || [];

        // Client-side filter by selected equipment if any
        if (this.filterEquipmentId) {
          // If the dossier list in response doesn't explicitly link equipment, we keep items as is, 
          // or filter if item.equipments contains the ID.
          // Since our endpoint returns items, let's filter if equipment ID matches or is linked.
          // Typically equipmentId filtering would be done at server side, but we can do client-side if data is small.
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

  openRelatedDetail(rel: any) {
    window.open(`/#/search/dossier/detail/${rel.id}`, '_blank');
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

    this.fileDownloadService.downloadFile(doc.latestVersionId, doc.name)
      .then(() => {
        this.messageService.add({
          severity: 'success',
          summary: 'Đang tải',
          detail: `Đã bắt đầu tải "${doc.name}"`,
        });
      })
      .catch((error: unknown) => {
        const message = error instanceof Error ? error.message : 'Không thể tải file';
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi tải file',
          detail: `${doc.name}: ${message}`,
        });
      });
  }
}
