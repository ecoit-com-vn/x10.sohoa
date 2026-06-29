import { Component, Input, Output, EventEmitter, inject, signal, computed, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { forkJoin, finalize } from 'rxjs';
import { DossierDocumentService } from '../../data-access/dossier-document.service';
import { DossierManagementService } from '../../data-access/dossier-management.service';
import {
  EavField,
  mergeExtractionPageResults,
  normalizeDossierDetail,
  parseFormDataJson,
  parseFormSchemaFields,
  parseMergedDataJson,
  pickFormDataForSchema,
  readFormSchemaJson,
  serializeFormDataForSchema,
} from '../../utils/dossier-form-schema.util';

@Component({
  selector: 'app-dossier-document-edit-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ButtonModule],
  templateUrl: './dossier-document-edit-dialog.component.html',
  styleUrl: './dossier-document-edit-dialog.component.scss',
})
export class DossierDocumentEditDialogComponent implements OnDestroy {
  private documentService = inject(DossierDocumentService);
  private dossierService = inject(DossierManagementService);
  private messageService = inject(MessageService);
  private sanitizer = inject(DomSanitizer);

  @Input({ required: true }) dossierId!: string;
  @Input({ required: true }) versionId!: string;
  @Input() documentName = '';
  @Input() mimeType: string | null = null;
  @Input() documentTypeId: string | null = null;
  @Input() formId: string | null = null;
  @Input() canEdit = false;
  @Input() totalPagesHint = 0;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() applied = new EventEmitter<void>();

  loading = signal(false);
  saving = signal(false);
  fields = signal<EavField[]>([]);
  draftData = signal<Record<string, unknown>>({});
  currentFormData = signal<Record<string, unknown>>({});
  rowVersion = signal(0);

  previewUrl = signal<string | null>(null);
  currentPage = signal(1);
  resolvedFormId = signal<string | null>(null);

  isPdf = computed(() => (this.mimeType ?? '').includes('pdf'));
  isImage = computed(() => (this.mimeType ?? '').startsWith('image/'));
  totalPages = computed(() => Math.max(this.totalPagesHint, 1));

  ngOnDestroy(): void {
    this.cleanupPreview();
  }

  close(): void {
    this.visibleChange.emit(false);
  }

  onShow(): void {
    this.currentPage.set(1);
    this.loadData();
  }

  onHide(): void {
    this.cleanupPreview();
  }

  private cleanupPreview(): void {
    const url = this.previewUrl();
    if (url) {
      this.documentService.revokePreviewBlobUrl(url);
      this.previewUrl.set(null);
    }
  }

  private loadPreview(): void {
    this.cleanupPreview();
    this.documentService
      .getPreviewBlobUrl(this.dossierId, this.versionId)
      .then((url) => this.previewUrl.set(url))
      .catch(() => {
        this.messageService.add({
          severity: 'warn',
          summary: 'Xem trước',
          detail: 'Không thể tải bản xem trước tài liệu',
        });
      });
  }

  previewSrc(): SafeResourceUrl | string {
    const base = this.previewUrl();
    if (!base) return '';
    const url = this.isPdf() ? `${base}#page=${this.currentPage()}` : base;
    return this.sanitizer.bypassSecurityTrustResourceUrl(url);
  }

  prevPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.update((p) => p - 1);
    }
  }

  nextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update((p) => p + 1);
    }
  }

  private loadData(): void {
    if (!this.dossierId || !this.versionId) return;

    this.loading.set(true);
    forkJoin({
      result: this.documentService.getDigitizationResultOrNull(this.dossierId, this.versionId),
      dossier: this.dossierService.getDossierById(this.dossierId),
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ result, dossier }) => {
          const meta = normalizeDossierDetail(dossier);
          if (!meta) {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được hồ sơ' });
            return;
          }

          this.rowVersion.set(meta.rowVersion);
          this.currentFormData.set(parseFormDataJson(meta.formDataJson));
          this.loadPreview();
          this.resolveFormAndLoad(result?.mergedDataJson ?? undefined, result?.resultJson ?? undefined);
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: this.documentService.digitizationResultErrorMessage(
              err,
              'Không tải được dữ liệu tài liệu'
            ),
          });
        },
      });
  }

  private resolveFormAndLoad(mergedDataJson?: string, resultJson?: string): void {
    if (this.documentTypeId) {
      this.documentService.lookupDocumentTypes().subscribe({
        next: (types) => {
          const match = types.find((t) => t.id === this.documentTypeId);
          const resolved = match?.formId ?? this.formId;
          if (!resolved) {
            this.messageService.add({
              severity: 'warn',
              summary: 'Thiếu form',
              detail: 'Loại văn bản chưa gắn biểu mẫu EAV',
            });
            return;
          }
          this.resolvedFormId.set(resolved);
          this.loadFormFields(resolved, mergedDataJson, resultJson);
        },
        error: () => this.loadFormFields(this.formId, mergedDataJson, resultJson),
      });
      return;
    }

    this.resolvedFormId.set(this.formId);
    this.loadFormFields(this.formId, mergedDataJson, resultJson);
  }

  private loadFormFields(
    formId: string | null | undefined,
    mergedDataJson?: string,
    resultJson?: string
  ): void {
    if (!formId) {
      this.fields.set([]);
      this.draftData.set({});
      return;
    }

    const mergedRaw = parseMergedDataJson(mergedDataJson);
    const merged = Object.keys(mergedRaw).length > 0
      ? mergedRaw
      : mergeExtractionPageResults(resultJson);

    this.dossierService.getFormTemplate(formId).subscribe({
      next: (template) => {
        const schemaJson = readFormSchemaJson(template);
        const parsedFields = parseFormSchemaFields(schemaJson);
        this.fields.set(parsedFields);
        this.draftData.set(this.buildDraftFromSchema(parsedFields, merged));
      },
      error: () => {
        this.fields.set([]);
        this.draftData.set({});
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không tải được biểu mẫu EAV',
        });
      },
    });
  }

  /** Sinh toàn bộ trường form — điền từ bóc tách nếu có, không thì để trống. */
  private buildDraftFromSchema(
    fields: EavField[],
    merged: Record<string, unknown>
  ): Record<string, unknown> {
    const extracted = Object.keys(merged).length > 0 ? pickFormDataForSchema(fields, merged) : {};
    const draft: Record<string, unknown> = {};

    for (const field of fields) {
      const key = field.key?.trim();
      if (!key) continue;
      if (field.type === 'checkbox') {
        draft[key] = extracted[key] ?? false;
      } else {
        draft[key] = extracted[key] ?? '';
      }
    }

    return draft;
  }

  setDraftFieldValue(key: string, value: unknown): void {
    this.draftData.update((data) => ({ ...data, [key]: value }));
  }

  setDraftCheckbox(key: string, event: Event): void {
    const target = event.target as HTMLInputElement;
    this.draftData.update((data) => ({ ...data, [key]: target.checked }));
  }

  confirmApply(): void {
    if (!this.canEdit) {
      this.messageService.add({ severity: 'warn', summary: 'Không được phép', detail: 'Hồ sơ đang khóa sửa' });
      return;
    }

    const fields = this.fields();
    if (!fields.length) {
      this.messageService.add({ severity: 'warn', summary: 'Không có form', detail: 'Không có trường để lưu' });
      return;
    }

    const nextData = { ...this.currentFormData() };
    for (const field of fields) {
      nextData[field.key] = this.draftData()[field.key];
    }

    this.saving.set(true);
    this.dossierService
      .saveFormData(this.dossierId, {
        formDataJson: serializeFormDataForSchema(fields, nextData),
        rowVersion: this.rowVersion(),
        changeNote: this.documentName
          ? `Cập nhật từ tài liệu "${this.documentName}"`
          : 'Cập nhật từ tài liệu',
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: 'Đã lưu thông tin tài liệu vào hồ sơ',
          });
          this.applied.emit();
          this.visibleChange.emit(false);
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể lưu dữ liệu hồ sơ',
          });
        },
      });
  }
}
