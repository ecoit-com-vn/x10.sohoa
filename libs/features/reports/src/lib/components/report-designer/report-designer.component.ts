// E:\ecoit\sohoax10\sohoa.frontend\apps\admin-portal\src\app\features\reports\report-designer.component.ts
import { Component, OnInit, Output, EventEmitter, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { environment } from '@env/environment';
import {  ReportGroup  } from '../report-group-manager/report-group-manager.component';

export interface DynamicReport {
  id?: number;
  groupId: number;
  name: string;
  sqlQuery: string;
  parametersJson?: string;
  allowedRoles?: string;
  isActive: boolean;
}

@Component({
  selector: 'app-report-designer',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DialogModule
  ],
  templateUrl: './report-designer.component.html'
})
export class ReportDesignerComponent implements OnInit {
  @Output() reportChanged = new EventEmitter<void>();

  placeholderText = '[{"name": "Name", "type": "text", "label": "Tên thiết bị"}]';
  optionsHelpText = 'Các kiểu dữ liệu được hỗ trợ: text, number, date, dropdown (cần thêm trường options: [{"label": "A", "value": "A"}])';

  reports: DynamicReport[] = [];
  groups: ReportGroup[] = [];
  
  displayDialog = false;
  dialogTitle = '';
  currentReport: DynamicReport = { groupId: 0, name: '', sqlQuery: '', isActive: true };
  isEdit = false;

  private http = inject(HttpClient);
  private messageService = inject(MessageService);
  private reportsApiUrl = `${environment.apiGatewayUrl}/api/v1/dynamic-reports`;
  private groupsApiUrl = `${environment.apiGatewayUrl}/api/v1/report-groups`;

  ngOnInit(): void {
    this.loadGroupsAndReports();
  }

  loadGroupsAndReports() {
    this.http.get<ReportGroup[]>(this.groupsApiUrl).subscribe({
      next: (data) => {
        this.groups = data;
        
        const allReports: DynamicReport[] = [];
        data.forEach(g => {
          if (g.dynamicReports) {
            g.dynamicReports.forEach((r: any) => {
              allReports.push({
                id: r.id,
                groupId: r.groupId,
                name: r.name,
                sqlQuery: r.sqlQuery,
                parametersJson: r.parametersJson,
                allowedRoles: r.allowedRoles,
                isActive: r.isActive
              });
            });
          }
        });
        this.reports = allReports;
      },
      error: (err) => {
        console.error(err);
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải cấu hình báo cáo' });
      }
    });
  }

  getGroupName(groupId: number): string {
    const g = this.groups.find(x => x.id === groupId);
    return g ? g.name : `Nhóm #${groupId}`;
  }

  showCreateDialog() {
    this.isEdit = false;
    this.dialogTitle = 'Cấu hình Báo cáo Động';
    this.currentReport = {
      groupId: this.groups.length > 0 ? this.groups[0].id! : 0,
      name: '',
      sqlQuery: '',
      parametersJson: '[]',
      allowedRoles: 'ADMIN,USER',
      isActive: true
    };
    this.displayDialog = true;
  }

  showEditDialog(report: DynamicReport) {
    this.isEdit = true;
    this.dialogTitle = 'Cập nhật cấu hình Báo cáo';
    this.currentReport = { ...report };
    this.displayDialog = true;
  }

  hideDialog() {
    this.displayDialog = false;
  }

  loadSampleParams() {
    this.currentReport.parametersJson = JSON.stringify([
      { name: 'Name', type: 'text', label: 'Tên thiết bị' }
    ], null, 2);
  }

  saveReport() {
    if (this.currentReport.parametersJson) {
      try {
        JSON.parse(this.currentReport.parametersJson);
      } catch (e) {
        this.messageService.add({ severity: 'error', summary: 'Lỗi định dạng', detail: 'Cấu hình tham số lọc không đúng định dạng JSON' });
        return;
      }
    }

    if (this.isEdit) {
      this.http.put(`${this.reportsApiUrl}/${this.currentReport.id}`, this.currentReport).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã cập nhật cấu hình báo cáo' });
          this.loadGroupsAndReports();
          this.reportChanged.emit();
          this.displayDialog = false;
        },
        error: (err) => {
          console.error(err);
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Cập nhật cấu hình thất bại' });
        }
      });
    } else {
      this.http.post(this.reportsApiUrl, this.currentReport).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã tạo báo cáo động mới' });
          this.loadGroupsAndReports();
          this.reportChanged.emit();
          this.displayDialog = false;
        },
        error: (err) => {
          console.error(err);
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Tạo mới cấu hình thất bại' });
        }
      });
    }
  }

  deleteReport(report: DynamicReport) {
    if (confirm(`Bạn có chắc chắn muốn xóa cấu hình báo cáo "${report.name}"?`)) {
      this.http.delete(`${this.reportsApiUrl}/${report.id}`).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xóa báo cáo động' });
          this.loadGroupsAndReports();
          this.reportChanged.emit();
        },
        error: (err) => {
          console.error(err);
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Xóa thất bại' });
        }
      });
    }
  }
}
