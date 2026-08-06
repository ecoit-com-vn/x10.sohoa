import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, computed, signal } from '@angular/core';
import { TabsModule } from 'primeng/tabs';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { FormsModule } from '@angular/forms';
import { BoundingBoxOverlayComponent, OcrOverlayRegion } from '@sohoa.frontend/shared/ocr-viewer';
import {
  FormulaRunResponse,
  OcrModuleErrorAnalysis,
  OcrModuleRegionDto,
  OcrModuleService,
  SealSignatureRunResponse,
  SpellcheckRunResult,
} from '../../data-access/ocr-module.service';

export interface OcrInsightsSourceDocument {
  bucket: string;
  filePath: string;
  documentVersionId?: string;
  totalPages: number;
  documentLabel?: string;
}

/**
 * Nội dung nghiệp vụ dùng chung cho 6 module Nhóm A (88, 90, 92, 93, 94, 95) trên tài liệu hồ sơ/thiết bị
 * đã số hóa sẵn — dùng lại được ở cả popup (OcrInsightsPanelComponent) lẫn trang chi tiết riêng, KHÔNG
 * đụng logic submit/re-extract hiện có của các màn hình gọi vào.
 */
@Component({
  selector: 'lib-ocr-insights-content',
  standalone: true,
  imports: [CommonModule, FormsModule, TabsModule, ToastModule, BoundingBoxOverlayComponent],
  providers: [MessageService],
  templateUrl: './ocr-insights-content.component.html',
  styleUrl: './ocr-insights-content.component.scss',
})
export class OcrInsightsContentComponent implements OnInit {
  @Input() sourceDocument: OcrInsightsSourceDocument | null = null;

  activeTab = signal('0');
  loading = signal(false);
  errorMessage = signal<string | null>(null);
  jobId = signal<string | null>(null);
  regionCount = signal(0);
  regions = signal<OcrModuleRegionDto[]>([]);

  selectedPage = signal(1);
  pageImageUrl = signal<string | null>(null);
  pageImageLoading = signal(false);
  pageNumbers = computed(() => [...new Set(this.regions().map((r) => r.pageNumber))].sort((a, b) => a - b));
  overviewOverlayRegions = computed(() =>
    this.regions()
      .filter((r) => r.pageNumber === this.selectedPage())
      .map((r) => this.toOverlayRegion(r)),
  );

  formulaLoading = signal(false);
  formulaSummary = signal<FormulaRunResponse | null>(null);
  formulaRegions = computed(() => this.regions().filter((r) => r.regionType === 'Formula'));

  sealSignatureLoading = signal(false);
  sealSignatureSummary = signal<SealSignatureRunResponse | null>(null);
  sealSignatureRegions = computed(() =>
    this.regions().filter((r) => r.regionType === 'Seal' || r.regionType === 'Signature'),
  );
  sealSignatureOverlayRegions = computed(() =>
    this.sealSignatureRegions()
      .filter((r) => r.pageNumber === this.selectedPage())
      .map((r) => this.toSealSignatureOverlayRegion(r)),
  );

  spellcheckLoading = signal(false);
  spellcheckSummary = signal<SpellcheckRunResult | null>(null);
  spellcheckRegions = computed(() => this.regions().filter((r) => !!r.spellcheckSuggestion));
  manualEditText = signal<Record<string, string>>({});

  errorAnalysisLoading = signal(false);
  errorAnalysisList = signal<OcrModuleErrorAnalysis[]>([]);

  constructor(
    private readonly ocrModuleService: OcrModuleService,
    private readonly messageService: MessageService,
  ) {}

  ngOnInit(): void {
    if (this.sourceDocument) {
      this.loadJob();
    }
  }

  onTabChange(value: string | number | undefined): void {
    this.activeTab.set(String(value ?? '0'));
  }

  retry(): void {
    this.loadJob();
  }

  private loadJob(): void {
    if (!this.sourceDocument) return;

    this.loading.set(true);
    this.errorMessage.set(null);

    this.ocrModuleService
      .createJobFromExisting({
        bucket: this.sourceDocument.bucket,
        filePath: this.sourceDocument.filePath,
        documentVersionId: this.sourceDocument.documentVersionId,
        totalPages: this.sourceDocument.totalPages,
      })
      .subscribe({
        next: (res) => {
          this.jobId.set(res.jobId);
          this.regionCount.set(res.regionCount);
          this.loading.set(false);
          this.messageService.add({
            severity: 'success',
            summary: 'Đã nạp dữ liệu OCR',
            detail: `Đã nạp ${res.regionCount} vùng văn bản từ kết quả OCR có sẵn của tài liệu này.`,
          });
          this.loadRegions();
        },
        error: (err) => {
          this.loading.set(false);
          this.errorMessage.set(
            err?.error?.message ?? 'Không đọc được kết quả OCR đã có của tài liệu này.',
          );
        },
      });
  }

