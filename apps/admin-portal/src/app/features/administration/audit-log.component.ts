import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { FormsModule } from '@angular/forms';

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
  imports: [CommonModule, TableModule, ButtonModule, CardModule, InputTextModule, ToastModule, FormsModule],
  providers: [MessageService],
  template: `
    <div class="p-4">
      <p-toast></p-toast>
      <p-card header="Nhật ký thao tác (Audit Log)">
        <div class="flex justify-content-between mb-3">
          <span class="p-input-icon-left">
            <i class="pi pi-search"></i>
            <input pInputText type="text" [(ngModel)]="searchTerm" placeholder="Tìm kiếm nhật ký..." (input)="onSearch()" />
          </span>
          <p-button label="Xuất Excel" icon="pi pi-file-excel" severity="success" (onClick)="exportExcel()"></p-button>
        </div>

        <p-table [value]="filteredLogs" [paginator]="true" [rows]="10" styleClass="p-datatable-striped">
          <ng-template pTemplate="header">
            <tr>
              <th>ID</th>
              <th>Hành động</th>
              <th>Người dùng</th>
              <th>Thời gian</th>
              <th>Chi tiết</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-log>
            <tr>
              <td>{{ log.id }}</td>
              <td>{{ log.action }}</td>
              <td>{{ log.user }}</td>
              <td>{{ log.timestamp }}</td>
              <td>{{ log.details }}</td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="5" class="text-center">Không có dữ liệu nhật ký.</td>
            </tr>
          </ng-template>
        </p-table>
      </p-card>
    </div>
  `
})
export class AuditLogComponent implements OnInit {
  logs: AuditLog[] = [
    { id: 'AL-001', action: 'CREATE_USER', user: 'admin', timestamp: '2026-05-28 10:00', details: 'Created user john.doe' },
    { id: 'AL-002', action: 'UPDATE_DOCUMENT', user: 'jane.doe', timestamp: '2026-05-28 10:15', details: 'Updated document DOC-123' },
    { id: 'AL-003', action: 'DELETE_FILE', user: 'admin', timestamp: '2026-05-28 10:30', details: 'Deleted file report.pdf' },
    { id: 'AL-004', action: 'SYNC_PMIS', user: 'system', timestamp: '2026-05-28 11:00', details: 'Synced equipment data with PMIS' }
  ];
  filteredLogs: AuditLog[] = [];
  searchTerm: string = '';

  constructor(private messageService: MessageService) {}

  ngOnInit() {
    this.filteredLogs = [...this.logs];
  }

  onSearch() {
    if (this.searchTerm) {
      this.filteredLogs = this.logs.filter(log => 
        log.action.toLowerCase().includes(this.searchTerm.toLowerCase()) ||
        log.user.toLowerCase().includes(this.searchTerm.toLowerCase()) ||
        log.details.toLowerCase().includes(this.searchTerm.toLowerCase())
      );
    } else {
      this.filteredLogs = [...this.logs];
    }
  }

  exportExcel() {
    // In a real app, this would call a backend API.
    this.messageService.add({ severity: 'info', summary: 'Đang xuất Excel', detail: 'Vui lòng chờ trong giây lát...' });
    
    // Simulate API call delay
    setTimeout(() => {
      this.messageService.add({ severity: 'success', summary: 'Hoàn tất', detail: 'Đã xuất file AuditLog.xlsx' });
      console.log('Called backend API to export Excel.');
    }, 1500);
  }
}
