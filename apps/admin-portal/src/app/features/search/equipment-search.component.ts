import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { environment } from '../../../environments/environment';
import { VirtualFoldersComponent } from '../digitization/components/virtual-folders/virtual-folders.component';

@Component({
  selector: 'app-equipment-search',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, ButtonModule, VirtualFoldersComponent],
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
          <span class="bc-current">Tìm kiếm thiết bị</span>
        </div>

        <div class="edit-header">
          <h2 class="edit-title" style="color: #002D72;">Tra cứu & Tìm kiếm Thiết bị</h2>
        </div>

        <p class="text-muted mb-4">
          Hệ thống hỗ trợ tra cứu nhanh toàn bộ thông tin thuộc tính kỹ thuật máy biến áp, cột điện, đường dây truyền tải điện của EVNHANOI đã được đồng bộ từ PMIS hoặc số hóa tại chỗ.
        </p>

        <!-- Search & Import Bar -->
        <div class="list-toolbar" style="display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 10px;">
          <div class="toolbar-left" style="display: flex; gap: 8px; width: 100%; max-width: 600px;">
            <input type="text" class="wf-search-input"
              placeholder="Nhập từ khóa tìm tên, mã định danh, hãng sản xuất..."
              [(ngModel)]="searchQuery"
              (keyup.enter)="onSearch()" 
              style="flex: 1;" />
            <button class="btn-tim" (click)="onSearch()" [disabled]="loading">
              <i class="pi pi-search"></i> Tìm kiếm
            </button>
          </div>
          <div class="toolbar-right" style="display: flex; gap: 8px;">
            <button class="btn-outlined" (click)="openTemplateDialog()">
              <i class="pi pi-download mr-1"></i> Tải file mẫu Excel
            </button>
            <button class="btn-green" (click)="openImportDialog()">
              <i class="pi pi-file-excel mr-1"></i> Nhập từ Excel
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
              <!-- skeleton loading rows -->
              <ng-container *ngIf="loading">
                <tr *ngFor="let item of [1, 2, 3]">
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 100px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 250px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 150px; border-radius: 4px;"></div></td>
                  <td class="col-tt"><div class="skeleton-shimmer" style="height: 24px; width: 100px; border-radius: 12px;"></div></td>
                </tr>
              </ng-container>

              <ng-container *ngIf="!loading">
                <tr *ngFor="let item of results">
                  <td><code>{{ item.id || item.code }}</code></td>
                  <td><b class="wf-name-link" (click)="openEquipmentDetail(item)" style="cursor: pointer; color: #002D72; text-decoration: underline;">{{ item.name }}</b></td>
                  <td><span class="text-muted">{{ item.type || item.typeName || 'Thiết bị' }}</span></td>
                  <td class="col-tt">
                    <span class="status-pill"
                      [class.status-active]="item.status === 'Đang hoạt động' || item.isActive"
                      [class.status-pending]="item.status === 'Đang bảo trì' || !item.isActive">
                      <i class="pi pi-clock"></i>
                      {{ item.status || (item.isActive ? 'Đang hoạt động' : 'Ngưng hoạt động') }}
                    </span>
                  </td>
                </tr>
                <tr *ngIf="results.length === 0">
                  <td colspan="4" class="empty-row">
                    <i class="pi pi-search"></i>
                    <div>Nhập từ khóa và nhấn <b>Tìm kiếm</b> để tra cứu thiết bị!</div>
                  </td>
                </tr>
              </ng-container>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer" *ngIf="results.length > 0 && !loading">
          <span class="record-count">Tổng số: <b>{{ totalRecords }}</b> bản ghi.</span>
          <div class="pagination">
            <button class="page-btn" (click)="prevPage()" [disabled]="currentPage === 1">
              <i class="pi pi-chevron-left"></i>
            </button>
            <span class="page-current">Trang {{ currentPage }} / {{ totalPages || 1 }}</span>
            <button class="page-btn" (click)="nextPage()" [disabled]="currentPage >= totalPages">
              <i class="pi pi-chevron-right"></i>
            </button>
            <select class="page-size-sel" [value]="pageSize" (change)="onPageSizeChange($event)">
              <option [value]="10">10 / trang</option>
              <option [value]="20">20 / trang</option>
              <option [value]="50">50 / trang</option>
            </select>
          </div>
        </div>

      </div>
    </div>

    <!-- Dialog Chi tiết thiết bị -->
    <p-dialog [(visible)]="displayDetailDialog" [header]="'Chi tiết hồ sơ thiết bị: ' + (selectedEquipment?.name || '')" [modal]="true" [style]="{ width: '90%' }">
      <div *ngIf="selectedEquipment" style="display: flex; flex-direction: column; gap: 16px; padding-top: 10px;">
        <!-- Thông tin chung -->
        <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 14px; background-color: #f8fafc; padding: 14px; border-radius: 8px; border: 1px solid #e2e8f0;">
          <div>
            <span style="font-size: 0.8rem; color: #64748b; display: block;">Mã số định danh:</span>
            <p style="margin: 4px 0 0 0; font-family: monospace; font-weight: bold; color: #002D72;">{{ selectedEquipment.id || selectedEquipment.code }}</p>
          </div>
          <div>
            <span style="font-size: 0.8rem; color: #64748b; display: block;">Tên thiết bị:</span>
            <p style="margin: 4px 0 0 0; font-weight: bold;">{{ selectedEquipment.name }}</p>
          </div>
          <div>
            <span style="font-size: 0.8rem; color: #64748b; display: block;">Phân loại:</span>
            <p style="margin: 4px 0 0 0;">{{ selectedEquipment.type || selectedEquipment.typeName || 'Thiết bị' }}</p>
          </div>
          <div>
            <span style="font-size: 0.8rem; color: #64748b; display: block;">Trạng thái vận hành:</span>
            <p style="margin: 4px 0 0 0;">
              <span class="status-pill" [class.status-active]="selectedEquipment.status === 'Đang hoạt động' || selectedEquipment.isActive" [class.status-pending]="selectedEquipment.status === 'Đang bảo trì' || !selectedEquipment.isActive" style="display: inline-block; padding: 2px 8px; font-size: 0.75rem;">
                {{ selectedEquipment.status || (selectedEquipment.isActive ? 'Đang hoạt động' : 'Ngưng hoạt động') }}
              </span>
            </p>
          </div>
        </div>

        <!-- Thư mục tài liệu ảo -->
        <div>
          <h4 style="color: #002D72; margin: 0 0 10px 0; border-bottom: 2px solid #FF6B00; padding-bottom: 4px; display: inline-block; font-size: 0.95rem; font-weight: bold;">
            Thư mục tài liệu ảo đính kèm
          </h4>
          <app-virtual-folders *ngIf="selectedEquipmentId" [equipmentId]="selectedEquipmentId" [isEmbedded]="true"></app-virtual-folders>
        </div>
      </div>
      <ng-template #footer>
        <div style="display: flex; justify-content: flex-end; padding-top: 10px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayDetailDialog = false">Đóng</button>
        </div>
      </ng-template>
    </p-dialog>

    <!-- Dialog Tải Excel mẫu -->
    <p-dialog [(visible)]="displayTemplateDialog" header="Tải Excel mẫu động theo thuộc tính EAV" [modal]="true" [style]="{ width: '450px' }">
      <div style="display: flex; flex-direction: column; gap: 14px; padding-top: 10px;">
        <div class="form-group">
          <label class="form-label" style="font-weight: 600;">Chọn Loại thiết bị kỹ thuật <span class="required">*</span></label>
          <select class="wf-input w-full" style="height: 38px; border: 1px solid #cbd5e1; border-radius: 6px; padding: 0 8px;" [(ngModel)]="selectedTypeIdForTemplate">
            <option value="">-- Chọn Loại thiết bị --</option>
            <option *ngFor="let type of equipmentTypes" [value]="type.id">{{ type.name }} ({{ type.code }})</option>
          </select>
        </div>
      </div>
      <ng-template #footer>
        <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayTemplateDialog = false">Hủy</button>
          <button class="btn-save btn-small" style="background-color: #002D72; color: white;" (click)="downloadTemplate()">Tải mẫu (.xlsx)</button>
        </div>
      </ng-template>
    </p-dialog>

    <!-- Dialog Nhập từ Excel -->
    <p-dialog [(visible)]="displayImportDialog" header="Bulk Import hồ sơ thiết bị từ Excel" [modal]="true" [style]="{ width: '550px' }">
      <div style="display: flex; flex-direction: column; gap: 14px; padding-top: 10px;">
        <div class="form-group">
          <label class="form-label" style="font-weight: 600;">Chọn Loại thiết bị để Nhập liệu <span class="required">*</span></label>
          <select class="wf-input w-full" style="height: 38px; border: 1px solid #cbd5e1; border-radius: 6px; padding: 0 8px;" [(ngModel)]="selectedTypeIdForImport">
            <option value="">-- Chọn Loại thiết bị --</option>
            <option *ngFor="let type of equipmentTypes" [value]="type.id">{{ type.name }} ({{ type.code }})</option>
          </select>
        </div>

        <div class="form-group">
          <label class="form-label" style="font-weight: 600;">Chọn file Excel mẫu đã điền dữ liệu <span class="required">*</span></label>
          <input type="file" accept=".xlsx" class="w-full mt-1" (change)="onFileChange($event)" />
        </div>

        <!-- Kết quả import -->
        <div *ngIf="importResult" class="import-result-box mt-3" style="background-color: #f8fafc; border: 1px solid #e2e8f0; padding: 12px; border-radius: 8px; font-size: 0.85rem;">
          <p class="font-bold text-success m-0 mb-2">{{ importResult.message }}</p>
          <div *ngIf="importResult.errors && importResult.errors.length > 0" style="max-height: 150px; overflow-y: auto;">
            <p class="font-bold text-danger m-0 mb-1">Chi tiết lỗi dòng:</p>
            <ul style="padding-left: 16px; margin: 0; color: #ef4444;">
              <li *ngFor="let err of importResult.errors">{{ err }}</li>
            </ul>
          </div>
        </div>
      </div>
      
      <ng-template #footer>
        <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayImportDialog = false" [disabled]="importing">Hủy</button>
          <button class="btn-save btn-small" style="background-color: #22c55e; color: white;" (click)="executeImport()" [disabled]="importing">
            <i class="pi pi-spin pi-spinner mr-1" *ngIf="importing"></i>
            {{ importing ? 'Đang xử lý...' : 'Thực hiện Import' }}
          </button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styles: `
    @keyframes shimmer {
      0% { background-position: -200% 0; }
      100% { background-position: 200% 0; }
    }
    .skeleton-shimmer {
      background: linear-gradient(90deg, #f3f4f6 25%, #e5e7eb 50%, #f3f4f6 75%);
      background-size: 200% 100%;
      animation: shimmer 1.5s infinite;
    }
    :global(html.dark-mode) .skeleton-shimmer {
      background: linear-gradient(90deg, #1e293b 25%, #334155 50%, #1e293b 75%);
      background-size: 200% 100%;
    }
  `
})
export class EquipmentSearchComponent implements OnInit {
  searchQuery: string = '';
  results: any[] = [];
  loading: boolean = false;

