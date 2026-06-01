// E:\ecoit\sohoax10\sohoa.frontend\apps\admin-portal\src\app\features\administration\upload-config.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService, ConfirmationService } from 'primeng/api';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-upload-config',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule],
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
          <span class="bc-text">Quản trị hệ thống</span>
          <span class="bc-sep">/</span>
          <span class="bc-current">Cấu hình File đính kèm</span>
        </div>

        <p class="text-muted mb-4">
          Cấu hình động cho các loại file đính kèm được phép tải lên hệ thống (định dạng tệp tin và dung lượng tối đa) theo từng phân hệ.
        </p>

        <!-- Toolbar -->
        <div class="list-toolbar">
          <div class="toolbar-left">
            <input type="text" class="wf-search-input"
              placeholder="Tìm theo phân hệ..."
              [(ngModel)]="searchKeyword"
              (keyup.enter)="onSearch()" />
            <button class="btn-tim" (click)="onSearch()">
              <i class="pi pi-search"></i> Tìm
            </button>
          </div>
          <div class="toolbar-right">
            <button class="btn-green" (click)="onAddNew()">
              <i class="pi pi-plus"></i> Thêm cấu hình mới
            </button>
          </div>
        </div>

        <!-- Table -->
        <div class="wf-table-wrap">
          <table class="wf-table">
            <thead>
              <tr>
                <th style="width: 80px;">STT</th>
                <th>Mã phân hệ (Module Code)</th>
                <th>Định dạng được phép</th>
                <th>Dung lượng tối đa (MB)</th>
                <th class="col-hd">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <!-- skeleton loading rows -->
              <ng-container *ngIf="loading">
                <tr *ngFor="let item of [1, 2, 3]">
                  <td class="col-stt"><div class="skeleton-shimmer" style="height: 16px; width: 24px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 120px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 200px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 60px; border-radius: 4px;"></div></td>
                  <td class="col-hd"><div class="skeleton-shimmer" style="height: 24px; width: 60px; border-radius: 4px;"></div></td>
                </tr>
              </ng-container>

              <ng-container *ngIf="!loading">
                <tr *ngFor="let c of filteredConfigs; let i = index">
                  <td class="col-stt text-muted">{{ i + 1 }}</td>
                  <td><b>{{ c.moduleCode }}</b></td>
                  <td>
                    <span *ngFor="let ext of splitExtensions(c.allowedExtensions)" 
                          class="status-pill status-active" 
                          style="background-color: #f1f5f9; color: #334155; border-color: #cbd5e1; margin-right: 4px; font-size: 0.78rem;">
                      {{ ext }}
                    </span>
                  </td>
                  <td><b class="text-orange" style="color: #FF6B00;">{{ c.maxFileSizeMb }} MB</b></td>
                  <td class="col-hd">
                    <button class="act-btn act-edit" (click)="onEdit(c)" title="Chỉnh sửa"><i class="pi pi-pencil"></i></button>
                    <button class="act-btn act-delete" (click)="onDelete(c)" title="Xóa"><i class="pi pi-trash"></i></button>
                  </td>
                </tr>
                <tr *ngIf="filteredConfigs.length === 0">
                  <td colspan="5" class="empty-row">
                    <i class="pi pi-inbox"></i>
                    <div>Không tìm thấy cấu hình nào phù hợp.</div>
                  </td>
                </tr>
              </ng-container>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer">
          <span class="record-count">Tổng số: <b>{{ configs.length }}</b> phân hệ đã cấu hình.</span>
        </div>
      </div>
    </div>

    <!-- Dialog Thêm/Sửa Cấu hình -->
    <p-dialog [(visible)]="displayDialog" [header]="dialogHeader" [modal]="true" [style]="{ width: '450px' }" styleClass="evn-dialog-custom">
      <div style="display: flex; flex-direction: column; gap: 14px; padding-top: 10px;">
        <div class="form-group">
          <label class="form-label">Mã phân hệ <span class="required">*</span></label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentConfig.moduleCode" placeholder="Ví dụ: THIET_BI, OCR, HO_SO..." [disabled]="isEdit || saving" />
        </div>
        
        <div class="form-group">
          <label class="form-label">Định dạng file được phép (Phân tách bằng dấu phẩy) <span class="required">*</span></label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentConfig.allowedExtensions" placeholder="Ví dụ: pdf,docx,xlsx,jpg" [disabled]="saving" />
          <small class="text-muted" style="font-size: 0.75rem;">Chỉ nhập phần mở rộng của file, không chứa dấu chấm.</small>
        </div>

        <div class="form-group">
          <label class="form-label">Dung lượng tối đa của một file (MB) <span class="required">*</span></label>
          <input type="number" class="wf-input w-full" [(ngModel)]="currentConfig.maxFileSizeMb" min="1" [disabled]="saving" />
        </div>
      </div>
      
      <ng-template #footer>
        <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayDialog = false" [disabled]="saving">Hủy</button>
          <button class="btn-save btn-small" (click)="onSaveConfig()" [disabled]="saving">
            <i class="pi pi-spin pi-spinner" *ngIf="saving" style="margin-right: 4px;"></i>
            Lưu
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
  `
})
export class UploadConfigComponent implements OnInit {
  configs: any[] = [];
  filteredConfigs: any[] = [];
  searchKeyword = '';

  displayDialog = false;
  dialogHeader = '';
  isEdit = false;
  currentConfig: any = {};
  
  loading = false;
  saving = false;

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/upload-configs`;

  constructor(
    private http: HttpClient,
    private messageService: MessageService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit() {
    this.loadConfigs();
  }

  loadConfigs() {
    this.loading = true;
    this.http.get<any[]>(this.apiUrl).subscribe({
      next: (data) => {
        this.configs = data;
        this.onSearch();
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải cấu hình tải lên.' });
      }
    });
  }

  onSearch() {
    if (this.searchKeyword) {
      const kw = this.searchKeyword.toLowerCase();
      this.filteredConfigs = this.configs.filter(c => 
        c.moduleCode.toLowerCase().includes(kw) || 
        c.allowedExtensions.toLowerCase().includes(kw)
      );
    } else {
      this.filteredConfigs = [...this.configs];
    }
  }

  splitExtensions(allowedExtensions: string): string[] {
    if (!allowedExtensions) return [];
    return allowedExtensions.split(',').map(e => e.trim().toUpperCase());
  }

  onAddNew() {
    this.isEdit = false;
    this.currentConfig = { moduleCode: '', allowedExtensions: 'pdf,docx,xlsx,jpg,png', maxFileSizeMb: 10 };
    this.dialogHeader = 'Thêm mới cấu hình Upload';
    this.displayDialog = true;
  }

  onEdit(config: any) {
    this.isEdit = true;
    this.currentConfig = { ...config };
    this.dialogHeader = 'Chỉnh sửa cấu hình Upload';
    this.displayDialog = true;
  }

  onSaveConfig() {
    if (!this.currentConfig.moduleCode || !this.currentConfig.allowedExtensions || !this.currentConfig.maxFileSizeMb) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập đầy đủ thông tin bắt buộc.' });
      return;
    }

    if (this.currentConfig.maxFileSizeMb <= 0) {
      this.messageService.add({ severity: 'error', summary: 'Giá trị không hợp lệ', detail: 'Dung lượng tối đa phải lớn hơn 0 MB.' });
      return;
    }

    this.saving = true;
    if (this.isEdit) {
      this.http.put(`${this.apiUrl}/${this.currentConfig.id}`, this.currentConfig).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật cấu hình thành công!' });
          this.loadConfigs();
          this.displayDialog = false;
          this.saving = false;
        },
        error: (err) => {
          this.saving = false;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể cập nhật cấu hình.' });
        }
      });
    } else {
      this.http.post(this.apiUrl, this.currentConfig).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Tạo cấu hình mới thành công!' });
          this.loadConfigs();
          this.displayDialog = false;
          this.saving = false;
        },
        error: (err) => {
          this.saving = false;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Tạo cấu hình mới thất bại.' });
        }
      });
    }
  }

  onDelete(config: any) {
    this.confirmationService.confirm({
      message: `Bạn có chắc chắn muốn xóa cấu hình cho phân hệ ${config.moduleCode}?`,
      header: 'Xác nhận xóa',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Đồng ý',
      rejectLabel: 'Hủy',
      accept: () => {
        this.http.delete(`${this.apiUrl}/${config.id}`).subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Xóa thành công', detail: 'Đã xóa cấu hình thành công!' });
            this.loadConfigs();
          },
          error: (err) => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể xóa cấu hình này.' });
          }
        });
      }
    });
  }
}
