import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { environment } from '@env/environment';

@Component({
  selector: 'app-unit-of-measurement',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule],
  providers: [MessageService],
  templateUrl: './unit-of-measurement.component.html'
})
export class UnitOfMeasurement implements OnInit {
  uoms = signal<any[]>([]);
  searchKeyword = signal<string>('');

  displayDialog = signal<boolean>(false);
  dialogHeader = signal<string>('');
  isEdit = signal<boolean>(false);
  isPrivate = signal<boolean>(false);
  currentItem = signal<any>({});

  private apiUrl = `${environment.apiGatewayUrl}/api/catalog`;

  // Computed signal for filteredUoms
  filteredUoms = computed(() => {
    const kw = this.searchKeyword().toLowerCase().trim();
    const allUoms = this.uoms() || [];
    if (!kw) {
      return [...allUoms];
    }
    return allUoms.filter(item => 
      (item.code?.toLowerCase().includes(kw) ?? false) || 
      (item.name?.toLowerCase().includes(kw) ?? false) || 
      (item.description?.toLowerCase().includes(kw) ?? false)
    );
  });

  constructor(private http: HttpClient, private messageService: MessageService) {}

  ngOnInit() {
    this.loadUoms();
  }

  loadUoms() {
    this.http.get<any[]>(this.apiUrl).subscribe({
      next: (data) => {
        // Lọc chỉ lấy đơn vị tính (UnitOfMeasure)
        this.uoms.set((data || []).filter(item => item && item.catalogType === 'UnitOfMeasure'));
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải danh mục đơn vị tính.' });
      }
    });
  }

  onSearch() {
    // Tự động thông qua computed
  }

  onAddNew() {
    this.isEdit.set(false);
    this.isPrivate.set(false);
    this.currentItem.set({ code: '', name: '', catalogType: 'UnitOfMeasure', parentId: null, description: '', unitId: null });
    this.dialogHeader.set('Thêm mới đơn vị tính');
    this.displayDialog.set(true);
  }

  onEdit(item: any) {
    this.isEdit.set(true);
    this.isPrivate.set(!!item.unitId);
    this.currentItem.set({ ...item });
    this.dialogHeader.set('Chỉnh sửa đơn vị tính');
    this.displayDialog.set(true);
  }

  onSaveItem() {
    const itemDraft = this.currentItem();
    if (!itemDraft.code || !itemDraft.name) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập Mã và Tên đơn vị tính.' });
      return;
    }

    itemDraft.unitId = this.isPrivate() ? -1 : null;

    if (this.isEdit()) {
      this.http.put(`${this.apiUrl}/${itemDraft.id}`, itemDraft).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật đơn vị tính thành công!' });
          this.loadUoms();
          this.displayDialog.set(false);
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể cập nhật đơn vị tính.' });
        }
      });
    } else {
      this.http.post<any>(this.apiUrl, itemDraft).subscribe({
        next: (created) => {
          this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Thêm đơn vị tính mới thành công!' });
          this.loadUoms();
          this.displayDialog.set(false);
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Thêm đơn vị tính mới thất bại.' });
        }
      });
    }
  }

  onDelete(item: any) {
    if (confirm(`Bạn có chắc chắn muốn xóa đơn vị tính ${item.name} (${item.code})?`)) {
      this.http.delete(`${this.apiUrl}/${item.id}`).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Xóa thành công', detail: 'Đã xóa đơn vị tính thành công!' });
          this.loadUoms();
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Xóa đơn vị tính thất bại.' });
        }
      });
    }
  }
}
