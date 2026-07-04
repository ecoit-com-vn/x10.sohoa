import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { of, catchError, switchMap } from 'rxjs';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import {
  EavField,
  formatFieldDisplayValue,
  guidsEqual,
  normalizeField,
  parseFormDataJson,
  pickFormDataForSchema,
  readFormSchemaJson
} from '@sohoa.frontend/features/dossier-management';
import {
  DocumentFulltextDetail,
  DocumentFulltextSearchService
} from '../../data-access/document-fulltext-search.service';
import { DossierLookupDocumentsTabComponent } from '../dossier-lookup-documents-tab/dossier-lookup-documents-tab.component';

@Component({
  selector: 'app-document-fulltext-detail',
  standalone: true,
  imports: [CommonModule, ToastModule, WfBreadcrumbComponent, DossierLookupDocumentsTabComponent],
  providers: [MessageService],
  templateUrl: './document-fulltext-detail.component.html',
  styleUrl: './document-fulltext-detail.component.scss'
})
export class DocumentFulltextDetailComponent implements OnDestroy {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private searchService = inject(DocumentFulltextSearchService);
  private messageService = inject(MessageService);
  private sanitizer = inject(DomSanitizer);

  versionId = signal<string | null>(null);
  returnKeyword = signal('');
  detail = signal<DocumentFulltextDetail | null>(null);
  dossier = signal<Record<string, unknown> | null>(null);
  loading = signal(true);
  loadingDossier = signal(false);
  loadingTemplate = signal(false);
  previewUrl = signal<string | null>(null);
  previewSafeSrc = signal<SafeResourceUrl | string>('');
  previewLoading = signal(false);
  dossierTab = signal<'info' | 'documents' | 'related'>('info');

  private previewLoadedKey: string | null = null;

  documentFields = signal<EavField[]>([]);
  documentFormData: Record<string, unknown> = {};
  dossierFields = signal<EavField[]>([]);
  dossierFormData: Record<string, unknown> = {};

  formatFieldDisplayValue = formatFieldDisplayValue;

  dossierId = computed(() => (this.detail()?.dossierId || '').trim() || null);

  isPdf = computed(() => (this.detail()?.mimeType || '').toLowerCase().includes('pdf'));

  isImage = computed(() => {
    const mime = (this.detail()?.mimeType || '').toLowerCase();
    return mime.startsWith('image/');
  });

  equipments = computed(() => {
    const d = this.dossier();
    const list = d?.['equipments'] ?? d?.['Equipments'] ?? [];
    return Array.isArray(list) ? list : [];
  });

  dossierFieldsLeft = computed(() => {
    const fields = this.dossierFields();
    const mid = Math.ceil(fields.length / 2);
    return fields.slice(0, mid);
  });

  dossierFieldsRight = computed(() => {
    const fields = this.dossierFields();
    const mid = Math.ceil(fields.length / 2);
    return fields.slice(mid);
  });

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get('versionId');
      if (!id) return;
      this.resetState();
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

  private resetState() {
    this.cleanupPreview();
    this.detail.set(null);
    this.dossier.set(null);
    this.documentFields.set([]);
    this.dossierFields.set([]);
    this.documentFormData = {};
    this.dossierFormData = {};
    this.loading.set(true);
    this.loadingDossier.set(false);
    this.loadingTemplate.set(false);
    this.dossierTab.set('info');
  }

