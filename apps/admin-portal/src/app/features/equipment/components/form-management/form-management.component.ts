import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { RouterLink } from '@angular/router';

interface EavForm {
  id: string;
  name: string;
  version: string;
  isActive: boolean;
  updatedAt: string;
}

@Component({
  selector: 'app-form-management',
  standalone: true,
  imports: [CommonModule, TableModule, ButtonModule, CardModule, TagModule, ToastModule, RouterLink],
  providers: [MessageService],
  template: `
    <div class="p-4">
      <p-toast></p-toast>
      <p-card header="Quản lý Biểu mẫu động (EAV Forms)">
        <div class="flex justify-content-end mb-3">
          <p-button label="Tạo biểu mẫu mới" icon="pi pi-plus" routerLink="../form-builder"></p-button>
        </div>

        <p-table [value]="forms" [paginator]="true" [rows]="10" styleClass="p-datatable-striped">
          <ng-template pTemplate="header">
            <tr>
              <th>ID</th>
              <th>Tên Biểu mẫu</th>
              <th>Phiên bản (Version)</th>
              <th>Trạng thái</th>
              <th>Cập nhật lần cuối</th>
              <th>Thao tác</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-form>
            <tr>
              <td>{{ form.id }}</td>
              <td>{{ form.name }}</td>
              <td>{{ form.version }}</td>
              <td>
                <p-tag [value]="form.isActive ? 'Đang kích hoạt' : 'Vô hiệu hóa'" [severity]="form.isActive ? 'success' : 'danger'"></p-tag>
              </td>
              <td>{{ form.updatedAt }}</td>
              <td>
                <div class="flex gap-2">
                  <p-button icon="pi pi-pencil" [text]="true" [rounded]="true" severity="info" pTooltip="Chỉnh sửa"></p-button>
                  <p-button [icon]="form.isActive ? 'pi pi-times-circle' : 'pi pi-check-circle'" 
                            [severity]="form.isActive ? 'danger' : 'success'" 
                            [text]="true" [rounded]="true" 
                            (onClick)="toggleStatus(form)"
                            [pTooltip]="form.isActive ? 'Vô hiệu hóa' : 'Kích hoạt'">
                  </p-button>
                </div>
              </td>
            </tr>
          </ng-template>
        </p-table>
      </p-card>
    </div>
  `
})
export class FormManagementComponent implements OnInit {
  forms: EavForm[] = [
    { id: 'F-001', name: 'Biểu mẫu Nhập liệu Máy biến áp', version: 'v1.2', isActive: true, updatedAt: '2026-05-27' },
    { id: 'F-002', name: 'Biểu mẫu Kiểm tra Cột điện', version: 'v2.0', isActive: false, updatedAt: '2026-05-20' },
    { id: 'F-003', name: 'Biểu mẫu Báo cáo sự cố', version: 'v1.0', isActive: true, updatedAt: '2026-05-28' }
  ];

  constructor(private messageService: MessageService) {}

  ngOnInit() {}

  toggleStatus(form: EavForm) {
    form.isActive = !form.isActive;
    const action = form.isActive ? 'Kích hoạt' : 'Vô hiệu hóa';
    this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã ' + action.toLowerCase() + ' biểu mẫu: ' + form.name });
  }
}
