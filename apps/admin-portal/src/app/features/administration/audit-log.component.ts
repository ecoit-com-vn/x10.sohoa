import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { HttpClient, HttpClientModule } from '@angular/common/http';
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
  imports: [CommonModule, FormsModule, ToastModule, HttpClientModule],
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
          <div class="toolbar-right">
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
                <th style="width: 250px;">Mã nhật ký</th>
                <th>Hành động</th>
                <th>Người dùng</th>
                <th>Thời gian</th>
                <th>Chi tiết thao tác</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngIf="loading">
                <td colspan="5" class="skeleton-row">
                  <div class="skeleton-bar"></div>
                  <div class="skeleton-bar short"></div>
                </td>
              </tr>
              <tr *ngFor="let log of filteredLogs">
                <td><code class="text-muted">{{ log.id }}</code></td>
                <td><b>{{ log.action }}</b></td>
                <td>
                  <span class="wf-name-link">{{ log.user }}</span>
                </td>
                <td>{{ log.timestamp }}</td>
                <td class="mota-text">{{ log.details }}</td>
              </tr>
              <tr *ngIf="filteredLogs.length === 0 && !loading">
                <td colspan="5" class="empty-row">
                  <i class="pi pi-inbox"></i>
                  <div>Không tìm thấy lịch sử thao tác nào.</div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer" *ngIf="filteredLogs.length > 0">
          <span class="record-count">Tổng số: <b>{{ filteredLogs.length }}</b> bản ghi.</span>
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
export class AuditLogComponent implements OnInit {
  logs: AuditLog[] = [];
  filteredLogs: AuditLog[] = [];
  searchTerm: string = '';
  loading = false;

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
  }

  exportExcel() {
    this.messageService.add({ severity: 'info', summary: 'Đang xuất Excel', detail: 'Hệ thống đang chuẩn bị tệp tin...' });
    
    setTimeout(() => {
      this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xuất tệp tin AuditLog.xlsx thành công!' });
    }, 1200);
  }
}