  private loadRegions(): void {
    const id = this.jobId();
    if (!id) return;

    this.ocrModuleService.getRegions(id, 1, 1000).subscribe((res) => {
      this.regions.set(res.items);
      const pages = this.pageNumbers();
      if (pages.length > 0) {
        this.selectedPage.set(pages[0]);
        this.loadPageImage();
      }
    });
  }

  private toOverlayRegion(region: OcrModuleRegionDto): OcrOverlayRegion {
    const confidence = region.confidence;
    const colorClass =
      confidence == null
        ? 'region-conf-unknown'
        : confidence >= 0.9
          ? 'region-conf-high'
          : confidence >= 0.7
            ? 'region-conf-medium'
            : 'region-conf-low';

    return {
      id: region.id,
      boxX0: region.boxX0,
      boxY0: region.boxY0,
      boxX1: region.boxX1,
      boxY1: region.boxY1,
      colorClass,
      tooltip: `${region.textRaw} — ${confidence != null ? (confidence * 100).toFixed(0) + '%' : 'Không rõ độ tin cậy'}`,
    };
  }

  private toSealSignatureOverlayRegion(region: OcrModuleRegionDto): OcrOverlayRegion {
    const isSeal = region.regionType === 'Seal';
    const label = isSeal ? 'Con dấu' : 'Chữ ký';
    const score = region.sealSignatureScore;

    return {
      id: region.id,
      boxX0: region.boxX0,
      boxY0: region.boxY0,
      boxX1: region.boxX1,
      boxY1: region.boxY1,
      colorClass: isSeal ? 'region-seal' : 'region-signature',
      tooltip: `${label} — ${score != null ? (score * 100).toFixed(0) + '%' : 'Không rõ độ tin cậy'}`,
    };
  }

  onSelectPage(page: number): void {
    if (page === this.selectedPage()) return;
    this.selectedPage.set(page);
    this.loadPageImage();
  }

  private loadPageImage(): void {
    const jobId = this.jobId();
    const page = this.selectedPage();
    if (!jobId) return;

    this.revokePageImageUrl();
    this.pageImageLoading.set(true);
    this.ocrModuleService.getPageImage(jobId, page).subscribe({
      next: (blob) => {
        this.pageImageUrl.set(URL.createObjectURL(blob));
        this.pageImageLoading.set(false);
      },
      error: () => {
        this.pageImageLoading.set(false);
      },
    });
  }

  private revokePageImageUrl(): void {
    const url = this.pageImageUrl();
    if (url) {
      URL.revokeObjectURL(url);
      this.pageImageUrl.set(null);
    }
  }

