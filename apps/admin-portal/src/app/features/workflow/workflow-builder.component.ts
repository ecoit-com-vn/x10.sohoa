import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OrderList } from 'primeng/orderlist';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { FormsModule } from '@angular/forms';

interface WorkflowStep {
  id: string;
  name: string;
  role: string;
}

@Component({
  selector: 'app-workflow-builder',
  standalone: true,
  imports: [CommonModule, OrderList, ButtonModule, CardModule, ToastModule, FormsModule],
  providers: [MessageService],
  template: `
    <div class="p-4">
      <p-toast></p-toast>
      <p-card header="Workflow Builder">
        <p class="mb-4 text-secondary">
          Kéo thả để sắp xếp thứ tự các bước duyệt trong quy trình.
        </p>

        <p-orderList [value]="steps" [listStyle]="{ 'max-height': '30rem' }" header="Các bước duyệt" filterBy="name" filterPlaceholder="Lọc theo tên..." [dragdrop]="true">
          <ng-template let-step pTemplate="item">
            <div class="flex align-items-center justify-content-between w-full p-2">
              <div class="flex align-items-center gap-3">
                <i class="pi pi-bars cursor-move text-gray-500"></i>
                <div>
                  <div class="font-bold mb-1">{{ step.name }}</div>
                  <div class="text-sm text-gray-600">Vai trò: {{ step.role }}</div>
                </div>
              </div>
              <p-button icon="pi pi-pencil" [text]="true" [rounded]="true" severity="info"></p-button>
            </div>
          </ng-template>
        </p-orderList>

        <div class="flex justify-content-end mt-4">
          <p-button label="Lưu quy trình" icon="pi pi-save" (onClick)="saveWorkflow()"></p-button>
        </div>
      </p-card>
    </div>
  `
})
export class WorkflowBuilderComponent {
  steps: WorkflowStep[] = [
    { id: '1', name: 'Nhân viên đề xuất', role: 'Nhân viên' },
    { id: '2', name: 'Trưởng phòng xem xét', role: 'Trưởng phòng' },
    { id: '3', name: 'Ban Giám đốc phê duyệt', role: 'Giám đốc' },
    { id: '4', name: 'Lưu trữ hồ sơ', role: 'Văn thư' }
  ];

  constructor(private messageService: MessageService) {}

  saveWorkflow() {
    console.log('Saved steps order:', this.steps);
    this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu cấu hình quy trình duyệt!' });
  }
}