  private loadDetail(versionId: string) {
    this.loading.set(true);
    this.loadingTemplate.set(true);

    this.searchService.getDetail(versionId).pipe(
      switchMap((res) => {
        this.detail.set(res);
        this.documentFormData = { ...parseFormDataJson(res.mergedDataJson ?? null) };

        const dossierId = (res.dossierId || '').trim();
        const template$ = dossierId
          ? this.searchService.getDocumentFormTemplate(dossierId, versionId).pipe(catchError(() => of(null)))
          : of(null);

        return template$.pipe(
          switchMap((template) => {
            this.applyDocumentTemplate(template);
            this.loadingTemplate.set(false);
            if (!dossierId) {
              return of(null);
            }
            this.loadingDossier.set(true);
            return this.searchService.getDossier(dossierId).pipe(catchError(() => of(null)));
          })
        );
      }),
      catchError((err) => {
        const msg = err?.error?.message || 'Không thể tải chi tiết tài liệu';
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: msg });
        this.loading.set(false);
        this.loadingDossier.set(false);
        this.loadingTemplate.set(false);
        return of(null);
      })
    ).subscribe({
      next: (dossierRes) => {
        if (dossierRes) {
          this.dossier.set(dossierRes as Record<string, unknown>);
          this.loadDossierFormTemplate(dossierRes as Record<string, unknown>);
        }
        this.loadingDossier.set(false);
        this.loading.set(false);
        this.loadPreviewIfPossible();
      }
    });
  }

  private loadPreviewIfPossible() {
    const d = this.detail();
    const dossierId = (d?.dossierId || '').trim();
    const versionId = (d?.documentVersionId || this.versionId() || '').trim();
    if (!dossierId || !versionId) return;

    const loadKey = `${dossierId}:${versionId}`;
    if (this.previewLoadedKey === loadKey && this.previewUrl()) {
      return;
    }

    this.previewLoading.set(true);
    this.searchService
      .getPreviewBlobUrl(dossierId, versionId)
      .then((url) => {
        if (this.previewUrl() && this.previewUrl() !== url) {
          window.URL.revokeObjectURL(this.previewUrl()!);
        }
        this.previewLoadedKey = loadKey;
        this.previewUrl.set(url);
        this.previewSafeSrc.set(this.sanitizer.bypassSecurityTrustResourceUrl(url));
        this.previewLoading.set(false);
      })
      .catch(() => {
        this.previewLoading.set(false);
        this.messageService.add({
          severity: 'warn',
          summary: 'Xem trước',
          detail: 'Không thể tải bản xem trước tài liệu'
        });
      });
  }

  private applyDocumentTemplate(template: unknown) {
    if (!template) {
      this.documentFields.set([]);
      return;
    }
    const schemaJson = readFormSchemaJson(template);
    if (!schemaJson) {
      this.documentFields.set([]);
      return;
    }
    try {
      const raw = JSON.parse(schemaJson);
      const fields: EavField[] = Array.isArray(raw) ? raw.map((f) => normalizeField(f)) : [];
      this.documentFields.set(fields);
      this.documentFormData = pickFormDataForSchema(fields, this.documentFormData);
    } catch {
      this.documentFields.set([]);
    }
  }

  private loadDossierFormTemplate(dossierRes: Record<string, unknown>) {
    const formId = (dossierRes['formId'] ?? dossierRes['FormId'] ?? null) as string | null;
    const dossierTypeId = (dossierRes['dossierTypeId'] ?? dossierRes['DossierTypeId'] ?? '') as string;
    const formDataJson = dossierRes['formDataJson'] ?? dossierRes['FormDataJson'];
    this.dossierFormData = { ...parseFormDataJson(formDataJson as string | null) };

    const apply = (template: unknown) => {
      if (!template) {
        this.dossierFields.set([]);
        return;
      }
      const schemaJson = readFormSchemaJson(template);
      if (!schemaJson) {
        this.dossierFields.set([]);
        return;
      }
      try {
        const raw = JSON.parse(schemaJson);
        const fields: EavField[] = Array.isArray(raw) ? raw.map((f) => normalizeField(f)) : [];
        this.dossierFields.set(fields);
        this.dossierFormData = pickFormDataForSchema(fields, this.dossierFormData);
      } catch {
        this.dossierFields.set([]);
      }
    };

    if (formId) {
      this.searchService.getFormTemplate(formId).subscribe({
        next: (template) => apply(template),
        error: () => this.dossierFields.set([])
      });
      return;
    }

    if (!dossierTypeId) {
      this.dossierFields.set([]);
      return;
    }

    this.searchService.getDossierTypeLookup().subscribe({
      next: (types) => {
        const list = Array.isArray(types) ? (types as Array<Record<string, unknown>>) : [];
        const found = list.find((t) => guidsEqual(String(t['id'] ?? t['Id']), dossierTypeId));
        const resolvedFormId = String(found?.['formId'] ?? found?.['FormId'] ?? '').trim() || null;
        if (!resolvedFormId) {
          this.dossierFields.set([]);
          return;
        }
        this.searchService.getFormTemplate(resolvedFormId).subscribe({
          next: (template) => apply(template),
          error: () => this.dossierFields.set([])
        });
      },
      error: () => this.dossierFields.set([])
    });
  }

  async onDownload() {
    const d = this.detail();
    if (!d) return;

    const dossierId = (d.dossierId || '').trim();
    const versionId = (d.documentVersionId || '').trim();
    if (!dossierId || !versionId) return;

    try {
      await this.searchService.downloadFile(dossierId, versionId, d.documentName);
    } catch {
      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi',
        detail: 'Không thể tải tài liệu'
      });
    }
  }

  onBack() {
    const keyword = this.returnKeyword();
    void this.router.navigate(['/search/documents'], {
      queryParams: keyword ? { keyword } : {}
    });
  }

  dossierValue(key: string, fallbackDash = true): string {
    const d = this.dossier();
    if (!d) return fallbackDash ? '—' : '';
    const pascal = key.charAt(0).toUpperCase() + key.slice(1);
    const val = d[key] ?? d[pascal];
    if (val === null || val === undefined || val === '') return fallbackDash ? '—' : '';
    return String(val);
  }

  dossierCreatorName(): string {
    const d = this.dossier();
    if (!d) return '—';
    const creator = d['creator'] ?? d['Creator'];
    if (creator && typeof creator === 'object') {
      const c = creator as Record<string, unknown>;
      return String(c['fullName'] ?? c['FullName'] ?? c['name'] ?? c['Name'] ?? '—');
    }
    return String(d['createdBy'] ?? d['CreatedBy'] ?? '—');
  }

  formatDate(value: string): string {
    if (!value || value === '—') return '—';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return value;
    return date.toLocaleDateString('vi-VN');
  }

  equipmentField(eq: unknown, ...keys: string[]): string {
    if (!eq || typeof eq !== 'object') return '—';
    const row = eq as Record<string, unknown>;
    for (const key of keys) {
      const val = row[key];
      if (val !== null && val !== undefined && val !== '') return String(val);
    }
    return '—';
  }

  trackByFieldKey(_index: number, field: EavField): string {
    return field.key;
  }

  private cleanupPreview() {
    const url = this.previewUrl();
    if (url) {
      window.URL.revokeObjectURL(url);
    }
    this.previewUrl.set(null);
    this.previewSafeSrc.set('');
    this.previewLoadedKey = null;
  }
}
