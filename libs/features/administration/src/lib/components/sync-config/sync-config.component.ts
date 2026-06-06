import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { MessageService } from 'primeng/api';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { environment } from '@env/environment';
import { AuthService } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-sync-config',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, ProgressSpinnerModule, HttpClientModule],
  providers: [MessageService],
  templateUrl: './sync-config.component.html',
  styleUrl: './sync-config.component.scss'
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
  public authService = inject(AuthService);

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
