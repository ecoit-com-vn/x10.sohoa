import { Component, OnInit, inject, PLATFORM_ID } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { MessageService } from 'primeng/api';
import { HttpClientModule } from '@angular/common/http';
import { AuthService } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-sync-config',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, ProgressSpinnerModule, HttpClientModule, WfBreadcrumbComponent],
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

  private messageService = inject(MessageService);
  public authService = inject(AuthService);
  private platformId = inject(PLATFORM_ID);

  ngOnInit() {
    this.loadConfig();
  }

  loadConfig() {
    if (isPlatformBrowser(this.platformId)) {
      try {
        const savedConfig = localStorage.getItem('pmis_sync_config');
        if (savedConfig) {
          const config = JSON.parse(savedConfig);
          this.syncConfig = {
            apiUrl: config.apiUrl || this.syncConfig.apiUrl,
            syncInterval: config.syncInterval || this.syncConfig.syncInterval,
            apiKey: config.apiKey || this.syncConfig.apiKey,
            forceOverwrite: config.forceOverwrite ?? this.syncConfig.forceOverwrite
          };
        }
      } catch (err) {
        console.warn('Không thể tải cấu hình PMIS từ localStorage:', err);
      }
    }
  }

  saveConfig() {
    this.saving = true;
    if (isPlatformBrowser(this.platformId)) {
      setTimeout(() => {
        try {
          localStorage.setItem('pmis_sync_config', JSON.stringify(this.syncConfig));
          this.messageService.add({ 
            severity: 'success', 
            summary: 'Thành công', 
            detail: 'Đã lưu cấu hình tham số đồng bộ PMIS (Lưu cục bộ)!' 
          });
        } catch (err) {
          console.error('Lỗi khi lưu cấu hình', err);
          this.messageService.add({ 
            severity: 'error', 
            summary: 'Lỗi', 
            detail: 'Không thể lưu cấu hình tham số đồng bộ.' 
          });
        }
        this.saving = false;
      }, 800);
    } else {
      this.saving = false;
    }
  }

  syncNow() {
    this.isSyncing = true;
    this.messageService.add({ 
      severity: 'info', 
      summary: 'Kích hoạt', 
      detail: 'Đang giả lập kết nối và đồng bộ dữ liệu từ PMIS...' 
    });
    
    if (isPlatformBrowser(this.platformId)) {
      setTimeout(() => {
        this.isSyncing = false;
        this.messageService.add({ 
          severity: 'success', 
          summary: 'Đồng bộ hoàn tất', 
          detail: 'Đồng bộ dữ liệu PMIS giả lập đã được thực hiện thành công!' 
        });
      }, 2000);
    } else {
      this.isSyncing = false;
    }
  }
}
