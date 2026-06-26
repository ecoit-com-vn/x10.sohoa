import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { MessageService } from 'primeng/api';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { PaginatorModule } from 'primeng/paginator';
import { ToastModule } from 'primeng/toast';
import { FileDownloadService } from '../../data-access/file-download.service';
import { DocumentManagementService } from '../../data-access/document-management.service';
import { FolderNode, Document, DocumentFilter } from '../../models/document.models';
import {
  convertFlatToTree,
  findBreadcrumbPath,
  getBreadcrumbLabel,
} from '../../utils/folder-tree.util';
import { of, switchMap, finalize, catchError, Observable } from 'rxjs';

import { DossierDocumentEditDialogComponent, DossierManagementService, BhsCatalogColumn } from '@sohoa.frontend/features/dossier-management';
import { DossierDocumentService } from '../../../../../dossier-management/src/lib/data-access/dossier-document.service';
import { EavField, normalizeField, readFormSchemaJson, parseFormDataJson, normalizeDossierDetail } from '../../../../../dossier-management/src/lib/utils/dossier-form-schema.util';
import { HttpClient } from '@angular/common/http';
import { AuthService, APP_CONFIG } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-dossier-search',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    DialogModule,
    PaginatorModule,
    ToastModule,
    DossierDocumentEditDialogComponent,
  ],
  providers: [MessageService],
  templateUrl: './dossier-search.component.html',
  styleUrl: './dossier-search.component.scss',
})
export class DossierSearchComponent implements OnInit, OnDestroy {
  private documentService = inject(DocumentManagementService);
  private messageService = inject(MessageService);
  private fileDownloadService = inject(FileDownloadService);
  private authService = inject(AuthService);
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
  private dossierService = inject(DossierManagementService);
  private dossierDocumentService = inject(DossierDocumentService);
  private sanitizer = inject(DomSanitizer);

  // ===== NEW SIGNALS FOR 2-TAB DETAIL & PREVIEW =====
  activeDetailTab = signal<'info' | 'documents'>('info');
  relatedEquipments = signal<any[]>([]);
  loadingEquipments = signal<boolean>(false);
  dynamicFields = signal<EavField[]>([]);
  detailFormData = signal<Record<string, any>>({});
  loadingForm = signal<boolean>(false);
  selectedDocument = signal<any | null>(null);
  previewUrl = signal<string | null>(null);
  loadingPreview = signal<boolean>(false);
  gridTypes = signal<any[]>([]);

  isPdf = computed(() => {
    const doc = this.selectedDocument();
    if (!doc) return false;
    const name = doc.name || '';
    const ext = name.split('.').pop()?.toLowerCase() ?? '';
    const mime = doc.mimeType || '';
    return mime.includes('pdf') || ext === 'pdf';
  });

  isImage = computed(() => {
    const doc = this.selectedDocument();
    if (!doc) return false;
    const name = doc.name || '';
    const ext = name.split('.').pop()?.toLowerCase() ?? '';
    const mime = doc.mimeType || '';
    return mime.startsWith('image/') || ['jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp'].includes(ext);
  });

  previewSrc = computed(() => {
    const base = this.previewUrl();
    if (!base) return '';
    return this.sanitizer.bypassSecurityTrustResourceUrl(base);
  });

  // ===== SIGNALS =====
  folderTree = signal<FolderNode[]>([]);
  flatFolderList = signal<FolderNode[]>([]); // Keep flat list for breadcrumb
  selectedFolder = signal<FolderNode | null>(null);
  documents = signal<Document[]>([]);
  first = signal(0);
  rows = signal(10);
  page = computed(() => Math.floor(this.first() / this.rows()) + 1);
  pageSize = computed(() => this.rows());
  totalDocuments = signal(0);
  loadingTree = signal(false);
  loadingDocuments = signal(false);
  downloadingDocumentIds = signal<Set<string>>(new Set());
  expandedFolders = signal<Set<string>>(new Set()); // Track expanded folder IDs
  showViewDocument = signal(false);
  viewTarget = signal<Document | null>(null);
  unitOptions = signal<any[]>([]);
  selectedUnitId = signal<number | null>(null);

