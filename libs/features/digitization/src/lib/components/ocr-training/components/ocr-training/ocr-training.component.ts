import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ToastModule } from 'primeng/toast';
// DropdownModule removed — using native <select> instead
import { DialogModule } from 'primeng/dialog';
import { ProgressBarModule } from 'primeng/progressbar';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { environment } from '@env/environment';

// ─── Models ─────────────────────────────────────────────────────────────────

interface OcrTrainingItem {
  id: number;
  fileName: string;
  contentType: string;
  fileSize: number;
  documentType: string;
  trainingStatus: string;
  isVerified: boolean;
  qualityScore: number | null;
  uploadedBy: string;
  uploadedAt: string;
}

interface OcrTrainingDetail extends OcrTrainingItem {
  filePath: string;
  bucketName: string;
  labelText: string | null;
  notes: string | null;
  verifiedBy: string | null;
  verifiedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

interface PagedResult {
  items: OcrTrainingItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

interface Statistics {
  pending: number;
  labeled: number;
  verified: number;
  rejected: number;
  total: number;
}

// ─── Component ──────────────────────────────────────────────────────────────

@Component({
  selector: 'app-ocr-training',
  standalone: true,
  imports: [
    CommonModule, FormsModule, HttpClientModule,
    TableModule, ButtonModule, CardModule, ToastModule,
    DialogModule, ProgressBarModule, TooltipModule
  ],
  providers: [MessageService],
  templateUrl: './ocr-training.component.html',
  styleUrl: './ocr-training.component.scss'
})
export class OcrTrainingComponent implements OnInit {

  private readonly API_BASE = `${environment.apiGatewayUrl}/api/v1/ocr-training`;

  // State
  items: OcrTrainingItem[] = [];
  statistics: Statistics | null = null;
  loading = false;
  totalCount = 0;
  page = 1;
  pageSize = 10;
  totalPages = 1;
  Math = Math;

  // Filters
  keyword = '';
  filterDocType = '';
  filterStatus = '';

  // Dialog states
  showUploadDialog = false;
  showLabelDialog = false;
  showVerifyDialog = false;
  showDeleteDialog = false;
  selectedItem: OcrTrainingItem | null = null;

  // Upload
  selectedFile: File | null = null;
  uploading = false;
  uploadProgress = 0;
  uploadForm = {
    documentType: 'Other',
    labelText: '',
    notes: '',
    uploadedBy: 'admin'
  };

  // Label form
  labelForm = {
    documentType: 'Other',
    trainingStatus: 'Labeled',
    qualityScore: null as number | null,
    labelText: '',
    notes: ''
  };

  // Verify form
  verifyForm = {
    isVerified: true,
    verifiedBy: '',
    notes: ''
  };

  constructor(private http: HttpClient, private messageService: MessageService) {}

  ngOnInit(): void {
    this.loadData();
    this.loadStatistics();
  }

  loadData(): void {
    this.loading = true;
    const params: Record<string, string> = {
      page: this.page.toString(),
      pageSize: this.pageSize.toString()
    };
    if (this.keyword) params['keyword'] = this.keyword;
    if (this.filterDocType) params['documentType'] = this.filterDocType;
    if (this.filterStatus) params['trainingStatus'] = this.filterStatus;

    const queryStr = new URLSearchParams(params).toString();
    this.http.get<PagedResult>(`${this.API_BASE}?${queryStr}`).subscribe({
      next: (res) => {
        this.items = res.items || [];
        this.totalCount = res.totalCount || 0;
        this.totalPages = res.totalPages || 1;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading OCR training data', err);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi tải dữ liệu',
          detail: 'Không thể tải danh sách dữ liệu huấn luyện từ máy chủ.'
        });
        this.items = [];
        this.totalCount = 0;
        this.totalPages = 1;
        this.loading = false;
      }
    });
  }

  loadStatistics(): void {
    this.http.get<Statistics>(`${this.API_BASE}/statistics`).subscribe({
      next: (res) => { this.statistics = res; },
      error: (err) => {
        console.error('Error loading OCR statistics', err);
        this.statistics = null;
      }
    });
  }

  // ─── Actions ───────────────────────────────────────────────────────────────

  onFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) this.selectedFile = input.files[0];
  }

  onFileDrop(event: DragEvent): void {
    event.preventDefault();
    const files = event.dataTransfer?.files;
    if (files?.length) this.selectedFile = files[0];
  }

  doUpload(): void {
    if (!this.selectedFile) return;
    this.uploading = true;
    this.uploadProgress = 0;

    const formData = new FormData();
    formData.append('file', this.selectedFile);
    formData.append('documentType', this.uploadForm.documentType);
    formData.append('labelText', this.uploadForm.labelText || '');
    formData.append('notes', this.uploadForm.notes || '');
    formData.append('uploadedBy', this.uploadForm.uploadedBy || 'admin');

    // Simulate progress
    const progressInterval = setInterval(() => {
      if (this.uploadProgress < 90) this.uploadProgress += 10;
    }, 200);

    this.http.post(`${this.API_BASE}/upload`, formData).subscribe({
      next: () => {
        clearInterval(progressInterval);
        this.uploadProgress = 100;
        setTimeout(() => {
          this.uploading = false;
          this.showUploadDialog = false;
          this.resetUpload();
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Upload dữ liệu huấn luyện thành công!' });
          this.loadData();
          this.loadStatistics();
        }, 500);
      },
      error: (err) => {
        clearInterval(progressInterval);
        this.uploading = false;
        this.uploadProgress = 0;
        console.error('Upload training file error', err);
        this.messageService.add({ 
          severity: 'error', 
          summary: 'Lỗi', 
          detail: 'Tải lên tệp tin huấn luyện thất bại. Vui lòng thử lại.' 
        });
      }
    });
  }

  openLabelDialog(item: OcrTrainingItem): void {
    this.selectedItem = item;
    this.labelForm = {
      documentType: item.documentType || 'Other',
      trainingStatus: item.trainingStatus,
      qualityScore: item.qualityScore,
      labelText: '',
      notes: ''
    };
    // Load full details for labelText
    this.http.get<OcrTrainingDetail>(`${this.API_BASE}/${item.id}`).subscribe({
      next: (detail) => {
        this.labelForm.labelText = detail.labelText || '';
        this.labelForm.notes = detail.notes || '';
      },
      error: () => {}
    });
    this.showLabelDialog = true;
  }

  saveLabel(): void {
    if (!this.selectedItem) return;
    this.http.put(`${this.API_BASE}/${this.selectedItem.id}/label`, this.labelForm).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu nhãn dữ liệu huấn luyện.' });
        this.showLabelDialog = false;
        this.loadData();
        this.loadStatistics();
      },
      error: (err) => {
        console.error('Save label error', err);
        this.messageService.add({ 
          severity: 'error', 
          summary: 'Lỗi', 
          detail: 'Không thể lưu nhãn dữ liệu huấn luyện.' 
        });
      }
    });
  }

  openVerifyDialog(item: OcrTrainingItem): void {
    this.selectedItem = item;
    this.verifyForm = { isVerified: true, verifiedBy: '', notes: '' };
    this.showVerifyDialog = true;
  }

  saveVerify(): void {
    if (!this.selectedItem || !this.verifyForm.verifiedBy) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập tên người xác nhận.' });
      return;
    }
    this.http.post(`${this.API_BASE}/${this.selectedItem.id}/verify`, this.verifyForm).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: `Bản ghi đã được ${this.verifyForm.isVerified ? 'xác nhận (Verified)' : 'từ chối (Rejected)'}.` });
        this.showVerifyDialog = false;
        this.loadData();
        this.loadStatistics();
      },
      error: (err) => {
        console.error('Verify error', err);
        this.messageService.add({ 
          severity: 'error', 
          summary: 'Lỗi', 
          detail: 'Không thể xác nhận chất lượng bản ghi này.' 
        });
      }
    });
  }

  confirmDelete(item: OcrTrainingItem): void {
    this.selectedItem = item;
    this.showDeleteDialog = true;
  }

  doDelete(): void {
    if (!this.selectedItem) return;
    this.http.delete(`${this.API_BASE}/${this.selectedItem.id}`).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xóa bản ghi dữ liệu huấn luyện.' });
        this.showDeleteDialog = false;
        this.loadData();
        this.loadStatistics();
      },
      error: (err) => {
        console.error('Delete error', err);
        this.messageService.add({ 
          severity: 'error', 
          summary: 'Lỗi', 
          detail: 'Xóa bản ghi dữ liệu huấn luyện thất bại.' 
        });
        this.showDeleteDialog = false;
      }
    });
  }

  resetUpload(): void {
    this.selectedFile = null;
    this.uploadProgress = 0;
    this.uploadForm = { documentType: 'Other', labelText: '', notes: '', uploadedBy: 'admin' };
  }

  goPage(p: number): void {
    this.page = p;
    this.loadData();
  }

  onPageSizeChange(): void {
    this.page = 1;
    this.loadData();
  }

  // ─── Helpers ───────────────────────────────────────────────────────────────

  isImage(ct: string): boolean {
    return ct?.startsWith('image/');
  }
  isPdf(ct: string): boolean {
    return ct === 'application/pdf';
  }

  getDocTypeLabel(type: string): string {
    const map: Record<string, string> = {
      SoDoLuoi: 'Sơ đồ lưới',
      SoDoTram: 'Sơ đồ trạm',
      BanVeKyThuat: 'Bản vẽ KT',
      KetQuaKiemTra: 'KQ kiểm tra',
      Other: 'Khác'
    };
    return map[type] || type;
  }

  getStatusLabel(status: string): string {
    const map: Record<string, string> = {
      Pending: '⏳ Chờ gán nhãn',
      Labeled: '🏷 Đã gán nhãn',
      Verified: '✅ Đã xác nhận',
      Rejected: '❌ Từ chối'
    };
    return map[status] || status;
  }

  formatFileSize(bytes: number): string {
    if (!bytes) return '—';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / 1048576).toFixed(1)} MB`;
  }

  formatDate(dateStr: string): string {
    if (!dateStr) return '—';
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString('vi-VN') + ' ' + d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
    } catch { return dateStr; }
  }

  truncate(text: string, maxLen: number): string {
    if (!text) return '';
    return text.length > maxLen ? text.substring(0, maxLen) + '...' : text;
  }

  // ─── Mock Data (Demo khi chưa có backend) ─────────────────────────────────

  getMockData(): OcrTrainingItem[] {
    return [
      { id: 1, fileName: 'so_do_luoi_110kv_ha_dong.jpg', contentType: 'image/jpeg', fileSize: 2345678, documentType: 'SoDoLuoi', trainingStatus: 'Verified', isVerified: true, qualityScore: 92, uploadedBy: 'nguyen.van.an', uploadedAt: '2026-05-20T08:30:00Z' },
      { id: 2, fileName: 'bien_ban_kiem_tra_tram_mai_dong.pdf', contentType: 'application/pdf', fileSize: 890432, documentType: 'KetQuaKiemTra', trainingStatus: 'Labeled', isVerified: false, qualityScore: 78, uploadedBy: 'tran.thi.bich', uploadedAt: '2026-05-21T10:15:00Z' },
      { id: 3, fileName: 'ban_ve_ky_thuat_duong_day_220kv.png', contentType: 'image/png', fileSize: 5123456, documentType: 'BanVeKyThuat', trainingStatus: 'Pending', isVerified: false, qualityScore: null, uploadedBy: 'le.van.cuong', uploadedAt: '2026-05-22T14:00:00Z' },
      { id: 4, fileName: 'so_do_tram_bien_ap_dong_da.tiff', contentType: 'image/tiff', fileSize: 8901234, documentType: 'SoDoTram', trainingStatus: 'Verified', isVerified: true, qualityScore: 95, uploadedBy: 'pham.quoc.dung', uploadedAt: '2026-05-23T09:45:00Z' },
      { id: 5, fileName: 'ket_qua_kiem_tra_dau_cuoi_nam_2025.pdf', contentType: 'application/pdf', fileSize: 1234567, documentType: 'KetQuaKiemTra', trainingStatus: 'Rejected', isVerified: false, qualityScore: 35, uploadedBy: 'hoang.thi.em', uploadedAt: '2026-05-24T11:20:00Z' },
      { id: 6, fileName: 'so_do_luoi_22kv_quan_cau_giay.jpg', contentType: 'image/jpeg', fileSize: 3456789, documentType: 'SoDoLuoi', trainingStatus: 'Labeled', isVerified: false, qualityScore: 65, uploadedBy: 'vu.manh.hung', uploadedAt: '2026-05-25T13:30:00Z' },
      { id: 7, fileName: 'ban_ve_mong_cot_dien_cat_linh.png', contentType: 'image/png', fileSize: 2789012, documentType: 'BanVeKyThuat', trainingStatus: 'Verified', isVerified: true, qualityScore: 88, uploadedBy: 'do.thi.giang', uploadedAt: '2026-05-26T07:00:00Z' },
      { id: 8, fileName: 'hs_nghiem_thu_duong_chet_2025.pdf', contentType: 'application/pdf', fileSize: 4567890, documentType: 'Other', trainingStatus: 'Pending', isVerified: false, qualityScore: null, uploadedBy: 'nguyen.van.an', uploadedAt: '2026-05-27T15:45:00Z' },
    ];
  }
}
