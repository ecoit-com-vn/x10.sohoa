import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { OcrInsightsContentComponent, OcrModuleService } from '@sohoa.frontend/features/ocr-module';

/**
 * Trang "Phân tích OCR nâng cao" cho màn hình quản trị "Quản lý dữ liệu huấn luyện AI-OCR" — giao diện
 * tái dùng y hệt trang phân tích OCR của hồ sơ (DossierOcrInsightsPageComponent), khác biệt duy nhất:
 * Job đã tồn tại sẵn (tạo lúc upload), nên truyền thẳng [existingJobId] thay vì [sourceDocument].
 */
@Component({
  selector: 'app-ai-ocr-training-document-ocr-insights-page',
  standalone: true,
  imports: [CommonModule, WfBreadcrumbComponent, OcrInsightsContentComponent],
  templateUrl: './ai-ocr-training-document-ocr-insights-page.component.html',
  styleUrl: './ai-ocr-training-document-ocr-insights-page.component.scss',
})
export class AiOcrTrainingDocumentOcrInsightsPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly ocrModuleService = inject(OcrModuleService);

  loading = signal(true);
  errorMessage = signal<string | null>(null);
  jobId = signal<string | null>(null);
  documentLabel = signal<string | null>(null);

  ngOnInit(): void {
    const jobId = this.route.snapshot.paramMap.get('jobId') ?? '';
    const label = this.route.snapshot.queryParamMap.get('label');
    this.documentLabel.set(label);

    if (!jobId) {
      this.loading.set(false);
      this.errorMessage.set('Thiếu thông tin file để phân tích OCR.');
      return;
    }

    this.ocrModuleService.getJob(jobId).subscribe({
      next: (job) => {
        this.loading.set(false);
        if (job.state !== 'Ready') {
          this.errorMessage.set('File này chưa xử lý OCR xong, vui lòng quay lại sau.');
          return;
        }
        this.jobId.set(jobId);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Không tìm thấy dữ liệu huấn luyện AI-OCR này.');
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