  // ===== DOSSIER SIGNALS =====
  dossiersList = signal<any[]>([]);
  totalDossiersList = signal<number>(0);
  selectedDossier = signal<any | null>(null);
  dossierBhsColumns = signal<BhsCatalogColumn[]>([]);
  loadingDossiers = signal<boolean>(false);
  loadingDossierDocuments = signal<boolean>(false);
  dossierDocuments = signal<any[]>([]);
  totalDossierDocuments = signal<number>(0);

  subFolders = computed(() => {
    const selected = this.selectedFolder();
    const flat = this.flatFolderList();
    if (!selected) {
      return flat.filter(f => !f.parentId)
        .sort((a, b) => {
          if (a.id === 'root-tba' && b.id === 'root-dd') return -1;
          if (a.id === 'root-dd' && b.id === 'root-tba') return 1;
          return a.name.localeCompare(b.name);
        });
    }
    return flat.filter(f => f.parentId === selected.id)
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  // Computed signals for breadcrumbs
  breadcrumbLabel = computed(() => {
    const folder = this.selectedFolder();
    if (!folder) return 'Kho tài liệu thiết bị';
    return getBreadcrumbLabel(folder.id, this.flatFolderList());
  });

  breadcrumbPath = computed(() => {
    const folder = this.selectedFolder();
    if (!folder) return [];
    return findBreadcrumbPath(folder?.id ?? null, this.flatFolderList());
  });

  ngOnInit() {
    this.initializeUnitFilter();
    this.dossierService.getBhsCatalogColumns().subscribe({
      next: (cols) => this.dossierBhsColumns.set(cols),
      error: () => console.error('Failed to load BHS catalog columns'),
    });
    this.dossierService.getGridTypeLookup().subscribe({
      next: (types) => this.gridTypes.set(types || []),
      error: () => console.error('Failed to load grid types'),
    });
  }

  ngOnDestroy() {
    this.cleanupPreview();
  }

  private selectDefaultFolder(flatList: FolderNode[]) {
    const rootFolders = flatList.filter(f => !f.parentId);
    if (rootFolders.length > 0) {
      this.selectFolder(rootFolders[0]);
    }
  }

  // ===== FOLDER OPERATIONS =====
  private loadFolderTree() {
    this.loadingTree.set(true);
    this.documentService.getFolderTree(this.selectedUnitId()).subscribe({
      next: (folders) => {
        this.flatFolderList.set(folders);
        const treeStructure = convertFlatToTree(folders);
        this.folderTree.set(treeStructure);
        this.loadingTree.set(false);
        this.expandedFolders.set(new Set<string>());
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải cây thư mục',
        });
        this.loadingTree.set(false);
      },
    });
  }

  isSelectableFolder(id: string): boolean {
    return !!id && id.startsWith('type_');
  }

  getFolderTypeLabel(folder: FolderNode): string {
    if (folder.id === 'root-tba' || folder.id === 'root-dd') {
      return 'Thư mục gốc';
    }
    if (
      folder.id === 'tba-cao-ap' ||
      folder.id === 'tba-trung-ap' ||
      folder.id === 'dd-cao-ap' ||
      folder.id === 'dd-trung-ap'
    ) {
      return 'Cấp lưới điện';
    }
    if (folder.parentId === 'tba-cao-ap' || folder.parentId === 'tba-trung-ap') {
      return 'Trạm biến áp';
    }
    if (folder.parentId === 'dd-cao-ap' || folder.parentId === 'dd-trung-ap') {
      return 'Đường dây';
    }
    if (this.isSelectableFolder(folder.id)) {
      return 'Hộp hồ sơ';
    }
    return 'Thư mục';
  }