  currentPage = 1;
  pageSize = 10;
  totalRecords = 0;

  // Detail Dialog states
  displayDetailDialog = false;
  selectedEquipment: any = null;
  selectedEquipmentId: string | null = null;

  // Excel Import/Template states
  displayTemplateDialog = false;
  displayImportDialog = false;
  equipmentTypes: any[] = [];
  selectedTypeIdForTemplate = '';
  selectedTypeIdForImport = '';
  selectedFileToImport: File | null = null;
  importing = false;
  importResult: any = null;

  private http = inject(HttpClient);
  private messageService = inject(MessageService);

  ngOnInit() {
    this.loadEquipmentTypes();
  }

  loadEquipmentTypes() {
    this.http.get<any[]>(`${environment.apiGatewayUrl}/api/EquipmentType`).subscribe({
      next: (res) => {
        this.equipmentTypes = res || [];
      },
      error: (err) => console.error('Lỗi tải loại thiết bị:', err)
    });
  }

  openEquipmentDetail(item: any) {
    this.selectedEquipment = item;
    this.selectedEquipmentId = item.id || item.code;
    this.displayDetailDialog = true;
  }

  openTemplateDialog() {
    this.loadEquipmentTypes();
    this.displayTemplateDialog = true;
  }

  downloadTemplate() {
    if (!this.selectedTypeIdForTemplate) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Vui lòng chọn loại thiết bị.' });
      return;
    }
    const url = `${environment.apiGatewayUrl}/api/Equipment/import-template/${this.selectedTypeIdForTemplate}`;
    this.http.get(url, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        const urlObj = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = urlObj;
        const selectedType = this.equipmentTypes.find(t => t.id === this.selectedTypeIdForTemplate);
        a.download = `Import_Template_${selectedType?.code || 'Equipment'}.xlsx`;
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(urlObj);
        a.remove();
        this.displayTemplateDialog = false;
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã tải xuống file Excel mẫu.' });
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Tải file mẫu thất bại.' });
      }
    });
  }

  openImportDialog() {
    this.loadEquipmentTypes();
    this.selectedFileToImport = null;
    this.importResult = null;
    this.displayImportDialog = true;
  }

  onFileChange(event: any) {
    const file = event.target.files[0];
    if (file) {
      this.selectedFileToImport = file;
    }
  }

  executeImport() {
    if (!this.selectedTypeIdForImport) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Vui lòng chọn loại thiết bị.' });
      return;
    }
    if (!this.selectedFileToImport) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Vui lòng chọn file Excel.' });
      return;
    }

    const formData = new FormData();
    formData.append('file', this.selectedFileToImport);

    this.importing = true;
    this.importResult = null;
    const url = `${environment.apiGatewayUrl}/api/Equipment/import?equipmentTypeId=${this.selectedTypeIdForImport}`;
    
    this.http.post<any>(url, formData).subscribe({
      next: (res) => {
        this.importing = false;
        this.importResult = res;
        if (res.successCount > 0) {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: res.message });
          this.onSearch(); // Tải lại danh sách thiết bị
        } else {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không có dòng dữ liệu nào được nhập thành công.' });
        }
      },
      error: (err) => {
        this.importing = false;
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Nhập dữ liệu từ Excel thất bại.' });
      }
    });
  }

  onSearch() {
    if (!this.searchQuery.trim()) {
      this.results = [];
      this.totalRecords = 0;
      return;
    }

    this.loading = true;
    const url = `${environment.apiGatewayUrl}/api/v1/equipment/search`;
    this.http.get<any>(url, {
      params: {
        query: this.searchQuery,
        page: this.currentPage.toString(),
        pageSize: this.pageSize.toString()
      }
    }).subscribe({
      next: (res) => {
        if (Array.isArray(res)) {
          this.results = res;
          this.totalRecords = res.length;
        } else {
          this.results = res.items || res.results || [];
          this.totalRecords = res.totalCount || res.total || this.results.length;
        }
        this.loading = false;
      },
      error: (err) => {
        console.error('Lỗi tìm kiếm thiết bị:', err);
        this.results = [];
        this.totalRecords = 0;
        this.loading = false;
      }
    });
  }

  nextPage() {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
      this.onSearch();
    }
  }

  prevPage() {
    if (this.currentPage > 1) {
      this.currentPage--;
      this.onSearch();
    }
  }

  onPageSizeChange(event: any) {
    this.pageSize = Number(event.target.value);
    this.currentPage = 1;
    this.onSearch();
  }

  get totalPages(): number {
    return Math.ceil(this.totalRecords / this.pageSize);
  }
}
