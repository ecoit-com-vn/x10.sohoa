import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { OcrInsightsContentComponent, OcrInsightsSourceDocument } from '@sohoa.frontend/features/ocr-module';
import { EquipmentService } from '../../data-access/equipment.service';

/**
 * Trang chi tiết "Phân tích OCR nâng cao" — thay cho popup cũ ở tab Tài liệu đính kèm của màn
 * hình thiết bị, mở trong tab mới để không mất dữ liệu đang nhập ở màn hình thiết bị.
 */
@Component({
  selector: 'app-equipment-ocr-insights-page',
  standalone: true,
  imports: [CommonModule, WfBreadcrumbComponent, OcrInsightsContentComponent],
  templateUrl: './equipment-ocr-insights-page.component.html',
  styleUrl: './equipment-ocr-insights-page.component.scss',
})
export class EquipmentOcrInsightsPageComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private equipmentService = inject(EquipmentService);

  loading = signal(true);
  errorMessage = signal<string | null>(null);
  sourceDocument = signal<OcrInsightsSourceDocument | null>(null);
  documentLabel = signal<string | null>(null);

  ngOnInit(): void {
    const equipmentId = this.route.snapshot.paramMap.get('id') ?? '';
    const documentVersionId = this.route.snapshot.paramMap.get('documentVersionId') ?? '';
    const label = this.route.snapshot.queryParamMap.get('label');
    this.documentLabel.set(label);

    if (!equipmentId || !documentVersionId) {
      this.loading.set(false);
      this.errorMessage.set('Thiếu thông tin tài liệu để phân tích OCR.');
      return;
    }

    this.equipmentService.getDigitizationProgressForEquipment(equipmentId, documentVersionId).subscribe({
      next: (progress) => {
        this.loading.set(false);
        if (!progress?.bucketName || !progress?.filePath) {
          this.errorMessage.set('Tài liệu này chưa có đủ thông tin vị trí lưu trữ file OCR.');
          return;
        }
        this.sourceDocument.set({
          bucket: progress.bucketName,
          filePath: progress.filePath,
          documentVersionId,
          totalPages: progress.totalPages ?? 0,
          documentLabel: label ?? undefined,
        });
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Không tải được thông tin OCR của tài liệu để phân tích.');
      },
    });
  }

  goBack(): void {
    if (window.history.length > 1) {
      window.history.back();
    } else {
      window.close();
    }
  }
}
