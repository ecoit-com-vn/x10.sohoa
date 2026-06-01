// E:\ecoit\sohoax10\sohoa.frontend\apps\admin-portal\src\app\features\reports\report-designer.component.ts
import { Component, OnInit, Output, EventEmitter, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { environment } from '../../../environments/environment';
import { ReportGroup } from './report-group-manager.component';

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
  template: `
    <div>
      <div class="list-toolbar mb-4">
        <div class="toolbar-left">
          <h3 class="text-base font-bold m-0" style="color: #002D72;">Thiết kế & Cấu hình Báo cáo Động</h3>
        </div>
        <div class="toolbar-right">
          <button class="btn-green btn-small" (click)="showCreateDialog()">
            <i class="pi pi-plus mr-1"></i> Thêm Báo Cáo Mới
          </button>
        </div>
      </div>

      <div class="wf-table-wrap">
        <table class="wf-table">
          <thead>
            <tr>
              <th style="width: 80px;" class="text-center">ID</th>
              <th style="width: 220px;">Nhóm báo cáo</th>
              <th>Tên báo cáo</th>
              <th>Phân quyền Roles</th>
              <th style="width: 120px;" class="text-center">Trạng thái</th>
              <th style="width: 150px;" class="text-center">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let report of reports">
              <td class="text-center text-muted">{{ report.id }}</td>
              <td><b class="text-gray-700">{{ getGroupName(report.groupId) }}</b></td>
              <td><b class="wf-name-link" (click)="showEditDialog(report)">{{ report.name }}</b></td>
              <td>
                <span class="text-muted text-xs bg-gray-100 px-2 py-0.5 rounded border">{{ report.allowedRoles || 'Tất cả (Public)' }}</span>
              </td>
              <td class="text-center">
                <span class="status-pill" [class.status-active]="report.isActive" [class.status-inactive]="!report.isActive">
                  <i class="pi pi-circle-on text-[8px] mr-1"></i>
                  {{ report.isActive ? 'Hoạt động' : 'Tạm khóa' }}
                </span>
              </td>
              <td class="text-center">
                <button class="act-btn act-edit mr-2" (click)="showEditDialog(report)" title="Chỉnh sửa">
                  <i class="pi pi-pencil"></i>
                </button>
                <button class="act-btn act-delete" (click)="deleteReport(report)" title="Xóa">
                  <i class="pi pi-trash"></i>
                </button>
              </td>
            </tr>
            <tr *ngIf="reports.length === 0">
              <td colspan="6" class="empty-row text-center py-4 text-muted">
                <i class="pi pi-inbox block text-2xl mb-2"></i>
                Không có cấu hình báo cáo động nào.
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Dialog CRUD -->
      <p-dialog [(visible)]="displayDialog" [header]="dialogTitle" [modal]="true" [style]="{width: '680px'}" styleClass="evn-dialog-custom">
        <div style="display: flex; flex-direction: column; gap: 14px; padding-top: 10px;">
          <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 14px;">
            <div class="form-group">
              <label class="form-label">Nhóm báo cáo <span class="required">*</span></label>
              <select class="wf-input w-full bg-white" [(ngModel)]="currentReport.groupId" required style="height: 38px;">
                <option value="0" disabled>-- Chọn nhóm báo cáo --</option>
                <option *ngFor="let g of groups" [value]="g.id">{{ g.name }}</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">Tên báo cáo <span class="required">*</span></label>
              <input type="text" class="wf-input w-full" [(ngModel)]="currentReport.name" required placeholder="Tên hiển thị báo cáo..." />
            </div>
          </div>

          <div class="form-group">
            <label class="form-label">Câu lệnh SQL Query (Sử dụng tham số hóa Oracle :ParamName) <span class="required">*</span></label>
            <textarea class="wf-input w-full font-mono text-xs" [(ngModel)]="currentReport.sqlQuery" rows="6" placeholder="SELECT Id, Code, Name FROM Equipments WHERE (:Name IS NULL OR Name LIKE '%' || :Name || '%')" required></textarea>
          </div>

          <div class="form-group">
            <div class="flex justify-between items-center mb-1">
              <label class="form-label m-0">Cấu hình Tham số lọc (JSON Array)</label>
              <a href="javascript:void(0)" class="text-xs text-blue-600 font-semibold hover:underline" (click)="loadSampleParams()">Bấm để chèn mẫu JSON</a>
            </div>
            <textarea class="wf-input w-full font-mono text-xs" [(ngModel)]="currentReport.parametersJson" rows="4" [placeholder]="placeholderText"></textarea>
            <span class="text-[10px] text-gray-500 mt-1 block">{{ optionsHelpText }}</span>
          </div>

          <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 14px; align-items: center;">
            <div class="form-group">
              <label class="form-label">Vai trò được xem (Roles cách nhau bởi dấu phẩy)</label>
              <input type="text" class="wf-input w-full" [(ngModel)]="currentReport.allowedRoles" placeholder="ADMIN,USER,VIEWER" />
            </div>
            <div class="form-group" style="display: flex; align-items: center; gap: 10px; padding-top: 15px;">
              <input type="checkbox" id="isActiveReportCheck" [(ngModel)]="currentReport.isActive" style="scale: 1.2; cursor: pointer;" />
              <label for="isActiveReportCheck" style="font-weight: 600; cursor: pointer; margin: 0;">Cho phép hoạt động</label>
            </div>
          </div>
        </div>

        <ng-template #footer>
          <div class="flex gap-2 justify-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
            <button class="btn-outlined btn-small" (click)="hideDialog()">Hủy</button>
            <button class="btn-save btn-small" [disabled]="!currentReport.groupId || !currentReport.name || !currentReport.sqlQuery" (click)="saveReport()">Lưu cấu hình</button>
          </div>
        </ng-template>
      </p-dialog>
    </div>
  `
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
