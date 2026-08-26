import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { Observable, finalize, forkJoin } from 'rxjs';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import {
  GridTypeOption,
  PmisDeviceTypeOption,
  PmisEquipmentTypeMapping,
  PmisEquipmentTypeMappingService,
  SystemEquipmentTypeOption,
} from '../../data-access/pmis-equipment-type-mapping.service';

interface EditForm {
  id: string | null;
  pmisMaLoaiTB: string;
  gridTypeId: number | null;
  equipmentTypeId: string;
  rowVersion: number | null;
}

@Component({
  selector: 'lib-pmis-equipment-type-mapping',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './pmis-equipment-type-mapping.component.html',
  styleUrl: './pmis-equipment-type-mapping.component.scss',
})
export class PmisEquipmentTypeMappingComponent implements OnInit {
  private readonly service = inject(PmisEquipmentTypeMappingService);
  private readonly messageService = inject(MessageService);

  mappings = signal<PmisEquipmentTypeMapping[]>([]);
  pmisDeviceTypes = signal<PmisDeviceTypeOption[]>([]);
  gridTypes = signal<GridTypeOption[]>([]);
  systemEquipmentTypes = signal<SystemEquipmentTypeOption[]>([]);

  loading = signal(false);
  saving = signal(false);
  deletingId = signal<string | null>(null);
  dialogVisible = signal(false);
  form = signal<EditForm>(this.emptyForm());

  /** Loại thiết bị hệ thống phân biệt cấp điện áp bằng GridTypeId — chỉ cho chọn đúng cấp đang chọn. */
  equipmentTypeOptions = computed(() => {
    const gridTypeId = this.form().gridTypeId;
    const all = this.systemEquipmentTypes();
    if (!gridTypeId) return all;
    return all.filter((t) => t.gridTypeId === gridTypeId);
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    forkJoin([
      this.service.getAll(),
      this.service.getGridTypes(),
      this.service.getSystemEquipmentTypes(),
    ])
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ([mappings, gridTypes, equipmentTypes]) => {
          this.mappings.set(mappings || []);
          this.gridTypes.set(gridTypes || []);
          this.systemEquipmentTypes.set(equipmentTypes || []);
        },
        error: (error) => this.showError(error, 'Không thể tải danh sách ánh xạ loại thiết bị PMIS.'),
      });

    // Danh mục PMIS gọi trực tiếp sang PMIS nên có thể chậm/lỗi riêng — tách khỏi luồng trên để
    // màn hình vẫn dùng được (admin tự nhập mã) khi PMIS chưa cấu hình xong.
    this.service.getPmisDeviceTypes().subscribe({
      next: (items) => this.pmisDeviceTypes.set(items || []),
      error: () =>
        this.messageService.add({
          severity: 'warn',
          summary: 'Không tải được danh mục PMIS',
          detail: 'Chưa lấy được danh sách loại thiết bị từ PMIS — vẫn có thể tự nhập mã loại thiết bị.',
        }),
    });
  }

  openCreate(): void {
    this.form.set(this.emptyForm());
    this.dialogVisible.set(true);
  }

  openEdit(item: PmisEquipmentTypeMapping): void {
    this.form.set({
      id: item.id,
      pmisMaLoaiTB: item.pmisMaLoaiTB,
      gridTypeId: item.gridTypeId,
      equipmentTypeId: item.equipmentTypeId,
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

  onGridTypeChange(gridTypeId: number | null): void {
    // Đổi cấp điện áp thì loại thiết bị đã chọn có thể không còn hợp lệ — xoá để buộc chọn lại.
    this.form.update((current) => ({ ...current, gridTypeId, equipmentTypeId: '' }));
  }

  pmisDeviceTypeName(maLoaiTB: string): string {
    return this.pmisDeviceTypes().find((t) => t.maLoaiTB === maLoaiTB)?.tenLoaiTB || '';
  }

  save(): void {
    const draft = this.form();

    if (!draft.pmisMaLoaiTB?.trim() || !draft.gridTypeId || !draft.equipmentTypeId) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Thiếu thông tin',
        detail: 'Vui lòng chọn đủ loại thiết bị PMIS, cấp điện áp và loại thiết bị hệ thống.',
      });
      return;
    }

    const request = {
      pmisMaLoaiTB: draft.pmisMaLoaiTB.trim(),
      gridTypeId: draft.gridTypeId,
      equipmentTypeId: draft.equipmentTypeId,
      rowVersion: draft.rowVersion ?? undefined,
    };

    this.saving.set(true);
    const call: Observable<unknown> = draft.id
      ? this.service.update(draft.id, request)
      : this.service.create(request);
    call.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.dialogVisible.set(false);
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu ánh xạ loại thiết bị.' });
        this.load();
      },
      error: (error) => this.showError(error, 'Không thể lưu ánh xạ loại thiết bị.'),
    });
  }

  remove(item: PmisEquipmentTypeMapping): void {
    if (!confirm(`Xoá ánh xạ loại thiết bị PMIS "${item.pmisMaLoaiTB}" (${item.gridTypeName})?`)) return;

    this.deletingId.set(item.id);
    this.service
      .delete(item.id)
      .pipe(finalize(() => this.deletingId.set(null)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xoá ánh xạ.' });
          this.load();
        },
        error: (error) => this.showError(error, 'Không thể xoá ánh xạ.'),
      });
  }

  private emptyForm(): EditForm {
    return { id: null, pmisMaLoaiTB: '', gridTypeId: null, equipmentTypeId: '', rowVersion: null };
  }

  private showError(error: any, fallback: string): void {
    this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: error?.error?.message || fallback });
  }
}
