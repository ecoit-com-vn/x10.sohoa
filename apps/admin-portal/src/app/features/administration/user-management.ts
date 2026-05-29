import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="wf-page">
      <div class="wf-card">
        
        <!-- Breadcrumb -->
        <div class="breadcrumb">
          <i class="pi pi-home bc-icon"></i>
          <span class="bc-text">Trang chủ</span>
          <span class="bc-sep">/</span>
          <span class="bc-text">Quản trị hệ thống</span>
          <span class="bc-sep">/</span>
          <span class="bc-current">Quản lý người dùng</span>
        </div>

        <!-- Toolbar -->
        <div class="list-toolbar">
          <div class="toolbar-left">
            <input type="text" class="wf-search-input"
              placeholder="Tìm kiếm tài khoản, tên..."
              [(ngModel)]="searchKeyword"
              (keyup.enter)="onSearch()" />
            <button class="btn-tim" (click)="onSearch()">
              <i class="pi pi-search"></i> Tìm
            </button>
          </div>
          <div class="toolbar-right">
            <button class="btn-green" (click)="onAddNew()">
              <i class="pi pi-plus"></i> Thêm mới
            </button>
            <button class="btn-excel" (click)="onExportExcel()">
              <i class="pi pi-file-excel"></i> Xuất Excel
            </button>
          </div>
        </div>

        <!-- Table -->
        <div class="wf-table-wrap">
          <table class="wf-table">
            <thead>
              <tr>
                <th class="col-stt">STT</th>
                <th>Tên đăng nhập</th>
                <th>Họ và tên</th>
                <th>Vai trò</th>
                <th class="col-tt">Trạng thái</th>
                <th class="col-hd">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let user of filteredUsers; let i = index">
                <td class="col-stt text-muted">{{ i + 1 }}</td>
                <td><b class="wf-name-link">{{ user.username }}</b></td>
                <td>{{ user.fullName }}</td>
                <td><span class="text-muted">{{ user.role }}</span></td>
                <td class="col-tt">
                  <span class="status-pill"
                    [class.status-active]="user.status === 'Hoạt động'"
                    [class.status-inactive]="user.status !== 'Hoạt động'">
                    <i class="pi pi-clock"></i>
                    {{ user.status }}
                  </span>
                </td>
                <td class="col-hd">
                  <button class="act-btn act-edit" (click)="onEdit(user)" title="Chỉnh sửa">
                    <i class="pi pi-pencil"></i>
                  </button>
                  <button class="act-btn act-delete" (click)="onDelete(user)" title="Xóa">
                    <i class="pi pi-trash"></i>
                  </button>
                </td>
              </tr>
              <tr *ngIf="filteredUsers.length === 0">
                <td colspan="6" class="empty-row">
                  <i class="pi pi-inbox"></i>
                  <div>Không tìm thấy người dùng phù hợp.</div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer">
          <span class="record-count">Tổng số: <b>{{ filteredUsers.length }}</b> bản ghi.</span>
          <div class="pagination">
            <button class="page-btn" disabled><i class="pi pi-chevron-left"></i></button>
            <span class="page-current">1</span>
            <button class="page-btn" disabled><i class="pi pi-chevron-right"></i></button>
            <select class="page-size-sel">
              <option>10 / trang</option>
              <option>20 / trang</option>
              <option>50 / trang</option>
            </select>
          </div>
        </div>

      </div>
    </div>
  `,
  styles: []
})
export class UserManagement {
  users = [
    { username: 'admin', fullName: 'Quản trị viên', role: 'Admin', status: 'Hoạt động' },
    { username: 'user1', fullName: 'Nguyễn Văn A', role: 'Kỹ sư cơ điện', status: 'Hoạt động' },
    { username: 'user2', fullName: 'Trần Thị B', role: 'Kiểm soát viên chất lượng', status: 'Hoạt động' },
    { username: 'user3', fullName: 'Lê Văn C', role: 'Nhân viên quét hồ sơ', status: 'Ngưng hoạt động' }
  ];

  filteredUsers = [...this.users];
  searchKeyword = '';

  onSearch() {
    if (this.searchKeyword) {
      const kw = this.searchKeyword.toLowerCase();
      this.filteredUsers = this.users.filter(u =>
        u.username.toLowerCase().includes(kw) ||
        u.fullName.toLowerCase().includes(kw) ||
        u.role.toLowerCase().includes(kw)
      );
    } else {
      this.filteredUsers = [...this.users];
    }
  }

  onAddNew() {
    alert('Chức năng thêm mới người dùng sẽ khả dụng khi kết nối cơ sở dữ liệu!');
  }

  onEdit(user: any) {
    alert(`Đang chỉnh sửa người dùng: ${user.fullName}`);
  }

  onDelete(user: any) {
    if (confirm(`Bạn có chắc chắn muốn xóa tài khoản ${user.username}?`)) {
      this.users = this.users.filter(u => u.username !== user.username);
      this.onSearch();
    }
  }

  onExportExcel() {
    alert('Xuất danh sách người dùng thành công dưới định dạng Excel!');
  }
}
