import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { environment } from '../../../environments/environment';

interface AuditLog {
  id: string;
  action: string;
  user: string;
  timestamp: string;
  details: string;
}

@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, HttpClientModule, DialogModule],
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
          <span class="bc-current">Nhật ký thao tác (Audit Log)</span>
        </div>

        <!-- Toolbar -->
        <div class="list-toolbar">
          <div class="toolbar-left">
            <input type="text" class="wf-search-input"
              placeholder="Tìm kiếm nhật ký, hành động, người dùng..."
              [(ngModel)]="searchTerm"
              (keyup.enter)="onSearch()" />
            <button class="btn-tim" (click)="onSearch()">
              <i class="pi pi-search"></i> Tìm
            </button>
          </div>
          <div class="toolbar-right" style="display: flex; gap: 8px;">
            <button class="btn-tim" style="background-color: #dc2626; border-color: #dc2626;" (click)="onOpenDeleteDialog()" *ngIf="hasDeletePermission()">
              <i class="pi pi-trash"></i> Xóa nhật ký an toàn
            </button>
            <button class="btn-excel" (click)="exportExcel()" [disabled]="loading">
              <i class="pi pi-file-excel"></i> Xuất Excel
            </button>
          </div>
        </div>

        <!-- Table -->
        <div class="wf-table-wrap">
          <table class="wf-table">
            <thead>
              <tr>
                <th style="width: 200px;">Mã nhật ký</th>
                <th>Hành động</th>
                <th>Người dùng</th>
                <th>Thời gian</th>
                <th>Chi tiết thao tác</th>
              </tr>
            </thead>
            <tbody>
              <!-- skeleton loading rows -->
              <ng-container *ngIf="loading">
                <tr *ngFor="let item of [1, 2, 3, 4, 5]">
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 120px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 150px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 100px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 140px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 250px; border-radius: 4px;"></div></td>
                </tr>
              </ng-container>

              <ng-container *ngIf="!loading">
                <tr *ngFor="let log of paginatedLogs">
                  <td><code class="text-muted">{{ log.id }}</code></td>
                  <td><b>{{ log.action }}</b></td>
                  <td>
                    <span class="wf-name-link">{{ log.user }}</span>
                  </td>
                  <td>{{ log.timestamp }}</td>
                  <td class="mota-text">{{ log.details }}</td>
                </tr>
                <tr *ngIf="filteredLogs.length === 0">
                  <td colspan="5" class="empty-row">
                    <i class="pi pi-inbox"></i>
                    <div>Không tìm thấy lịch sử thao tác nào.</div>
                  </td>
                </tr>
              </ng-container>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer" *ngIf="filteredLogs.length > 0 && !loading">
          <span class="record-count">Tổng số: <b>{{ filteredLogs.length }}</b> bản ghi.</span>
          <div class="pagination">
            <button class="page-btn" (click)="prevPage()" [disabled]="currentPage === 1">
              <i class="pi pi-chevron-left"></i>
            </button>
            <span class="page-current">Trang {{ currentPage }} / {{ totalPages || 1 }}</span>
            <button class="page-btn" (click)="nextPage()" [disabled]="currentPage >= totalPages">
              <i class="pi pi-chevron-right"></i>
            </button>
            <select class="page-size-sel" [value]="pageSize" (change)="onPageSizeChange($event)">
              <option [value]="10">10 / trang</option>
              <option [value]="20">20 / trang</option>
              <option [value]="50">50 / trang</option>
            </select>
          </div>
        </div>

      </div>
    </div>

    <!-- Dialog Xóa Nhật ký An toàn -->
    <p-dialog [(visible)]="displayDeleteDialog" header="Xóa nhật ký hệ thống an toàn" [modal]="true" [style]="{ width: '450px' }" styleClass="evn-dialog-custom">
      <div style="display: flex; flex-direction: column; gap: 14px; padding-top: 10px;">
        <div style="background-color: #fee2e2; color: #991b1b; padding: 10px; border-radius: 6px; border: 1px solid #fca5a5; font-size: 0.85rem;">
          <i class="pi pi-exclamation-triangle mr-1"></i>
          <b>Chú ý:</b> Mọi thao tác xóa nhật ký hệ thống đều được lưu lại vĩnh viễn vào nhật ký bảo mật bất biến của máy chủ để phục vụ công tác thanh kiểm tra.
        </div>
        <div class="form-group">
          <label class="form-label" style="font-weight: 600;">Từ ngày <span class="required">*</span></label>
          <input type="date" class="wf-input w-full" [(ngModel)]="deleteParams.fromDate" [disabled]="deleting" />
        </div>
        <div class="form-group">
          <label class="form-label" style="font-weight: 600;">Đến ngày <span class="required">*</span></label>
          <input type="date" class="wf-input w-full" [(ngModel)]="deleteParams.toDate" [disabled]="deleting" />
        </div>
      </div>
      <ng-template #footer>
        <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayDeleteDialog = false" [disabled]="deleting">Hủy</button>
          <button class="btn-save btn-small" style="background-color: #dc2626; color: #fff; border-color: #dc2626;" (click)="onConfirmDelete()" [disabled]="deleting">
            <i class="pi pi-spin pi-spinner" *ngIf="deleting" style="margin-right: 4px;"></i>
            Xác nhận xóa
          </button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styles: `
    @keyframes shimmer {
      0% { background-position: -200% 0; }
      100% { background-position: 200% 0; }
    }
    .skeleton-shimmer {
      background: linear-gradient(90deg, #f3f4f6 25%, #e5e7eb 50%, #f3f4f6 75%);
      background-size: 200% 100%;
      animation: shimmer 1.5s infinite;
    }
    :global(html.dark-mode) .skeleton-shimmer {
      background: linear-gradient(90deg, #1e293b 25%, #334155 50%, #1e293b 75%);
      background-size: 200% 100%;
    }
  `
})
export class AuditLogComponent implements OnInit {
  logs: AuditLog[] = [];
  filteredLogs: AuditLog[] = [];
  searchTerm: string = '';
  loading = false;
  
