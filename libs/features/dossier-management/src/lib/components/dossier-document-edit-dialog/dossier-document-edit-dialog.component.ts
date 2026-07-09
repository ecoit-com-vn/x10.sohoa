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

  buildDocumentDraftFromSources,

  mergeExtractionPageResults,
  normalizeDossierDetail,
  parseFormDataJson,
  parseFormSchemaFields,
  parseMergedDataJson,
  readApiField,
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

  @Input() lookupMode = false;

  @Input() publishMode = false;

  @Input() hasExtractionResult = false;

  @Input() totalPagesHint = 0;

  @Input() visible = false;

  @Output() visibleChange = new EventEmitter<boolean>();

  @Output() applied = new EventEmitter<void>();



  loading = signal(false);

  saving = signal(false);

  fields = signal<EavField[]>([]);

  draftData = signal<Record<string, unknown>>({});

  currentFormData = signal<Record<string, unknown>>({});



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

      .getPreviewBlobUrl(this.dossierId, this.versionId, this.lookupMode)

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



    const requests: {
      dossier: ReturnType<DossierManagementService['getDossierById']>;
      result: ReturnType<DossierDocumentService['getDigitizationResultOrNull']>;
    } = {
      dossier: this.lookupMode
        ? this.dossierService.getDossierByEquipmentLookup(this.dossierId)
        : this.dossierService.getDossierById(this.dossierId),
      result: this.documentService.getDigitizationResultOrNull(
        this.dossierId,
        this.versionId,
        this.lookupMode
      )
    };



    forkJoin(requests)

      .pipe(finalize(() => this.loading.set(false)))

      .subscribe({

        next: ({ result, dossier }) => {

          const meta = normalizeDossierDetail(dossier);

          if (!meta) {

            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được hồ sơ' });

            return;

          }



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

    this.loadFormFields(mergedDataJson, resultJson);

  }



  private loadFormFields(

    mergedDataJson?: string,

    resultJson?: string

  ): void {

    const mergedRaw = parseMergedDataJson(mergedDataJson);
    const merged =
      Object.keys(mergedRaw).length > 0 ? mergedRaw : mergeExtractionPageResults(resultJson);



    this.documentService

      .getDocumentFormTemplate(this.dossierId, this.versionId, this.lookupMode)

      .subscribe({

        next: (template) => {

          if (!template) {

            this.applyEmptyForm('Không tải được biểu mẫu EAV');

            return;

          }



          const schemaJson = readFormSchemaJson(template);

          if (!schemaJson) {

            this.applyEmptyForm('Biểu mẫu EAV không có schema hợp lệ');

            return;

          }



          const parsedFields = parseFormSchemaFields(schemaJson);

          this.fields.set(parsedFields);

          this.draftData.set(buildDocumentDraftFromSources(parsedFields, merged, this.currentFormData()));

          const templateId = readApiField<string>(template as Record<string, unknown>, 'id', 'Id');

          this.resolvedFormId.set(templateId ?? this.formId);

        },

        error: (err) => {

          const detail =

            err?.error?.message ||

            (this.documentTypeId

              ? 'Loại văn bản chưa gắn biểu mẫu EAV hoặc biểu mẫu không tồn tại'

              : 'Không tải được biểu mẫu EAV');

          this.applyEmptyForm(detail);

        },

      });

  }



  private applyEmptyForm(detail: string): void {

    this.fields.set([]);

    this.draftData.set({});

    this.messageService.add({

      severity: 'error',

      summary: 'Lỗi',

      detail,

    });

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



    const mergedDataJson = serializeFormDataForSchema(fields, this.draftData());



    this.saving.set(true);

    this.documentService

      .saveDocumentExtractionData(this.dossierId, this.versionId, mergedDataJson)

      .pipe(finalize(() => this.saving.set(false)))

      .subscribe({

        next: () => {

          this.messageService.add({

            severity: 'success',

            summary: 'Thành công',

            detail: 'Đã lưu thông tin tài liệu',

          });

          this.applied.emit();

          this.visibleChange.emit(false);

        },

        error: (err) => {

          this.messageService.add({

            severity: 'error',

            summary: 'Lỗi',

            detail: err?.error?.message || 'Không thể lưu dữ liệu tài liệu',

          });

        },

      });

  }

}


