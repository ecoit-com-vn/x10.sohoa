import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { finalize } from 'rxjs';
import { environment } from '@env/environment';
import { EcoPaginatorComponent, WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';

type ApiKeyDialogMode = 'create' | 'view' | 'edit';

@Component({
  selector: 'app-external-api-key',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, WfBreadcrumbComponent, EcoPaginatorComponent],
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

  historyDialogVisible = signal(false);
  historyTarget = signal<any | null>(null);
  callLogs = signal<any[]>([]);
  callLogsLoading = signal(false);
  callLogsPage = signal(1);
  callLogsPageSize = signal(10);
  callLogsTotal = signal(0);

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

  openHistory(item: any): void {
    this.historyTarget.set(item);
    this.callLogs.set([]);
    this.callLogsTotal.set(0);
    this.callLogsPage.set(1);
    this.historyDialogVisible.set(true);
    this.loadCallLogs();
  }

  closeHistoryDialog(): void {
    this.historyDialogVisible.set(false);
    this.historyTarget.set(null);
  }

  loadCallLogs(): void {
    const item = this.historyTarget();
    if (!item?.id) return;

    this.callLogsLoading.set(true);
    this.http.get<any>(`${this.apiUrl}/${item.id}/call-logs`, {
      params: { page: String(this.callLogsPage()), pageSize: String(this.callLogsPageSize()) },
    }).pipe(
      finalize(() => this.callLogsLoading.set(false))
    ).subscribe({
      next: response => {
        const items = response?.items ?? response?.Items ?? (Array.isArray(response) ? response : []);
        const total = response?.totalCount ?? response?.TotalCount ?? response?.total ?? items.length;
        this.callLogs.set(items);
        this.callLogsTotal.set(total);
      },
      error: error => this.showError(error, 'Không thể tải lịch sử gọi API.'),
    });
  }

  goToCallLogsPage(page: string | number): void {
    const requestedPage = Number(page);
    if (!Number.isInteger(requestedPage) || requestedPage < 1) return;
    this.callLogsPage.set(requestedPage);
    this.loadCallLogs();
  }

  onCallLogsPageSizeChange(pageSize: number): void {
    this.callLogsPageSize.set(pageSize);
    this.callLogsPage.set(1);
    this.loadCallLogs();
  }

  getLogHttpMethod(log: any): string {
    return log?.httpMethod || log?.method || log?.HttpMethod || '---';
  }

  getLogEndpoint(log: any): string {
    return log?.requestPath || log?.endpoint || log?.path || log?.RequestPath || '---';
  }

  getLogStatusCode(log: any): number | string {
    return log?.statusCode ?? log?.responseStatusCode ?? log?.StatusCode ?? '---';
  }

  getLogCalledAt(log: any): string {
    return this.formatDate(log?.calledAt || log?.createdAt || log?.CalledAt || log?.CreatedAt);
  }

  getLogIpAddress(log: any): string {
    return log?.ipAddress || log?.clientIp || log?.IpAddress || '---';
  }

  getLogDuration(log: any): string {
    const duration = log?.durationMs ?? log?.duration ?? log?.DurationMs;
    return duration === undefined || duration === null ? '---' : `${duration} ms`;
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
