import { Component } from '@angular/core';
import { TableModule } from 'primeng/table';
import { Button } from 'primeng/button';

@Component({
  selector: 'app-user-management',
  imports: [TableModule, Button],
  template: `
    <div class="card">
      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem;">
        <h2>Quản lý Người dùng</h2>
        <p-button label="Thêm mới" icon="pi pi-plus"></p-button>
      </div>
      <p-table [value]="users" [tableStyle]="{ 'min-width': '50rem' }">
        <ng-template #header>
          <tr>
            <th>Tên đăng nhập</th>
            <th>Họ và tên</th>
            <th>Vai trò</th>
            <th>Trạng thái</th>
            <th>Thao tác</th>
          </tr>
        </ng-template>
        <ng-template #body let-user>
          <tr>
            <td>{{ user.username }}</td>
            <td>{{ user.fullName }}</td>
            <td>{{ user.role }}</td>
            <td>{{ user.status }}</td>
            <td>
              <p-button icon="pi pi-pencil" [rounded]="true" [text]="true" severity="info" />
              <p-button icon="pi pi-trash" [rounded]="true" [text]="true" severity="danger" />
            </td>
          </tr>
        </ng-template>
      </p-table>
    </div>
  `,
  styles: `
    h2 { margin-top: 0; }
  `,
})
export class UserManagement {
  users = [
    { username: 'admin', fullName: 'Quản trị viên', role: 'Admin', status: 'Hoạt động' },
    { username: 'user1', fullName: 'Người dùng 1', role: 'User', status: 'Hoạt động' }
  ];
}
