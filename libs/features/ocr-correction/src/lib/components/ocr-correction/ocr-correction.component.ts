import { Component, OnInit, OnDestroy } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
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
    ToastModule,
    WfBreadcrumbComponent,
  ],
  providers: [MessageService],
  templateUrl: './ocr-correction.component.html',
  styleUrl: './ocr-correction.component.scss'
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
