import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SplitterModule } from 'primeng/splitter';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { Subject, Subscription } from 'rxjs';
import { debounceTime } from 'rxjs/operators';

@Component({
  selector: 'app-ocr-correction',
  standalone: true,
  imports: [
    CommonModule,
    SplitterModule,
    FormsModule,
    ToastModule
  ],
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
          <span class="bc-text">Lưu trữ Vật lý & OCR</span>
          <span class="bc-sep">/</span>
          <span class="bc-current">Hiệu đính AI-OCR</span>
        </div>

        <div class="edit-header">
          <div>
            <h2 class="edit-title" style="color: #002D72;">Hiệu đính kết quả AI-OCR</h2>
            <p class="text-xs text-secondary mt-1 m-0">Đối chiếu trực tiếp ảnh gốc bên trái để chỉnh sửa văn bản nhận dạng bên phải</p>
          </div>
          <div class="edit-actions" style="display: flex; align-items: center; gap: 8px;">
            <button class="btn-outlined" 
              [class.btn-filter-active]="noiseReductionEnabled"
              (click)="toggleNoiseReduction()">
              <i class="pi" [class.pi-eye]="noiseReductionEnabled" [class.pi-eye-slash]="!noiseReductionEnabled"></i>
              {{ noiseReductionEnabled ? 'Đã bật Giảm nhiễu' : 'Bật Giảm nhiễu ảnh' }}
            </button>
            <button class="btn-save" (click)="saveResult()">
              <i class="pi pi-save"></i> Lưu kết quả hiệu đính
            </button>
          </div>
        </div>

        <!-- Splitter Workspace -->
        <div style="border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; margin-top: 15px;">
          <p-splitter [style]="{'height': '550px'}" [panelSizes]="[50, 50]">
            
            <!-- Panel 1: Original Image Document -->
            <ng-template #panel>
              <div class="p-3" style="display: flex; flex-direction: column; height: 100%; background: #f8fafc;">
                <h3 class="m-0 mb-2 font-bold" style="font-size: 0.85rem; color: #002D72; text-transform: uppercase;">
                  <i class="pi pi-image mr-1"></i> Ảnh tài liệu gốc quét kỹ thuật
                </h3>
                <div style="flex: 1; border: 2px dashed #cbd5e1; display: flex; align-items: center; justify-content: center; overflow: hidden; background: #ffffff; border-radius: 6px; padding: 10px; position: relative;"
                     [class.grayscale]="noiseReductionEnabled" 
                     [class.contrast-125]="noiseReductionEnabled">
                  <div *ngIf="isProcessingNoise" style="position: absolute; top: 0; left: 0; right: 0; bottom: 0; background: rgba(255,255,255,0.75); display: flex; flex-direction: column; align-items: center; justify-content: center; z-index: 10; border-radius: 6px;">
                    <i class="pi pi-spin pi-spinner" style="font-size: 2rem; color: #002D72; margin-bottom: 8px;"></i>
                    <span style="font-size: 0.75rem; color: #4b5563; font-weight: 600;">Đang xử lý thuật toán giảm nhiễu...</span>
                  </div>
                  <img src="https://images.unsplash.com/photo-1586075010923-2dd4570fb338?auto=format&fit=crop&q=80&w=600" 
                       alt="Tài liệu mẫu" 
                       style="max-width: 100%; max-height: 100%; object-fit: contain; box-shadow: 0 4px 10px rgba(0,0,0,0.1);" />
                </div>
              </div>
            </ng-template>
            
            <!-- Panel 2: Text Editor (Ground Truth) -->
            <ng-template #panel>
              <div class="p-3" style="display: flex; flex-direction: column; height: 100%; background: #ffffff;">
                <h3 class="m-0 mb-2 font-bold" style="font-size: 0.85rem; color: #002D72; text-transform: uppercase;">
                  <i class="pi pi-file-edit mr-1"></i> Văn bản máy đọc & hiệu đính
                </h3>
                <textarea class="wf-textarea" 
                          style="flex: 1; font-family: 'Courier New', monospace; font-size: 0.9rem; line-height: 1.5; resize: none; border-color: #cbd5e1; padding: 12px;" 
                          [(ngModel)]="ocrText"
                          (ngModelChange)="onTextChange($event)">
                </textarea>
                <div *ngIf="autoSavedTime" style="display: flex; justify-content: flex-end; align-items: center; gap: 4px; font-size: 0.75rem; color: #16a34a; margin-top: 6px;">
                  <i class="pi pi-check-circle"></i>
                  <span>Tự động lưu lúc: {{ autoSavedTime }}</span>
                </div>
              </div>
            </ng-template>
            
          </p-splitter>
        </div>

      </div>
    </div>
  `,
  styles: [`
    .grayscale {
      filter: grayscale(100%);
    }
    .contrast-125 {
      filter: contrast(125%) grayscale(100%);
    }
  `]
})
export class OcrCorrectionComponent implements OnInit, OnDestroy {
  noiseReductionEnabled = false;
  isProcessingNoise = false;
  ocrText = 'CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc\n\nBIÊN BẢN GIAO NHẬN HỒ SƠ TÀI LIỆU KỸ THUẬT\nNgày lập: 28/05/2026\n\nNỘI DUNG SỐ HÓA:\nHồ sơ máy biến áp ngâm dầu T1 thuộc trạm biến áp 110kV Đông Anh.\n- Hãng chế tạo: ABB Việt Nam.\n- Công suất định mức: 63 MVA.\n- Dòng điện định mức cuộn cao áp: 330 A.\n- Điện áp định mức: 115 +/- 9 x 1.78% / 38.5 / 23 kV.\n- Khối lượng dầu cách điện: 18.500 kg.\n- Tổng khối lượng MBA: 82.000 kg.\n- Tiêu chuẩn áp dụng: IEC 60076.\n\nNgười thực hiện quét: Nguyễn Văn An.\nKiểm soát chất lượng: Trần Thị Bích.';
  
  autoSavedTime = '';
  private textChange$ = new Subject<string>();
  private autoSaveSub!: Subscription;

  constructor(private messageService: MessageService) {}

  ngOnInit() {
    // Đăng ký auto save sau 2 giây ngưng gõ phím
    this.autoSaveSub = this.textChange$.pipe(
      debounceTime(2000)
    ).subscribe(() => {
      this.autoSave();
    });
  }

  ngOnDestroy() {
    if (this.autoSaveSub) {
      this.autoSaveSub.unsubscribe();
    }
  }

  onTextChange(text: string) {
    this.textChange$.next(text);
  }

  toggleNoiseReduction() {
    this.isProcessingNoise = true;
    this.messageService.add({
      severity: 'info',
      summary: 'Đang tiền xử lý ảnh',
      detail: 'Đang áp dụng thuật toán giảm nhiễu AI-OCR, vui lòng đợi...',
      life: 800
    });

    setTimeout(() => {
      this.noiseReductionEnabled = !this.noiseReductionEnabled;
      this.isProcessingNoise = false;
      this.messageService.add({
        severity: 'success',
        summary: this.noiseReductionEnabled ? 'Đã bật giảm nhiễu' : 'Đã tắt giảm nhiễu',
        detail: this.noiseReductionEnabled 
          ? 'Đã kích hoạt thuật toán Grayscale & Contrast nâng cao thành công.' 
          : 'Đã tắt thuật toán tiền xử lý ảnh lọc nhiễu.'
      });
    }, 800);
  }

  saveResult() {
    this.messageService.add({
      severity: 'success',
      summary: 'Lưu kết quả thành công',
      detail: 'Văn bản hiệu đính (Ground Truth) đã được cập nhật thành công vào cơ sở dữ liệu số hóa!'
    });
    this.autoSavedTime = new Date().toLocaleTimeString('vi-VN');
  }

  autoSave() {
    // Giả lập lưu ngầm tự động
    this.autoSavedTime = new Date().toLocaleTimeString('vi-VN');
    this.messageService.add({
      severity: 'success',
      summary: 'Tự động lưu',
      detail: 'Hệ thống đã tự động lưu dữ liệu hiệu đính mới nhất.',
      life: 2000
    });
  }
}
