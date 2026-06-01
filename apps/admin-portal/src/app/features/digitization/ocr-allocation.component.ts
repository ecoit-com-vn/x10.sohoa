import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { HttpClient } from '@angular/common/http';
import { DigitizationTaskService } from '../../core/services/digitization-task.service';
import { environment } from '../../../environments/environment';

interface DocumentTask {
  id: string;
  dossierId: string;
  name: string;
  pages: number;
  status: string;
  assignee: string | null;
}

@Component({
  selector: 'app-ocr-allocation',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule],
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
          <span class="bc-text">Số hóa dữ liệu</span>
          <span class="bc-sep">/</span>
          <span class="bc-current">Phân bổ nhập liệu OCR</span>
        </div>

        <div class="edit-header">
          <h2 class="edit-title">Phân bổ công việc nhập liệu OCR</h2>
          <div class="edit-actions">
            <button class="btn-green" (click)="onAutoAssign()" [disabled]="loading">
              <i class="pi pi-bolt"></i> Tự động phân bổ
            </button>
          </div>
        </div>

        <p class="text-muted mb-4">
          Quản lý và phân công các tài liệu cần nhận dạng ký tự quang học (OCR) hoặc hiệu đính kết quả sau OCR cho các kiểm soát viên và nhân viên kỹ thuật.
        </p>

        <!-- Table -->
        <div class="wf-table-wrap">
          <table class="wf-table">
            <thead>
              <tr>
                <th style="width: 120px;">Mã tài liệu</th>
                <th>Tên tài liệu số hóa</th>
                <th style="width: 100px; text-align: center;">Số trang</th>
                <th class="col-tt">Trạng thái</th>
                <th style="width: 250px;">Người phụ trách</th>
                <th class="col-hd">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <!-- skeleton loading rows -->
              <ng-container *ngIf="loading">
                <tr *ngFor="let item of [1, 2, 3]">
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 80px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 250px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 40px; border-radius: 4px; margin: auto;"></div></td>
                  <td class="col-tt"><div class="skeleton-shimmer" style="height: 24px; width: 100px; border-radius: 12px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 32px; width: 100%; border-radius: 4px;"></div></td>
                  <td class="col-hd"><div class="skeleton-shimmer" style="height: 32px; width: 60px; border-radius: 4px;"></div></td>
                </tr>
              </ng-container>

              <ng-container *ngIf="!loading">
                <tr *ngFor="let task of paginatedTasks; let i = index">
                  <td><code class="text-muted">{{ task.dossierId }}</code></td>
                  <td><b class="wf-name-link" (click)="viewDoc(task)">{{ task.name }}</b></td>
                  <td style="text-align: center;">{{ task.pages }}</td>
                  <td class="col-tt">
                    <span class="status-pill"
                      [class.status-inactive]="task.status === 'Chờ phân công'"
                      [class.status-pending]="task.status === 'Đang xử lý'"
                      [class.status-active]="task.status === 'Hoàn thành'">
                      <i class="pi pi-clock"></i>
                      {{ task.status }}
                    </span>
                  </td>
                  <td>
                    <select class="wf-select w-full" [(ngModel)]="task.assignee" style="height: 32px; padding: 2px 8px;" [disabled]="task.status === 'Hoàn thành'">
                      <option [value]="null" disabled>-- Chọn người phụ trách --</option>
                      <option *ngFor="let u of users" [value]="u.code">{{ u.name }}</option>
                    </select>
                  </td>
                  <td class="col-hd">
                    <button class="btn-tim btn-small" (click)="assignTask(task)" style="height: 32px; padding: 0 10px;" [disabled]="task.status === 'Hoàn thành'">
                      <i class="pi pi-check"></i> Lưu
                    </button>
                  </td>
                </tr>
                <tr *ngIf="tasks.length === 0">
                  <td colspan="6" class="empty-row">
                    <i class="pi pi-inbox"></i>
                    <div>Chưa có tài liệu nào cần phân công.</div>
                  </td>
                </tr>
              </ng-container>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer" *ngIf="tasks.length > 0 && !loading">
          <span class="record-count">Tổng số: <b>{{ tasks.length }}</b> bản ghi.</span>
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
export class OcrAllocationComponent implements OnInit {
  tasks: DocumentTask[] = [];
  loading = false;

  users: any[] = [];
  
  currentPage = 1;
  pageSize = 10;

  private digitizationTaskService = inject(DigitizationTaskService);
  private messageService = inject(MessageService);
  private http = inject(HttpClient);

  ngOnInit() {
    this.loadUsers();
    this.loadTasks();
  }

  loadUsers() {
    this.http.get<any[]>(`${environment.apiGatewayUrl}/api/v1/users`).subscribe({
      next: (data) => {
        this.users = data.map(u => ({
          name: `${u.fullName} (${u.username})`,
          code: u.username
        }));
        if (this.users.length === 0) {
          this.users = [
            { name: 'Quản trị viên (Admin)', code: 'admin' }
          ];
        }
      },
      error: () => {
        this.users = [
          { name: 'Quản trị viên (Admin)', code: 'admin' }
        ];
      }
    });
  }

  loadTasks() {
    this.loading = true;
    this.digitizationTaskService.getTasks().subscribe({
      next: (data) => {
        this.tasks = (data || []).map(item => ({
          id: item.id,
          dossierId: item.dossierId,
          name: `Hồ sơ bản vẽ kỹ thuật thiết bị đường dây #${item.dossierId}`,
          pages: Math.floor(Math.random() * 30) + 5, // Simulated page count
          status: this.mapBackendStatus(item.status, item.assignedToUserId),
          assignee: item.assignedToUserId || null
        }));
        this.loading = false;
        this.currentPage = 1;
      },
      error: (err) => {
        console.error('Error loading digitization tasks', err);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải danh sách công việc phân bổ từ máy chủ.'
        });
        this.loading = false;
      }
    });
  }

  mapBackendStatus(status: string, assignee: string | null): string {
    if (!assignee) return 'Chờ phân công';
    if (status === 'Pending') return 'Đang xử lý';
    if (status === 'Completed') return 'Hoàn thành';
    return status;
  }

  get paginatedTasks(): DocumentTask[] {
    const startIndex = (this.currentPage - 1) * this.pageSize;
    return this.tasks.slice(startIndex, startIndex + this.pageSize);
  }

  get totalPages(): number {
    return Math.ceil(this.tasks.length / this.pageSize);
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

  assignTask(task: DocumentTask) {
    if (task.assignee) {
      this.digitizationTaskService.assignTask(task.dossierId, task.assignee, 'Phân công qua giao diện điều phối').subscribe({
        next: () => {
          const uName = this.users.find(u => u.code === task.assignee)?.name;
          this.messageService.add({ 
            severity: 'success', 
            summary: 'Thành công', 
            detail: `Đã phân công tài liệu ${task.dossierId} cho ${uName}` 
          });
          this.loadTasks();
        },
        error: (err) => {
          console.error('Error assigning task', err);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể thực hiện phân công tác vụ trên hệ thống.'
          });
        }
      });
    } else {
      this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Vui lòng chọn người phụ trách!' });
    }
  }

  onAutoAssign() {
    let unassignedCount = 0;
    const unassignedTasks = this.tasks.filter(t => !t.assignee);
    
    if (unassignedTasks.length === 0) {
      this.messageService.add({ severity: 'info', summary: 'Thông báo', detail: 'Tất cả công việc đã được phân bổ.' });
      return;
    }

    unassignedTasks.forEach((task, idx) => {
      unassignedCount++;
      const targetUser = this.users[idx % this.users.length].code;
      this.digitizationTaskService.assignTask(task.dossierId, targetUser, 'Tự động phân bổ hệ thống').subscribe({
        next: () => {
          unassignedCount--;
          if (unassignedCount === 0) {
            this.messageService.add({ severity: 'success', summary: 'Hoàn tất', detail: 'Đã tự động phân bổ công việc công bằng!' });
            this.loadTasks();
          }
        },
        error: () => {
          unassignedCount--;
          if (unassignedCount === 0) {
            this.loadTasks();
          }
        }
      });
    });
  }

  viewDoc(task: DocumentTask) {
    this.messageService.add({
      severity: 'info',
      summary: 'Xem chi tiết',
      detail: `Đang mở tài liệu: ${task.name}`
    });
  }
}
