import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SplitterModule } from 'primeng/splitter';
import { ToggleButtonModule } from 'primeng/togglebutton';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TextareaModule } from 'primeng/textarea';

@Component({
  selector: 'app-ocr-correction',
  standalone: true,
  imports: [
    CommonModule,
    SplitterModule,
    ToggleButtonModule,
    FormsModule,
    ButtonModule,
    TextareaModule
  ],
  template: `
    <div class="card flex flex-col gap-4">
      <div class="flex justify-between items-center">
        <h2 class="text-xl font-bold">Hiệu đính AI-OCR</h2>
        <div class="flex items-center gap-4">
            <p-toggleButton 
                [(ngModel)]="noiseReductionEnabled" 
                onLabel="Đã bật Giảm bóng mờ & Nhiễu" 
                offLabel="Bật Giảm bóng mờ & Nhiễu" 
                onIcon="pi pi-check" 
                offIcon="pi pi-times">
            </p-toggleButton>
            <p-button label="Lưu kết quả" icon="pi pi-save"></p-button>
        </div>
      </div>
      
      <p-splitter [style]="{'height': '600px'}" [panelSizes]="[50, 50]">
        <ng-template #panel>
          <div class="p-4 flex flex-col h-full bg-gray-50 dark:bg-gray-800">
            <h3 class="font-semibold mb-2">Ảnh tài liệu gốc</h3>
            <div class="flex-1 border-2 border-dashed border-gray-300 flex items-center justify-center overflow-hidden" [class.grayscale]="noiseReductionEnabled" [class.contrast-125]="noiseReductionEnabled">
              <!-- Placeholder for image -->
              <img src="https://via.placeholder.com/600x800.png?text=Document+Image" alt="Document" class="max-w-full max-h-full object-contain" />
            </div>
          </div>
        </ng-template>
        <ng-template #panel>
          <div class="p-4 flex flex-col h-full">
            <h3 class="font-semibold mb-2">Văn bản máy đọc (Text Editor)</h3>
            <textarea pTextarea class="w-full flex-1 p-3 border rounded font-mono" [(ngModel)]="ocrText" style="resize: none;"></textarea>
          </div>
        </ng-template>
      </p-splitter>
    </div>
  `
})
export class OcrCorrectionComponent {
  noiseReductionEnabled = false;
  ocrText = 'CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc\n\nBIÊN BẢN GIAO NHẬN TÀI LIỆU\n...';
}
