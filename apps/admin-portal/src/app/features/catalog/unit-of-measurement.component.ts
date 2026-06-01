import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-unit-of-measurement',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule],
  providers: [MessageService],
  template: `
    <div class="wf-page">
      <p-toast></p-toast>
      <div class="wf-card">
        
        <!-- Breadcrumb -->
        <div class="breadcrumb">
          <i class="pi pi-home bc-icon"></i>
          <span class="bc-text">Trang chủ</span>
          <span class="bc-sep">/</span>
          <span class="bc-text">Hệ thống Danh mục</span>
          <span class="bc-sep">/</span>
          <span class="bc-current">Danh mục đơn vị tính</span>
        </div>

        <p class="text-muted mb-4">
          Quản lý danh sách các Đơn vị tính (UoM) dùng cho hồ sơ kỹ thuật, thiết bị trên toàn hệ thống EVNHANOI (Ví dụ: Cái, Bộ, Mét, Chiếc, Kilôgam...).
        </p>

        <!-- Toolbar -->
        <div class="list-toolbar">
          <div class="toolbar-left">
            <input type="text" class="wf-search-input"
              placeholder="Tìm nhanh đơn vị tính..."
              [(ngModel)]="searchKeyword"
              (keyup.enter)="onSearch()" />
            <button class="btn-tim" (click)="onSearch()">
              <i class="pi pi-search"></i> Tìm
            </button>
          </div>
          <div class="toolbar-right">
            <button class="btn-green" (click)="onAddNew()">
              <i class="pi pi-plus"></i> Thêm đơn vị tính
            </button>
          </div>
        </div>

        <!-- Table -->
        <div class="wf-table-wrap">
          <table class="wf-table">
            <thead>
              <tr>
                <th class="col-stt">STT</th>
                <th>Mã đơn vị tính (Code)</th>
                <th>Tên đơn vị tính (Name)</th>
                <th>Phân loại danh mục (Catalog Type)</th>
                <th>Mô tả ghi chú</th>
                <th class="col-hd">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let item of filteredUoms; let i = index">
                <td class="col-stt text-muted">{{ i + 1 }}</td>
                <td><code>{{ item.code }}</code></td>
                <td><b class="wf-name-link" (click)="onEdit(item)">{{ item.name }}</b></td>
                <td>
                  <span class="status-pill status-active" style="background-color: #ecfdf5; color: #065f46; border-color: #a7f3d0;">
                    {{ item.catalogType }}
                  </span>
                  <span *ngIf="item.unitId" class="status-pill" style="margin-left: 5px; background-color: #fef3c7; color: #92400e; border-color: #fde68a;">
                    Dùng riêng ĐV
                  </span>
                </td>

                <td><span class="text-muted" style="font-size: 0.82rem;">{{ item.description }}</span></td>
                <td class="col-hd">
                  <button class="act-btn act-edit" (click)="onEdit(item)" title="Chỉnh sửa"><i class="pi pi-pencil"></i></button>
                  <button class="act-btn act-delete" (click)="onDelete(item)" title="Xóa"><i class="pi pi-trash"></i></button>
                </td>
              </tr>
              <tr *ngIf="filteredUoms.length === 0">
                <td colspan="6" class="empty-row">
                  <i class="pi pi-inbox"></i>
                  <div>Không tìm thấy đơn vị tính nào.</div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer">
          <span class="record-count">Tổng số: <b>{{ uoms.length }}</b> đơn vị tính.</span>
        </div>

      </div>
    </div>

    <!-- Dialog Thêm/Sửa Đơn vị tính -->
    <p-dialog [(visible)]="displayDialog" [header]="dialogHeader" [modal]="true" [style]="{ width: '450px' }" styleClass="evn-dialog-custom">
      <div style="display: flex; flex-direction: column; gap: 14px; padding-top: 10px;">
        <div class="form-group">
          <label class="form-label">Mã đơn vị tính <span class="required">*</span></label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentItem.code" placeholder="Ví dụ: UOM_CAI, UOM_MET..." [disabled]="isEdit" />
        </div>
        
        <div class="form-group">
          <label class="form-label">Tên đơn vị tính <span class="required">*</span></label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentItem.name" placeholder="Ví dụ: Cái, Mét, Kilôgam..." />
        </div>
        
        <div class="form-group" style="display: flex; align-items: center; gap: 8px; padding-top: 5px;">
          <input type="checkbox" id="chkPrivate" [(ngModel)]="isPrivate" style="scale: 1.1; cursor: pointer;" />
          <label for="chkPrivate" style="font-size: 0.9rem; font-weight: 600; cursor: pointer; user-select: none;">Dùng riêng cho đơn vị của tôi</label>
        </div>

        <div class="form-group">
          <label class="form-label">Mô tả chi tiết</label>
          <textarea class="wf-textarea w-full" rows="3" [(ngModel)]="currentItem.description" placeholder="Mô tả phạm vi áp dụng..."></textarea>
        </div>
      </div>
      
      <ng-template #footer>
        <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayDialog = false">Hủy</button>
          <button class="btn-save btn-small" (click)="onSaveItem()">Lưu</button>
        </div>
      </ng-template>
    </p-dialog>
  `
})
export class UnitOfMeasurement implements OnInit {
  uoms: any[] = [];
  filteredUoms: any[] = [];
  searchKeyword = '';

