import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { of, catchError, switchMap } from 'rxjs';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
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
import { ReportDossierType, getReportDossierConfig } from '../../data-access/report-dossier.config';
import { ReportDossierService } from '../../data-access/report-dossier.service';
import { DossierLookupDocumentsTabComponent } from '@sohoa.frontend/features/search';

@Component({
  selector: 'app-report-dossier-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, DossierLookupDocumentsTabComponent, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './report-dossier-detail.component.html',
  styleUrl: './report-dossier-detail.component.scss'
})
export class ReportDossierDetailComponent {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private reportService = inject(ReportDossierService);
  private dossierService = inject(DossierManagementService);
  private messageService = inject(MessageService);

  reportType = signal<ReportDossierType | null>(null);
  dossierId = signal<string | null>(null);
  dossier = signal<any>(null);
  loading = signal(true);
  loadingType = signal(false);
  activeTab = signal<'info' | 'documents'>('info');

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
    const type = this.route.snapshot.data['reportType'] as ReportDossierType;
    this.reportType.set(type);

    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get('id');
      this.dossierId.set(id);
      if (id) this.loadDetail(id);
    });
  }

  loadDetail(id: string) {
    const type = this.reportType();
    if (!type) return;

    const cfg = getReportDossierConfig(type);
    this.loading.set(true);
    this.loadingType.set(true);

    this.reportService.getDetail(cfg, id).pipe(
      switchMap((res) => {
        this.dossier.set(res);
        const formId = (res as any)?.formId ?? (res as any)?.FormId ?? null;
        const dossierTypeId = (res as any)?.dossierTypeId ?? (res as any)?.DossierTypeId;
        const formDataJson = (res as any)?.formDataJson ?? (res as any)?.FormDataJson;
        this.detailFormData = { ...parseFormDataJson(formDataJson) };
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
        if (template) this.applyFormTemplate(template);
        this.loading.set(false);
        this.loadingType.set(false);
      }
    });
  }

  private resolveFormTemplate(formId: string | null, dossierTypeId: string) {
    if (formId) return this.dossierService.getFormTemplate(formId);
    if (!dossierTypeId) return of(null);
    return this.dossierService.getDossierTypeLookup().pipe(
      catchError(() => of([] as any[])),
      switchMap((types) => {
        const found = Array.isArray(types)
          ? types.find((t: any) => guidsEqual(t.id ?? t.Id, dossierTypeId))
          : undefined;
        const resolvedFormId = found?.formId ?? found?.FormId ?? null;
        return resolvedFormId ? this.dossierService.getFormTemplate(resolvedFormId) : of(null);
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
    const type = this.reportType();
    if (!type) return;
    void this.router.navigate([getReportDossierConfig(type).listRoute]);
  }
}
