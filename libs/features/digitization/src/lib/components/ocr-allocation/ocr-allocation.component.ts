import { Component, OnInit, inject, signal, computed, effect } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { HttpClient } from '@angular/common/http';
import { DigitizationTaskService } from '@sohoa.frontend/shared/core';
import { environment } from '@env/environment';
import { finalize } from 'rxjs';

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
  imports: [CommonModule, FormsModule, ToastModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './ocr-allocation.component.html',
  styleUrl: './ocr-allocation.component.scss'
})
export class OcrAllocationComponent implements OnInit {
  tasks = signal<DocumentTask[]>([]);
  loading = signal<boolean>(false);
  users = signal<any[]>([]);
  
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);
  totalCount = signal<number>(0);
  searchKeyword = signal<string>('');

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
        const userList = Array.isArray(data) ? data : (data && Array.isArray((data as any).items) ? (data as any).items : (data && Array.isArray((data as any).value) ? (data as any).value : []));
        this.users.set(userList.map((u: any) => ({
          name: `${u?.fullName || ''} (${u?.username || ''})`,
          code: u?.username || ''
        })));
        if (this.users().length === 0) {
          this.users.set([
            { name: 'Quản trị viên (Admin)', code: 'admin' }
          ]);
        }
      },
      error: () => {
        this.users.set([
          { name: 'Quản trị viên (Admin)', code: 'admin' }
        ]);
      }
    });
  }

  constructor() {
    effect(() => {
      const kw = this.searchKeyword();
      this.currentPage.set(1);
    }, { allowSignalWrites: true });

    effect(() => {
      const page = this.currentPage();
      const size = this.pageSize();
      const kw = this.searchKeyword();
      this.loadTasks();
    }, { allowSignalWrites: true });
  }

  loadTasks() {
    this.loading.set(true);
    this.digitizationTaskService.getTasks(this.currentPage(), this.pageSize(), this.searchKeyword())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          const list = res?.items || [];
          this.tasks.set(list.map((item: any) => ({
            id: item.id,
            dossierId: item.dossierId,
            name: `Hồ sơ bản vẽ kỹ thuật thiết bị đường dây #${item.dossierId}`,
            pages: Math.floor(Math.random() * 30) + 5, // Simulated page count
            status: this.mapBackendStatus(item.status, item.assignedToUserId),
            assignee: item.assignedToUserId || null
          })));
          this.totalCount.set(res?.totalCount || 0);
        },
        error: (err) => {
          console.error('Error loading digitization tasks', err);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể tải danh sách công việc phân bổ từ máy chủ.'
          });
          this.tasks.set([]);
          this.totalCount.set(0);
        }
      });
  }

  mapBackendStatus(status: string, assignee: string | null): string {
    if (!assignee) return 'Chờ phân công';
    if (status === 'Pending') return 'Đang xử lý';
    if (status === 'Completed') return 'Hoàn thành';
    return status;
  }

  paginatedTasks = computed(() => {
    return this.tasks();
  });

  totalPages = computed(() => {
    return Math.ceil(this.totalCount() / this.pageSize());
  });

  nextPage() {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update(p => p + 1);
    }
  }

  prevPage() {
    if (this.currentPage() > 1) {
      this.currentPage.update(p => p - 1);
    }
  }

  goToPage(page: any) {
    const p = Number(page);
    if (p >= 1 && p <= this.totalPages()) {
      this.currentPage.set(p);
    }
  }

  onPageSizeChange(event: Event) {
    const target = event.target as HTMLSelectElement;
    this.pageSize.set(Number(target.value));
    this.currentPage.set(1);
  }

  assignTask(task: DocumentTask) {
    if (task.assignee) {
      this.digitizationTaskService.assignTask(task.dossierId, task.assignee, 'Phân công qua giao diện điều phối').subscribe({
        next: () => {
          const uName = this.users().find(u => u.code === task.assignee)?.name;
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
    const currentTasks = this.tasks();
    const unassignedTasks = currentTasks.filter(t => !t.assignee);
    const activeUsers = this.users();
    
    if (unassignedTasks.length === 0) {
      this.messageService.add({ severity: 'info', summary: 'Thông báo', detail: 'Tất cả công việc đã được phân bổ.' });
      return;
    }

    unassignedTasks.forEach((task, idx) => {
      unassignedCount++;
      const targetUser = activeUsers[idx % activeUsers.length].code;
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
