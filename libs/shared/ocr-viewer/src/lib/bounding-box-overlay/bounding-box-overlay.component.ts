import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface OcrOverlayRegion {
  id: string;
  boxX0: number;
  boxY0: number;
  boxX1: number;
  boxY1: number;
  label?: string;
  /** Lớp CSS quyết định màu khoanh vùng — xem các lớp region-* định nghĩa sẵn trong component. */
  colorClass?: 'region-text' | 'region-seal' | 'region-signature' | 'region-formula' | 'region-diff' | 'region-handwritten';
  tooltip?: string;
}

/**
 * Overlay khoanh vùng văn bản trên ảnh trang tài liệu — dùng chung cho Detection, So sánh mẫu,
 * Con dấu/chữ ký, Loại chữ viết. Toạ độ box theo hệ quy chiếu pixel gốc (naturalWidth/naturalHeight),
 * hiển thị bằng vị trí phần trăm để không phải tính lại khi zoom.
 */
@Component({
  selector: 'lib-bounding-box-overlay',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './bounding-box-overlay.component.html',
  styleUrl: './bounding-box-overlay.component.scss',
})
export class BoundingBoxOverlayComponent {
  @Input() imageUrl = '';
  @Input() naturalWidth = 1000;
  @Input() naturalHeight = 1000;
  @Input() regions: OcrOverlayRegion[] = [];
  @Output() regionClick = new EventEmitter<OcrOverlayRegion>();

  zoom = signal(1);

  zoomIn(): void {
    this.zoom.update((z) => Math.min(z + 0.25, 3));
  }

  zoomOut(): void {
    this.zoom.update((z) => Math.max(z - 0.25, 0.5));
  }

  resetZoom(): void {
    this.zoom.set(1);
  }

  leftPercent(region: OcrOverlayRegion): number {
    return this.naturalWidth > 0 ? (region.boxX0 / this.naturalWidth) * 100 : 0;
  }

  topPercent(region: OcrOverlayRegion): number {
    return this.naturalHeight > 0 ? (region.boxY0 / this.naturalHeight) * 100 : 0;
  }

  widthPercent(region: OcrOverlayRegion): number {
    return this.naturalWidth > 0 ? ((region.boxX1 - region.boxX0) / this.naturalWidth) * 100 : 0;
  }

  heightPercent(region: OcrOverlayRegion): number {
    return this.naturalHeight > 0 ? ((region.boxY1 - region.boxY0) / this.naturalHeight) * 100 : 0;
  }

  onRegionClick(region: OcrOverlayRegion): void {
    this.regionClick.emit(region);
  }
}
