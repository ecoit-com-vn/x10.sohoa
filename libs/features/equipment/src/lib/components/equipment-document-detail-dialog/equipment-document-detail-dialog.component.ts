import { Component, Input, Output, EventEmitter, inject, signal, computed, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { finalize, forkJoin } from 'rxjs';
import { EquipmentService } from '../../data-access/equipment.service';
import { FileDownloadService } from '../../data-access/file-download.service';

import {
  EavField,
  buildDocumentDraftFromSources,
  mergeExtractionPageResults,
  parseFormSchemaFields,
  parseMergedDataJson,
  readFormSchemaJson,
  serializeFormDataForSchema,
} from '@sohoa.frontend/features/dossier-management';

@Component({
  selector: 'app-equipment-document-detail-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ButtonModule],
  templateUrl: './equipment-document-detail-dialog.component.html',
  styleUrl: './equipment-document-detail-dialog.component.scss',
})
export class EquipmentDocumentDetailDialogComponent implements OnDestroy {
  private equipmentService = inject(EquipmentService);
  private fileDownloadService = inject(FileDownloadService);
  private messageService = inject(MessageService);
  private sanitizer = inject(DomSanitizer);

  @Input({ required: true }) equipmentId!: string;
  @Input({ required: true }) versionId!: string;
  @Input() documentName = '';
  @Input() mimeType: string | null = null;
  @Input() canEdit = false;
  @Input() totalPagesHint = 0;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() applied = new EventEmitter<void>();

  loading = signal(false);
  saving = signal(false);
  savingMode = signal<'save' | 'apply' | null>(null);
  hasExtractionData = signal(false);
  fields = signal<EavField[]>([]);
  draftData = signal<Record<string, unknown>>({});
  currentEquipmentData = signal<Record<string, unknown>>({});

  previewUrl = signal<string | null>(null);
  currentPage = signal(1);

  isPdf = computed(() => (this.mimeType ?? '').includes('pdf'));
  isImage = computed(() => (this.mimeType ?? '').startsWith('image/'));
  totalPages = computed(() => Math.max(this.totalPagesHint, 1));
  dialogHeader = computed(() => {
    const name = this.documentName || 'Tài liệu đính kèm';
    if (!this.hasExtractionData()) {
      return `Xem tài liệu — ${name}`;
    }
    return `${this.canEdit ? 'Hiệu chỉnh thông số bóc tách — ' : 'Chi tiết thông số bóc tách — '}${name}`;
  });
  formPaneTitle = computed(() =>
    this.hasExtractionData() ? 'Thông số kỹ thuật bóc tách' : 'Biểu mẫu thông số thiết bị'
  );
  formEditable = computed(() => this.canEdit && this.hasExtractionData());

  ngOnDestroy(): void {
    this.cleanupPreview();
  }

  close(): void {
    this.visibleChange.emit(false);
  }

  onShow(): void {
    this.currentPage.set(1);
    this.hasExtractionData.set(false);
    this.loadData();
  }

  onHide(): void {
    this.cleanupPreview();
  }

  private cleanupPreview(): void {
    const url = this.previewUrl();
    if (url) {
      this.fileDownloadService.revokePreviewBlobUrl(url);
      this.previewUrl.set(null);
    }
  }

  private loadPreview(): void {
    this.cleanupPreview();
    this.fileDownloadService
      .getPreviewBlobUrl(this.versionId, this.equipmentId)
      .then((url) => {
        this.previewUrl.set(url);
      })
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
    if (!this.equipmentId || !this.versionId) return;

    this.loading.set(true);

    forkJoin({
      equipment: this.equipmentService.getById(this.equipmentId),
      result: this.equipmentService.getDigitizationResultForEquipmentOrNull(
        this.equipmentId,
        this.versionId
      ),
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ equipment, result }) => {
          if (!equipment) {
            this.messageService.add({
              severity: 'error',
              summary: 'Lỗi',
              detail: 'Không tải được thiết bị kỹ thuật',
            });
            return;
          }

          let formValuesObj: Record<string, unknown> = {};
          if (equipment.formValues) {
            try {
              formValuesObj = JSON.parse(equipment.formValues) as Record<string, unknown>;
            } catch {
              formValuesObj = {};
            }
          }
          this.currentEquipmentData.set(formValuesObj);

          const hasResult =
            !!result &&
            (!!result.mergedDataJson?.trim() ||
              !!result.resultJson?.trim() ||
              result.status === 'Completed');
          this.hasExtractionData.set(hasResult);

          this.loadPreview();
          this.loadFormFields(result?.mergedDataJson, result?.resultJson, hasResult);
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không tải được dữ liệu tài liệu/thiết bị',
          });
        },
      });
  }

  private loadFormFields(
    mergedDataJson?: string,
    resultJson?: string,
    hasExtraction = false
  ): void {
    const mergedRaw = hasExtraction ? parseMergedDataJson(mergedDataJson) : {};
    const merged =
      Object.keys(mergedRaw).length > 0 ? mergedRaw : hasExtraction ? mergeExtractionPageResults(resultJson) : {};

    this.equipmentService.getFormTemplate(this.equipmentId).subscribe({
      next: (template) => {
        if (!template) {
          this.applyEmptyForm('Không tải được biểu mẫu EAV thiết bị');
          return;
        }

        const schemaJson = readFormSchemaJson(template);
        if (!schemaJson) {
          this.applyEmptyForm('Biểu mẫu EAV thiết bị không có schema hợp lệ');
          return;
        }

        const parsedFields = parseFormSchemaFields(schemaJson);
        this.fields.set(parsedFields);

        if (hasExtraction) {
          this.draftData.set(
            buildDocumentDraftFromSources(parsedFields, merged, this.currentEquipmentData())
          );
        } else {
          this.draftData.set(buildDocumentDraftFromSources(parsedFields, {}, this.currentEquipmentData()));
        }
      },
      error: (err) => {
        this.applyEmptyForm(err?.error?.message || 'Không tải được biểu mẫu EAV thiết bị');
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

  /** Chỉ lưu kết quả bóc tách — không đụng FormValues thiết bị. */
  saveExtractionOnly(): void {
    this.persistExtraction(false);
  }

  /** Lưu kết quả bóc tách và thay thế toàn bộ thông số thiết bị. */
  applyEquipmentFormValues(): void {
    this.persistExtraction(true);
  }

  private persistExtraction(updateEquipmentFormValues: boolean): void {
    const fields = this.fields();
    if (!fields.length) {
      this.messageService.add({ severity: 'warn', summary: 'Không có form', detail: 'Không có trường để lưu' });
      return;
    }

    const mergedDataJson = serializeFormDataForSchema(fields, this.draftData());
    this.savingMode.set(updateEquipmentFormValues ? 'apply' : 'save');
    this.saving.set(true);

    this.equipmentService
      .saveEquipmentExtractionData(
        this.equipmentId,
        this.versionId,
        mergedDataJson,
        updateEquipmentFormValues
      )
      .pipe(finalize(() => {
        this.saving.set(false);
        this.savingMode.set(null);
      }))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: updateEquipmentFormValues
              ? 'Đã lưu kết quả bóc tách và cập nhật toàn bộ thông số thiết bị.'
              : 'Đã lưu kết quả bóc tách.',
          });
          this.applied.emit();
          this.close();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể lưu dữ liệu bóc tách',
          });
        },
      });
  }
}
