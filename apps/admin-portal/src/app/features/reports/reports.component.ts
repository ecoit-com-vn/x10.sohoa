import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule, CardModule, ButtonModule, ToastModule],
  providers: [MessageService],
  template: `
    <div class="p-4">
      <p-toast></p-toast>
      <p-card header="Báo cáo thống kê">
        <div class="flex flex-column gap-3">
          <p>Tải báo cáo thống kê dưới dạng file Excel từ hệ thống.</p>
          <div>
            <p-button label="Xuất Excel Báo cáo Hồ sơ" icon="pi pi-file-excel" (onClick)="exportExcel()" [loading]="loading"></p-button>
          </div>
        </div>
      </p-card>
    </div>
  `
})
export class ReportsComponent {
  loading = false;
  private http = inject(HttpClient);
  private messageService = inject(MessageService);
  private apiUrl = 'http://localhost:5000/api/v1/reports/export'; // Mock URL

  exportExcel() {
    this.loading = true;
    this.http.get(this.apiUrl, { responseType: 'blob', observe: 'response' }).subscribe({
      next: (response) => {
        this.loading = false;
        
        // Extract filename from Content-Disposition header if available
        let fileName = 'baocao.xlsx';
        const contentDisposition = response.headers.get('content-disposition');
        if (contentDisposition && contentDisposition.indexOf('attachment') !== -1) {
          const matches = /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/.exec(contentDisposition);
          if (matches != null && matches[1]) { 
            fileName = matches[1].replace(/['"]/g, '');
          }
        }
        
        // Download blob
        const blob = new Blob([response.body as Blob], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        
        window.URL.revokeObjectURL(url);
        document.body.removeChild(a);
        
        this.messageService.add({severity:'success', summary:'Thành công', detail:'Đã xuất file báo cáo'});
      },
      error: (err) => {
        this.loading = false;
        console.error('Export report error', err);
        this.messageService.add({severity:'error', summary:'Lỗi', detail:'Không thể xuất báo cáo Excel'});
      }
    });
  }
}
