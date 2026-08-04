import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { finalize } from 'rxjs';
import { environment } from '@env/environment';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';

type ApiKeyDialogMode = 'create' | 'view' | 'edit';

@Component({
  selector: 'app-external-api-key',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './external-api-key.component.html',
  styleUrl: './external-api-key.component.scss',
})
export class ExternalApiKeyComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly messageService = inject(MessageService);
  private readonly apiUrl = `${environment.apiGatewayUrl}/api/v1/external-api-keys`;

  apiKeys = signal<any[]>([]);
  loading = signal(false);
  saving = signal(false);
  deleting = signal(false);
  regenerating = signal(false);
  dialogVisible = signal(false);
  deleteDialogVisible = signal(false);
  regenerateDialogVisible = signal(false);
  dialogMode = signal<ApiKeyDialogMode>('create');
  form = signal<any>(this.emptyForm());
  deleteTarget = signal<any | null>(null);
  generatedPrivateKey = signal<string | null>(null);
  showPrivateKey = signal(false);

  ngOnInit(): void {
    this.loadApiKeys();
  }

  loadApiKeys(): void {
    this.loading.set(true);
    this.http.get<any[]>(this.apiUrl).pipe(
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: items => this.apiKeys.set(items || []),
      error: error => this.showError(error, 'Không thể tải danh sách cấu hình API.'),
    });
  }

  openCreate(): void {
    this.dialogMode.set('create');
    this.form.set(this.emptyForm());
    this.generatedPrivateKey.set(null);
    this.showPrivateKey.set(false);
    this.dialogVisible.set(true);
  }

  openView(item: any): void {
    this.loadItem(item.id, 'view');
  }

  openEdit(item: any): void {
    this.loadItem(item.id, 'edit');
  }

  save(): void {
    const draft = this.form();
    if (!draft.keyName?.trim()) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập tên API key.' });
      return;
    }

    const payload = {
      keyName: draft.keyName.trim(),
      isActive: !!draft.isActive,
      expiresAt: draft.expiresAt || null,
      note: draft.note?.trim() || null,
    };

    this.saving.set(true);
    const request$ = this.dialogMode() === 'edit'
      ? this.http.put(`${this.apiUrl}/${draft.id}`, payload)
      : this.http.post<any>(this.apiUrl, payload);

    request$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: response => {
        if (this.dialogMode() === 'create') {
          this.generatedPrivateKey.set(response?.privateKey || null);
          this.form.set(response?.item || draft);
          this.dialogMode.set('view');
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã tạo API key.' });
        } else {
          this.dialogVisible.set(false);
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã cập nhật API key.' });
        }
        this.loadApiKeys();
      },
      error: error => this.showError(error, 'Không thể lưu API key.'),
    });
  }

  requestDelete(item: any): void {
    this.deleteTarget.set(item);
    this.deleteDialogVisible.set(true);
  }

  requestRegenerate(): void {
    if (!this.form()?.id) return;
    this.regenerateDialogVisible.set(true);
  }

  confirmRegenerate(): void {
    const id = this.form()?.id;
    if (!id || this.regenerating()) return;

    this.regenerating.set(true);
    this.http.post<any>(`${this.apiUrl}/${id}/regenerate`, {}).pipe(
      finalize(() => this.regenerating.set(false))
    ).subscribe({
      next: response => {
        this.generatedPrivateKey.set(response?.privateKey || null);
        this.showPrivateKey.set(true);
        this.regenerateDialogVisible.set(false);
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã tạo chuỗi API mới.' });
        this.loadApiKeys();
      },
      error: error => this.showError(error, 'Không thể đổi chuỗi API.'),
    });
  }

  confirmDelete(): void {
    const item = this.deleteTarget();
    if (!item || this.deleting()) return;

    this.deleting.set(true);
    this.http.delete(`${this.apiUrl}/${item.id}`).pipe(
      finalize(() => this.deleting.set(false))
    ).subscribe({
      next: () => {
        this.deleteDialogVisible.set(false);
        this.deleteTarget.set(null);
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xóa API key.' });
        this.loadApiKeys();
      },
      error: error => this.showError(error, 'Không thể xóa API key.'),
    });
  }

  closeDialog(): void {
    if (!this.saving()) this.dialogVisible.set(false);
  }

  formatDate(value: string | null | undefined): string {
    if (!value) return '---';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString('vi-VN');
  }

  formatDateOnly(value: string | null | undefined): string {
    if (!value) return '---';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString('vi-VN');
  }

  togglePrivateKeyVisibility(): void {
    this.showPrivateKey.update(visible => !visible);
  }

  updateFormField(field: string, value: any): void {
    this.form.update(current => ({ ...current, [field]: value }));
  }

  private loadItem(id: number, mode: ApiKeyDialogMode): void {
    this.loading.set(true);
    this.http.get<any>(`${this.apiUrl}/${id}`).pipe(
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: response => {
        const item = response?.item || response;
        this.dialogMode.set(mode);
        this.generatedPrivateKey.set(response?.privateKey || null);
        this.showPrivateKey.set(false);
        this.form.set({ ...item, expiresAt: this.toDateInput(item?.expiresAt) });
        this.dialogVisible.set(true);
      },
      error: error => this.showError(error, 'Không thể tải thông tin API key.'),
    });
  }

  private emptyForm(): any {
    return { keyName: '', isActive: true, expiresAt: null, note: '' };
  }

  private toDateInput(value: string | null | undefined): string | null {
    return value ? String(value).slice(0, 10) : null;
  }

  private showError(error: any, fallback: string): void {
    this.messageService.add({
      severity: 'error',
      summary: 'Lỗi',
      detail: error?.error?.message || fallback,
    });
  }
}
