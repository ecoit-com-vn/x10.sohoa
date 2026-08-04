import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { DialogModule } from 'primeng/dialog';
import { OcrInsightsContentComponent, OcrInsightsSourceDocument } from '../ocr-insights-content/ocr-insights-content.component';

/**
 * Popup "Phân tích OCR nâng cao" — dùng ở những màn hình vẫn cần xem nhanh dạng dialog
 * (vd. equipment-documents). Toàn bộ nghiệp vụ nằm trong OcrInsightsContentComponent
 * để dùng lại được cho trang chi tiết riêng (dossier-documents).
 */
@Component({
  selector: 'lib-ocr-insights-panel',
  standalone: true,
  imports: [CommonModule, DialogModule, OcrInsightsContentComponent],
  templateUrl: './ocr-insights-panel.component.html',
  styleUrl: './ocr-insights-panel.component.scss',
})
export class OcrInsightsPanelComponent {
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Input() sourceDocument: OcrInsightsSourceDocument | null = null;

  onHide(): void {
    this.visible = false;
    this.visibleChange.emit(false);
  }
}
