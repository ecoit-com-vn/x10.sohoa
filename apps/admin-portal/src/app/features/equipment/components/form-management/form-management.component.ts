import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { RouterLink, Router } from '@angular/router';
import { EavFormService, EavFormTemplate } from '../../../../core/services/eav-form.service';

@Component({
  selector: 'app-form-management',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, RouterLink],
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
          <span class="bc-text">Quản lý thiết bị</span>
          <span class="bc-sep">/</span>
          <span class="bc-current">Quản lý biểu mẫu (EAV)</span>
        </div>

        <!-- Toolbar -->
        <div class="list-toolbar">
          <div class="toolbar-left">
            <input type="text" class="wf-search-input"
              placeholder="Tìm kiếm biểu mẫu..."
              [(ngModel)]="searchKeyword"
              (keyup.enter)="onSearch()" />
            <button class="btn-tim" (click)="onSearch()">
              <i class="pi pi-search"></i> Tìm
            </button>
          </div>
          <div class="toolbar-right">
            <button class="btn-green" routerLink="../form-builder">
              <i class="pi pi-plus"></i> Tạo biểu mẫu mới
            </button>
          </div>
        </div>

        <!-- Table -->
        <div class="wf-table-wrap">
          <table class="wf-table">
            <thead>
              <tr>
                <th style="width: 250px;">Mã số</th>
                <th>Tên biểu mẫu thuộc tính thiết bị</th>
                <th style="width: 120px; text-align: center;">Phiên bản</th>
                <th class="col-tt">Trạng thái</th>
                <th>Cập nhật lần cuối</th>
                <th class="col-hd">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngIf="loading">
                <td colspan="6" class="skeleton-row">
                  <div class="skeleton-bar"></div>
                  <div class="skeleton-bar short"></div>
                </td>
              </tr>
              <tr *ngFor="let form of filteredForms; let i = index">
                <td><code class="text-muted">{{ form.id }}</code></td>
                <td><b class="wf-name-link" (click)="onEdit(form)">{{ form.name }}</b></td>
                <td style="text-align: center; font-family: monospace; font-weight: bold;">v{{ form.version }}.0</td>
                <td class="col-tt">
                  <span class="status-pill"
                    [class.status-active]="form.isActive"
                    [class.status-inactive]="!form.isActive">
                    <i class="pi pi-clock"></i>
                    {{ form.isActive ? 'Đang hoạt động' : 'Ngưng hoạt động' }}
                  </span>
                </td>
                <td>{{ form.createdAt | date:'dd/MM/yyyy HH:mm' }}</td>
                <td class="col-hd">
                  <button class="act-btn act-edit" (click)="onEdit(form)" title="Chỉnh sửa">
                    <i class="pi pi-pencil"></i>
                  </button>
                  <button class="act-btn act-delete" 
                    *ngIf="form.isActive"
                    (click)="deactivateForm(form)" 
                    title="Vô hiệu hóa">
                    <i class="pi pi-times-circle"></i>
                  </button>
                </td>
              </tr>
              <tr *ngIf="filteredForms.length === 0 && !loading">
                <td colspan="6" class="empty-row">
                  <i class="pi pi-inbox"></i>
                  <div>Chưa có biểu mẫu động nào. Nhấp tạo biểu mẫu để thiết lập!</div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer" *ngIf="filteredForms.length > 0">
          <span class="record-count">Tổng số: <b>{{ filteredForms.length }}</b> bản ghi.</span>
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
  `
})
export class FormManagementComponent implements OnInit {
  forms: EavFormTemplate[] = [];
  filteredForms: EavFormTemplate[] = [];
  searchKeyword = '';
  loading = false;

  private eavFormService = inject(EavFormService);
  private messageService = inject(MessageService);
  private router = inject(Router);

  ngOnInit() {
    this.loadForms();
  }

  loadForms() {
    this.loading = true;
    this.eavFormService.getTemplates().subscribe({
      next: (data) => {
        this.forms = data || [];
        this.filteredForms = [...this.forms];
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading forms', err);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải danh sách biểu mẫu từ máy chủ.'
        });
        this.loading = false;
      }
    });
  }

  onSearch() {
    if (this.searchKeyword) {
      const term = this.searchKeyword.toLowerCase();
      this.filteredForms = this.forms.filter(f =>
        f.name.toLowerCase().includes(term) ||
        f.id.toLowerCase().includes(term)
      );
    } else {
      this.filteredForms = [...this.forms];
    }
  }

  deactivateForm(form: EavFormTemplate) {
    if (confirm(`Bạn có chắc chắn muốn vô hiệu hóa biểu mẫu: ${form.name}?`)) {
      this.eavFormService.deleteTemplate(form.id).subscribe({
        next: () => {
          this.messageService.add({ 
            severity: 'success', 
            summary: 'Thành công', 
            detail: `Đã vô hiệu hóa biểu mẫu thành công!` 
          });
          this.loadForms();
        },
        error: (err) => {
          console.error('Error deactivating form', err);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể vô hiệu hóa biểu mẫu.'
          });
        }
      });
    }
  }

  onEdit(form: EavFormTemplate) {
    this.router.navigate(['/equipment/form-builder'], { queryParams: { id: form.id } });
  }
}
