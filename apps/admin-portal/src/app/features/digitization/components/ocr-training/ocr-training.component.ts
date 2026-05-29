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
import { environment } from '../../../../../environments/environment';

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
  template: `
    <div class="ocr-training-page p-4">
      <p-toast></p-toast>

      <!-- ═══════════════════════════════════════════════════════════════════
           BREADCRUMB
      ═══════════════════════════════════════════════════════════════════ -->
      <div class="breadcrumb mb-3">
        <span class="bc-item">Trang chủ</span>
        <span class="bc-sep">›</span>
        <span class="bc-item">Số hóa hồ sơ</span>
        <span class="bc-sep">›</span>
        <span class="bc-item active">Dữ liệu Huấn luyện OCR</span>
      </div>

      <!-- ═══════════════════════════════════════════════════════════════════
           STATISTICS CARDS
      ═══════════════════════════════════════════════════════════════════ -->
      <div class="stats-grid mb-4" *ngIf="statistics">
        <div class="stat-card total">
          <div class="stat-icon"><i class="pi pi-database"></i></div>
          <div class="stat-info">
            <div class="stat-value">{{ statistics.total }}</div>
            <div class="stat-label">Tổng số bản ghi</div>
          </div>
        </div>
        <div class="stat-card pending">
          <div class="stat-icon"><i class="pi pi-clock"></i></div>
          <div class="stat-info">
            <div class="stat-value">{{ statistics.pending }}</div>
            <div class="stat-label">Chờ gán nhãn</div>
          </div>
        </div>
        <div class="stat-card labeled">
          <div class="stat-icon"><i class="pi pi-tag"></i></div>
          <div class="stat-info">
            <div class="stat-value">{{ statistics.labeled }}</div>
            <div class="stat-label">Đã gán nhãn</div>
          </div>
        </div>
        <div class="stat-card verified">
          <div class="stat-icon"><i class="pi pi-check-circle"></i></div>
          <div class="stat-info">
            <div class="stat-value">{{ statistics.verified }}</div>
            <div class="stat-label">Đã xác nhận</div>
          </div>
        </div>
        <div class="stat-card rejected">
          <div class="stat-icon"><i class="pi pi-times-circle"></i></div>
          <div class="stat-info">
            <div class="stat-value">{{ statistics.rejected }}</div>
            <div class="stat-label">Từ chối</div>
          </div>
        </div>
      </div>

      <!-- ═══════════════════════════════════════════════════════════════════
           MAIN CARD
      ═══════════════════════════════════════════════════════════════════ -->
      <div class="main-card">
        <!-- Toolbar -->
        <div class="toolbar mb-3">
          <div class="toolbar-left">
            <input
              type="text"
              class="search-input"
              placeholder="Tìm kiếm tên file, người upload..."
              [(ngModel)]="keyword"
              (keyup.enter)="loadData()"
            />
            <button class="btn-search" (click)="loadData()">
              <i class="pi pi-search"></i> Tìm
            </button>
            <select class="filter-select" [(ngModel)]="filterDocType" (change)="loadData()">
              <option value="">-- Loại tài liệu --</option>
              <option value="SoDoLuoi">Sơ đồ lưới</option>
              <option value="SoDoTram">Sơ đồ trạm</option>
              <option value="BanVeKyThuat">Bản vẽ kỹ thuật</option>
              <option value="KetQuaKiemTra">Kết quả kiểm tra</option>
              <option value="Other">Khác</option>
            </select>
            <select class="filter-select" [(ngModel)]="filterStatus" (change)="loadData()">
              <option value="">-- Trạng thái --</option>
              <option value="Pending">Chờ gán nhãn</option>
              <option value="Labeled">Đã gán nhãn</option>
              <option value="Verified">Đã xác nhận</option>
              <option value="Rejected">Từ chối</option>
            </select>
          </div>
          <div class="toolbar-right">
            <button class="btn-upload" (click)="showUploadDialog = true">
              <i class="pi pi-upload"></i> Upload dữ liệu
            </button>
            <button class="btn-refresh" (click)="loadData(); loadStatistics()">
              <i class="pi pi-refresh"></i>
            </button>
          </div>
        </div>

        <!-- Data Table -->
        <div class="table-wrapper">
          <table class="evn-table">
            <thead>
              <tr>
                <th class="col-num">STT</th>
                <th>Tên file</th>
                <th>Loại tài liệu</th>
                <th>Kích thước</th>
                <th>Chất lượng</th>
                <th>Trạng thái</th>
                <th>Xác nhận</th>
                <th>Người upload</th>
                <th>Ngày upload</th>
                <th class="col-actions">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngIf="loading">
                <td colspan="10" class="loading-cell">
                  <i class="pi pi-spin pi-spinner"></i> Đang tải dữ liệu...
                </td>
              </tr>
              <tr *ngIf="!loading && items.length === 0">
                <td colspan="10" class="empty-cell">
                  <i class="pi pi-inbox"></i>
                  <div>Chưa có dữ liệu huấn luyện. Hãy upload file đầu tiên!</div>
                </td>
              </tr>
              <tr *ngFor="let item of items; let i = index" [class.row-verified]="item.isVerified">
                <td class="text-center">{{ (page - 1) * pageSize + i + 1 }}</td>
                <td>
                  <div class="file-info">
                    <i class="pi" [class.pi-image]="isImage(item.contentType)" [class.pi-file-pdf]="isPdf(item.contentType)" [class.pi-file]="!isImage(item.contentType) && !isPdf(item.contentType)"></i>
                    <span class="file-name" [title]="item.fileName">{{ truncate(item.fileName, 35) }}</span>
                  </div>
                </td>
                <td><span class="doc-type-badge">{{ getDocTypeLabel(item.documentType) }}</span></td>
                <td>{{ formatFileSize(item.fileSize) }}</td>
                <td>
                  <div *ngIf="item.qualityScore !== null" class="quality-bar-wrap">
                    <div class="quality-bar" [style.width.%]="item.qualityScore" [class.q-good]="item.qualityScore >= 70" [class.q-medium]="item.qualityScore >= 40 && item.qualityScore < 70" [class.q-poor]="item.qualityScore < 40"></div>
                    <span class="quality-label">{{ item.qualityScore }}%</span>
                  </div>
                  <span *ngIf="item.qualityScore === null" class="text-muted">—</span>
                </td>
                <td><span class="status-pill" [class]="'status-' + item.trainingStatus.toLowerCase()">{{ getStatusLabel(item.trainingStatus) }}</span></td>
                <td>
                  <i class="pi" [class.pi-check-circle]="item.isVerified" [class.pi-minus-circle]="!item.isVerified" [class.verified-icon]="item.isVerified" [class.unverified-icon]="!item.isVerified"></i>
                </td>
                <td>{{ item.uploadedBy }}</td>
                <td>{{ formatDate(item.uploadedAt) }}</td>
                <td>
                  <div class="action-buttons">
                    <button class="btn-act btn-label" (click)="openLabelDialog(item)" pTooltip="Gán nhãn / chỉnh sửa" tooltipPosition="top">
                      <i class="pi pi-pencil"></i>
                    </button>
                    <button class="btn-act btn-verify" (click)="openVerifyDialog(item)" pTooltip="Xác nhận chất lượng" tooltipPosition="top">
                      <i class="pi pi-shield"></i>
                    </button>
                    <button class="btn-act btn-delete" (click)="confirmDelete(item)" pTooltip="Xóa bản ghi" tooltipPosition="top">
                      <i class="pi pi-trash"></i>
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Pagination -->
        <div class="pagination-bar" *ngIf="totalCount > 0">
          <span class="page-info">Hiển thị {{ (page - 1) * pageSize + 1 }}–{{ Math.min(page * pageSize, totalCount) }} / {{ totalCount }} bản ghi</span>
          <div class="page-controls">
            <button class="page-btn" [disabled]="page === 1" (click)="goPage(1)"><i class="pi pi-angle-double-left"></i></button>
            <button class="page-btn" [disabled]="page === 1" (click)="goPage(page - 1)"><i class="pi pi-angle-left"></i></button>
            <span class="page-num">Trang {{ page }} / {{ totalPages }}</span>
            <button class="page-btn" [disabled]="page >= totalPages" (click)="goPage(page + 1)"><i class="pi pi-angle-right"></i></button>
            <button class="page-btn" [disabled]="page >= totalPages" (click)="goPage(totalPages)"><i class="pi pi-angle-double-right"></i></button>
            <select class="page-size-select" [(ngModel)]="pageSize" (change)="onPageSizeChange()">
              <option value="10">10 / trang</option>
              <option value="20">20 / trang</option>
              <option value="50">50 / trang</option>
            </select>
          </div>
        </div>
      </div>

      <!-- ═══════════════════════════════════════════════════════════════════
           UPLOAD DIALOG
      ═══════════════════════════════════════════════════════════════════ -->
      <p-dialog
        header="Upload dữ liệu huấn luyện OCR"
        [(visible)]="showUploadDialog"
        [modal]="true"
        [style]="{width: '560px'}"
        [closable]="true"
        styleClass="evn-dialog">

        <div class="dialog-body">
          <!-- Drop Zone -->
          <div class="drop-zone"
            (dragover)="$event.preventDefault()"
            (drop)="onFileDrop($event)"
            (click)="fileInput.click()">
            <i class="pi pi-cloud-upload dz-icon"></i>
            <div class="dz-text">
              <strong>Kéo thả file vào đây</strong> hoặc nhấn để chọn file
            </div>
            <div class="dz-hint">Hỗ trợ: JPEG, PNG, TIFF, BMP, PDF (tối đa 50MB)</div>
            <input #fileInput type="file" hidden accept=".jpg,.jpeg,.png,.tiff,.tif,.bmp,.pdf" (change)="onFileSelect($event)" />
          </div>

          <!-- Selected file preview -->
          <div class="selected-file" *ngIf="selectedFile">
            <i class="pi pi-file-pdf" *ngIf="isPdf(selectedFile.type)"></i>
            <i class="pi pi-image" *ngIf="isImage(selectedFile.type)"></i>
            <span class="sf-name">{{ selectedFile.name }}</span>
            <span class="sf-size">({{ formatFileSize(selectedFile.size) }})</span>
            <button class="sf-remove" (click)="selectedFile = null"><i class="pi pi-times"></i></button>
          </div>

          <!-- Form fields -->
          <div class="form-row">
            <label>Loại tài liệu <span class="required">*</span></label>
            <select class="filter-select w-full" [(ngModel)]="uploadForm.documentType">
              <option value="SoDoLuoi">Sơ đồ lưới</option>
              <option value="SoDoTram">Sơ đồ trạm</option>
              <option value="BanVeKyThuat">Bản vẽ kỹ thuật</option>
              <option value="KetQuaKiemTra">Kết quả kiểm tra</option>
              <option value="Other">Khác</option>
            </select>
          </div>
          <div class="form-row">
            <label>Văn bản nhãn (Ground Truth)</label>
            <textarea class="label-textarea" rows="4" [(ngModel)]="uploadForm.labelText"
              placeholder="Nhập văn bản chính xác của tài liệu để huấn luyện OCR (nếu có)..."></textarea>
          </div>
          <div class="form-row">
            <label>Ghi chú</label>
            <input type="text" class="search-input w-full" [(ngModel)]="uploadForm.notes" placeholder="Ghi chú bổ sung..." />
          </div>
          <div class="form-row">
            <label>Người upload</label>
            <input type="text" class="search-input w-full" [(ngModel)]="uploadForm.uploadedBy" placeholder="Tên tài khoản..." />
          </div>

          <!-- Progress bar -->
          <div class="upload-progress" *ngIf="uploading">
            <div class="progress-track">
              <div class="progress-fill" [style.width.%]="uploadProgress"></div>
            </div>
            <span class="progress-text">Đang upload... {{ uploadProgress }}%</span>
          </div>
        </div>

        <ng-template pTemplate="footer">
          <button class="btn-cancel" (click)="showUploadDialog = false; resetUpload()">Hủy</button>
          <button class="btn-upload" [disabled]="!selectedFile || uploading" (click)="doUpload()">
            <i class="pi pi-upload"></i> {{ uploading ? 'Đang upload...' : 'Upload ngay' }}
          </button>
        </ng-template>
      </p-dialog>

      <!-- ═══════════════════════════════════════════════════════════════════
           LABEL DIALOG
      ═══════════════════════════════════════════════════════════════════ -->
      <p-dialog
        header="Gán nhãn dữ liệu huấn luyện"
        [(visible)]="showLabelDialog"
        [modal]="true"
        [style]="{width: '600px'}"
        styleClass="evn-dialog">

        <div class="dialog-body" *ngIf="selectedItem">
          <div class="detail-info-row">
            <span class="detail-label">File:</span>
            <span class="detail-value"><i class="pi pi-file mr-1"></i> {{ selectedItem.fileName }}</span>
          </div>
          <div class="form-row">
            <label>Loại tài liệu <span class="required">*</span></label>
            <select class="filter-select w-full" [(ngModel)]="labelForm.documentType">
              <option value="SoDoLuoi">Sơ đồ lưới</option>
              <option value="SoDoTram">Sơ đồ trạm</option>
              <option value="BanVeKyThuat">Bản vẽ kỹ thuật</option>
              <option value="KetQuaKiemTra">Kết quả kiểm tra</option>
              <option value="Other">Khác</option>
            </select>
          </div>
          <div class="form-row">
            <label>Trạng thái</label>
            <select class="filter-select w-full" [(ngModel)]="labelForm.trainingStatus">
              <option value="Pending">Chờ gán nhãn</option>
              <option value="Labeled">Đã gán nhãn</option>
              <option value="Rejected">Từ chối</option>
            </select>
          </div>
          <div class="form-row">
            <label>Điểm chất lượng ảnh (0–100)</label>
            <input type="number" min="0" max="100" class="search-input w-full" [(ngModel)]="labelForm.qualityScore" placeholder="Ví dụ: 85" />
          </div>
          <div class="form-row">
            <label>Văn bản nhãn (Ground Truth) <span class="required">*</span></label>
            <textarea class="label-textarea" rows="6" [(ngModel)]="labelForm.labelText"
              placeholder="Nhập văn bản chính xác được nhận dạng từ ảnh, dùng làm dữ liệu huấn luyện OCR..."></textarea>
          </div>
          <div class="form-row">
            <label>Ghi chú</label>
            <input type="text" class="search-input w-full" [(ngModel)]="labelForm.notes" placeholder="Ghi chú bổ sung..." />
          </div>
        </div>

        <ng-template pTemplate="footer">
          <button class="btn-cancel" (click)="showLabelDialog = false">Hủy</button>
          <button class="btn-upload" (click)="saveLabel()">
            <i class="pi pi-save"></i> Lưu nhãn
          </button>
        </ng-template>
      </p-dialog>

      <!-- ═══════════════════════════════════════════════════════════════════
           VERIFY DIALOG
      ═══════════════════════════════════════════════════════════════════ -->
      <p-dialog
        header="Xác nhận chất lượng dữ liệu"
        [(visible)]="showVerifyDialog"
        [modal]="true"
        [style]="{width: '480px'}"
        styleClass="evn-dialog">

        <div class="dialog-body" *ngIf="selectedItem">
          <div class="detail-info-row">
            <span class="detail-label">File:</span>
            <span class="detail-value">{{ selectedItem.fileName }}</span>
          </div>
          <div class="verify-options">
            <div class="verify-option" [class.selected]="verifyForm.isVerified === true" (click)="verifyForm.isVerified = true">
              <i class="pi pi-check-circle verify-ok"></i>
              <div>
                <strong>Xác nhận (Verified)</strong>
                <p>Dữ liệu đạt chất lượng, dùng để huấn luyện mô hình OCR.</p>
              </div>
            </div>
            <div class="verify-option" [class.selected]="verifyForm.isVerified === false" (click)="verifyForm.isVerified = false">
              <i class="pi pi-times-circle verify-bad"></i>
              <div>
                <strong>Từ chối (Rejected)</strong>
                <p>Dữ liệu không đạt chất lượng, loại khỏi tập huấn luyện.</p>
              </div>
            </div>
          </div>
          <div class="form-row mt-3">
            <label>Người xác nhận <span class="required">*</span></label>
            <input type="text" class="search-input w-full" [(ngModel)]="verifyForm.verifiedBy" placeholder="Họ tên chuyên gia xác nhận..." />
          </div>
          <div class="form-row">
            <label>Lý do / Ghi chú</label>
            <textarea class="label-textarea" rows="3" [(ngModel)]="verifyForm.notes" placeholder="Ghi chú lý do xác nhận hoặc từ chối..."></textarea>
          </div>
        </div>

        <ng-template pTemplate="footer">
          <button class="btn-cancel" (click)="showVerifyDialog = false">Hủy</button>
          <button class="btn-upload" (click)="saveVerify()">
            <i class="pi pi-shield"></i> Xác nhận
          </button>
        </ng-template>
      </p-dialog>

      <!-- DELETE CONFIRM DIALOG -->
      <p-dialog
        header="Xác nhận xóa"
        [(visible)]="showDeleteDialog"
        [modal]="true"
        [style]="{width: '400px'}"
        styleClass="evn-dialog">
        <div class="dialog-body">
          <div class="delete-warning">
            <i class="pi pi-exclamation-triangle warn-icon"></i>
            <p>Bạn có chắc chắn muốn xóa bản ghi dữ liệu huấn luyện này không?</p>
            <p class="delete-file-name" *ngIf="selectedItem">{{ selectedItem.fileName }}</p>
            <p class="warn-note">Hành động này không thể hoàn tác.</p>
          </div>
        </div>
        <ng-template pTemplate="footer">
          <button class="btn-cancel" (click)="showDeleteDialog = false">Hủy</button>
          <button class="btn-delete-confirm" (click)="doDelete()">
            <i class="pi pi-trash"></i> Xóa
          </button>
        </ng-template>
      </p-dialog>

    </div>
  `,
  styles: [`
    /* ─── Page Layout ───────────────────────────────────────────── */
    .ocr-training-page {
      font-family: 'Inter', 'Segoe UI', sans-serif;
      background: #f5f7fb;
      min-height: 100vh;
    }

    /* ─── Breadcrumb ──────────────────────────────────────────────── */
    .breadcrumb { display: flex; align-items: center; gap: 6px; font-size: 0.82rem; color: #6b7280; }
    .bc-sep { color: #9ca3af; }
    .bc-item.active { color: #002D72; font-weight: 600; }

    /* ─── Stats Cards ──────────────────────────────────────────────── */
    .stats-grid {
      display: grid;
      grid-template-columns: repeat(5, 1fr);
      gap: 14px;
    }
    @media (max-width: 1024px) { .stats-grid { grid-template-columns: repeat(3, 1fr); } }
    @media (max-width: 640px)  { .stats-grid { grid-template-columns: repeat(2, 1fr); } }

    .stat-card {
      display: flex; align-items: center; gap: 14px;
      background: #fff; border-radius: 10px; padding: 16px 18px;
      box-shadow: 0 1px 6px rgba(0,0,0,.07); border-left: 4px solid transparent;
    }
    .stat-card.total   { border-left-color: #002D72; }
    .stat-card.pending { border-left-color: #f59e0b; }
    .stat-card.labeled { border-left-color: #3b82f6; }
    .stat-card.verified{ border-left-color: #10b981; }
    .stat-card.rejected{ border-left-color: #ef4444; }

    .stat-icon {
      width: 44px; height: 44px; border-radius: 50%;
      display: flex; align-items: center; justify-content: center;
      font-size: 1.25rem;
    }
    .total   .stat-icon { background: #eff6ff; color: #002D72; }
    .pending .stat-icon { background: #fffbeb; color: #d97706; }
    .labeled .stat-icon { background: #eff6ff; color: #2563eb; }
    .verified .stat-icon{ background: #ecfdf5; color: #059669; }
    .rejected .stat-icon{ background: #fef2f2; color: #dc2626; }

    .stat-value { font-size: 1.6rem; font-weight: 700; color: #1e293b; line-height: 1; }
    .stat-label { font-size: 0.75rem; color: #6b7280; margin-top: 3px; }

    /* ─── Main Card ──────────────────────────────────────────────── */
    .main-card {
      background: #fff; border-radius: 10px;
      box-shadow: 0 1px 8px rgba(0,0,0,.08); overflow: hidden;
      padding: 18px;
    }

    /* ─── Toolbar ──────────────────────────────────────────────── */
    .toolbar {
      display: flex; align-items: center; justify-content: space-between;
      gap: 10px; flex-wrap: wrap;
    }
    .toolbar-left { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .toolbar-right { display: flex; align-items: center; gap: 8px; }

    .search-input {
      height: 36px; padding: 0 12px; border: 1px solid #d1d5db;
      border-radius: 6px; font-size: 0.85rem; outline: none;
      transition: border-color .2s;
    }
    .search-input:focus { border-color: #002D72; box-shadow: 0 0 0 2px rgba(0,45,114,.12); }
    .search-input.w-full { width: 100%; }

    .filter-select {
      height: 36px; padding: 0 10px; border: 1px solid #d1d5db;
      border-radius: 6px; font-size: 0.85rem; outline: none;
      background: #fff; cursor: pointer;
    }
    .filter-select.w-full { width: 100%; }

    .btn-search {
      height: 36px; padding: 0 16px; background: #002D72; color: #fff;
      border: none; border-radius: 6px; font-size: 0.85rem;
      cursor: pointer; display: flex; align-items: center; gap: 6px;
      transition: background .2s;
    }
    .btn-search:hover { background: #003d99; }

    .btn-upload {
      height: 36px; padding: 0 16px; background: #FF6B00; color: #fff;
      border: none; border-radius: 6px; font-size: 0.85rem;
      cursor: pointer; display: flex; align-items: center; gap: 6px;
      font-weight: 600; transition: background .2s;
    }
    .btn-upload:hover { background: #e55f00; }
    .btn-upload:disabled { opacity: 0.6; cursor: not-allowed; }

    .btn-refresh {
      height: 36px; width: 36px; background: #f3f4f6; color: #374151;
      border: 1px solid #d1d5db; border-radius: 6px; font-size: 0.9rem;
      cursor: pointer; display: flex; align-items: center; justify-content: center;
      transition: all .2s;
    }
    .btn-refresh:hover { background: #e5e7eb; }

    /* ─── Table ──────────────────────────────────────────────── */
    .table-wrapper { overflow-x: auto; }
    .evn-table {
      width: 100%; border-collapse: collapse; font-size: 0.84rem;
    }
    .evn-table thead tr {
      background: #d0e1fd; /* màu xanh nhạt đặc trưng EVNHANOI */
    }
    .evn-table th {
      padding: 10px 12px; text-align: left; font-weight: 600;
      color: #002D72; font-size: 0.82rem; white-space: nowrap;
      border-bottom: 2px solid #b8d0f8;
    }
    .evn-table td {
      padding: 9px 12px; border-bottom: 1px solid #f1f5f9;
      vertical-align: middle; color: #374151;
    }
    .evn-table tbody tr:hover { background: #f8faff; }
    .evn-table tbody tr.row-verified { background: #f0fdf4; }

    .col-num { width: 50px; text-align: center; }
    .col-actions { width: 110px; text-align: center; }

    .loading-cell, .empty-cell {
      text-align: center; padding: 40px; color: #9ca3af;
    }
    .empty-cell { display: flex; flex-direction: column; align-items: center; gap: 8px; font-size: 0.9rem; }
    .empty-cell .pi { font-size: 2rem; color: #d1d5db; }

    /* ─── File Info ──────────────────────────────────────────────── */
    .file-info { display: flex; align-items: center; gap: 7px; }
    .file-info .pi { font-size: 1rem; color: #6b7280; }
    .file-name { color: #002D72; font-weight: 500; }

    /* ─── Badges / Pills ──────────────────────────────────────────────── */
    .doc-type-badge {
      display: inline-block; padding: 2px 8px; border-radius: 10px;
      background: #eff6ff; color: #1d4ed8; font-size: 0.75rem; font-weight: 500;
    }

    .status-pill {
      display: inline-block; padding: 3px 10px; border-radius: 12px;
      font-size: 0.75rem; font-weight: 600; white-space: nowrap;
    }
    .status-pending  { background: #fef3c7; color: #92400e; }
    .status-labeled  { background: #dbeafe; color: #1e40af; }
    .status-verified { background: #d1fae5; color: #065f46; }
    .status-rejected { background: #fee2e2; color: #991b1b; }

    /* ─── Quality Bar ──────────────────────────────────────────────── */
    .quality-bar-wrap {
      display: flex; align-items: center; gap: 6px; min-width: 90px;
    }
    .quality-bar {
      flex: 1; height: 6px; border-radius: 3px; background: #e5e7eb;
      position: relative; overflow: hidden;
    }
    .quality-bar::after { /* This shows the filled portion */
      content: ''; position: absolute; left: 0; top: 0; height: 100%;
      width: inherit; border-radius: 3px;
    }
    /* Override with colored bar */
    .quality-bar-wrap .quality-bar {
      background: linear-gradient(to right, #e5e7eb 0%, #e5e7eb 100%);
    }
    .quality-bar.q-good   { background: #10b981; }
    .quality-bar.q-medium { background: #f59e0b; }
    .quality-bar.q-poor   { background: #ef4444; }
    .quality-label { font-size: 0.75rem; color: #6b7280; white-space: nowrap; }

    .verified-icon   { color: #10b981; font-size: 1rem; }
    .unverified-icon { color: #d1d5db; font-size: 1rem; }
    .text-muted { color: #9ca3af; }

    /* ─── Action Buttons ──────────────────────────────────────────────── */
    .action-buttons { display: flex; gap: 5px; justify-content: center; }
    .btn-act {
      width: 30px; height: 30px; border: none; border-radius: 6px;
      cursor: pointer; display: flex; align-items: center; justify-content: center;
      font-size: 0.85rem; transition: all .2s;
    }
    .btn-label  { background: #eff6ff; color: #2563eb; }
    .btn-label:hover  { background: #dbeafe; }
    .btn-verify { background: #f0fdf4; color: #059669; }
    .btn-verify:hover { background: #d1fae5; }
    .btn-delete { background: #fef2f2; color: #dc2626; }
    .btn-delete:hover { background: #fee2e2; }

    /* ─── Pagination ──────────────────────────────────────────────── */
    .pagination-bar {
      display: flex; align-items: center; justify-content: space-between;
      padding-top: 14px; border-top: 1px solid #f1f5f9; margin-top: 8px;
      flex-wrap: wrap; gap: 10px;
    }
    .page-info { font-size: 0.82rem; color: #6b7280; }
    .page-controls { display: flex; align-items: center; gap: 6px; }
    .page-btn {
      width: 30px; height: 30px; border: 1px solid #d1d5db; border-radius: 5px;
      background: #fff; cursor: pointer; display: flex; align-items: center;
      justify-content: center; font-size: 0.8rem; color: #374151; transition: all .2s;
    }
    .page-btn:hover:not(:disabled) { background: #002D72; color: #fff; border-color: #002D72; }
    .page-btn:disabled { opacity: 0.4; cursor: not-allowed; }
    .page-num { font-size: 0.83rem; color: #374151; padding: 0 6px; }
    .page-size-select {
      height: 30px; padding: 0 8px; border: 1px solid #d1d5db;
      border-radius: 5px; font-size: 0.82rem;
    }

    /* ─── Dialogs ──────────────────────────────────────────────── */
    .dialog-body { padding: 8px 0; display: flex; flex-direction: column; gap: 14px; }
    .form-row { display: flex; flex-direction: column; gap: 5px; }
    .form-row label { font-size: 0.83rem; color: #374151; font-weight: 500; }
    .required { color: #ef4444; margin-left: 2px; }
    .label-textarea {
      width: 100%; padding: 8px 12px; border: 1px solid #d1d5db;
      border-radius: 6px; font-size: 0.85rem; resize: vertical;
      font-family: inherit; outline: none; transition: border-color .2s;
    }
    .label-textarea:focus { border-color: #002D72; box-shadow: 0 0 0 2px rgba(0,45,114,.12); }

    .detail-info-row {
      display: flex; gap: 8px; padding: 10px; background: #f8faff;
      border-radius: 6px; border: 1px solid #e0e7ff; font-size: 0.84rem;
    }
    .detail-label { color: #6b7280; font-weight: 500; white-space: nowrap; }
    .detail-value { color: #1e293b; font-weight: 600; }

    /* ─── Drop Zone ──────────────────────────────────────────────── */
    .drop-zone {
      border: 2px dashed #93c5fd; border-radius: 10px;
      padding: 30px 20px; text-align: center; cursor: pointer;
      background: #f8faff; transition: all .2s;
    }
    .drop-zone:hover { border-color: #002D72; background: #eff6ff; }
    .dz-icon { font-size: 2.5rem; color: #93c5fd; display: block; margin-bottom: 10px; }
    .dz-text { font-size: 0.9rem; color: #374151; margin-bottom: 6px; }
    .dz-hint { font-size: 0.78rem; color: #9ca3af; }

    .selected-file {
      display: flex; align-items: center; gap: 8px; padding: 8px 12px;
      background: #f0fdf4; border-radius: 6px; border: 1px solid #bbf7d0;
      font-size: 0.84rem;
    }
    .selected-file .pi { color: #059669; font-size: 1.1rem; }
    .sf-name { flex: 1; color: #065f46; font-weight: 500; }
    .sf-size { color: #6b7280; }
    .sf-remove {
      background: none; border: none; cursor: pointer; color: #9ca3af;
      padding: 2px; border-radius: 3px; font-size: 0.85rem;
    }
    .sf-remove:hover { color: #ef4444; }

    /* Progress Bar */
    .upload-progress { display: flex; flex-direction: column; gap: 5px; }
    .progress-track {
      width: 100%; height: 6px; background: #e5e7eb;
      border-radius: 3px; overflow: hidden;
    }
    .progress-fill {
      height: 100%; background: linear-gradient(90deg, #002D72, #FF6B00);
      border-radius: 3px; transition: width .3s;
    }
    .progress-text { font-size: 0.78rem; color: #6b7280; text-align: center; }

    /* ─── Verify Dialog ──────────────────────────────────────────────── */
    .verify-options { display: flex; flex-direction: column; gap: 10px; }
    .verify-option {
      display: flex; align-items: flex-start; gap: 12px; padding: 12px;
      border: 2px solid #e5e7eb; border-radius: 8px; cursor: pointer;
      transition: all .2s;
    }
    .verify-option:hover { border-color: #93c5fd; background: #f8faff; }
    .verify-option.selected { border-color: #002D72; background: #eff6ff; }
    .verify-option .pi { font-size: 1.5rem; margin-top: 2px; }
    .verify-ok  { color: #10b981; }
    .verify-bad { color: #ef4444; }
    .verify-option strong { font-size: 0.9rem; color: #1e293b; }
    .verify-option p { font-size: 0.8rem; color: #6b7280; margin: 3px 0 0; }

    /* ─── Delete Dialog ──────────────────────────────────────────────── */
    .delete-warning { text-align: center; padding: 10px 0; }
    .warn-icon { font-size: 3rem; color: #f59e0b; display: block; margin-bottom: 14px; }
    .delete-warning p { color: #374151; font-size: 0.9rem; margin: 0 0 6px; }
    .delete-file-name { font-weight: 600; color: #002D72 !important; }
    .warn-note { color: #ef4444 !important; font-size: 0.82rem !important; }

    .btn-cancel {
      height: 36px; padding: 0 16px; background: #f3f4f6; color: #374151;
      border: 1px solid #d1d5db; border-radius: 6px; font-size: 0.85rem;
      cursor: pointer; transition: all .2s;
    }
    .btn-cancel:hover { background: #e5e7eb; }

    .btn-delete-confirm {
      height: 36px; padding: 0 16px; background: #dc2626; color: #fff;
      border: none; border-radius: 6px; font-size: 0.85rem;
      cursor: pointer; display: flex; align-items: center; gap: 6px;
      font-weight: 600; transition: background .2s;
    }
    .btn-delete-confirm:hover { background: #b91c1c; }

    .mt-3 { margin-top: 12px; }
  `]
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