  getFolderIcon(folder: FolderNode): string {
    if (folder.id === 'root-tba') return 'pi-server';
    if (folder.id === 'root-dd') return 'pi-sitemap';
    if (
      folder.id === 'tba-cao-ap' ||
      folder.id === 'tba-trung-ap' ||
      folder.id === 'dd-cao-ap' ||
      folder.id === 'dd-trung-ap'
    ) {
      return 'pi-bolt';
    }
    if (folder.parentId === 'tba-cao-ap' || folder.parentId === 'tba-trung-ap') {
      return 'pi-building';
    }
    if (folder.parentId === 'dd-cao-ap' || folder.parentId === 'dd-trung-ap') {
      return 'pi-share-alt';
    }
    if (this.isSelectableFolder(folder.id)) {
      return 'pi-box';
    }
    return 'pi-folder';
  }

  onSelectSubFolder(folder: FolderNode) {
    const expanded = this.expandedFolders();
    expanded.add(folder.id);
    if (folder.parentId) {
      expanded.add(folder.parentId);
      const flat = this.flatFolderList();
      const parent = flat.find(f => f.id === folder.parentId);
      if (parent && parent.parentId) {
        expanded.add(parent.parentId);
      }
    }
    this.expandedFolders.set(new Set(expanded));
    this.selectFolder(folder);
  }

  selectFolder(folder: FolderNode) {
    this.selectedFolder.set(folder);
    this.first.set(0);
    this.selectedDossier.set(null); // Clear selected dossier detail when folder changes
    if (folder.id && this.isSelectableFolder(folder.id)) {
      this.loadDossiers();
    } else {
      this.documents.set([]);
      this.totalDocuments.set(0);
    }
  }

