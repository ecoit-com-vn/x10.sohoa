import { Component, OnInit, OnDestroy, inject, signal, computed, effect } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { MenuItem, MessageService } from 'primeng/api';
import { Menu, MenuModule } from 'primeng/menu';
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

import { DossierDocumentEditDialogComponent, DossierManagementService, BhsCatalogColumn, DossierDocumentService, EavField, normalizeField, readFormSchemaJson, parseFormDataJson, normalizeDossierDetail } from '@sohoa.frontend/features/dossier-management';
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
    MenuModule,
    DossierDocumentEditDialogComponent,
    WfBreadcrumbComponent,
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
  activeDetailTab = signal<'info' | 'documents' | 'related'>('info');
  relatedEquipments = signal<any[]>([]);
  loadingEquipments = signal<boolean>(false);
  dynamicFields = signal<EavField[]>([]);
  detailFormData = signal<Record<string, any>>({});
  loadingForm = signal<boolean>(false);
  selectedDocument = signal<any | null>(null);
  previewUrl = signal<string | null>(null);
  loadingPreview = signal<boolean>(false);
  gridTypes = signal<any[]>([]);

  // Phân trang thiết bị liên quan
  equipmentFirst = signal<number>(0);
  equipmentRows = signal<number>(10);
  paginatedEquipments = computed(() => {
    const start = this.equipmentFirst();
    const end = start + this.equipmentRows();
    return this.relatedEquipments().slice(start, end);
  });

  // Chia đôi thuộc tính động EAV để hiển thị 2 cột sạch đẹp dạng văn bản
  leftDynamicFields = computed(() => {
    const fields = this.dynamicFields();
    return fields.slice(0, Math.ceil(fields.length / 2));
  });

  rightDynamicFields = computed(() => {
    const fields = this.dynamicFields();
    return fields.slice(Math.ceil(fields.length / 2));
  });

  onEquipmentPageChange(event: any) {
    this.equipmentFirst.set(event.first);
    this.equipmentRows.set(event.rows);
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

  // ===== NEW SIGNALS FOR DOSSIER SEARCH FILTER =====
  searchGridType = signal<string>('ALL');
  searchInfraName = signal<string>('');
  searchBoxName = signal<string>('');

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
  filteredFolderTree = computed(() => {
    const boxName = this.searchBoxName().trim().toLowerCase();
    const gridType = this.searchGridType();
    const infraName = this.searchInfraName().trim().toLowerCase();
    const flat = this.flatFolderList();

    if (!boxName && gridType === 'ALL' && !infraName) {
      return this.folderTree();
    }

    const validBoxes = flat.filter(f => {
      if (!f.id.startsWith('type_')) return false;
      if (boxName && !f.name.toLowerCase().includes(boxName)) return false;
      if (gridType === 'HIGH') {
        if (!f.id.includes('tba-cao-ap_') && !f.id.includes('dd-cao-ap_')) return false;
      } else if (gridType === 'MEDIUM') {
        if (!f.id.includes('tba-trung-ap_') && !f.id.includes('dd-trung-ap_')) return false;
      }
      if (infraName && f.parentId) {
        const parent = flat.find(p => p.id === f.parentId);
        if (!parent || !parent.name.toLowerCase().includes(infraName)) return false;
      }
      return true;
    });

    const validNodeIds = new Set<string>();
    for (const box of validBoxes) {
      validNodeIds.add(box.id);
      let currParentId = box.parentId;
      while (currParentId) {
        validNodeIds.add(currParentId);
        const parentNode = flat.find(p => p.id === currParentId);
        currParentId = parentNode ? parentNode.parentId : null;
      }
    }

    const filteredFlatList = flat.filter(f => validNodeIds.has(f.id));
    return convertFlatToTree(filteredFlatList);
  });

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
  actionMenuItems: MenuItem[] = [];

  openActionMenu(item: any, event: MouseEvent, menu: Menu): void {
    this.actionMenuItems = [
      {
        label: 'Xem chi tiết hồ sơ',
        title: 'Xem chi tiết hồ sơ',
        icon: 'pi pi-eye color-teal',
        command: () => this.onViewDossierDetail(item),
      },
    ];
    menu.toggle(event);
  }

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

  infrastructures = computed(() => {
    const flat = this.flatFolderList();
    const infraNodes = flat.filter(f => 
      f.parentId === 'tba-cao-ap' || 
      f.parentId === 'tba-trung-ap' || 
      f.parentId === 'dd-cao-ap' || 
      f.parentId === 'dd-trung-ap'
    );
    const uniqueMap = new Map<string, any>();
    for (const node of infraNodes) {
      uniqueMap.set(node.name, { id: node.id, name: node.name });
    }
    return Array.from(uniqueMap.values()).sort((a, b) => a.name.localeCompare(b.name));
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

  constructor() {
    effect(() => {
      const boxName = this.searchBoxName();
      const gridType = this.searchGridType();
      const infraName = this.searchInfraName();
      const flat = this.flatFolderList();
      if (flat.length === 0) return;

      const validBoxes = flat.filter(f => {
        if (!f.id.startsWith('type_')) return false;
        if (boxName && !f.name.toLowerCase().includes(boxName.trim().toLowerCase())) return false;
        if (gridType === 'HIGH') {
          if (!f.id.includes('tba-cao-ap_') && !f.id.includes('dd-cao-ap_')) return false;
        } else if (gridType === 'MEDIUM') {
          if (!f.id.includes('tba-trung-ap_') && !f.id.includes('dd-trung-ap_')) return false;
        }
        if (infraName && f.parentId) {
          const parent = flat.find(p => p.id === f.parentId);
          if (!parent || !parent.name.toLowerCase().includes(infraName.trim().toLowerCase())) return false;
        }
        return true;
      });

      if (validBoxes.length > 0 && (boxName || gridType !== 'ALL' || infraName)) {
        const parentsToExpand = new Set<string>();
        for (const box of validBoxes) {
          let currParentId = box.parentId;
          while (currParentId) {
            parentsToExpand.add(currParentId);
            const parentNode = flat.find(p => p.id === currParentId);
            currParentId = parentNode ? parentNode.parentId : null;
          }
        }
        const expanded = new Set<string>([
          'root-tba', 'root-dd', 'tba-cao-ap', 'tba-trung-ap', 'dd-cao-ap', 'dd-trung-ap',
          ...Array.from(parentsToExpand)
        ]);
        this.expandedFolders.set(expanded);
      }
    });
  }

  onClearFilters() {
    this.searchGridType.set('ALL');
    this.searchInfraName.set('');
    this.searchBoxName.set('');
    this.expandedFolders.set(new Set<string>([
      'root-tba', 'root-dd', 'tba-cao-ap', 'dd-cao-ap'
    ]));
  }

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

  private loadFolderTree() {
    this.loadingTree.set(true);
    this.documentService.getFolderTree(this.selectedUnitId()).subscribe({
      next: (folders) => {
        this.flatFolderList.set(folders);
        const treeStructure = convertFlatToTree(folders);
        this.folderTree.set(treeStructure);
        this.loadingTree.set(false);

        // Mặc định chỉ mở cấp 1 và hộp lưới điện cao áp
        const toExpand = new Set<string>([
          'root-tba',
          'root-dd',
          'tba-cao-ap',
          'dd-cao-ap',
        ]);

        this.expandedFolders.set(toExpand);
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
    let cleanId = id.substring('type_'.length);
    const prefixes = ['tba-cao-ap_', 'tba-trung-ap_', 'dd-cao-ap_', 'dd-trung-ap_'];
    for (const prefix of prefixes) {
      if (cleanId.startsWith(prefix)) {
        cleanId = cleanId.substring(prefix.length);
        break;
      }
    }

    const parts = cleanId.split('_');
    if (parts.length < 2) {
      this.loadingDocuments.set(false);
      return;
    }
    const infraId = parts[0];
    const dossierTypeId = parts[1];

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

    this.dossierService.getCatalogDossiers(filter).subscribe({
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
    window.open(`/#/search/dossier/detail/${item.id}`, '_blank');
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

    this.http
      .get<Array<{ id: number; name: string; parentId?: number | null }>>(
        `${this.config.apiGatewayUrl}/api/v1/organization-units/lookup`
      )
      .subscribe({
        next: (units) => {
          const options = Array.isArray(units) ? units : [];
          this.unitOptions.set(options);

          if (options.length > 0) {
            const defaultUnit =
              userUnitId != null
                ? options.find((u) => u.id === userUnitId) ?? options[0]
                : options[0];
            this.selectedUnitId.set(defaultUnit?.id ?? userUnitId);
          }

          this.loadFolderTree();
        },
        error: () => {
          this.unitOptions.set([]);
          this.loadFolderTree();
        },
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
