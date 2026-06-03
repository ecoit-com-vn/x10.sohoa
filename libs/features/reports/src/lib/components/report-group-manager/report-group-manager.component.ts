// E:\ecoit\sohoax10\sohoa.frontend\apps\admin-portal\src\app\features\reports\report-group-manager.component.ts
import { Component, OnInit, Output, EventEmitter, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { environment } from '@env/environment';

export interface ReportGroup {
  id?: number;
  name: string;
  sortOrder: number;
  description?: string;
  dynamicReports?: any[];
}

@Component({
  selector: 'app-report-group-manager',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DialogModule
  ],
  templateUrl: './report-group-manager.component.html'
})
export class ReportGroupManagerComponent implements OnInit {
  @Output() groupChanged = new EventEmitter<void>();

  groups: ReportGroup[] = [];
  displayDialog = false;
  dialogTitle = '';
  currentGroup: ReportGroup = { name: '', sortOrder: 0 };
  isEdit = false;

  private http = inject(HttpClient);
  private messageService = inject(MessageService);
  private apiUrl = `${environment.apiGatewayUrl}/api/v1/report-groups`;

  ngOnInit(): void {
    this.loadGroups();
  }

  loadGroups() {
    this.http.get<ReportGroup[]>(this.apiUrl).subscribe({
      next: (data) => {
        this.groups = data;
      },
      error: (err) => {
        console.error('Load report groups error', err);
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách nhóm báo cáo' });
      }
    });
  }

  showCreateDialog() {
    this.isEdit = false;
    this.dialogTitle = 'Thêm Nhóm Báo cáo';
    this.currentGroup = { name: '', sortOrder: this.groups.length + 1, description: '' };
    this.displayDialog = true;
  }

  showEditDialog(group: ReportGroup) {
    this.isEdit = true;
    this.dialogTitle = 'Sửa Nhóm Báo cáo';
    this.currentGroup = { ...group };
    this.displayDialog = true;
  }

  hideDialog() {
    this.displayDialog = false;
  }

  saveGroup() {
    if (!this.currentGroup.name) return;

    if (this.isEdit) {
      this.http.put(`${this.apiUrl}/${this.currentGroup.id}`, this.currentGroup).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã cập nhật nhóm báo cáo' });
          this.loadGroups();
          this.groupChanged.emit();
          this.displayDialog = false;
        },
        error: (err) => {
          console.error(err);
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Cập nhật thất bại' });
        }
      });
    } else {
      this.http.post(this.apiUrl, this.currentGroup).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã thêm nhóm báo cáo mới' });
          this.loadGroups();
          this.groupChanged.emit();
          this.displayDialog = false;
        },
        error: (err) => {
          console.error(err);
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Thêm mới thất bại' });
        }
      });
    }
  }

  deleteGroup(group: ReportGroup) {
    if (confirm(`Bạn có chắc chắn muốn xóa nhóm "${group.name}"? Tất cả báo cáo thuộc nhóm này cũng sẽ bị xóa.`)) {
      this.http.delete(`${this.apiUrl}/${group.id}`).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xóa nhóm báo cáo' });
          this.loadGroups();
          this.groupChanged.emit();
        },
        error: (err) => {
          console.error(err);
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Xóa thất bại' });
        }
      });
    }
  }
}
