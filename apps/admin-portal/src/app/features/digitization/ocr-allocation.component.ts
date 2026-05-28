import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { Select } from 'primeng/select';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { FormsModule } from '@angular/forms';
import { TagModule } from 'primeng/tag';

interface DocumentTask {
  id: string;
  name: string;
  pages: number;
  status: string;
  assignee: string | null;
}

@Component({
  selector: 'app-ocr-allocation',
  standalone: true,
  imports: [CommonModule, TableModule, ButtonModule, CardModule, Select, ToastModule, FormsModule, TagModule],
  providers: [MessageService],
  template: `
    <div class="p-4">
      <p-toast></p-toast>
      <p-card header="Phân bổ công việc nhập liệu OCR">
        <p class="mb-4 text-secondary">
          Quản lý và phân công các tài liệu cần nhận dạng ký tự quang học (OCR) hoặc sửa lỗi sau OCR cho các nhân viên.
        </p>

        <p-table [value]="tasks" [paginator]="true" [rows]="5" styleClass="p-datatable-gridlines">
          <ng-template pTemplate="header">
            <tr>
              <th>Mã Tài liệu</th>
              <th>Tên Tài liệu</th>
              <th>Số trang</th>
              <th>Trạng thái</th>
              <th>Người phụ trách</th>
              <th>Thao tác</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-task>
            <tr>
              <td>{{ task.id }}</td>
              <td>{{ task.name }}</td>
              <td>{{ task.pages }}</td>
              <td>
                <p-tag [value]="task.status" [severity]="getStatusSeverity(task.status)"></p-tag>
              </td>
              <td>
                <p-select [options]="users" [(ngModel)]="task.assignee" optionLabel="name" optionValue="code" placeholder="Chọn nhân viên" appendTo="body" styleClass="w-full"></p-select>
              </td>
              <td>
                <p-button label="Lưu" icon="pi pi-check" size="small" (onClick)="assignTask(task)"></p-button>
              </td>
            </tr>
          </ng-template>
        </p-table>
      </p-card>
    </div>
  `
})
export class OcrAllocationComponent implements OnInit {
  tasks: DocumentTask[] = [
    { id: 'DOC-001', name: 'Hợp đồng mua bán thiết bị', pages: 12, status: 'Chờ phân công', assignee: null },
    { id: 'DOC-002', name: 'Biên bản nghiệm thu', pages: 5, status: 'Đang xử lý', assignee: 'user2' },
    { id: 'DOC-003', name: 'Hồ sơ thiết kế kỹ thuật', pages: 45, status: 'Chờ phân công', assignee: null },
    { id: 'DOC-004', name: 'Báo cáo kiểm định', pages: 8, status: 'Hoàn thành', assignee: 'user1' }
  ];

  users = [
    { name: 'Nguyễn Văn A', code: 'user1' },
    { name: 'Trần Thị B', code: 'user2' },
    { name: 'Lê Văn C', code: 'user3' }
  ];

  constructor(private messageService: MessageService) {}

  ngOnInit() {}

  getStatusSeverity(status: string) {
    switch (status) {
      case 'Hoàn thành': return 'success';
      case 'Đang xử lý': return 'warning';
      case 'Chờ phân công': return 'danger';
      default: return 'info';
    }
  }

  assignTask(task: DocumentTask) {
    if (task.assignee) {
      task.status = 'Đang xử lý';
      this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã phân công tài liệu ' + task.id });
    } else {
      this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Vui lòng chọn người phụ trách' });
    }
  }
}
