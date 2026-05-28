import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-borrow-return',
  standalone: true,
  imports: [CommonModule, TableModule, ButtonModule, CardModule, ToastModule, DialogModule, InputTextModule, FormsModule],
  providers: [MessageService],
  template: `
    <div class="p-4">
      <p-toast></p-toast>
      <p-card header="Quản lý Mượn/Trả hồ sơ">
        <p-table [value]="requests" [paginator]="true" [rows]="10" [loading]="loading">
          <ng-template pTemplate="header">
            <tr>
              <th>ID</th>
              <th>Người yêu cầu</th>
              <th>Hồ sơ</th>
              <th>Ngày tạo</th>
              <th>Trạng thái</th>
              <th>Thao tác</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-req>
            <tr>
              <td>{{req.id}}</td>
              <td>{{req.requester}}</td>
              <td>{{req.recordName}}</td>
              <td>{{req.createdAt | date:'dd/MM/yyyy'}}</td>
              <td>{{req.status}}</td>
              <td>
                <p-button icon="pi pi-check" severity="success" [rounded]="true" [text]="true" title="Duyệt" (onClick)="updateStatus(req.id, 'APPROVED')" *ngIf="req.status === 'PENDING'"></p-button>
                <p-button icon="pi pi-times" severity="danger" [rounded]="true" [text]="true" title="Từ chối" (onClick)="updateStatus(req.id, 'REJECTED')" *ngIf="req.status === 'PENDING'"></p-button>
              </td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="6" class="text-center">Không có yêu cầu mượn/trả nào.</td>
            </tr>
          </ng-template>
        </p-table>
      </p-card>
    </div>
  `
})
export class BorrowReturnComponent implements OnInit {
  requests: any[] = [];
  loading = false;
  private http = inject(HttpClient);
  private messageService = inject(MessageService);
  private apiUrl = 'http://localhost:5000/api/v1/workflow/requests'; // Mock URL

  ngOnInit() {
    this.loadRequests();
  }

  loadRequests() {
    this.loading = true;
    this.http.get<any[]>(this.apiUrl).subscribe({
      next: (data) => {
        this.requests = data || [];
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading requests', err);
        this.messageService.add({severity:'error', summary: 'Lỗi', detail: 'Không thể tải danh sách mượn/trả'});
        this.loading = false;
      }
    });
  }

  updateStatus(id: number, status: string) {
    this.http.put(`${this.apiUrl}/${id}/status`, { status }).subscribe({
      next: () => {
        this.messageService.add({severity:'success', summary: 'Thành công', detail: `Đã cập nhật trạng thái yêu cầu thành ${status}`});
        this.loadRequests();
      },
      error: (err) => {
        console.error('Error updating status', err);
        this.messageService.add({severity:'error', summary: 'Lỗi', detail: 'Không thể cập nhật trạng thái'});
      }
    });
  }
}
