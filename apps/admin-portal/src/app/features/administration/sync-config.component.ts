import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { FormsModule } from '@angular/forms';
import { ProgressSpinnerModule } from 'primeng/progressspinner';

@Component({
  selector: 'app-sync-config',
  standalone: true,
  imports: [CommonModule, CardModule, ButtonModule, InputTextModule, InputNumberModule, ToastModule, FormsModule, ProgressSpinnerModule],
  providers: [MessageService],
  template: `
    <div class="p-4">
      <p-toast></p-toast>
      <p-card header="Cấu hình Đồng bộ PMIS">
        <p class="mb-4 text-secondary">
          Thiết lập tham số đồng bộ hệ thống và thực hiện đồng bộ dữ liệu ngay lập tức với hệ thống PMIS.
        </p>

        <div class="grid">
          <div class="col-12 md:col-6">
            <div class="field mb-4">
              <label for="apiUrl" class="block mb-2 font-bold">PMIS API URL</label>
              <input id="apiUrl" type="text" pInputText [(ngModel)]="syncConfig.apiUrl" class="w-full" />
            </div>
            
            <div class="field mb-4">
              <label for="syncInterval" class="block mb-2 font-bold">Chu kỳ đồng bộ tự động (phút)</label>
              <p-inputNumber inputId="syncInterval" [(ngModel)]="syncConfig.syncInterval" class="w-full" [style]="{'width':'100%'}"></p-inputNumber>
            </div>

            <div class="field mb-4">
              <label for="apiKey" class="block mb-2 font-bold">API Key (Secret)</label>
              <input id="apiKey" type="password" pInputText [(ngModel)]="syncConfig.apiKey" class="w-full" />
            </div>

            <div class="flex gap-2">
              <p-button label="Lưu cấu hình" icon="pi pi-save" (onClick)="saveConfig()"></p-button>
              <p-button label="Đồng bộ PMIS ngay lập tức" icon="pi pi-sync" severity="warning" (onClick)="syncNow()" [loading]="isSyncing"></p-button>
            </div>
          </div>
          
          <div class="col-12 md:col-6 flex align-items-center justify-content-center">
            <div *ngIf="isSyncing" class="text-center">
              <p-progressSpinner></p-progressSpinner>
              <p class="mt-3 font-bold text-primary">Đang đồng bộ dữ liệu với PMIS...</p>
            </div>
          </div>
        </div>
      </p-card>
    </div>
  `
})
export class SyncConfigComponent implements OnInit {
  syncConfig = {
    apiUrl: 'https://api.pmis.evn.vn/v1/sync',
    syncInterval: 60,
    apiKey: '************************'
  };

  isSyncing = false;

  constructor(private messageService: MessageService) {}

  ngOnInit() {}

  saveConfig() {
    this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu cấu hình đồng bộ!' });
  }

  syncNow() {
    this.isSyncing = true;
    this.messageService.add({ severity: 'info', summary: 'Đang xử lý', detail: 'Bắt đầu đồng bộ PMIS...' });
    
    // Simulate API call for sync
    setTimeout(() => {
      this.isSyncing = false;
      this.messageService.add({ severity: 'success', summary: 'Đồng bộ hoàn tất', detail: 'Đã đồng bộ 156 bản ghi thành công.' });
    }, 2500);
  }
}
