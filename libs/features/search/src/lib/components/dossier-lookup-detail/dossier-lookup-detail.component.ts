import { Component, inject, signal, computed } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { of, catchError, switchMap } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';

import { DossierLookupDocumentsTabComponent } from '../dossier-lookup-documents-tab/dossier-lookup-documents-tab.component';
import {
  DossierManagementService,
  EavField,
  formatFieldDisplayValue,
  guidsEqual,
  normalizeField,
  parseFormDataJson,
  pickFormDataForSchema,
  readFormSchemaJson
} from '@sohoa.frontend/features/dossier-management';

@Component({
  selector: 'app-dossier-lookup-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, DossierLookupDocumentsTabComponent, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './dossier-lookup-detail.component.html',
  styleUrl: './dossier-lookup-detail.component.scss'
})
export class DossierLookupDetailComponent {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
  private dossierService = inject(DossierManagementService);
  private messageService = inject(MessageService);

  dossierId = signal<string | null>(null);
  dossier = signal<any>(null);
  loading = signal<boolean>(true);
  loadingType = signal<boolean>(false);
  activeTab = signal<'info' | 'documents'>('info');

  // Form templates
  formTemplate = signal<any>(null);
  dynamicFields = signal<EavField[]>([]);
  detailFormData: Record<string, any> = {};

  equipments = computed(() => {
    const d = this.dossier();
    const list = d?.equipments ?? d?.Equipments ?? [];
    return Array.isArray(list) ? list : [];
  });

  formatFieldDisplayValue = formatFieldDisplayValue;

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get('id');
      this.dossierId.set(id);
      if (id) {
        this.loadDetail(id);
      }
    });

    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const versionId = (params.get('documentVersionId') || '').trim();
      if (versionId) {
        void this.router.navigate(['/search/documents', versionId], {
          replaceUrl: true,
          queryParams: { keyword: params.get('keyword') || null }
        });
      }
    });
  }

  loadDetail(id: string) {
    this.loading.set(true);
    this.loadingType.set(true);

    this.http.get<any>(`${this.config.apiGatewayUrl}/api/v1/dossiers-by-equipment/${id}`).pipe(
      switchMap((res) => {
        this.dossier.set(res);
        const formId = res.formId ?? res.FormId ?? null;
        const dossierTypeId = res.dossierTypeId ?? res.DossierTypeId;
        const formDataJson = res.formDataJson ?? res.FormDataJson;

        const pendingFormData = parseFormDataJson(formDataJson);
        this.detailFormData = { ...pendingFormData };

        return this.resolveFormTemplate(formId, dossierTypeId);
      }),
      catchError((err) => {
        const msg = err?.error?.message || 'Không thể tải chi tiết hồ sơ';
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: msg });
        this.loading.set(false);
        this.loadingType.set(false);
        return of(null);
      })
    ).subscribe({
      next: (template) => {
        if (template) {
          this.applyFormTemplate(template);
        }
        this.loading.set(false);
        this.loadingType.set(false);
      }
    });
  }

  private resolveFormTemplate(formId: string | null, dossierTypeId: string) {
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
          ? types.find((t: any) => guidsEqual(t.id ?? t.Id, dossierTypeId))
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
      this.formTemplate.set(null);
      this.dynamicFields.set([]);
      return;
    }
    this.formTemplate.set(template);
    const schemaJson = readFormSchemaJson(template);
    if (!schemaJson) {
      this.dynamicFields.set([]);
      return;
    }
    try {
      const raw = JSON.parse(schemaJson);
      const fields: EavField[] = Array.isArray(raw) ? raw.map((f) => normalizeField(f)) : [];
      this.dynamicFields.set(fields);
      this.detailFormData = pickFormDataForSchema(fields, this.detailFormData);
    } catch {
      this.dynamicFields.set([]);
    }
  }

  trackByFieldKey(_index: number, field: EavField): string {
    return field.key;
  }

  onBack(): void {
    void this.router.navigate(['/search/dossier-by-equipment']);
  }
}
