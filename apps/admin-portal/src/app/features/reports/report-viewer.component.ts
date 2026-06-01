// E:\ecoit\sohoax10\sohoa.frontend\apps\admin-portal\src\app\features\reports\report-viewer.component.ts
import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { environment } from '../../../environments/environment';
import { ReportGroup } from './report-group-manager.component';
import { DynamicReport } from './report-designer.component';

interface FilterParam {
  name: string;
  type: string;
  label: string;
  options?: { label: string; value: any }[];
}

@Component({
  selector: 'app-report-viewer',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule
  ],
  template: `
    <div class="grid grid-cols-4 gap-6">
      
      <!-- Cột trái: Danh sách báo cáo phân nhóm -->
      <div class="col-span-1 border-r pr-4 border-gray-200" style="border-right: 1px solid #e2e8f0;">
        <h4 class="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3">Danh mục Báo cáo</h4>
        
        <div class="space-y-4">
          <div *ngFor="let group of groups">
            <div class="font-bold text-gray-700 flex items-center mb-1 text-xs bg-gray-100 p-2 rounded" style="background: #f1f5f9;">
              <i class="pi pi-folder mr-2" style="color: #002D72;"></i>
              <span>{{ group.name }}</span>
            </div>
            
            <ul class="pl-4 space-y-1 list-none" style="margin: 0; padding-left: 10px;">
              <li *ngFor="let r of group.dynamicReports">
                <a 
                  href="javascript:void(0)" 
                  class="block p-2 text-xs rounded hover:bg-blue-50 transition-colors"
                  [ngClass]="{'bg-blue-100 text-blue-800 font-semibold border-l-4 border-blue-600': selectedReport?.id === r.id, 'text-gray-600': selectedReport?.id !== r.id}"
                  (click)="selectReport(r)"
                >
                  <i class="pi pi-file mr-1"></i>
                  {{ r.name }}
                </a>
              </li>
              <li *ngIf="!group.dynamicReports || group.dynamicReports.length === 0" class="text-xs text-gray-400 italic pl-4 py-1">
                Không có báo cáo
              </li>
            </ul>
          </div>
          <div *ngIf="groups.length === 0" class="text-xs text-gray-400 italic">
            Không có dữ liệu nhóm báo cáo
          </div>
        </div>
      </div>

      <!-- Cột phải: Bộ lọc, Dữ liệu và Xuất Excel -->
      <div class="col-span-3">
        <div *ngIf="!selectedReport" class="flex flex-col items-center justify-center h-64 text-gray-400 border-2 border-dashed border-gray-200 rounded-lg" style="border: 2px dashed #e2e8f0; border-radius: 8px;">
          <i class="pi pi-chart-bar text-5xl mb-3" style="color: #002D72; opacity: 0.3; font-size: 3rem;"></i>
          <p class="text-sm">Vui lòng lựa chọn một báo cáo từ danh sách bên trái để bắt đầu.</p>
        </div>

        <div *ngIf="selectedReport" class="space-y-6" style="display: flex; flex-direction: column; gap: 20px;">
          <!-- Tiêu đề báo cáo -->
          <div class="border-b pb-3" style="border-bottom: 1px solid #e2e8f0; padding-bottom: 10px;">
            <h3 class="text-xl font-bold" style="color: #002D72; margin: 0;">{{ selectedReport.name }}</h3>
            <p class="text-xs text-gray-500 mt-1" *ngIf="selectedReport.allowedRoles" style="margin: 4px 0 0 0;">Vai trò được xem: {{ selectedReport.allowedRoles }}</p>
          </div>

          <!-- Bộ lọc động -->
          <div class="bg-gray-50 p-4 border border-gray-200 rounded-lg" *ngIf="parsedParams.length > 0" style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 15px;">
            <h4 class="text-xs font-bold text-gray-700 uppercase tracking-wider mb-3" style="margin: 0 0 10px 0;"><i class="pi pi-filter mr-1"></i> Bộ lọc báo cáo</h4>
            <div style="display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 14px;">
              <div *ngFor="let param of parsedParams">
                <label class="block text-xs font-bold mb-1 text-gray-600" style="display: block; margin-bottom: 4px; font-weight: 600;">{{ param.label }}</label>
                
                <!-- Text Parameter -->
                <input 
                  *ngIf="param.type === 'text'" 
                  type="text" 
                  [(ngModel)]="filterValues[param.name]" 
                  class="wf-input w-full"
                  placeholder="Nhập giá trị..."
                />

                <!-- Number Parameter -->
                <input 
                  *ngIf="param.type === 'number'" 
                  type="number" 
                  [(ngModel)]="filterValues[param.name]" 
                  class="wf-input w-full"
                  placeholder="Nhập số..."
                />

                <!-- Date Parameter -->
                <input 
                  *ngIf="param.type === 'date'" 
                  type="date" 
                  [(ngModel)]="filterValues[param.name]" 
                  class="wf-input w-full"
                />

                <!-- Dropdown Parameter -->
                <select 
                  *ngIf="param.type === 'dropdown'" 
                  [(ngModel)]="filterValues[param.name]"
                  class="wf-input w-full bg-white"
                  style="height: 38px;"
                >
                  <option [ngValue]="null">-- Tất cả --</option>
                  <option *ngFor="let opt of param.options" [ngValue]="opt.value">{{ opt.label }}</option>
                </select>
              </div>
            </div>
          </div>

          <!-- Nút hành động -->
          <div class="flex gap-2" style="display: flex; gap: 8px;">
            <button 
              class="btn-tim"
              [disabled]="loading"
              (click)="executeReport()"
              style="height: 36px; padding: 0 16px;"
            >
              <i class="pi pi-search mr-1"></i> Xem Báo Cáo
            </button>
            
            <button 
              class="btn-excel"
              [disabled]="loading"
              (click)="exportExcel()"
              style="height: 36px; padding: 0 16px; display: flex; align-items: center; justify-content: center; gap: 4px;"
            >
              <i class="pi pi-file-excel"></i> Xuất Excel
            </button>
          </div>

          <!-- Spinner Loading -->
          <div *ngIf="loading" class="flex flex-col items-center justify-center py-12" style="display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 40px 0;">
            <i class="pi pi-spin pi-spinner text-3xl text-blue-600 mb-2" style="font-size: 2rem; color: #002D72;"></i>
            <span class="text-xs text-gray-500">Đang tổng hợp dữ liệu báo cáo...</span>
          </div>

          <!-- Grid hiển thị kết quả -->
          <div *ngIf="!loading && hasExecuted">
            <div *ngIf="reportData.length === 0" class="empty-row text-center p-8 bg-gray-50 border border-gray-100 rounded-lg text-gray-500 text-sm" style="background: #f8fafc; border: 1px solid #f1f5f9; border-radius: 8px; padding: 30px; text-align: center;">
              Không có dữ liệu phù hợp với điều kiện lọc.
            </div>

            <div *ngIf="reportData.length > 0" class="space-y-2">
              <div class="flex justify-between items-center mb-2">
                <span class="text-xs text-gray-500 font-semibold">Tìm thấy <b>{{ reportData.length }}</b> bản ghi</span>
              </div>
              
              <div class="wf-table-wrap" style="max-height: 500px; overflow-y: auto;">
                <table class="wf-table">
                  <thead>
                    <tr>
                      <th *ngFor="let col of colHeaders">{{ col }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr *ngFor="let row of reportData">
                      <td *ngFor="let col of colHeaders">{{ row[col] }}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class ReportViewerComponent implements OnInit {
  groups: ReportGroup[] = [];
  selectedReport: DynamicReport | null = null;
  parsedParams: FilterParam[] = [];
  filterValues: { [key: string]: any } = {};

  loading = false;
  hasExecuted = false;
  reportData: any[] = [];
  colHeaders: string[] = [];

  private http = inject(HttpClient);
  private messageService = inject(MessageService);
  private groupsApiUrl = `${environment.apiGatewayUrl}/api/v1/report-groups`;
  private executeApiUrl = `${environment.apiGatewayUrl}/api/v1/reports/execute`;
  private exportApiUrl = `${environment.apiGatewayUrl}/api/v1/reports/export`;

  ngOnInit(): void {
    this.loadReportGroups();
  }

  loadReportGroups() {
    this.http.get<ReportGroup[]>(this.groupsApiUrl).subscribe({
      next: (data) => {
        this.groups = data.map(g => ({
          ...g,
          dynamicReports: g.dynamicReports?.filter((r: any) => r.isActive) || []
        }));
      },
      error: (err) => {
        console.error(err);
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách báo cáo' });
      }
    });
  }

  selectReport(report: DynamicReport) {
    this.selectedReport = report;
    this.hasExecuted = false;
    this.reportData = [];
    this.colHeaders = [];
    this.filterValues = {};
    
    this.parsedParams = [];
    if (report.parametersJson) {
      try {
        this.parsedParams = JSON.parse(report.parametersJson);
        this.parsedParams.forEach(p => {
          this.filterValues[p.name] = null;
        });
      } catch (e) {
        console.error('Failed to parse parameters JSON', e);
      }
    }
  }

  executeReport() {
    if (!this.selectedReport) return;

    this.loading = true;
    this.hasExecuted = true;
    
    const body = {
      parameters: this.filterValues
    };

    this.http.post<any[]>(`${this.executeApiUrl}/${this.selectedReport.id}`, body).subscribe({
      next: (data) => {
        this.reportData = data;
        this.loading = false;
        if (data.length > 0) {
          this.colHeaders = Object.keys(data[0]);
        } else {
          this.colHeaders = [];
        }
      },
      error: (err) => {
        this.loading = false;
        console.error(err);
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Thực thi báo cáo thất bại. Vui lòng kiểm tra lại cấu hình SQL hoặc tham số.' });
      }
    });
  }

  exportExcel() {
    if (!this.selectedReport) return;

    this.loading = true;
    
    const body = {
      parameters: this.filterValues
    };

    this.http.post(`${this.exportApiUrl}/${this.selectedReport.id}`, body, { responseType: 'blob', observe: 'response' }).subscribe({
      next: (response) => {
        this.loading = false;
        
        let fileName = `${this.selectedReport?.name.replace(/ /g, '_')}_EVNHANOI.xlsx`;
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
        
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xuất dữ liệu Excel báo cáo thành công!' });
      },
      error: (err) => {
        this.loading = false;
        console.error(err);
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Xuất Excel thất bại. Vui lòng thử lại sau.' });
      }
    });
  }
}
