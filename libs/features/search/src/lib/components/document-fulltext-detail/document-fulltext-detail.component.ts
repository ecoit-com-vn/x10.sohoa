import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { switchMap, of, catchError } from 'rxjs';
import {
  DossierDocumentService,
  DossierManagementService,
  EavField,
  formatFieldDisplayValue,
  guidsEqual,
  parseFormDataJson,
  parseFormSchemaFields,
  parseMergedDataJson,
  pickFormDataForSchema,
  readFormSchemaJson
} from '@sohoa.frontend/features/dossier-management';
import {
  DocumentFulltextSearchDetail,
  DocumentFulltextSearchService
} from '../../data-access/document-fulltext-search.service';
import { FileDownloadService } from '../../data-access/file-download.service';
import { DossierLookupDocumentsTabComponent } from '../dossier-lookup-documents-tab/dossier-lookup-documents-tab.component';

interface EquipmentRow {
  equipmentCode?: string;
  EquipmentCode?: string;
  code?: string;
  equipmentName?: string;
  EquipmentName?: string;
  name?: string;
  serialNumber?: string;
  SerialNumber?: string;
}

@Component({
  selector: 'app-document-fulltext-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, DossierLookupDocumentsTabComponent],
  providers: [MessageService],
  templateUrl: './document-fulltext-detail.component.html',
  styleUrl: './document-fulltext-detail.component.scss'
})
export class DocumentFulltextDetailComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private searchService = inject(DocumentFulltextSearchService);
  private documentService = inject(DossierDocumentService);
  private dossierService = inject(DossierManagementService);
  private fileDownloadService = inject(FileDownloadService);
  private messageService = inject(MessageService);
  private sanitizer = inject(DomSanitizer);

  detail = signal<DocumentFulltextSearchDetail | null>(null);
  dossier = signal<Record<string, unknown> | null>(null);
  loading = signal(true);
  loadingDossier = signal(false);
  loadingPreview = signal(false);
  downloading = signal(false);
  activeDossierTab = signal<'info' | 'documents'>('info');
  returnKeyword = signal('');

  documentFields = signal<EavField[]>([]);
  documentFormData: Record<string, unknown> = {};
  dossierDynamicFields = signal<EavField[]>([]);
  dossierFormData: Record<string, unknown> = {};

  previewUrl = signal<string | null>(null);
  currentPage = signal(1);
  totalPagesHint = signal(1);

  versionId = signal('');

  isPdf = computed(() => {
    const mime = this.detail()?.mimeType ?? '';
    const name = this.detail()?.documentName ?? '';
    return mime.includes('pdf') || name.toLowerCase().endsWith('.pdf');
  });

  isImage = computed(() => (this.detail()?.mimeType ?? '').startsWith('image/'));
  totalPages = computed(() => Math.max(this.totalPagesHint(), 1));
  equipments = computed((): EquipmentRow[] => {
    const d = this.dossier();
    const list = (d?.['equipments'] ?? d?.['Equipments']) as EquipmentRow[] | undefined;
    return Array.isArray(list) ? list : [];
  });

  formatFieldDisplayValue = formatFieldDisplayValue;

  ngOnInit() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get('versionId');
      if (!id) return;
      this.versionId.set(id);
      this.loadDetail(id);
    });

    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.returnKeyword.set((params.get('keyword') || '').trim());
    });
  }

  ngOnDestroy() {
    this.cleanupPreview();
  }

  private loadDetail(versionId: string) {
    this.loading.set(true);
    this.searchService.getDetail(versionId).subscribe({
      next: (res) => {
        this.detail.set(res);
        this.loadDocumentFields(res);
        if (res.dossierId) {
          this.loadDossier(res.dossierId);
          this.loadPreview(res.dossierId, versionId);
        } else {
          this.loading.set(false);
        }
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không tải được chi tiết tài liệu.'
        });
      }
    });
  }

  private loadDocumentFields(detail: DocumentFulltextSearchDetail) {
    const merged = parseMergedDataJson(detail.mergedDataJson ?? undefined);
    this.documentFormData = { ...merged };

    if (!detail.documentTypeId) {
      this.documentFields.set([]);
      return;
    }

    this.documentService.lookupDocumentTypes().subscribe({
      next: (types) => {
        const match = types.find((t) => guidsEqual(t.id, detail.documentTypeId!));
        const formId = match?.formId;
        if (!formId) {
          this.documentFields.set([]);
          return;
        }
        this.dossierService.getFormTemplate(formId).subscribe({
          next: (template) => {
            const fields = parseFormSchemaFields(readFormSchemaJson(template));
            this.documentFields.set(fields);
            this.documentFormData = pickFormDataForSchema(fields, merged);
          },
          error: () => this.documentFields.set([])
        });
      },
      error: () => this.documentFields.set([])
    });
  }

  private loadDossier(dossierId: string) {
    this.loadingDossier.set(true);
    this.dossierService.getDossierByEquipmentLookup(dossierId).pipe(
      switchMap((res) => {
        this.dossier.set(res as Record<string, unknown>);
        const formId = (res as { formId?: string; FormId?: string }).formId
          ?? (res as { formId?: string; FormId?: string }).FormId
          ?? null;
        const dossierTypeId = (res as { dossierTypeId?: string; DossierTypeId?: string }).dossierTypeId
          ?? (res as { dossierTypeId?: string; DossierTypeId?: string }).DossierTypeId;
        const formDataJson = (res as { formDataJson?: string; FormDataJson?: string }).formDataJson
          ?? (res as { formDataJson?: string; FormDataJson?: string }).FormDataJson;
        this.dossierFormData = parseFormDataJson(formDataJson);

        if (formId) {
          return this.dossierService.getFormTemplate(formId);
        }
        if (!dossierTypeId) {
          return of(null);
        }
        return this.dossierService.getDossierTypeLookup().pipe(
          catchError(() => of([] as Array<{ id?: string; Id?: string; formId?: string; FormId?: string }>)),
          switchMap((types) => {
            const found = types.find((t) => guidsEqual(t.id ?? t.Id, dossierTypeId));
            const resolvedFormId = found?.formId ?? found?.FormId;
            return resolvedFormId ? this.dossierService.getFormTemplate(resolvedFormId) : of(null);
          })
        );
      }),
      catchError(() => {
        this.messageService.add({
          severity: 'warn',
          summary: 'Cảnh báo',
          detail: 'Không tải được thông tin hồ sơ liên quan.'
        });
        return of(null);
      })
    ).subscribe({
      next: (template) => {
        if (template) {
          const fields = parseFormSchemaFields(readFormSchemaJson(template));
          this.dossierDynamicFields.set(fields);
          this.dossierFormData = pickFormDataForSchema(fields, this.dossierFormData);
        }
        this.loadingDossier.set(false);
        this.loading.set(false);
      },
      error: () => {
        this.loadingDossier.set(false);
        this.loading.set(false);
      }
    });
  }

  private loadPreview(dossierId: string, versionId: string) {
    this.loadingPreview.set(true);
    this.documentService
      .getPreviewBlobUrl(dossierId, versionId, true)
      .then((url) => this.previewUrl.set(url))
      .catch(() => {
        this.messageService.add({
          severity: 'warn',
          summary: 'Xem trước',
          detail: 'Không thể tải bản xem trước tài liệu.'
        });
      })
      .finally(() => this.loadingPreview.set(false));
  }

  private cleanupPreview() {
    const url = this.previewUrl();
    if (url) {
      this.documentService.revokePreviewBlobUrl(url);
      this.previewUrl.set(null);
    }
  }

  previewSrc(): SafeResourceUrl | string {
    const base = this.previewUrl();
    if (!base) return '';
    const url = this.isPdf() ? `${base}#page=${this.currentPage()}` : base;
    return this.sanitizer.bypassSecurityTrustResourceUrl(url);
  }

  prevPage() {
    if (this.currentPage() > 1) {
      this.currentPage.update((p) => p - 1);
    }
  }

  nextPage() {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update((p) => p + 1);
    }
  }

  onBack() {
    const keyword = this.returnKeyword();
    if (keyword) {
      this.router.navigate(['/search/documents'], { queryParams: { keyword } });
      return;
    }
    this.router.navigate(['/search/documents']);
  }

  openDossierDetail() {
    const dossierId = this.detail()?.dossierId;
    if (dossierId) {
      this.router.navigate(['/search/dossier-by-equipment', dossierId]);
    }
  }

  async onDownload() {
    const detail = this.detail();
    if (!detail) return;
    this.downloading.set(true);
    try {
      await this.fileDownloadService.downloadFile(detail.documentVersionId, detail.documentName);
    } catch (error: unknown) {
      const msg = error instanceof Error ? error.message : 'Không thể tải tài liệu.';
      this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: msg });
    } finally {
      this.downloading.set(false);
    }
  }

  trackByFieldKey(_index: number, field: EavField) {
    return field.key;
  }
}
