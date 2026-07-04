import { Component, Input, Output, EventEmitter, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageService } from 'primeng/api';
import { forkJoin, finalize } from 'rxjs';
import { DossierDocumentService } from '../../data-access/dossier-document.service';
import { DossierManagementService } from '../../data-access/dossier-management.service';
import {
  ApplyExtractionMode,
  EavField,
  applyExtractionToFormData,
  displayFieldValue,
  hasExtractedValue,
  mergeExtractionPageResults,
  mergeFormDataForSave,
  normalizeDossierDetail,
  parseFormDataJson,
  parseFormSchemaFields,
  parseMergedDataJson,
  pickFormDataForSchema,
  readFormSchemaJson,
} from '../../utils/dossier-form-schema.util';

@Component({
  selector: 'app-dossier-extraction-confirm-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ButtonModule, CheckboxModule],
  templateUrl: './dossier-extraction-confirm-dialog.component.html',
  styleUrl: './dossier-extraction-confirm-dialog.component.scss',
})
export class DossierExtractionConfirmDialogComponent {
  private documentService = inject(DossierDocumentService);
  private dossierService = inject(DossierManagementService);
  private messageService = inject(MessageService);

  @Input({ required: true }) dossierId!: string;
  @Input({ required: true }) versionId!: string;
  @Input() documentName = '';
  @Input() formId: string | null = null;
  @Input() canEdit = false;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() applied = new EventEmitter<void>();

  loading = signal(false);
  saving = signal(false);
  fields = signal<EavField[]>([]);
  draftData = signal<Record<string, unknown>>({});
  currentFormData = signal<Record<string, unknown>>({});
  selectedKeys = signal<Set<string>>(new Set());
  applyMode: ApplyExtractionMode = 'fillEmpty';
  rowVersion = signal(0);

  selectableFields = computed(() =>
    this.fields().filter((f) => hasExtractedValue(this.draftData()[f.key]))
  );

  displayValue = displayFieldValue;
  hasExtractedValue = hasExtractedValue;

  close(): void {
    this.visibleChange.emit(false);
  }

  onShow(): void {
    this.loadData();
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

          if (!result) {
            this.messageService.add({
              severity: 'warn',
              summary: 'Chưa có kết quả',
              detail: 'Tài liệu chưa có kết quả bóc tách.',
            });
            this.visibleChange.emit(false);
            return;
          }

          this.rowVersion.set(meta.rowVersion);
          this.currentFormData.set(parseFormDataJson(meta.formDataJson));

          const resolvedFormId = this.formId ?? meta.formId;
          if (!resolvedFormId) {
            this.messageService.add({
              severity: 'warn',
              summary: 'Thiếu form',
              detail: 'Hồ sơ chưa gắn biểu mẫu EAV',
            });
            return;
          }

          const mergedRaw = parseMergedDataJson(result.mergedDataJson ?? undefined);
          const merged =
            Object.keys(mergedRaw).length > 0
              ? mergedRaw
              : mergeExtractionPageResults(result.resultJson ?? undefined);

          this.loadFormTemplate(resolvedFormId, merged);
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: this.documentService.digitizationResultErrorMessage(err),
          });
        },
      });
  }

  private loadFormTemplate(formId: string, merged: Record<string, unknown>): void {
    this.dossierService.getDossierFormTemplate(this.dossierId, formId).subscribe({
      next: (template) => {
        const schemaJson = readFormSchemaJson(template);
        const parsedFields = parseFormSchemaFields(schemaJson);
        this.fields.set(parsedFields);

        const proposed = pickFormDataForSchema(parsedFields, merged);
        this.draftData.set({ ...proposed });

        const defaults = new Set<string>();
        for (const field of parsedFields) {
          if (hasExtractedValue(proposed[field.key])) {
            defaults.add(field.key);
          }
        }
        this.selectedKeys.set(defaults);
      },
      error: () => {
        this.fields.set([]);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không tải được biểu mẫu EAV',
        });
      },
    });
  }

  isSelected(key: string): boolean {
    return this.selectedKeys().has(key);
  }

  toggleField(key: string, checked: boolean): void {
    const next = new Set(this.selectedKeys());
    if (checked) next.add(key);
    else next.delete(key);
    this.selectedKeys.set(next);
  }

  selectAll(): void {
    this.selectedKeys.set(new Set(this.selectableFields().map((f) => f.key)));
  }

  clearSelection(): void {
    this.selectedKeys.set(new Set());
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
    const selected = this.selectedKeys();
    if (!selected.size) {
      this.messageService.add({ severity: 'warn', summary: 'Chưa chọn trường', detail: 'Chọn ít nhất một trường để áp dụng' });
      return;
    }

    const nextData = applyExtractionToFormData(
      fields,
      this.currentFormData(),
      this.draftData(),
      selected,
      this.applyMode
    );

    this.saving.set(true);
    this.dossierService
      .saveFormData(this.dossierId, {
        formDataJson: mergeFormDataForSave(fields, this.currentFormData(), nextData, fields),
        rowVersion: this.rowVersion(),
        changeNote: this.documentName
          ? `Áp dụng bóc tách OCR từ tài liệu "${this.documentName}"`
          : 'Áp dụng bóc tách OCR',
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (res) => {
          const updated = normalizeDossierDetail(res?.data ?? res);
          if (updated) {
            this.rowVersion.set(updated.rowVersion);
            this.currentFormData.set(parseFormDataJson(updated.formDataJson));
          }
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: 'Đã áp dụng dữ liệu bóc tách vào hồ sơ',
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