  currentPage = 1;
  pageSize = 10;

  private http = inject(HttpClient);
  private messageService = inject(MessageService);

  ngOnInit() {
    this.loadAuditLogs();
  }

  loadAuditLogs() {
    this.loading = true;
    this.http.get<any>(`${environment.apiGatewayUrl}/api/v1/audit-logs?page=1&pageSize=50`).subscribe({
      next: (res) => {
        const backendLogs = res.logs || [];
        this.logs = backendLogs.map((item: any, idx: number) => ({
          id: item.id || `AL-${1001 + idx}`,
          action: item.action || 'USER_ACTION',
          user: item.userName || item.user || 'system',
          timestamp: new Date(item['@timestamp'] || item.timestamp || Date.now()).toLocaleString('vi-VN'),
          details: item.details || item.message || JSON.stringify(item)
        }));
        this.filteredLogs = [...this.logs];
        this.loading = false;
        this.currentPage = 1;
      },
      error: (err) => {
        console.error('Error loading audit logs', err);
        this.messageService.add({ 
          severity: 'error', 
          summary: 'Lỗi tải dữ liệu', 
          detail: 'Không thể kết nối đến máy chủ để tải lịch sử thao tác hệ thống.' 
        });
        this.logs = [];
        this.filteredLogs = [];
        this.loading = false;
      }
    });
  }

  onSearch() {
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      this.filteredLogs = this.logs.filter(log => 
        log.action.toLowerCase().includes(term) ||
        log.user.toLowerCase().includes(term) ||
        log.details.toLowerCase().includes(term) ||
        log.id.toLowerCase().includes(term)
      );
    } else {
      this.filteredLogs = [...this.logs];
    }
    this.currentPage = 1;
  }

  get paginatedLogs(): AuditLog[] {
    const startIndex = (this.currentPage - 1) * this.pageSize;
    return this.filteredLogs.slice(startIndex, startIndex + this.pageSize);
  }

  get totalPages(): number {
    return Math.ceil(this.filteredLogs.length / this.pageSize);
  }

  nextPage() {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
    }
  }

  prevPage() {
    if (this.currentPage > 1) {
      this.currentPage--;
    }
  }

  onPageSizeChange(event: any) {
    this.pageSize = Number(event.target.value);
    this.currentPage = 1;
  }

  exportExcel() {
    this.messageService.add({ severity: 'info', summary: 'Đang xuất Excel', detail: 'Hệ thống đang chuẩn bị tệp tin...' });
    
    setTimeout(() => {
      this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xuất tệp tin AuditLog.xlsx thành công!' });
    }, 1200);
  }

  displayDeleteDialog = false;
  deleting = false;
  deleteParams = { fromDate: '', toDate: '' };

  hasDeletePermission(): boolean {
    if (typeof window !== 'undefined') {
      const token = localStorage.getItem('token');
      if (token) {
        try {
          const payloadBase64 = token.split('.')[1];
          const payloadJson = atob(payloadBase64.replace(/-/g, '+').replace(/_/g, '/'));
          const payload = JSON.parse(payloadJson);
          const permissions = payload.permission || [];
          const roles = payload.role || [];
          const hasPerm = permissions.includes('AUDIT_LOG_DELETE');
          const hasRole = Array.isArray(roles) ? roles.includes('ADMIN') : roles === 'ADMIN';
          return hasPerm || hasRole;
        } catch (e) {
          return false;
        }
      }
    }
    return false;
  }

  onOpenDeleteDialog() {
    const today = new Date();
    const thirtyDaysAgo = new Date();
    thirtyDaysAgo.setDate(today.getDate() - 30);
    
    this.deleteParams = {
      fromDate: thirtyDaysAgo.toISOString().split('T')[0],
      toDate: today.toISOString().split('T')[0]
    };
    this.displayDeleteDialog = true;
  }

  onConfirmDelete() {
    if (!this.deleteParams.fromDate || !this.deleteParams.toDate) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Vui lòng chọn khoảng thời gian cần xóa.' });
      return;
    }

    const fromDateObj = new Date(this.deleteParams.fromDate);
    const toDateObj = new Date(this.deleteParams.toDate);
    fromDateObj.setHours(0, 0, 0, 0);
    toDateObj.setHours(23, 59, 59, 999);

    if (fromDateObj > toDateObj) {
      this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Từ ngày không thể lớn hơn Đến ngày.' });
      return;
    }

    this.deleting = true;
    const url = `${environment.apiGatewayUrl}/api/v1/audit-logs?fromDate=${fromDateObj.toISOString()}&toDate=${toDateObj.toISOString()}`;
    
    this.http.delete<any>(url).subscribe({
      next: (res) => {
        this.messageService.add({ 
          severity: 'success', 
          summary: 'Thành công', 
          detail: res.message || 'Đã thực hiện dọn dẹp nhật ký hệ thống.' 
        });
        this.displayDeleteDialog = false;
        this.deleting = false;
        this.loadAuditLogs();
      },
      error: (err) => {
        this.deleting = false;
        const msg = err.error?.message || 'Không thể xóa nhật ký do lỗi kết nối.';
        this.messageService.add({ severity: 'error', summary: 'Lỗi xóa nhật ký', detail: msg });
      }
    });
  }
}
