// E:\ecoit\sohoax10\sohoa.frontend\apps\admin-portal\src\app\features\administration\upload-config.component.ts
import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService, ConfirmationService } from 'primeng/api';
import { environment } from '@env/environment';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-upload-config',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule],
  providers: [MessageService],
  templateUrl: './upload-config.component.html',
  styleUrl: './upload-config.component.scss'
})
export class UploadConfigComponent implements OnInit {
  configs = signal<any[]>([]);
  filteredConfigs = signal<any[]>([]); // computed instead
  searchKeyword = signal<string>('');

  displayDialog = signal<boolean>(false);
  dialogHeader = signal<string>('');
  isEdit = signal<boolean>(false);
  currentConfig = signal<any>({});
  
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/upload-configs`;

  // Computed signal for filteredConfigs
  computedFilteredConfigs = computed(() => {
    const kw = this.searchKeyword().toLowerCase().trim();
    const allConfigs = this.configs() || [];
    if (!kw) {
      return [...allConfigs];
    }
    return allConfigs.filter(c => 
      (c.moduleCode?.toLowerCase().includes(kw) ?? false) || 
      (c.allowedExtensions?.toLowerCase().includes(kw) ?? false)
    );
  });

  constructor(
    private http: HttpClient,
    private messageService: MessageService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit() {
    this.loadConfigs();
  }

  loadConfigs() {
    this.loading.set(true);
    this.http.get<any[]>(this.apiUrl)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (data) => {
          this.configs.set(data || []);
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải cấu hình tải lên.' });
        }
      });
  }

  onSearch() {
    // Tự động thông qua computed
  }

  splitExtensions(allowedExtensions: string): string[] {
    if (!allowedExtensions) return [];
    return allowedExtensions.split(',').map(e => e.trim().toUpperCase());
  }

  onAddNew() {
    this.isEdit.set(false);
    this.currentConfig.set({ moduleCode: '', allowedExtensions: 'pdf,docx,xlsx,jpg,png', maxFileSizeMb: 10 });
    this.dialogHeader.set('Thêm mới cấu hình Upload');
    this.displayDialog.set(true);
  }

  onEdit(config: any) {
    this.isEdit.set(true);
    this.currentConfig.set({ ...config });
    this.dialogHeader.set('Chỉnh sửa cấu hình Upload');
    this.displayDialog.set(true);
  }

  onSaveConfig() {
    const configDraft = this.currentConfig();
    if (!configDraft.moduleCode || !configDraft.allowedExtensions || !configDraft.maxFileSizeMb) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập đầy đủ thông tin bắt buộc.' });
      return;
    }

    if (configDraft.maxFileSizeMb <= 0) {
      this.messageService.add({ severity: 'error', summary: 'Giá trị không hợp lệ', detail: 'Dung lượng tối đa phải lớn hơn 0 MB.' });
      return;
    }

    this.saving.set(true);
    if (this.isEdit()) {
      this.http.put(`${this.apiUrl}/${configDraft.id}`, configDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật cấu hình thành công!' });
            this.loadConfigs();
            this.displayDialog.set(false);
          },
          error: (err) => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể cập nhật cấu hình.' });
          }
        });
    } else {
      this.http.post(this.apiUrl, configDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Tạo cấu hình mới thành công!' });
            this.loadConfigs();
            this.displayDialog.set(false);
          },
          error: (err) => {
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
