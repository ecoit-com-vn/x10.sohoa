import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SplitterModule } from 'primeng/splitter';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';

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
          <div class="edit-actions">
            <button class="btn-outlined" 
              [class.btn-filter-active]="noiseReductionEnabled"
              (click)="toggleNoiseReduction()">
              <i class="pi" [class.pi-check]="noiseReductionEnabled" [class.pi-times]="!noiseReductionEnabled"></i>
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
                <div style="flex: 1; border: 2px dashed #cbd5e1; display: flex; align-items: center; justify-content: center; overflow: hidden; background: #ffffff; border-radius: 6px; padding: 10px;"
                     [class.grayscale]="noiseReductionEnabled" 
                     [class.contrast-125]="noiseReductionEnabled">
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
                          style="flex: 1; font-family: 'Courier New', monospace; font-size: 0.9rem; line-height: 1.5; resize: none; border-color: #cbd5e1;" 
                          [(ngModel)]="ocrText">
                </textarea>
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
export class OcrCorrectionComponent {
  noiseReductionEnabled = false;
  ocrText = 'CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc\n\nBIÊN BẢN GIAO NHẬN HỒ SƠ TÀI LIỆU KỸ THUẬT\nNgày lập: 28/05/2026\n\nNỘI DUNG SỐ HÓA:\nHồ sơ máy biến áp ngâm dầu T1 thuộc trạm biến áp 110kV Đông Anh.\n- Hãng chế tạo: ABB Việt Nam.\n- Công suất định mức: 63 MVA.\n- Dòng điện định mức cuộn cao áp: 330 A.\n- Điện áp định mức: 115 +/- 9 x 1.78% / 38.5 / 23 kV.\n- Khối lượng dầu cách điện: 18.500 kg.\n- Tổng khối lượng MBA: 82.000 kg.\n- Tiêu chuẩn áp dụng: IEC 60076.\n\nNgười thực hiện quét: Nguyễn Văn An.\nKiểm soát chất lượng: Trần Thị Bích.';

  constructor(private messageService: MessageService) {}

  toggleNoiseReduction() {
    this.noiseReductionEnabled = !this.noiseReductionEnabled;
    this.messageService.add({
      severity: 'info',
      summary: this.noiseReductionEnabled ? 'Đã bật bộ lọc' : 'Đã tắt bộ lọc',
      detail: this.noiseReductionEnabled 
        ? 'Bật thuật toán Grayscale & Contrast nâng cao để tăng độ sắc nét vùng biên văn bản.' 
        : 'Tắt thuật toán tiền xử lý ảnh lọc nhiễu.'
    });
  }

  saveResult() {
    this.messageService.add({
      severity: 'success',
      summary: 'Lưu kết quả thành công',
      detail: 'Văn bản hiệu đính (Ground Truth) đã được cập nhật thành công vào cơ sở dữ liệu số hóa!'
    });
  }
}
