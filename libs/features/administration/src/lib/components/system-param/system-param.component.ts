import { Component, OnInit } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { environment } from '@env/environment';
import { AuthService } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-system-param',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './system-param.component.html'
})
export class SystemParam implements OnInit {
  params: any[] = [];
  displayDialog = false;
  dialogHeader = '';
  currentParam: any = {};

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/system-params`;

  constructor(
    private http: HttpClient,
    private messageService: MessageService,
    public authService: AuthService
  ) {}

  ngOnInit() {
    this.loadParams();
  }

  loadParams() {
    this.http.get<any[]>(this.apiUrl).subscribe({
      next: (data) => {
        this.params = data;
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải cấu hình tham số hệ thống.' });
      }
    });
  }

  onEdit(param: any) {
    this.currentParam = { ...param };
    this.dialogHeader = `Chỉnh sửa cấu hình: ${param.paramKey}`;
    this.displayDialog = true;
  }

  onSaveParam() {
    if (!this.currentParam.paramValue) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập giá trị tham số.' });
      return;
    }

    this.http.put(`${this.apiUrl}/${this.currentParam.paramKey}`, this.currentParam).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Thay đổi tham số cấu hình thành công!' });
        this.loadParams();
        this.displayDialog = false;
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Cập nhật tham số hệ thống thất bại.' });
      }
    });
  }
}
