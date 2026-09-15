import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { finalize } from 'rxjs';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import {
  OrganizationUnitOption,
  PmisUnitCodeMapping,
  PmisUnitMappingService,
} from '../../data-access/pmis-unit-mapping.service';
import { formatUtcDate } from '../../data-access/date-format.util';

interface CreateForm {
  pmisUnitCode: string;
  unitId: number | null;
  note: string;
}

@Component({
  selector: 'lib-pmis-unit-mapping',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './pmis-unit-mapping.component.html',
  styleUrl: './pmis-unit-mapping.component.scss',
})
export class PmisUnitMappingComponent implements OnInit {
  private readonly service = inject(PmisUnitMappingService);
  private readonly messageService = inject(MessageService);

  mappings = signal<PmisUnitCodeMapping[]>([]);
  units = signal<OrganizationUnitOption[]>([]);
  loading = signal(false);
  saving = signal(false);

  dialogVisible = signal(false);
  form = signal<CreateForm>(this.emptyForm());

  deleteDialogVisible = signal(false);
  deleteTarget = signal<PmisUnitCodeMapping | null>(null);
  deleting = signal(false);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.service
      .getAll()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (items) => this.mappings.set(items || []),
        error: (error) => this.showError(error, 'Không thể tải danh sách ánh xạ mã đơn vị PMIS.'),
      });
  }

  openCreate(): void {
    this.form.set(this.emptyForm());
    // Tải danh sách đơn vị mỗi lần mở — đảm bảo thấy được đơn vị vừa thêm ở màn "Đơn vị" gần đây.
    this.service.getOrganizationUnits().subscribe({
      next: (units) => this.units.set((units || []).sort((a, b) => a.code.localeCompare(b.code))),
      error: (error) => this.showError(error, 'Không thể tải danh sách đơn vị.'),
    });
    this.dialogVisible.set(true);
  }

  closeDialog(): void {
    if (!this.saving()) this.dialogVisible.set(false);
  }

  updateFormField<K extends keyof CreateForm>(field: K, value: CreateForm[K]): void {
    this.form.update((current) => ({ ...current, [field]: value }));
  }

  save(): void {
    const draft = this.form();
    if (!draft.pmisUnitCode.trim()) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Mã đơn vị PMIS không được để trống.' });
      return;
    }
    if (!draft.unitId) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Chưa chọn đơn vị để ánh xạ.' });
      return;
    }

    this.saving.set(true);
    this.service
      .create({ pmisUnitCode: draft.pmisUnitCode.trim(), unitId: draft.unitId, note: draft.note.trim() || null })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.dialogVisible.set(false);
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: 'Đã thêm ánh xạ. Hệ thống sẽ tự đồng bộ lại trong ít phút để cập nhật Trạm/Đường dây/Thiết bị thuộc đơn vị này.',
          });
          this.load();
        },
        error: (error) => this.showError(error, 'Không thể thêm ánh xạ mã đơn vị PMIS.'),
      });
  }

  openDelete(item: PmisUnitCodeMapping): void {
    this.deleteTarget.set(item);
    this.deleteDialogVisible.set(true);
  }

  closeDeleteDialog(): void {
    if (!this.deleting()) this.deleteDialogVisible.set(false);
  }

  confirmDelete(): void {
    const target = this.deleteTarget();
    if (!target) return;

    this.deleting.set(true);
    this.service
      .delete(target.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.deleteDialogVisible.set(false);
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xoá ánh xạ.' });
          this.load();
        },
        error: (error) => this.showError(error, 'Không thể xoá ánh xạ.'),
      });
  }

  formatDate(value: string | null | undefined): string {
    return formatUtcDate(value);
  }

  private emptyForm(): CreateForm {
    return { pmisUnitCode: '', unitId: null, note: '' };
  }

  private showError(error: any, fallback: string): void {
    this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: error?.error?.message || fallback });
  }
}
