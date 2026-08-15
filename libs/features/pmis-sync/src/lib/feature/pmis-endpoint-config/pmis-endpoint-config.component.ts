import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { finalize, forkJoin } from 'rxjs';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import {
  PmisApiEndpointConfig,
  PmisApiEndpointHeader,
  PmisEndpointConfigService,
} from '../../data-access/pmis-endpoint-config.service';

interface EditableHeader extends PmisApiEndpointHeader {
  /** true nếu header bí mật này đã có giá trị lưu sẵn — để trống ô nhập nghĩa là giữ nguyên. */
  hasStoredValue: boolean;
}

interface EditForm {
  apiCode: string;
  displayName: string;
  url: string;
  timeoutSeconds: number | null;
  isActive: boolean;
  rowVersion: number;
}

@Component({
  selector: 'lib-pmis-endpoint-config',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './pmis-endpoint-config.component.html',
  styleUrl: './pmis-endpoint-config.component.scss',
})
export class PmisEndpointConfigComponent implements OnInit {
  private readonly service = inject(PmisEndpointConfigService);
  private readonly messageService = inject(MessageService);

  endpoints = signal<PmisApiEndpointConfig[]>([]);
  loading = signal(false);
  saving = signal(false);

  dialogVisible = signal(false);
  form = signal<EditForm>(this.emptyForm());
  headers = signal<EditableHeader[]>([]);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.service
      .getAll()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (items) => this.endpoints.set(items || []),
        error: (error) => this.showError(error, 'Không thể tải danh sách cấu hình API PMIS.'),
      });
  }

  openEdit(item: PmisApiEndpointConfig): void {
    this.loading.set(true);
    this.service
      .getHeaders(item.apiCode)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (headers) => {
          this.form.set({
            apiCode: item.apiCode,
            displayName: item.displayName,
            url: item.url || '',
            timeoutSeconds: item.timeoutSeconds,
            isActive: item.isActive,
            rowVersion: item.rowVersion,
          });
          this.headers.set(
            headers.map((h) => ({
              ...h,
              hasStoredValue: h.isSecret && !!h.headerValue,
              headerValue: h.isSecret ? '' : h.headerValue,
            }))
          );
          this.dialogVisible.set(true);
        },
        error: (error) => this.showError(error, 'Không thể tải header của API này.'),
      });
  }

  closeDialog(): void {
    if (!this.saving()) this.dialogVisible.set(false);
  }

  updateFormField<K extends keyof EditForm>(field: K, value: EditForm[K]): void {
    this.form.update((current) => ({ ...current, [field]: value }));
  }

  addHeaderRow(): void {
    this.headers.update((rows) => [
      ...rows,
      { id: '', headerKey: '', headerValue: '', isSecret: false, hasStoredValue: false },
    ]);
  }

  removeHeaderRow(index: number): void {
    this.headers.update((rows) => rows.filter((_, i) => i !== index));
  }

  updateHeaderField(index: number, field: keyof EditableHeader, value: any): void {
    this.headers.update((rows) =>
      rows.map((row, i) => (i === index ? { ...row, [field]: value } : row))
    );
  }

  save(): void {
    const draft = this.form();
    const rows = this.headers();

    if (rows.some((h) => !h.headerKey.trim())) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Tên header không được để trống.' });
      return;
    }

    this.saving.set(true);
    forkJoin([
      this.service.update(draft.apiCode, {
        url: draft.url.trim() || null,
        timeoutSeconds: draft.timeoutSeconds,
        isActive: draft.isActive,
        rowVersion: draft.rowVersion,
      }),
      this.service.replaceHeaders(
        draft.apiCode,
        rows.map((h) => ({
          id: h.id,
          headerKey: h.headerKey.trim(),
          headerValue: h.headerValue,
          isSecret: h.isSecret,
        }))
      ),
    ])
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.dialogVisible.set(false);
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu cấu hình API PMIS.' });
          this.load();
        },
        error: (error) => this.showError(error, 'Không thể lưu cấu hình — dữ liệu có thể đã bị người khác cập nhật, vui lòng tải lại.'),
      });
  }

  private emptyForm(): EditForm {
    return { apiCode: '', displayName: '', url: '', timeoutSeconds: null, isActive: false, rowVersion: 1 };
  }

  private showError(error: any, fallback: string): void {
    this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: error?.error?.message || fallback });
  }
}
