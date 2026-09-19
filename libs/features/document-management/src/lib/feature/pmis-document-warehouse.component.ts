import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { finalize, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { PmisDocumentCatalogService } from '../data-access/pmis-document-catalog.service';
import { PmisCatalogNode, PmisDocumentItem, PmisInfrastructureLookupItem } from '../models/pmis-catalog.models';
import { convertPmisFlatToTree, findPmisBreadcrumbPath, groupInfrastructureNodesByType } from '../utils/pmis-catalog-tree.util';

const NODE_ICONS: { [key: string]: string } = {
  root: 'pi-sitemap',
  unit: 'pi-building',
  group: 'pi-folder',
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
  imports: [CommonModule, FormsModule, DialogModule, SelectModule, ToastModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './pmis-document-warehouse.component.html',
  styleUrl: './document-management.component.css',
})
export class PmisDocumentWarehouseComponent {
  private readonly catalogService = inject(PmisDocumentCatalogService);
  private readonly messageService = inject(MessageService);

  nodeIcons = NODE_ICONS;

  // Cấp gốc mặc định (ảo) bao toàn bộ các Đơn vị (công ty) - không gọi API load Đơn vị cho đến khi
  // người dùng click mở node này; click vào từng công ty mới gọi tiếp API load Trạm/Đường dây/Thiết bị
  // của riêng công ty đó (load lười theo cấp, tránh tải toàn bộ cây 1 lần như trước).
  readonly ROOT_NODE_ID = '__root__';
  readonly ROOT_NODE_LABEL = 'Kho tài liệu PMIS';
  private unitsLoaded = false;
  private readonly loadedUnitChildren = new Set<string>();

  loadingNodeIds = signal<Set<string>>(new Set());
  flatNodes = signal<PmisCatalogNode[]>([]);
  tree = computed<PmisCatalogNode[]>(() => [
    {
      id: this.ROOT_NODE_ID,
      name: this.ROOT_NODE_LABEL,
      parentId: null,
      nodeType: 'root',
      documentCount: 0,
      children: groupInfrastructureNodesByType(convertPmisFlatToTree(this.flatNodes())),
    },
  ]);
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

  /** Chỉ node Trạm/Đường dây/Thiết bị mới có danh sách tài liệu riêng để xem (gốc/Đơn vị chỉ để điều hướng). */
  canShowDocuments = computed(() => {
    const node = this.selectedNode();
    return !!node && (node.nodeType === 'substation' || node.nodeType === 'line' || node.nodeType === 'equipment');
  });

  /** Chọn 1 Đơn vị (công ty) hoặc 1 thư mục "Trạm biến áp"/"Đường dây" hiển thị danh sách con của nó
   * sang bảng bên phải - cây chỉ dùng để điều hướng, danh sách duyệt/click tiếp nằm ở bảng cho quen
   * thuộc như các màn danh sách khác. Đọc thẳng `children` đã dựng sẵn trên chính node đang chọn (tree()
   * computed) thay vì lọc lại flatNodes - vì 2 thư mục "group" chỉ tồn tại trên cây, không có trong
   * flatNodes. */
  canShowChildList = computed(() => {
    const type = this.selectedNode()?.nodeType;
    return type === 'unit' || type === 'group';
  });

  childNodesForTable = computed<PmisCatalogNode[]>(() => this.selectedNode()?.children ?? []);

  // Tìm Trạm/Đường dây trên TOÀN BỘ công ty (không cần duyệt tay qua từng công ty trong cây) — tải 1
  // lần khi mở dropdown lần đầu, lọc theo từ khóa ngay trong p-select (giống các dropdown lookup khác
  // trong dự án), chọn xong tự tải + mở đúng nhánh cây chứa nó rồi hiển thị tài liệu.
  private infraSearchLoaded = false;
  infraSearchLoading = signal(false);
  infraSearchOptions = signal<PmisInfrastructureLookupItem[]>([]);
  selectedInfraSearchId = signal<string | null>(null);

  loadInfraSearchOptionsIfNeeded(): void {
    if (this.infraSearchLoaded) return;
    this.infraSearchLoaded = true;

    this.infraSearchLoading.set(true);
    this.catalogService
      .searchInfrastructures()
      .pipe(finalize(() => this.infraSearchLoading.set(false)))
      .subscribe({
        next: (items) => this.infraSearchOptions.set(items || []),
        error: () => {
          this.infraSearchLoaded = false;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách Trạm/Đường dây.' });
        },
      });
  }

  onInfraSearchSelected(infraId: string | null): void {
    this.selectedInfraSearchId.set(null); // reset để có thể chọn lại đúng item đó lần sau
    if (!infraId) return;

    const item = this.infraSearchOptions().find((i) => i.id === infraId);
    if (item) this.goToInfrastructure(item);
  }

  /** Tải kèm Danh sách Đơn vị (nếu cây gốc chưa mở) + Trạm/Đường dây/Thiết bị của đúng Đơn vị chứa item
   * (nếu chưa tải), rồi mở sẵn nhánh cây (gốc → Đơn vị → thư mục Trạm/Đường dây) và chọn đúng node. */
  private goToInfrastructure(item: PmisInfrastructureLookupItem): void {
    const proceed = () => {
      const groupId = `${item.unitNodeId}__group_${item.nodeType}`;
      this.expandedNodeIds.update((current) => new Set(current).add(this.ROOT_NODE_ID).add(item.unitNodeId).add(groupId));

      const node = this.flatNodes().find((n) => n.id === item.id);
      if (node) this.selectNode(node);
    };

    const needUnits = !this.unitsLoaded;
    const needChildren = !this.loadedUnitChildren.has(item.unitNodeId);
    if (!needUnits && !needChildren) {
      proceed();
      return;
    }

    if (needUnits) this.setNodeLoading(this.ROOT_NODE_ID, true);
    if (needChildren) {
      this.loadedUnitChildren.add(item.unitNodeId);
      this.setNodeLoading(item.unitNodeId, true);
    }

    forkJoin({
      units: needUnits ? this.catalogService.getCatalogUnits() : of<PmisCatalogNode[]>([]),
      children: needChildren ? this.catalogService.getCatalogUnitChildren(item.unitNodeId) : of<PmisCatalogNode[]>([]),
    })
      .pipe(
        finalize(() => {
          if (needUnits) this.setNodeLoading(this.ROOT_NODE_ID, false);
          if (needChildren) this.setNodeLoading(item.unitNodeId, false);
        }),
        catchError(() => {
          if (needChildren) this.loadedUnitChildren.delete(item.unitNodeId);
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải dữ liệu của Trạm/Đường dây này.' });
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (!result) return;
        if (needUnits) this.unitsLoaded = true;
        this.flatNodes.update((current) => this.mergeNodes(this.mergeNodes(current, result.units), result.children));
        proceed();
      });
  }

  private mergeNodes(current: PmisCatalogNode[], incoming: PmisCatalogNode[]): PmisCatalogNode[] {
    if (!incoming || incoming.length === 0) return current;
    const map = new Map(current.map((n) => [n.id, n]));
    incoming.forEach((n) => map.set(n.id, n));
    return Array.from(map.values());
  }

  private setNodeLoading(nodeId: string, loading: boolean): void {
    this.loadingNodeIds.update((current) => {
      const next = new Set(current);
      if (loading) next.add(nodeId);
      else next.delete(nodeId);
      return next;
    });
  }

  isNodeLoading(nodeId: string): boolean {
    return this.loadingNodeIds().has(nodeId);
  }

  /** Sau khi upload thủ công, số đếm tài liệu (documentCount) hiển thị trên cây đã cũ - buộc tải lại
   * đúng nhánh của Đơn vị chứa node vừa upload (không tải lại toàn bộ cây). */
  private refreshAncestorUnitChildren(node: PmisCatalogNode): void {
    const path = findPmisBreadcrumbPath(node.id, this.flatNodes());
    const unitAncestor = path.find((n) => n.nodeType === 'unit');
    if (!unitAncestor) return;
    this.loadedUnitChildren.delete(unitAncestor.id);
    this.loadUnitChildrenIfNeeded(unitAncestor.id);
  }

  private loadUnitsIfNeeded(): void {
    if (this.unitsLoaded) return;
    this.unitsLoaded = true;

    this.setNodeLoading(this.ROOT_NODE_ID, true);
    this.catalogService
      .getCatalogUnits()
      .pipe(finalize(() => this.setNodeLoading(this.ROOT_NODE_ID, false)))
      .subscribe({
        next: (nodes) => this.flatNodes.update((current) => this.mergeNodes(current, nodes)),
        error: () => {
          this.unitsLoaded = false; // Cho phép click lại để thử tải lại nếu lần này lỗi.
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách đơn vị.' });
        },
      });
  }

  private loadUnitChildrenIfNeeded(unitNodeId: string): void {
    if (this.loadedUnitChildren.has(unitNodeId)) return;
    this.loadedUnitChildren.add(unitNodeId);

    this.setNodeLoading(unitNodeId, true);
    this.catalogService
      .getCatalogUnitChildren(unitNodeId)
      .pipe(finalize(() => this.setNodeLoading(unitNodeId, false)))
      .subscribe({
        next: (nodes) => this.flatNodes.update((current) => this.mergeNodes(current, nodes)),
        error: () => {
          this.loadedUnitChildren.delete(unitNodeId);
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải dữ liệu của đơn vị này.' });
        },
      });
  }

  toggleExpand(node: PmisCatalogNode, event: Event): void {
    event.stopPropagation();

    const willExpand = !this.isExpanded(node.id);
    this.expandedNodeIds.update((current) => {
      const next = new Set(current);
      if (next.has(node.id)) next.delete(node.id);
      else next.add(node.id);
      return next;
    });

    if (!willExpand) return;

    if (node.id === this.ROOT_NODE_ID) {
      this.loadUnitsIfNeeded();
    } else if (node.nodeType === 'unit') {
      this.loadUnitChildrenIfNeeded(node.id);
    }
  }

  isExpanded(nodeId: string): boolean {
    return this.expandedNodeIds().has(nodeId);
  }

  selectNode(node: PmisCatalogNode): void {
    this.selectedNode.set(node);
    if (node.nodeType === 'substation' || node.nodeType === 'line' || node.nodeType === 'equipment') {
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
          this.refreshAncestorUnitChildren(node);
          this.loadDocuments();
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err?.error?.message || 'Không thể upload tài liệu.' });
        },
      });
  }
}