  loadDossiers() {
    this.loadingDocuments.set(true);
    const selected = this.selectedFolder();
    if (!selected || !selected.id.startsWith('type_')) {
      this.loadingDocuments.set(false);
      return;
    }

    const id = selected.id;
    const cleanId = id.substring('type_'.length);
    const firstUnderscoreIdx = cleanId.indexOf('_');
    if (firstUnderscoreIdx === -1) {
      this.loadingDocuments.set(false);
      return;
    }
    const rest = cleanId.substring(firstUnderscoreIdx + 1);
    const lastUnderscoreIdx = rest.lastIndexOf('_');
    if (lastUnderscoreIdx === -1) {
      this.loadingDocuments.set(false);
      return;
    }
    const infraId = rest.substring(0, lastUnderscoreIdx);
    const dossierTypeId = rest.substring(lastUnderscoreIdx + 1);

    console.log('[DEBUG] selected folder id:', id);
    console.log('[DEBUG] parsed infraId:', infraId);
    console.log('[DEBUG] parsed dossierTypeId:', dossierTypeId);

    const filter = {
      infrastructureId: infraId,
      dossierTypeId: dossierTypeId,
      unitId: this.selectedUnitId() || undefined,
      page: this.page(),
      pageSize: this.pageSize(),
    };

    console.log('[DEBUG] sending filter to API:', filter);

    this.dossierService.getDossiers(filter).subscribe({
      next: (response) => {
        this.dossiersList.set(response.items || []);
        this.totalDossiersList.set(response.totalCount || 0);
        this.loadingDocuments.set(false);
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải danh sách hồ sơ',
        });
        this.loadingDocuments.set(false);
      }
    });
  }

  onDossierPageChange(event: any) {
    this.first.set(event.first);
    this.rows.set(event.rows);
    this.loadDossiers();
  }

  onViewDossierDetail(item: any) {
    this.cleanupPreview();
    this.selectedDossier.set(null);
    this.selectedDocument.set(null);
    this.relatedEquipments.set([]);
    this.dynamicFields.set([]);
    this.detailFormData.set({});
    this.activeDetailTab.set('info');

    this.loadingForm.set(true);
    this.dossierService.getDossierById(item.id).pipe(
      switchMap((fullDossier) => {
        const normalized = normalizeDossierDetail(fullDossier);
        this.selectedDossier.set(normalized || fullDossier);

        this.loadDossierDocuments(item.id);
        this.loadRelatedEquipments(item.id);

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
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải chi tiết dữ liệu hồ sơ',
        });
        console.error(err);
      }
    });
  }

  getGridTypeName(gridTypeId: number | null): string {
    if (gridTypeId == null) return '-';
    const found = this.gridTypes().find(t => t.id === gridTypeId);
    return found ? found.name : `Lưới điện ${gridTypeId}`;
  }

  private resolveFormTemplate(formId: string | null, dossierTypeId: string): Observable<any> {
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
          ? types.find((t: any) => {
              const aId = t.id ?? t.Id;
              return aId && dossierTypeId && String(aId).toLowerCase() === String(dossierTypeId).toLowerCase();
            })
          : undefined;
        const resolvedFormId = found?.formId ?? found?.FormId ?? null;
        if (!resolvedFormId) {
          return of(null);
        }
        return this.dossierService.getFormTemplate(resolvedFormId);
      })
    );
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

  loadRelatedEquipments(dossierId: string) {
    this.loadingEquipments.set(true);
    this.dossierService.getEquipments(dossierId).pipe(
      finalize(() => this.loadingEquipments.set(false))
    ).subscribe({
      next: (eqs) => {
        this.relatedEquipments.set(eqs || []);
      },
      error: (err) => {
        console.error('Failed to load related equipments', err);
        this.relatedEquipments.set([]);
      }
    });
  }

  cleanupPreview(): void {
    const url = this.previewUrl();
    if (url) {
      this.dossierDocumentService.revokePreviewBlobUrl(url);
      this.previewUrl.set(null);
    }
  }

  onViewDocumentDetail(doc: any) {
    this.cleanupPreview();
    this.selectedDocument.set(doc);
    if (!doc.latestVersionId) {
      return;
    }

    const dossierId = doc.dossierId || this.selectedDossier()?.id;
    if (!dossierId) {
      return;
    }

    this.loadingPreview.set(true);
    this.dossierDocumentService.getPreviewBlobUrl(dossierId, doc.latestVersionId)
      .then((url: string) => {
        this.previewUrl.set(url);
      })
      .catch((err: any) => {
        console.error(err);
        this.messageService.add({
          severity: 'warn',
          summary: 'Xem trước',
          detail: 'Không thể tải bản xem trước tài liệu',
        });
      })
      .finally(() => {
        this.loadingPreview.set(false);
      });
  }

  onCloseDocumentDetail() {
    this.cleanupPreview();
    this.selectedDocument.set(null);
  }

  loadDossierDocuments(dossierId: string) {
    this.loadingDossierDocuments.set(true);
    const filter: DocumentFilter = {
      folderId: 'dossier_' + dossierId,
      page: 1,
      pageSize: 50,
    };

    this.documentService.getDocuments(filter, this.selectedUnitId()).subscribe({
      next: (response) => {
        this.dossierDocuments.set(response.items);
        this.totalDossierDocuments.set(response.totalCount);
        this.loadingDossierDocuments.set(false);
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải danh sách tài liệu của hồ sơ',
        });
        this.loadingDossierDocuments.set(false);
      },
    });
  }

  getCatalogValue(item: any, col: BhsCatalogColumn): string {
    const data = item?.catalogData ?? item?.CatalogData ?? {};
    const value = data[col.key] ?? data[col.code];
    return value != null && String(value).trim() !== '' ? String(value) : '-';
  }

  toggleFolderExpand(folder: FolderNode, event: Event) {
    event.stopPropagation();
    if (!folder.children || folder.children.length === 0) {
      this.selectFolder(folder);
      return;
    }

    const expanded = this.expandedFolders();
    if (expanded.has(folder.id)) {
      expanded.delete(folder.id);
    } else {
      expanded.add(folder.id);
    }
    this.expandedFolders.set(new Set(expanded));
    this.selectFolder(folder);
  }

  // ===== DOCUMENT OPERATIONS =====
  private loadDocuments() {
    this.loadingDocuments.set(true);
    const filter: DocumentFilter = {
      folderId: this.selectedFolder()?.id,
      page: this.page(),
      pageSize: this.pageSize(),
    };

    this.documentService.getDocuments(filter, this.selectedUnitId()).subscribe({
      next: (response) => {
        this.documents.set(response.items);
        this.totalDocuments.set(response.totalCount);
        this.loadingDocuments.set(false);
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải danh sách tài liệu',
        });
        this.loadingDocuments.set(false);
      },
    });
  }

  onPageChange(event: any) {
    this.first.set(event.first);
    this.rows.set(event.rows);
    this.loadDocuments();
  }

  formatFileSize(bytes?: number): string {
    if (!bytes || bytes <= 0) return '-';
    const units = ['B', 'KB', 'MB', 'GB'];
    let size = bytes;
    let unitIndex = 0;
    while (size >= 1024 && unitIndex < units.length - 1) {
      size /= 1024;
      unitIndex++;
    }
    return `${size.toFixed(unitIndex === 0 ? 0 : 1)} ${units[unitIndex]}`;
  }

  formatDate(value?: Date | string): string {
    if (!value) return '-';
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) return '-';
    return date.toLocaleString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  getFileIcon(fileName: string): string {
    const ext = fileName.split('.').pop()?.toLowerCase() ?? '';
    if (['jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp'].includes(ext)) return 'pi-image';
    if (['pdf'].includes(ext)) return 'pi-file-pdf';
    if (['doc', 'docx'].includes(ext)) return 'pi-file-word';
    if (['xls', 'xlsx'].includes(ext)) return 'pi-file-excel';
    return 'pi-file';
  }

  trackByDocumentId(index: number, doc: Document): string {
    return doc.id;
  }

  isDownloadingDocument(docId: string): boolean {
    return this.downloadingDocumentIds().has(docId);
  }

  onDownloadDocument(doc: Document) {
    if (!doc.latestVersionId) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Không thể tải',
        detail: 'Tài liệu chưa có phiên bản file để tải xuống.',
      });
      return;
    }

    const downloading = new Set(this.downloadingDocumentIds());
    downloading.add(doc.id);
    this.downloadingDocumentIds.set(downloading);

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
      })
      .finally(() => {
        const next = new Set(this.downloadingDocumentIds());
        next.delete(doc.id);
        this.downloadingDocumentIds.set(next);
      });
  }

  onViewDocument(doc: Document) {
    if (!doc.dossierId) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Xem chi tiết',
        detail: 'Tài liệu này không thuộc hồ sơ nào.',
      });
      return;
    }
    this.viewTarget.set(doc);
    this.showViewDocument.set(true);
  }

  private initializeUnitFilter() {
    const userUnitId = this.authService.getUserUnitId();
    this.selectedUnitId.set(userUnitId);

    this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/v1/organization-units`).subscribe({
      next: (units) => {
        const allUnits = Array.isArray(units) ? units : [];
        if (userUnitId) {
          const userUnit = allUnits.find(u => u.id == userUnitId);
          const children = allUnits.filter(u => u.parentId == userUnitId);
          
          const options: any[] = [];
          if (userUnit) {
            options.push(userUnit);
          }
          options.push(...children);
          this.unitOptions.set(options);
        }
        this.loadFolderTree();
      },
      error: () => {
        this.loadFolderTree();
      }
    });
  }

  onUnitChange(unitId: any) {
    const numericId = unitId ? parseInt(unitId, 10) : null;
    this.selectedUnitId.set(numericId);
    this.selectedFolder.set(null);
    this.documents.set([]);
    this.totalDocuments.set(0);
    this.expandedFolders.set(new Set<string>());
    this.loadFolderTree();
  }

  trackByFolderId(index: number, folder: FolderNode): string {
    return folder.id;
  }
}
