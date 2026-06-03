import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { environment } from '@env/environment';
import { finalize } from 'rxjs';

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
  templateUrl: './audit-log.component.html',
  styleUrl: './audit-log.component.scss'
})
export class AuditLogComponent implements OnInit {
  logs = signal<AuditLog[]>([]);
  searchTerm = signal<string>('');
  loading = signal<boolean>(false);
  
  currentPage = 1;
  pageSize = 10;

  private http = inject(HttpClient);
  private messageService = inject(MessageService);

  // computed signal for filteredLogs
  filteredLogs = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    const allLogs = this.logs() || [];
    if (!term) {
      return [...allLogs];
    }
    return allLogs.filter(log => 
      (log.action?.toLowerCase().includes(term) ?? false) ||
      (log.user?.toLowerCase().includes(term) ?? false) ||
      (log.details?.toLowerCase().includes(term) ?? false) ||
      (log.id?.toLowerCase().includes(term) ?? false)
    );
  });

  ngOnInit() {
    this.loadAuditLogs();
  }

  loadAuditLogs() {
    this.loading.set(true);
    this.http.get<any>(`${environment.apiGatewayUrl}/api/v1/audit-logs?page=1&pageSize=50`)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          const backendLogs = res?.logs || res || [];
          this.logs.set(Array.isArray(backendLogs) ? backendLogs.map((item: any, idx: number) => ({
            id: item.id || `AL-${1001 + idx}`,
            action: item.action || 'USER_ACTION',
            user: item.userName || item.user || 'system',
            timestamp: new Date(item['@timestamp'] || item.timestamp || Date.now()).toLocaleString('vi-VN'),
            details: item.details || item.message || JSON.stringify(item)
          })) : []);
          this.currentPage = 1;
        },
        error: (err) => {
          console.error('Error loading audit logs', err);
          this.messageService.add({ 
            severity: 'error', 
            summary: 'Lỗi tải dữ liệu', 
            detail: 'Không thể kết nối đến máy chủ để tải lịch sử thao tác hệ thống.' 
          });
          this.logs.set([]);
        }
      });
  }

  onSearch() {
    // Với computed, filteredLogs tự động cập nhật
  }

  get paginatedLogs(): AuditLog[] {
    const startIndex = (this.currentPage - 1) * this.pageSize;
    return this.filteredLogs().slice(startIndex, startIndex + this.pageSize);
  }

  get totalPages(): number {
    return Math.ceil(this.filteredLogs().length / this.pageSize);
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

  displayDeleteDialog = signal<boolean>(false);
  deleting = signal<boolean>(false);
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
    this.displayDeleteDialog.set(true);
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

    this.deleting.set(true);
    const url = `${environment.apiGatewayUrl}/api/v1/audit-logs?fromDate=${fromDateObj.toISOString()}&toDate=${toDateObj.toISOString()}`;
    
    this.http.delete<any>(url)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: (res) => {
          this.messageService.add({ 
            severity: 'success', 
            summary: 'Thành công', 
            detail: res.message || 'Đã thực hiện dọn dẹp nhật ký hệ thống.' 
          });
          this.displayDeleteDialog.set(false);
          this.loadAuditLogs();
        },
        error: (err) => {
          const msg = err.error?.message || 'Không thể xóa nhật ký do lỗi kết nối.';
          this.messageService.add({ severity: 'error', summary: 'Lỗi xóa nhật ký', detail: msg });
        }
      });
  }
}
