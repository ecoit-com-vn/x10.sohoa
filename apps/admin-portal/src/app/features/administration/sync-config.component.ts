import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { MessageService } from 'primeng/api';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-sync-config',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, ProgressSpinnerModule, HttpClientModule],
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
          <span class="bc-current">Cấu hình đồng bộ PMIS</span>
        </div>

        <div class="edit-header">
          <h2 class="edit-title">Thiết lập kết nối PMIS</h2>
          <div class="edit-actions">
            <button class="btn-tim" (click)="confirmSync()" [disabled]="isSyncing || saving">
              <i class="pi pi-spin pi-spinner" *ngIf="isSyncing"></i>
              <i class="pi pi-sync" *ngIf="!isSyncing"></i>
              {{ isSyncing ? 'Đang đồng bộ...' : 'Đồng bộ ngay' }}
            </button>
            <button class="btn-save" (click)="saveConfig()" [disabled]="saving || isSyncing">
              <i class="pi pi-spin pi-spinner" *ngIf="saving" style="margin-right: 4px;"></i>
              <i class="pi pi-save" *ngIf="!saving"></i> Lưu cấu hình
            </button>
          </div>
        </div>

        <p class="text-muted mb-4">
          Thiết lập các tham số kết nối hệ thống Quản lý Kỹ thuật nguồn và lưới điện (PMIS) của Tập đoàn Điện lực Việt Nam để đồng bộ tự động dữ liệu thiết bị, đường dây và trạm biến áp về hệ thống số hóa.
        </p>

        <div class="form-grid-3">
          <div class="form-group">
            <label class="form-label">PMIS API Endpoint URL <span class="required">*</span></label>
            <input type="text" class="wf-input w-full" [(ngModel)]="syncConfig.apiUrl" placeholder="https://api.pmis.evn.vn/v1/sync" [disabled]="saving || isSyncing" />
          </div>
          <div class="form-group">
            <label class="form-label">Chu kỳ đồng bộ tự động (phút) <span class="required">*</span></label>
            <input type="number" class="wf-input w-full" [(ngModel)]="syncConfig.syncInterval" min="5" [disabled]="saving || isSyncing" />
          </div>
          <div class="form-group">
            <label class="form-label">API Key (Secret Token) <span class="required">*</span></label>
            <input type="password" class="wf-input w-full" [(ngModel)]="syncConfig.apiKey" placeholder="Nhập API Key bảo mật..." [disabled]="saving || isSyncing" />
          </div>
        </div>

        <div class="epbuoc-box mt-4" style="max-width: 500px;">
          <label class="epbuoc-wrap" for="chk-force">
            <input type="checkbox" id="chk-force" class="epbuoc-cb" [(ngModel)]="syncConfig.forceOverwrite" [disabled]="saving || isSyncing" />
            <span class="epbuoc-label">Ghi đè dữ liệu cục bộ</span>
          </label>
          <div class="epbuoc-note">
            Nếu chọn, dữ liệu đồng bộ từ PMIS sẽ tự động ghi đè lên các thay đổi thủ công cục bộ hiện tại.
          </div>
        </div>

        <!-- Sync Progress Display -->
        <div *ngIf="isSyncing" class="mt-4 p-4 text-center" style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px;">
          <p-progressSpinner [style]="{width: '50px', height: '50px'}" strokeWidth="4" animationDuration=".8s"></p-progressSpinner>
          <h4 class="m-2 font-bold" style="color: #002D72;">Đang kích hoạt tiến trình nền và kéo dữ liệu PMIS...</h4>
          <p class="text-xs text-muted m-0">Vui lòng không đóng trình duyệt cho đến khi tiến trình hoàn tất.</p>
        </div>

      </div>
    </div>

    <!-- Confirm Modal Overlay -->
    <div *ngIf="showConfirmModal" class="confirm-modal-overlay">
      <div class="confirm-modal-card">
        <div class="confirm-modal-header">
          <i class="pi pi-exclamation-triangle confirm-modal-icon"></i>
          <h3 class="confirm-modal-title">Xác nhận Đồng bộ lớn từ PMIS</h3>
        </div>
        <div class="confirm-modal-body">
          <p class="mb-3">
            Hệ thống chuẩn bị kích hoạt tiến trình đồng bộ dữ liệu thiết bị, đường dây và trạm biến áp từ kho dữ liệu PMIS của EVN.
          </p>
          <div class="mb-3 text-red-warning" *ngIf="syncConfig.forceOverwrite">
            <i class="pi pi-info-circle"></i> <b>Cảnh báo nghiêm trọng:</b> Tùy chọn <b>'Ghi đè dữ liệu cục bộ'</b> đang được kích hoạt. Các thông tin được hiệu đính thủ công trên hệ thống số hóa có thể bị ghi đè hoàn toàn bởi dữ liệu nguồn từ PMIS!
          </div>
          <p class="m-0 text-muted">
            Tiến trình đồng bộ lớn này có thể mất vài phút và gây tải nặng tạm thời lên tài nguyên mạng của hệ thống. Bạn có chắc chắn muốn bắt đầu?
          </p>
        </div>
        <div class="confirm-modal-footer">
          <button class="btn-cancel" (click)="showConfirmModal = false">Hủy thao tác</button>
          <button class="btn-confirm-action" (click)="executeSync()">Bắt đầu Đồng bộ</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .confirm-modal-overlay {
      position: fixed;
      top: 0;
      left: 0;
      width: 100vw;
      height: 100vh;
      background: rgba(15, 23, 42, 0.6);
      backdrop-filter: blur(4px);
      display: flex;
      justify-content: center;
      align-items: center;
      z-index: 9999;
    }
    .confirm-modal-card {
      background: #ffffff;
      border-radius: 12px;
      width: 500px;
      max-width: 90%;
      box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04);
      border: 1px solid #e2e8f0;
      overflow: hidden;
      animation: modalFadeIn 0.25s ease-out;
    }
    @keyframes modalFadeIn {
      from { transform: scale(0.95); opacity: 0; }
      to { transform: scale(1); opacity: 1; }
    }
    .confirm-modal-header {
      background: #fff5f5;
      padding: 18px 24px;
      display: flex;
      align-items: center;
      gap: 12px;
      border-bottom: 1px solid #fee2e2;
    }
    .confirm-modal-icon {
      font-size: 1.5rem;
      color: #dc2626;
    }
    .confirm-modal-title {
      margin: 0;
      font-size: 1.15rem;
      font-weight: 700;
      color: #991b1b;
    }
    .confirm-modal-body {
      padding: 24px;
      font-size: 0.875rem;
      line-height: 1.6;
      color: #334155;
    }
    .text-red-warning {
      color: #b91c1c;
      background: #fef2f2;
      padding: 10px 14px;
      border-radius: 6px;
      border-left: 4px solid #ef4444;
    }
    .confirm-modal-footer {
      padding: 16px 24px;
      background: #f8fafc;
      border-top: 1px solid #e2e8f0;
      display: flex;
      justify-content: flex-end;
      gap: 12px;
    }
    .btn-cancel {
      background: #ffffff;
      border: 1px solid #cbd5e1;
      color: #475569;
      padding: 8px 16px;
      font-size: 0.875rem;
      font-weight: 600;
      border-radius: 6px;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-cancel:hover {
      background: #f1f5f9;
      border-color: #94a3b8;
    }
    .btn-confirm-action {
      background: #dc2626;
      border: none;
      color: #ffffff;
      padding: 8px 16px;
      font-size: 0.875rem;
      font-weight: 600;
      border-radius: 6px;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-confirm-action:hover {
      background: #b91c1c;
      box-shadow: 0 4px 6px -1px rgba(220, 38, 38, 0.2);
    }
  `]
})
export class SyncConfigComponent implements OnInit {
  syncConfig = {
    apiUrl: 'https://api.pmis.evn.vn/v1/sync',
    syncInterval: 60,
    apiKey: '************************',
    forceOverwrite: false
  };

  isSyncing = false;
  saving = false;
  showConfirmModal = false;

  confirmSync() {
    this.showConfirmModal = true;
  }

  executeSync() {
    this.showConfirmModal = false;
    this.syncNow();
  }

  private http = inject(HttpClient);
  private messageService = inject(MessageService);

  ngOnInit() {
    this.loadConfig();
  }

  loadConfig() {
    this.http.get<any>(`${environment.apiGatewayUrl}/api/v1/sync/config`).subscribe({
      next: (config) => {
        if (config) {
          this.syncConfig = {
            apiUrl: config.apiUrl || this.syncConfig.apiUrl,
            syncInterval: config.syncInterval || this.syncConfig.syncInterval,
            apiKey: config.apiKey || this.syncConfig.apiKey,
            forceOverwrite: config.forceOverwrite ?? this.syncConfig.forceOverwrite
          };
        }
      },
      error: (err) => {
        console.warn('Không thể tải cấu hình PMIS từ backend, dùng mặc định:', err);
      }
    });
  }

  saveConfig() {
    this.saving = true;
    this.http.post(`${environment.apiGatewayUrl}/api/v1/sync/config`, this.syncConfig).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu cấu hình tham số đồng bộ PMIS!' });
        this.saving = false;
      },
      error: (err) => {
        this.saving = false;
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể lưu cấu hình tham số đồng bộ.' });
      }
    });
  }

  syncNow() {
    this.isSyncing = true;
    this.messageService.add({ severity: 'info', summary: 'Kích hoạt', detail: 'Đang gửi yêu cầu kích hoạt đồng bộ nền lên máy chủ...' });
    
    this.http.post(`${environment.apiGatewayUrl}/api/v1/sync/trigger-now`, {}).subscribe({
      next: (res: any) => {
        this.isSyncing = false;
        this.messageService.add({ 
          severity: 'success', 
          summary: 'Đồng bộ hoàn tất', 
          detail: 'Yêu cầu đồng bộ PMIS đã được kích hoạt chạy ngầm thành công!' 
        });
      },
      error: (err) => {
        this.isSyncing = false;
        console.error('Trigger sync error', err);
        this.messageService.add({ 
          severity: 'error', 
          summary: 'Lỗi đồng bộ', 
          detail: 'Kích hoạt tiến trình đồng bộ ngầm thất bại. Vui lòng thử lại.' 
        });
      }
    });
  }
}
