import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { finalize } from 'rxjs';
import { EcoPaginatorComponent, WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import {
  DigitalSignatureEndpointConfig,
  DigitalSignatureEndpointConfigService,
} from '../../services/digital-signature-endpoint-config.service';
import {
  DigitalSignatureSignHistoryItem,
  DigitalSignatureSignHistoryService,
} from '../../services/digital-signature-sign-history.service';

interface EditForm {
  apiCode: string;
  displayName: string;
  url: string;
  isActive: boolean;
  rowVersion: number;
}

@Component({
  selector: 'app-digital-signature-endpoint-config',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, WfBreadcrumbComponent, EcoPaginatorComponent],
  providers: [MessageService],
  templateUrl: './digital-signature-endpoint-config.component.html',
  styleUrl: './digital-signature-endpoint-config.component.scss',
})
export class DigitalSignatureEndpointConfigComponent implements OnInit {
  private readonly service = inject(DigitalSignatureEndpointConfigService);
  private readonly historyService = inject(DigitalSignatureSignHistoryService);
  private readonly messageService = inject(MessageService);

  activeTab = signal<'config' | 'history'>('config');

  // ===== Tab 1: Thiết lập API =====
  endpoints = signal<DigitalSignatureEndpointConfig[]>([]);
  loading = signal(false);
  saving = signal(false);

  dialogVisible = signal(false);
  form = signal<EditForm>(this.emptyForm());

  // ===== Tab 2: Lịch sử ký số =====
  historyItems = signal<DigitalSignatureSignHistoryItem[]>([]);
  historyLoading = signal(false);
  historyTotalCount = signal(0);
  historyPage = signal(1);
  historyPageSize = signal(10);
  historyKeyword = signal('');
  historyStatus = signal<string>('');
  historyFromDate = signal<string>('');
  historyToDate = signal<string>('');

  historyDetailVisible = signal(false);
  historyDetail = signal<DigitalSignatureSignHistoryItem | null>(null);

  ngOnInit(): void {
    this.load();
  }

  switchTab(tab: 'config' | 'history'): void {
    this.activeTab.set(tab);
    if (tab === 'history' && this.historyItems().length === 0 && !this.historyLoading()) {
      this.loadHistory();
    }
  }

  // ===== Tab 1 methods =====

  load(): void {
    this.loading.set(true);
    this.service
      .getAll()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (items) => this.endpoints.set(items || []),
        error: (error) => this.showError(error, 'Không thể tải danh sách cấu hình API ký số.'),
      });
  }

  openEdit(item: DigitalSignatureEndpointConfig): void {
    this.form.set({
      apiCode: item.apiCode,
      displayName: item.displayName,
      url: item.url || '',
      isActive: item.isActive,
      rowVersion: item.rowVersion,
    });
    this.dialogVisible.set(true);
  }

  closeDialog(): void {
    if (!this.saving()) this.dialogVisible.set(false);
  }

  updateFormField<K extends keyof EditForm>(field: K, value: EditForm[K]): void {
    this.form.update((current) => ({ ...current, [field]: value }));
  }

  save(): void {
    const draft = this.form();

    this.saving.set(true);
    this.service
      .update(draft.apiCode, {
        url: draft.url.trim() || null,
        isActive: draft.isActive,
        rowVersion: draft.rowVersion,
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.dialogVisible.set(false);
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu cấu hình API ký số.' });
          this.load();
        },
        error: (error) =>
          this.showError(error, 'Không thể lưu cấu hình — dữ liệu có thể đã bị người khác cập nhật, vui lòng tải lại.'),
      });
  }

  private emptyForm(): EditForm {
    return { apiCode: '', displayName: '', url: '', isActive: true, rowVersion: 1 };
  }

  // ===== Tab 2 methods =====

  loadHistory(): void {
    this.historyLoading.set(true);
    this.historyService
      .getAll({
        page: this.historyPage(),
        pageSize: this.historyPageSize(),
        keyword: this.historyKeyword() || null,
        status: this.historyStatus() || null,
        fromDate: this.historyFromDate() || null,
        toDate: this.historyToDate() || null,
      })
      .pipe(finalize(() => this.historyLoading.set(false)))
      .subscribe({
        next: (res) => {
          this.historyItems.set(res?.items || []);
          this.historyTotalCount.set(res?.totalCount || 0);
        },
        error: (error) => this.showError(error, 'Không thể tải lịch sử ký số.'),
      });
  }

  onHistoryFilterChange(): void {
    this.historyPage.set(1);
    this.loadHistory();
  }

  onHistoryResetFilter(): void {
    this.historyKeyword.set('');
    this.historyStatus.set('');
    this.historyFromDate.set('');
    this.historyToDate.set('');
    this.historyPage.set(1);
    this.loadHistory();
  }

  onHistoryPageChange(event: { first: number; rows: number }): void {
    this.historyPageSize.set(event.rows);
    this.historyPage.set(Math.floor(event.first / event.rows) + 1);
    this.loadHistory();
  }

  viewHistoryDetail(item: DigitalSignatureSignHistoryItem): void {
    this.historyDetail.set(item);
    this.historyDetailVisible.set(true);
  }

  closeHistoryDetail(): void {
    this.historyDetailVisible.set(false);
  }

  private showError(error: any, fallback: string): void {
    this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: error?.error?.message || fallback });
  }
}
