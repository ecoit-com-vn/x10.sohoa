import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { FormsModule } from '@angular/forms';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-borrow-return',
  standalone: true,
  imports: [CommonModule, HttpClientModule, ToastModule, FormsModule],
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
          <span class="bc-text">Thu thập & xử lý</span>
          <span class="bc-sep">/</span>
          <span class="bc-current">Quản lý Mượn/Trả hồ sơ</span>
        </div>

        <div class="edit-header">
          <h2 class="edit-title" style="color: #002D72;">Quản lý Yêu cầu Mượn/Trả hồ sơ kỹ thuật</h2>
        </div>

        <p class="text-muted mb-4">
          Phê duyệt hoặc từ chối các yêu cầu xin mượn hồ sơ kỹ thuật đường dây và trạm biến áp của nhân viên kỹ thuật điện lực để truy cập tài liệu số hóa gốc.
        </p>

        <!-- Table -->
        <div class="wf-table-wrap">
          <table class="wf-table">
            <thead>
              <tr>
                <th style="width: 250px;">Mã số yêu cầu</th>
                <th>Người yêu cầu mượn</th>
                <th>Mã hồ sơ kỹ thuật</th>
                <th style="width: 160px;">Ngày gửi yêu cầu</th>
                <th class="col-tt">Trạng thái phê duyệt</th>
                <th class="col-hd">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let req of requests; let i = index">
                <td><code>{{ req.id }}</code></td>
                <td><b class="wf-name-link">{{ req.requester || '—' }}</b></td>
                <td>{{ req.recordName || '—' }}</td>
                <td>{{ req.createdAt | date:'dd/MM/yyyy HH:mm' }}</td>
                <td class="col-tt">
                  <span class="status-pill"
                    [class.status-pending]="req.status === 'PENDING'"
                    [class.status-active]="req.status === 'APPROVED' || req.status === 'BORROWED' || req.status === 'RETURNED'"
                    [class.status-inactive]="req.status === 'REJECTED'">
                    <i class="pi pi-clock"></i>
                    {{ getStatusLabel(req.status) }}
                  </span>
                </td>
                <td class="col-hd">
                  <ng-container *ngIf="req.status === 'PENDING'">
                    <button class="act-btn act-edit" (click)="updateStatus(req.id, 'APPROVED')" title="Duyệt yêu cầu">
                      <i class="pi pi-check"></i>
                    </button>
                  </ng-container>
                  <ng-container *ngIf="req.status === 'APPROVED'">
                    <button class="act-btn act-edit" (click)="updateStatus(req.id, 'BORROWED')" title="Đã cho mượn">
                      <i class="pi pi-folder-open"></i>
                    </button>
                  </ng-container>
                  <ng-container *ngIf="req.status === 'BORROWED'">
                    <button class="act-btn act-edit" (click)="updateStatus(req.id, 'RETURNED')" title="Đã trả hồ sơ">
                      <i class="pi pi-replay"></i>
                    </button>
                  </ng-container>
                  <span *ngIf="req.status === 'RETURNED' || req.status === 'REJECTED'" class="text-muted">—</span>
                </td>
              </tr>
              <tr *ngIf="requests.length === 0 && !loading">
                <td colspan="6" class="empty-row">
                  <i class="pi pi-inbox"></i>
                  <div>Chưa có yêu cầu mượn/trả hồ sơ nào được gửi lên.</div>
                </td>
              </tr>
              <tr *ngIf="loading">
                <td colspan="6" class="skeleton-row">
                  <div class="skeleton-bar"></div>
                  <div class="skeleton-bar short"></div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer" *ngIf="requests.length > 0">
          <span class="record-count">Tổng số: <b>{{ requests.length }}</b> bản ghi.</span>
          <div class="pagination">
            <button class="page-btn" disabled><i class="pi pi-chevron-left"></i></button>
            <span class="page-current">1</span>
            <button class="page-btn" disabled><i class="pi pi-chevron-right"></i></button>
            <select class="page-size-sel">
              <option>10 / trang</option>
              <option>20 / trang</option>
              <option>50 / trang</option>
            </select>
          </div>
        </div>

      </div>
    </div>
  `
})
export class BorrowReturnComponent implements OnInit {
  requests: any[] = [];
  loading = false;
  private http = inject(HttpClient);
  private messageService = inject(MessageService);
  private apiUrl = `${environment.apiGatewayUrl}/api/v1/workflows`;

  ngOnInit() {
    this.loadRequests();
  }

  loadRequests() {
    this.loading = true;
    this.http.get<any[]>(this.apiUrl).subscribe({
      next: (data) => {
        // Map backend BorrowRecord objects to UI requests model
        this.requests = (data || []).map(item => ({
          id: item.id,
          requester: item.requesterId || 'Chuyên viên kỹ thuật',
          recordName: item.dossierId,
          createdAt: new Date(item.requestDate),
          status: this.mapBackendState(item.state)
        }));
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading requests via API', err);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi tải dữ liệu',
          detail: 'Không thể kết nối tới máy chủ để tải yêu cầu mượn/trả.'
        });
        this.requests = [];
        this.loading = false;
      }
    });
  }

  mapBackendState(state: any): string {
    // Backend enum: Requested=0, Approved=1, Borrowed=2, Returned=3
    switch (state) {
      case 0:
      case 'Requested':
        return 'PENDING';
      case 1:
      case 'Approved':
        return 'APPROVED';
      case 2:
      case 'Borrowed':
        return 'BORROWED';
      case 3:
      case 'Returned':
        return 'RETURNED';
      default:
        return 'PENDING';
    }
  }

  mapStateToBackendEnum(status: string): number {
    switch (status) {
      case 'APPROVED': return 1;
      case 'BORROWED': return 2;
      case 'RETURNED': return 3;
      default: return 0;
    }
  }

  updateStatus(id: string, status: string) {
    const backendState = this.mapStateToBackendEnum(status);
    
    this.http.put(`${this.apiUrl}/${id}/state`, backendState).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success', 
          summary: 'Thành công', 
          detail: `Đã phê duyệt cập nhật trạng thái yêu cầu thành công!`
        });
        this.loadRequests();
      },
      error: (err) => {
        console.error('Error updating status via API', err);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể cập nhật trạng thái yêu cầu trên hệ thống.'
        });
      }
    });
  }

  getStatusLabel(status: string): string {
    switch (status) {
      case 'PENDING': return 'Chờ phê duyệt';
      case 'APPROVED': return 'Đã duyệt';
      case 'BORROWED': return 'Đang mượn';
      case 'RETURNED': return 'Đã trả';
      case 'REJECTED': return 'Từ chối';
      default: return status;
    }
  }
}
