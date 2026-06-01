import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-system-param',
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
          <span class="bc-text">Quản trị hệ thống</span>
          <span class="bc-sep">/</span>
          <span class="bc-current">Cấu hình tham số hệ thống</span>
        </div>

        <p class="text-muted mb-4">
          Xem danh sách các tham số cài đặt hệ thống EVNHANOI và chỉnh sửa các giá trị cấu hình tương ứng. (Lưu ý: Để đảm bảo an toàn hệ thống, các khóa tham số được thiết lập cố định và không thể thêm mới hoặc xóa bỏ).
        </p>

        <!-- Table -->
        <div class="wf-table-wrap">
          <table class="wf-table">
            <thead>
              <tr>
                <th class="col-stt">STT</th>
                <th>Tên cấu hình (Key)</th>
                <th>Giá trị cài đặt (Value)</th>
                <th>Kiểu dữ liệu</th>
                <th>Mô tả chức năng</th>
                <th class="col-hd">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let param of params; let i = index">
                <td class="col-stt text-muted">{{ i + 1 }}</td>
                <td><code>{{ param.paramKey }}</code></td>
                <td>
                  <span style="font-weight: 600; color: #002D72;">{{ param.paramValue }}</span>
                </td>
                <td>
                  <span class="status-pill status-active" style="background-color: #f1f5f9; color: #475569; border-color: #cbd5e1;">
                    {{ param.dataType }}
                  </span>
                </td>
                <td><span class="text-muted" style="font-size: 0.82rem;">{{ param.description }}</span></td>
                <td class="col-hd">
                  <button class="act-btn act-edit" (click)="onEdit(param)" title="Chỉnh sửa cấu hình">
                    <i class="pi pi-pencil"></i>
                  </button>
                </td>
              </tr>
              <tr *ngIf="params.length === 0">
                <td colspan="6" class="empty-row">
                  <i class="pi pi-inbox"></i>
                  <div>Không tìm thấy tham số cấu hình hệ thống nào.</div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer">
          <span class="record-count">Tổng số: <b>{{ params.length }}</b> tham số hệ thống cố định.</span>
        </div>

      </div>
    </div>

    <!-- Dialog Chỉnh Sửa Giá Trị Tham Số -->
    <p-dialog [(visible)]="displayDialog" [header]="dialogHeader" [modal]="true" [style]="{ width: '450px' }" styleClass="evn-dialog-custom">
      <div style="display: flex; flex-direction: column; gap: 14px; padding-top: 10px;">
        <div class="form-group">
          <label class="form-label" style="font-weight: 600;">Khóa tham số</label>
          <input type="text" class="wf-input w-full" [value]="currentParam.paramKey" disabled style="background-color: #f1f5f9; color: #64748b;" />
        </div>
        
        <div class="form-group">
          <label class="form-label" style="font-weight: 600;">Giá trị cấu hình <span class="required">*</span></label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentParam.paramValue" placeholder="Nhập giá trị mới cho tham số..." />
        </div>
        
        <div class="form-group">
          <label class="form-label">Mô tả chức năng</label>
          <textarea class="wf-textarea w-full" rows="3" [(ngModel)]="currentParam.description" placeholder="Ghi chú chi tiết tham số này dùng làm gì..."></textarea>
        </div>
      </div>
      
      <ng-template #footer>
        <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayDialog = false">Hủy</button>
          <button class="btn-save btn-small" (click)="onSaveParam()">Cập nhật</button>
        </div>
      </ng-template>
    </p-dialog>
  `
})
export class SystemParam implements OnInit {
  params: any[] = [];
  displayDialog = false;
  dialogHeader = '';
  currentParam: any = {};

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/system-params`;

  constructor(private http: HttpClient, private messageService: MessageService) {}

  ngOnInit() {
    this.loadParams();
  }

  loadParams() {
    this.http.get<any[]>(this.apiUrl).subscribe({
      next: (data) => {
        this.params = data;
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải cấu hình tham số hệ thống.' });
      }
    });
  }

  onEdit(param: any) {
    this.currentParam = { ...param };
    this.dialogHeader = `Chỉnh sửa cấu hình: ${param.paramKey}`;
    this.displayDialog = true;
  }

  onSaveParam() {
    if (!this.currentParam.paramValue) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập giá trị tham số.' });
      return;
    }

    this.http.put(`${this.apiUrl}/${this.currentParam.paramKey}`, this.currentParam).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Thay đổi tham số cấu hình thành công!' });
        this.loadParams();
        this.displayDialog = false;
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Cập nhật tham số hệ thống thất bại.' });
      }
    });
  }
}
