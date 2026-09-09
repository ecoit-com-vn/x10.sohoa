import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { finalize } from 'rxjs';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { PmisDocumentCatalogService } from '../data-access/pmis-document-catalog.service';
import { PmisCatalogNode, PmisDocumentItem } from '../models/pmis-catalog.models';
import { convertPmisFlatToTree, findPmisBreadcrumbPath } from '../utils/pmis-catalog-tree.util';

const NODE_ICONS: { [key: string]: string } = {
  unit: 'pi-building',
  substation: 'pi-bolt',
  line: 'pi-share-alt',
  equipment: 'pi-box',
};

const PAGE_SIZE = 10;

/**
 * "Kho tài liệu PMIS" — xem tài liệu Trạm biến áp/Đường dây/Thiết bị đã đồng bộ từ PMIS. Cây Đơn vị →
 * Trạm/Đường dây → Thiết bị được tổng hợp từ dữ liệu thật (PmisDocumentCatalogController), không có
 * thao tác tạo/sửa/xoá thư mục như "Kho tài liệu thiết bị" — chỉ đọc, chỉ có nút tải về.
 */
@Component({
  selector: 'app-pmis-document-warehouse',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './pmis-document-warehouse.component.html',
  styleUrl: './document-management.component.css',
})
export class PmisDocumentWarehouseComponent implements OnInit {
  private readonly catalogService = inject(PmisDocumentCatalogService);
  private readonly messageService = inject(MessageService);

  nodeIcons = NODE_ICONS;

  loadingTree = signal(false);
  flatNodes = signal<PmisCatalogNode[]>([]);
  tree = computed(() => convertPmisFlatToTree(this.flatNodes()));
  expandedNodeIds = signal<Set<string>>(new Set());

  selectedNode = signal<PmisCatalogNode | null>(null);
  breadcrumbPath = computed(() => findPmisBreadcrumbPath(this.selectedNode()?.id ?? null, this.flatNodes()));

  keyword = signal('');
  loadingDocuments = signal(false);
  documents = signal<PmisDocumentItem[]>([]);
  totalCount = signal(0);
  page = signal(1);
  pageSize = PAGE_SIZE;
  downloadingId = signal<string | null>(null);

  // Upload thủ công — dùng khi đồng bộ tự động từ PMIS lỗi.
  uploadDialogVisible = signal(false);
  uploadFile = signal<File | null>(null);
  uploadDocumentName = signal('');
  uploadDocumentType = signal('');
  uploading = signal(false);

  /** Chỉ node Trạm/Đường dây/Thiết bị mới có danh sách tài liệu riêng để xem (Đơn vị chỉ để điều hướng). */
  canShowDocuments = computed(() => {
    const node = this.selectedNode();
    return !!node && node.nodeType !== 'unit';
  });

  ngOnInit(): void {
    this.loadTree();
  }

  loadTree(): void {
    this.loadingTree.set(true);
    this.catalogService
      .getCatalogTree()
      .pipe(finalize(() => this.loadingTree.set(false)))
      .subscribe({
        next: (nodes) => {
          this.flatNodes.set(nodes || []);
          // Mở sẵn tất cả node Đơn vị cho dễ thấy có dữ liệu ngay khi vào màn.
          this.expandedNodeIds.set(new Set((nodes || []).filter((n) => n.nodeType === 'unit').map((n) => n.id)));
        },
        error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải cây Kho tài liệu PMIS.' }),
      });
  }

  toggleExpand(node: PmisCatalogNode, event: Event): void {
    event.stopPropagation();
    this.expandedNodeIds.update((current) => {
      const next = new Set(current);
      if (next.has(node.id)) next.delete(node.id);
      else next.add(node.id);
      return next;
    });
  }

  isExpanded(nodeId: string): boolean {
    return this.expandedNodeIds().has(nodeId);
  }

  selectNode(node: PmisCatalogNode): void {
    this.selectedNode.set(node);
    if (node.nodeType !== 'unit') {
      this.page.set(1);
      this.loadDocuments();
    } else {
      this.documents.set([]);
      this.totalCount.set(0);
    }
  }

  onSearch(): void {
    this.page.set(1);
    this.loadDocuments();
  }

  onResetFilters(): void {
    this.keyword.set('');
    this.page.set(1);
    this.loadDocuments();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.page.set(page);
    this.loadDocuments();
  }

  totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount() / this.pageSize));
  }

  private loadDocuments(): void {
    const node = this.selectedNode();
    if (!node || node.nodeType === 'unit') return;

    this.loadingDocuments.set(true);
    this.catalogService
      .getCatalogDocuments(node.id, this.keyword() || null, this.page(), this.pageSize)
      .pipe(finalize(() => this.loadingDocuments.set(false)))
      .subscribe({
        next: (res) => {
          this.documents.set(res.items || []);
          this.totalCount.set(res.totalCount || 0);
        },
        error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách tài liệu.' }),
      });
  }

  formatFileSize(bytes: number | null): string {
    if (!bytes || bytes <= 0) return '—';
    const units = ['B', 'KB', 'MB', 'GB'];
    let value = bytes;
    let unitIndex = 0;
    while (value >= 1024 && unitIndex < units.length - 1) {
      value /= 1024;
      unitIndex++;
    }
    return `${value.toFixed(unitIndex === 0 ? 0 : 1)} ${units[unitIndex]}`;
  }

  download(doc: PmisDocumentItem): void {
    if (this.downloadingId()) return;
    this.downloadingId.set(doc.id);
    this.catalogService
      .downloadDocument(doc.id, doc.documentName || undefined)
      .catch((err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err?.message || 'Không thể tải file.' });
      })
      .finally(() => this.downloadingId.set(null));
  }

  openUploadDialog(): void {
    this.uploadFile.set(null);
    this.uploadDocumentName.set('');
    this.uploadDocumentType.set('');
    this.uploadDialogVisible.set(true);
  }

  closeUploadDialog(): void {
    if (!this.uploading()) this.uploadDialogVisible.set(false);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.uploadFile.set(file);
    if (file && !this.uploadDocumentName()) {
      this.uploadDocumentName.set(file.name);
    }
  }

  confirmUpload(): void {
    const node = this.selectedNode();
    const file = this.uploadFile();
    if (!node || !file) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu thông tin', detail: 'Vui lòng chọn file cần upload.' });
      return;
    }

    this.uploading.set(true);
    this.catalogService
      .uploadDocument(node.id, file, this.uploadDocumentName() || undefined, this.uploadDocumentType() || undefined)
      .pipe(finalize(() => this.uploading.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã upload tài liệu.' });
          this.uploadDialogVisible.set(false);
          this.loadTree();
          this.loadDocuments();
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err?.error?.message || 'Không thể upload tài liệu.' });
        },
      });
  }
}
