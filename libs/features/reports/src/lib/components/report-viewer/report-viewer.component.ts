// E:\ecoit\sohoax10\sohoa.frontend\apps\admin-portal\src\app\features\reports\report-viewer.component.ts
import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { environment } from '@env/environment';
import {  ReportGroup  } from '../report-group-manager/report-group-manager.component';
import {  DynamicReport  } from '../report-designer/report-designer.component';

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
  templateUrl: './report-viewer.component.html'
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