  displayDialog = false;
  dialogHeader = '';
  isEdit = false;
  isPrivate = false;
  currentItem: any = {};

  private apiUrl = `${environment.apiGatewayUrl}/api/Catalog`;

  constructor(private http: HttpClient, private messageService: MessageService) {}

  ngOnInit() {
    this.loadUoms();
  }

  loadUoms() {
    this.http.get<any[]>(this.apiUrl).subscribe({
      next: (data) => {
        // Lọc chỉ lấy đơn vị tính (UnitOfMeasure)
        this.uoms = data.filter(item => item.catalogType === 'UnitOfMeasure');
        this.onSearch();
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải danh mục đơn vị tính.' });
      }
    });
  }

  onSearch() {
    if (this.searchKeyword) {
      const kw = this.searchKeyword.toLowerCase();
      this.filteredUoms = this.uoms.filter(item => 
        item.code.toLowerCase().includes(kw) || 
        item.name.toLowerCase().includes(kw) || 
        (item.description && item.description.toLowerCase().includes(kw))
      );
    } else {
      this.filteredUoms = [...this.uoms];
    }
  }

  onAddNew() {
    this.isEdit = false;
    this.isPrivate = false;
    this.currentItem = { code: '', name: '', catalogType: 'UnitOfMeasure', parentId: null, description: '', unitId: null };
    this.dialogHeader = 'Thêm mới đơn vị tính';
    this.displayDialog = true;
  }

  onEdit(item: any) {
    this.isEdit = true;
    this.isPrivate = !!item.unitId;
    this.currentItem = { ...item };
    this.dialogHeader = 'Chỉnh sửa đơn vị tính';
    this.displayDialog = true;
  }

  onSaveItem() {
    if (!this.currentItem.code || !this.currentItem.name) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập Mã và Tên đơn vị tính.' });
      return;
    }

    this.currentItem.unitId = this.isPrivate ? -1 : null;

    if (this.isEdit) {
      this.http.put(`${this.apiUrl}/${this.currentItem.id}`, this.currentItem).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật đơn vị tính thành công!' });
          this.loadUoms();
          this.displayDialog = false;
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể cập nhật đơn vị tính.' });
        }
      });
    } else {
      this.http.post<any>(this.apiUrl, this.currentItem).subscribe({
        next: (created) => {
          this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Thêm đơn vị tính mới thành công!' });
          this.loadUoms();
          this.displayDialog = false;
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