  runFormulaRecognition(): void {
    const id = this.jobId();
    if (!id) return;

    this.formulaLoading.set(true);
    this.ocrModuleService.runFormulaRecognition(id, this.selectedPage()).subscribe({
      next: (summary) => {
        this.formulaSummary.set(summary);
        this.formulaLoading.set(false);
        this.loadRegions();
        this.messageService.add({
          severity: 'success',
          summary: 'Đã nhận dạng công thức',
          detail: `Tìm thấy ${summary.formulaRegionCount}/${summary.totalRegions} vùng giống công thức kỹ thuật.`,
        });
      },
      error: (err) => {
        this.formulaLoading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err?.error?.message ?? 'Không nhận dạng được công thức.',
        });
      },
    });
  }

  runSealSignatureDetection(): void {
    const id = this.jobId();
    if (!id) return;

    this.sealSignatureLoading.set(true);
    this.ocrModuleService.runSealSignatureDetection(id, this.selectedPage()).subscribe({
      next: (summary) => {
        this.sealSignatureSummary.set(summary);
        this.sealSignatureLoading.set(false);
        this.loadRegions();
        this.messageService.add({
          severity: 'success',
          summary: 'Đã xử lý con dấu/chữ ký',
          detail: `Phát hiện ${summary.sealCount} vùng con dấu, gợi ý ${summary.signatureCount} vùng chữ ký.`,
        });
      },
      error: (err) => {
        this.sealSignatureLoading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err?.error?.message ?? 'Không xử lý được con dấu/chữ ký.',
        });
      },
    });
  }

  runSpellcheck(): void {
    const id = this.jobId();
    if (!id) return;

    this.spellcheckLoading.set(true);
    this.ocrModuleService.runSpellcheck(id, this.selectedPage()).subscribe({
      next: (summary) => {
        this.spellcheckSummary.set(summary);
        this.spellcheckLoading.set(false);
        this.loadRegions();
        this.messageService.add({
          severity: 'success',
          summary: 'Đã kiểm tra chính tả',
          detail: `Tìm thấy ${summary.suggestionCount}/${summary.totalRegionsChecked} vùng có gợi ý sửa lỗi.`,
        });
      },
      error: (err) => {
        this.spellcheckLoading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err?.error?.message ?? 'Không kiểm tra được chính tả.',
        });
      },
    });
  }

  acceptSuggestion(region: OcrModuleRegionDto): void {
    const jobId = this.jobId();
    if (!jobId || !region.spellcheckSuggestion) return;

    this.ocrModuleService
      .updateSpellcheckStatus(jobId, region.id, { status: 'Accepted', suggestionText: region.spellcheckSuggestion })
      .subscribe(() => {
        this.messageService.add({ severity: 'success', summary: 'Đã chấp nhận gợi ý', detail: '' });
        this.loadRegions();
      });
  }

  rejectSuggestion(region: OcrModuleRegionDto): void {
    const jobId = this.jobId();
    if (!jobId) return;

    this.ocrModuleService.updateSpellcheckStatus(jobId, region.id, { status: 'Rejected' }).subscribe(() => {
      this.messageService.add({ severity: 'info', summary: 'Đã từ chối gợi ý', detail: '' });
      this.loadRegions();
    });
  }

  onManualEditChange(regionId: string, value: string): void {
    this.manualEditText.update((map) => ({ ...map, [regionId]: value }));
  }

  saveManualEdit(region: OcrModuleRegionDto): void {
    const jobId = this.jobId();
    const manualText = this.manualEditText()[region.id];
    if (!jobId || !manualText?.trim()) return;

    this.ocrModuleService
      .updateSpellcheckStatus(jobId, region.id, { status: 'ManuallyEdited', manualText: manualText.trim() })
      .subscribe(() => {
        this.messageService.add({ severity: 'success', summary: 'Đã lưu sửa tay', detail: '' });
        this.loadRegions();
      });
  }

  runErrorAnalysis(): void {
    const id = this.jobId();
    if (!id) return;

    this.errorAnalysisLoading.set(true);
    this.ocrModuleService.runErrorAnalysis(id, this.selectedPage()).subscribe({
      next: (errors) => {
        this.errorAnalysisList.set(errors);
        this.errorAnalysisLoading.set(false);
        this.messageService.add({
          severity: 'success',
          summary: 'Đã phân tích lỗi',
          detail: `Tìm thấy ${errors.length} lỗi cần xem xét.`,
        });
      },
      error: (err) => {
        this.errorAnalysisLoading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err?.error?.message ?? 'Không phân tích được lỗi.',
        });
      },
    });
  }

  exportErrorAnalysis(): void {
    const rows = this.errorAnalysisList();
    if (rows.length === 0) return;

    const header = 'Trang,Danh mục lỗi,Mức độ,Chi tiết,Trạng thái\n';
    const csv = rows
      .map((r) => [r.pageNumber, r.errorCategory, r.severity, (r.detail ?? '').replace(/"/g, '""'), r.resolvedStatus]
        .map((v) => `"${v}"`).join(','))
      .join('\n');

    const blob = new Blob(['﻿' + header + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `phan-tich-loi-ocr-${this.jobId()}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  resolveError(error: OcrModuleErrorAnalysis): void {
    const jobId = this.jobId();
    if (!jobId) return;

    this.ocrModuleService.resolveErrorAnalysis(jobId, error.id).subscribe(() => {
      this.errorAnalysisList.update((list) =>
        list.map((e) => (e.id === error.id ? { ...e, resolvedStatus: 'Resolved' } : e)),
      );
      this.messageService.add({ severity: 'success', summary: 'Đã xác nhận hoàn tất', detail: '' });
    });
  }
}
