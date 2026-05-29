import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule, HttpClientModule, ToastModule],
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
          <span class="bc-text">Báo cáo thống kê</span>
          <span class="bc-sep">/</span>
          <span class="bc-current">Xuất báo cáo</span>
        </div>

        <div class="edit-header">
          <h2 class="edit-title" style="color: #002D72;">Báo cáo Thống kê & Tổng hợp Dữ liệu</h2>
        </div>

        <p class="text-muted mb-4">
          Hệ thống hỗ trợ tự động tổng hợp, kết xuất báo cáo đo lường chất lượng số hóa, tỷ lệ xử lý OCR thành công, và tổng số tài liệu kỹ thuật của EVNHANOI đã được số hóa lưu trữ. Dữ liệu báo cáo được biên dịch trực tiếp thời gian thực dưới định dạng bảng tính Microsoft Excel.
        </p>

        <div class="p-4" style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; max-width: 500px;">
          <h3 class="m-0 mb-3 font-bold" style="font-size: 0.95rem; color: #002D72;">
            <i class="pi pi-file-excel mr-1"></i> Báo cáo tổng hợp số hóa toàn diện
          </h3>
          <p class="text-xs text-muted mb-4">
            Bao gồm thống kê chi tiết theo từng đơn vị quản lý điện lực (Đông Anh, Hà Đông, Hai Bà Trưng...), số lượng trang hồ sơ bản vẽ kỹ thuật, và tỷ lệ nhận dạng AI-OCR đạt CER >= 90%.
          </p>
          <div>
            <button class="btn-excel" (click)="exportExcel()" [disabled]="loading">
              <i class="pi pi-spin pi-spinner" *ngIf="loading"></i>
              <i class="pi pi-file-excel" *ngIf="!loading"></i>
              {{ loading ? 'Đang biên dịch báo cáo...' : 'Tải Xuất Báo cáo Excel' }}
            </button>
          </div>
        </div>

      </div>
    </div>
  `
})
export class ReportsComponent {
  loading = false;
  private http = inject(HttpClient);
  private messageService = inject(MessageService);
  private apiUrl = `${environment.apiGatewayUrl}/api/v1/reports/export`;

  exportExcel() {
    this.loading = true;
    this.messageService.add({
      severity: 'info',
      summary: 'Khởi động tiến trình',
      detail: 'Hệ thống đang truy vấn cơ sở dữ liệu để tổng hợp số liệu thống kê...'
    });

    this.http.get(this.apiUrl, { responseType: 'blob', observe: 'response' }).subscribe({
      next: (response) => {
        this.loading = false;
        
        let fileName = 'BaoCaoTongHopSoHoa_EVNHANOI.xlsx';
        const contentDisposition = response.headers.get('content-disposition');
        if (contentDisposition && contentDisposition.indexOf('attachment') !== -1) {
          const matches = /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/.exec(contentDisposition);
          if (matches != null && matches[1]) { 
            fileName = matches[1].replace(/['"]/g, '');
          }
        }
        
        const blob = new Blob([response.body as Blob], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        
        window.URL.revokeObjectURL(url);
        document.body.removeChild(a);
        
        this.messageService.add({severity:'success', summary:'Thành công', detail:'Đã kết xuất báo cáo Excel thành công!'});
      },
      error: (err) => {
        this.loading = false;
        console.error('Export excel error', err);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể kết xuất báo cáo từ máy chủ. Vui lòng thử lại sau.'
        });
      }
    });
  }
}
