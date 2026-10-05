import { Component, Input, Output, EventEmitter, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { MessageService } from 'primeng/api';
import { finalize, catchError, of } from 'rxjs';
import { EcoPaginatorComponent } from '@sohoa.frontend/shared/layout';
import { EquipmentService } from '../../data-access/equipment.service';

/**
 * Popup "Lịch sử di chuyển thiết bị" — dùng chung cho cả 2 điểm vào: cột Thao tác ở Quản lý thiết bị
 * (device-list) và cột Thao tác của bảng thiết bị nhúng trong màn Quản lý trạm biến áp/đường dây.
 * Chỉ cần gọi open(equipmentId, label) từ component cha.
 */
@Component({
  selector: 'app-equipment-transfer-history-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, SelectModule, DatePickerModule, EcoPaginatorComponent],
  templateUrl: './equipment-transfer-history-dialog.component.html',
  styleUrl: './equipment-transfer-history-dialog.component.scss',
})
export class EquipmentTransferHistoryDialogComponent {
  private equipmentService = inject(EquipmentService);
  private messageService = inject(MessageService);

  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();

  equipmentId: string | null = null;
  equipmentLabel = signal<string>('');

  loading = signal(false);
  items = signal<any[]>([]);
  totalCount = signal(0);
  page = signal(1);
  pageSize = signal(10);

  infrastructures = signal<any[]>([]);
  filterInfrastructureId = signal<string | null>(null);
  filterFromDate = signal<Date | null>(null);
  filterToDate = signal<Date | null>(null);

  readonly startIndex = computed(() => (this.page() - 1) * this.pageSize());

  open(equipmentId: string, label?: string): void {
    this.equipmentId = equipmentId;
    this.equipmentLabel.set(label || '');
    this.filterInfrastructureId.set(null);
    this.filterFromDate.set(null);
    this.filterToDate.set(null);
    this.page.set(1);
    this.visible = true;
    this.visibleChange.emit(true);

    if (this.infrastructures().length === 0) {
      this.equipmentService.getAllInfrastructures().pipe(catchError(() => of([]))).subscribe(list => {
        this.infrastructures.set(Array.isArray(list) ? list : []);
      });
    }

    this.loadHistory();
  }

  close(): void {
    this.visible = false;
    this.visibleChange.emit(false);
  }

  onSearch(): void {
    this.page.set(1);
    this.loadHistory();
  }

  onReset(): void {
    this.filterInfrastructureId.set(null);
    this.filterFromDate.set(null);
    this.filterToDate.set(null);
    this.page.set(1);
    this.loadHistory();
  }

  onPageChange(event: { first?: number; rows?: number }): void {
    const rows = Number(event.rows) || this.pageSize();
    const first = Number(event.first) || 0;
    this.pageSize.set(rows);
    this.page.set(Math.floor(first / rows) + 1);
    this.loadHistory();
  }

  private loadHistory(): void {
    if (!this.equipmentId) return;

    this.loading.set(true);
    this.equipmentService.getTransferHistory(
      this.equipmentId,
      this.page(),
      this.pageSize(),
      this.filterInfrastructureId(),
      this.filterFromDate(),
      this.filterToDate()
    ).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: (res) => {
        this.items.set(res?.items ?? []);
        this.totalCount.set(res?.totalCount ?? 0);
      },
      error: (err) => {
        this.items.set([]);
        this.totalCount.set(0);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err?.error?.message || 'Không thể tải lịch sử di chuyển thiết bị.'
        });
      }
    });
  }
}
