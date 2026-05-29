import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-equipment-search',
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
          <span class="bc-text">Quản lý thiết bị</span>
          <span class="bc-sep">/</span>
          <span class="bc-current">Tìm kiếm thiết bị</span>
        </div>

        <div class="edit-header">
          <h2 class="edit-title" style="color: #002D72;">Tra cứu & Tìm kiếm Thiết bị</h2>
        </div>

        <p class="text-muted mb-4">
          Hệ thống hỗ trợ tra cứu nhanh toàn bộ thông tin thuộc tính kỹ thuật máy biến áp, cột điện, đường dây truyền tải điện của EVNHANOI đã được đồng bộ từ PMIS hoặc số hóa tại chỗ.
        </p>

        <!-- Search Bar -->
        <div class="list-toolbar">
          <div class="toolbar-left" style="width: 100%; max-width: 600px;">
            <input type="text" class="wf-search-input"
              placeholder="Nhập từ khóa tìm tên, mã định danh, hãng sản xuất..."
              [(ngModel)]="searchQuery"
              (keyup.enter)="onSearch()" 
              style="flex: 1; max-width: 450px;" />
            <button class="btn-tim" (click)="onSearch()">
              <i class="pi pi-search"></i> Tìm kiếm
            </button>
          </div>
        </div>

        <!-- Table -->
        <div class="wf-table-wrap mt-3">
          <table class="wf-table">
            <thead>
              <tr>
                <th style="width: 150px;">Mã số định danh</th>
                <th>Tên gọi thiết bị</th>
                <th>Phân loại thiết bị</th>
                <th class="col-tt">Trạng thái vận hành</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let item of results">
                <td><code>{{ item.id }}</code></td>
                <td><b class="wf-name-link">{{ item.name }}</b></td>
                <td><span class="text-muted">{{ item.type }}</span></td>
                <td class="col-tt">
                  <span class="status-pill"
                    [class.status-active]="item.status === 'Đang hoạt động'"
                    [class.status-pending]="item.status === 'Đang bảo trì'">
                    <i class="pi pi-clock"></i>
                    {{ item.status }}
                  </span>
                </td>
              </tr>
              <tr *ngIf="results.length === 0 && !loading">
                <td colspan="4" class="empty-row">
                  <i class="pi pi-search"></i>
                  <div>Nhập từ khóa và nhấn <b>Tìm kiếm</b> để tra cứu thiết bị!</div>
                </td>
              </tr>
              <tr *ngIf="loading">
                <td colspan="4" class="skeleton-row">
                  <div class="skeleton-bar"></div>
                  <div class="skeleton-bar short"></div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer" *ngIf="results.length > 0">
          <span class="record-count">Tổng số: <b>{{ results.length }}</b> bản ghi.</span>
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
export class EquipmentSearchComponent {
  searchQuery: string = '';
  results: any[] = [];
  loading: boolean = false;

  onSearch() {
    this.loading = true;
    this.results = [];
    
    // Giả lập kết nối Elasticsearch API
    setTimeout(() => {
      this.results = [
        { id: 'EQ-MBA-01', name: 'Máy biến áp dầu T1 110kV Đông Anh', type: 'Máy biến áp (MBA)', status: 'Đang hoạt động' },
        { id: 'EQ-CD-102', name: 'Cột điện bê tông ly tâm 12m vị trí 45 tuyến Gia Lâm', type: 'Cột điện', status: 'Đang bảo trì' },
        { id: 'EQ-MC-204', name: 'Máy cắt hợp bộ trung thế M5 trạm Nghĩa Đô', type: 'Thiết bị đóng cắt', status: 'Đang hoạt động' }
      ].filter(item => 
        item.name.toLowerCase().includes(this.searchQuery.toLowerCase()) || 
        item.id.toLowerCase().includes(this.searchQuery.toLowerCase()) ||
        item.type.toLowerCase().includes(this.searchQuery.toLowerCase())
      );
      this.loading = false;
    }, 500);
  }
}
