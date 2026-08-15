import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { finalize } from 'rxjs';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import {
  PmisManualSearchCriteria,
  PmisManualSyncService,
  PmisSyncObjectType,
  PmisSyncPreviewItem,
} from '../../data-access/pmis-manual-sync.service';
import { PmisHistoryService, SyncHistory, SyncHistoryDetail } from '../../data-access/pmis-history.service';

interface TabDef {
  type: PmisSyncObjectType;
  label: string;
}

const TABS: TabDef[] = [
  { type: 'SUBSTATION', label: 'Trạm biến áp' },
  { type: 'TRANSMISSION_LINE', label: 'Đường dây' },
  { type: 'EQUIPMENT', label: 'Thiết bị' },
];

function emptyCriteria(): PmisManualSearchCriteria {
  return { skip: 0, take: 100 };
}

@Component({
  selector: 'lib-pmis-manual-sync',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './pmis-manual-sync.component.html',
  styleUrl: './pmis-manual-sync.component.scss',
})
export class PmisManualSyncComponent {
  private readonly manualSyncService = inject(PmisManualSyncService);
  private readonly historyService = inject(PmisHistoryService);
  private readonly messageService = inject(MessageService);

  tabs = TABS;
  activeTab = signal<PmisSyncObjectType>('SUBSTATION');
  activeTabLabel = computed(() => TABS.find((t) => t.type === this.activeTab())?.label ?? '');

  searchDialogVisible = signal(false);
  criteria = signal<PmisManualSearchCriteria>(emptyCriteria());
  searching = signal(false);
  saving = signal(false);

  results = signal<PmisSyncPreviewItem[]>([]);
  resultsLoaded = signal(false);
  selectedCodes = signal<Set<string>>(new Set());

  historyDialogVisible = signal(false);
  historyLoading = signal(false);
  historyItems = signal<SyncHistory[]>([]);
  historyDetailDialogVisible = signal(false);
  historyDetailLoading = signal(false);
  historyDetails = signal<SyncHistoryDetail[]>([]);
  historyDetailTarget = signal<SyncHistory | null>(null);

  selectedCount = computed(() => this.selectedCodes().size);
  allSelected = computed(() => this.results().length > 0 && this.selectedCodes().size === this.results().length);

  selectTab(type: PmisSyncObjectType): void {
    this.activeTab.set(type);
    this.results.set([]);
    this.resultsLoaded.set(false);
    this.selectedCodes.set(new Set());
  }

  openSearchDialog(): void {
    this.criteria.set(emptyCriteria());
    this.searchDialogVisible.set(true);
  }

  closeSearchDialog(): void {
    if (!this.searching()) this.searchDialogVisible.set(false);
  }

  updateCriteriaField<K extends keyof PmisManualSearchCriteria>(field: K, value: PmisManualSearchCriteria[K]): void {
    this.criteria.update((current) => ({ ...current, [field]: value }));
  }

  search(): void {
    const objectType = this.activeTab();
    if (objectType === 'EQUIPMENT' && !this.criteria().maTBA && !this.criteria().maDuongDay) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Thiếu tiêu chí',
        detail: 'Vui lòng nhập Mã trạm biến áp hoặc Mã đường dây để tìm thiết bị.',
      });
      return;
    }

    this.searching.set(true);
    this.manualSyncService
      .search(objectType, this.criteria())
      .pipe(finalize(() => this.searching.set(false)))
      .subscribe({
        next: (response) => {
          this.results.set(response.items);
          this.resultsLoaded.set(true);
          this.selectedCodes.set(new Set(response.items.map((i) => i.pmisCode))); // mặc định tick chọn hết
          this.searchDialogVisible.set(false);
        },
        error: (error) => this.showError(error, 'Không thể tìm kiếm dữ liệu từ PMIS.'),
      });
  }

  isSelected(pmisCode: string): boolean {
    return this.selectedCodes().has(pmisCode);
  }

  toggleSelection(pmisCode: string, checked: boolean): void {
    this.selectedCodes.update((current) => {
      const next = new Set(current);
      if (checked) next.add(pmisCode);
      else next.delete(pmisCode);
      return next;
    });
  }

  toggleSelectAll(checked: boolean): void {
    this.selectedCodes.set(checked ? new Set(this.results().map((i) => i.pmisCode)) : new Set());
  }

  save(): void {
    const selected = this.results().filter((r) => this.selectedCodes().has(r.pmisCode));
    if (selected.length === 0) {
      this.messageService.add({ severity: 'warn', summary: 'Chưa chọn', detail: 'Vui lòng chọn ít nhất 1 bản ghi để đồng bộ.' });
      return;
    }

    this.saving.set(true);
    this.manualSyncService
      .save(this.activeTab(), selected.map((r) => r.rawData))
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (response) => {
          const detail = response.failedCount > 0
            ? `Thành công ${response.successCount}/${response.total}, lỗi ${response.failedCount}.`
            : `Đã đồng bộ thành công ${response.successCount} bản ghi.`;
          this.messageService.add({
            severity: response.failedCount > 0 ? 'warn' : 'success',
            summary: 'Hoàn tất đồng bộ',
            detail,
          });
          this.results.set([]);
          this.resultsLoaded.set(false);
          this.selectedCodes.set(new Set());
        },
        error: (error) => this.showError(error, 'Không thể lưu dữ liệu đồng bộ.'),
      });
  }

  openHistory(): void {
    this.historyDialogVisible.set(true);
    this.historyLoading.set(true);
    this.historyService
      .getHistory(this.activeTab(), 1, 20)
      .pipe(finalize(() => this.historyLoading.set(false)))
      .subscribe({
        next: (response) => this.historyItems.set(response.items),
        error: (error) => this.showError(error, 'Không thể tải lịch sử đồng bộ.'),
      });
  }

  openHistoryDetail(history: SyncHistory): void {
    this.historyDetailTarget.set(history);
    this.historyDetailDialogVisible.set(true);
    this.historyDetailLoading.set(true);
    this.historyService
      .getHistoryItems(history.id, 1, 100)
      .pipe(finalize(() => this.historyDetailLoading.set(false)))
      .subscribe({
        next: (response) => this.historyDetails.set(response.items),
        error: (error) => this.showError(error, 'Không thể tải chi tiết lịch sử.'),
      });
  }

  formatDate(value: string | null | undefined): string {
    if (!value) return '---';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString('vi-VN');
  }

  private showError(error: any, fallback: string): void {
    this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: error?.error?.message || fallback });
  }
}
