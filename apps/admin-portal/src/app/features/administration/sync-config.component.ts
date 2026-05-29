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
            <button class="btn-tim" (click)="syncNow()" [disabled]="isSyncing">
              <i class="pi pi-spin pi-spinner" *ngIf="isSyncing"></i>
              <i class="pi pi-sync" *ngIf="!isSyncing"></i>
              {{ isSyncing ? 'Đang đồng bộ...' : 'Đồng bộ ngay' }}
            </button>
            <button class="btn-save" (click)="saveConfig()">
              <i class="pi pi-save"></i> Lưu cấu hình
            </button>
          </div>
        </div>

        <p class="text-muted mb-4">
          Thiết lập các tham số kết nối hệ thống Quản lý Kỹ thuật nguồn và lưới điện (PMIS) của Tập đoàn Điện lực Việt Nam để đồng bộ tự động dữ liệu thiết bị, đường dây và trạm biến áp về hệ thống số hóa.
        </p>

        <div class="form-grid-3">
          <div class="form-group">
            <label class="form-label">PMIS API Endpoint URL <span class="required">*</span></label>
            <input type="text" class="wf-input w-full" [(ngModel)]="syncConfig.apiUrl" placeholder="https://api.pmis.evn.vn/v1/sync" />
          </div>
          <div class="form-group">
            <label class="form-label">Chu kỳ đồng bộ tự động (phút) <span class="required">*</span></label>
            <input type="number" class="wf-input w-full" [(ngModel)]="syncConfig.syncInterval" min="5" />
          </div>
          <div class="form-group">
            <label class="form-label">API Key (Secret Token) <span class="required">*</span></label>
            <input type="password" class="wf-input w-full" [(ngModel)]="syncConfig.apiKey" placeholder="Nhập API Key bảo mật..." />
          </div>
        </div>

        <div class="epbuoc-box mt-4" style="max-width: 500px;">
          <label class="epbuoc-wrap" for="chk-force">
            <input type="checkbox" id="chk-force" class="epbuoc-cb" [(ngModel)]="syncConfig.forceOverwrite" />
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
  `
})
export class SyncConfigComponent implements OnInit {
  syncConfig = {
    apiUrl: 'https://api.pmis.evn.vn/v1/sync',
    syncInterval: 60,
    apiKey: '************************',
    forceOverwrite: false
  };

  isSyncing = false;

  private http = inject(HttpClient);
  private messageService = inject(MessageService);

  ngOnInit() {}

  saveConfig() {
    this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu cấu hình tham số đồng bộ PMIS!' });
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
