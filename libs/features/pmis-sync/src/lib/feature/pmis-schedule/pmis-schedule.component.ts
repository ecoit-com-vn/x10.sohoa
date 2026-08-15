import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { finalize } from 'rxjs';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { PmisFrequencyUnit, PmisScheduleService, SyncConfig } from '../../data-access/pmis-schedule.service';
import { PmisHistoryService, SyncHistory, SyncHistoryDetail } from '../../data-access/pmis-history.service';

interface EditForm {
  objectType: string;
  isEnabled: boolean;
  frequencyValue: number;
  frequencyUnit: PmisFrequencyUnit;
  rowVersion: number;
}

const OBJECT_TYPE_LABELS: Record<string, string> = {
  SUBSTATION: 'Trạm biến áp',
  TRANSMISSION_LINE: 'Đường dây',
  EQUIPMENT: 'Thiết bị',
};

const FREQUENCY_UNIT_LABELS: Record<PmisFrequencyUnit, string> = {
  MINUTE: 'Phút',
  HOUR: 'Giờ',
  DAY: 'Ngày',
};

@Component({
  selector: 'lib-pmis-schedule',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './pmis-schedule.component.html',
  styleUrl: './pmis-schedule.component.scss',
})
export class PmisScheduleComponent implements OnInit {
  private readonly scheduleService = inject(PmisScheduleService);
  private readonly historyService = inject(PmisHistoryService);
  private readonly messageService = inject(MessageService);

  objectTypeLabels = OBJECT_TYPE_LABELS;
  frequencyUnitOptions: PmisFrequencyUnit[] = ['MINUTE', 'HOUR', 'DAY'];
  frequencyUnitLabels = FREQUENCY_UNIT_LABELS;

  configs = signal<SyncConfig[]>([]);
  loading = signal(false);
  savingType = signal<string | null>(null);
  editForms = signal<Record<string, EditForm>>({});

  historyDialogVisible = signal(false);
  historyLoading = signal(false);
  historyItems = signal<SyncHistory[]>([]);
  historyTarget = signal<string | null>(null);

  historyDetailDialogVisible = signal(false);
  historyDetailLoading = signal(false);
  historyDetails = signal<SyncHistoryDetail[]>([]);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.scheduleService
      .getAll()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (items) => {
          this.configs.set(items);
          const forms: Record<string, EditForm> = {};
          for (const item of items) {
            forms[item.objectType] = {
              objectType: item.objectType,
              isEnabled: item.isEnabled,
              frequencyValue: item.frequencyValue,
              frequencyUnit: item.frequencyUnit,
              rowVersion: item.rowVersion,
            };
          }
          this.editForms.set(forms);
        },
        error: (error) => this.showError(error, 'Không thể tải cấu hình lịch đồng bộ.'),
      });
  }

  form(objectType: string): EditForm {
    return this.editForms()[objectType];
  }

  updateField<K extends keyof EditForm>(objectType: string, field: K, value: EditForm[K]): void {
    this.editForms.update((current) => ({
      ...current,
      [objectType]: { ...current[objectType], [field]: value },
    }));
  }

  save(objectType: string): void {
    const draft = this.form(objectType);
    if (draft.frequencyValue <= 0) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Tần suất đồng bộ phải lớn hơn 0.' });
      return;
    }

    this.savingType.set(objectType);
    this.scheduleService
      .update(objectType, {
        isEnabled: draft.isEnabled,
        frequencyValue: draft.frequencyValue,
        frequencyUnit: draft.frequencyUnit,
        rowVersion: draft.rowVersion,
      })
      .pipe(finalize(() => this.savingType.set(null)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu cấu hình lịch đồng bộ.' });
          this.load();
        },
        error: (error) => this.showError(error, 'Không thể lưu — dữ liệu có thể đã bị người khác cập nhật, vui lòng tải lại.'),
      });
  }

  openHistory(objectType: string): void {
    this.historyTarget.set(objectType);
    this.historyDialogVisible.set(true);
    this.historyLoading.set(true);
    this.historyService
      .getHistory(objectType as any, 1, 20)
      .pipe(finalize(() => this.historyLoading.set(false)))
      .subscribe({
        next: (response) => this.historyItems.set(response.items),
        error: (error) => this.showError(error, 'Không thể tải lịch sử đồng bộ.'),
      });
  }

  openHistoryDetail(history: SyncHistory): void {
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
