import { Component, Input, Output, EventEmitter, inject, input, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { finalize } from 'rxjs';
import {
  PmisDocumentCatalogService,
  PmisCatalogNode,
  PmisDocumentItem,
  convertPmisFlatToTree,
} from '@sohoa.frontend/features/document-management';
import { DossierDocumentService, DocumentTypeLookupItem } from '../../data-access/dossier-document.service';
import { formatDocumentDate, formatDocumentFileSize } from '../../utils/document-display.util';

type PickerPhase = 'pick' | 'configure';

const NODE_ICONS: { [key: string]: string } = {
  unit: 'pi-building',
  substation: 'pi-bolt',
  line: 'pi-share-alt',
  equipment: 'pi-box',
};

/**
 * "Chọn từ kho PMIS" — sibling của DossierFolderPickerDialogComponent, cùng UI 2 bước (chọn tài liệu →
 * chọn loại văn bản), nhưng duyệt cây Kho tài liệu PMIS (Đơn vị→Trạm/Đường dây→Thiết bị) thay vì cây
 * thư mục, và CHỈ hiện đúng nhánh Trạm/Đường dây/Thiết bị đã gắn với hồ sơ này (infrastructureIds/
 * equipmentIds) — ẩn hẳn, không chỉ làm mờ, để không thể chọn nhầm tài liệu của đối tượng khác.
 */
@Component({
  selector: 'app-dossier-pmis-picker-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ButtonModule],
  templateUrl: './dossier-pmis-picker-dialog.component.html',
  styleUrl: '../dossier-folder-picker-dialog/dossier-folder-picker-dialog.component.scss',
})
export class DossierPmisPickerDialogComponent {
  private catalogService = inject(PmisDocumentCatalogService);
  private dossierDocumentService = inject(DossierDocumentService);
  private messageService = inject(MessageService);

  @Input({ required: true }) dossierId!: string;
  /** input() (signal-based) thay vì @Input() thường — allowedTree là computed() và CHỈ re-tính khi 1
   * signal nó đọc đổi giá trị; @Input() thường là field thuần, đọc trong computed() không đăng ký phụ
   * thuộc, nên nếu Trạm/Thiết bị hồ sơ đến muộn hơn cây (dossier còn đang tải) thì computed sẽ không
   * bao giờ tính lại — cây sẽ kẹt ở kết quả rỗng/sai của lần tính đầu tiên. */
  infrastructureIds = input<string[]>([]);
  equipmentIds = input<string[]>([]);
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() documentsAdded = new EventEmitter<void>();

  nodeIcons = NODE_ICONS;

  phase = signal<PickerPhase>('pick');
  loadingTree = signal(false);
  flatNodes = signal<PmisCatalogNode[]>([]);
  expandedNodeIds = signal<Set<string>>(new Set());
  selectedNode = signal<PmisCatalogNode | null>(null);

  /** Node THẬT SỰ được phép chọn (Trạm/Đường dây/Thiết bị hồ sơ này đã gắn) — khác allowedTree's node
   * set, vốn còn giữ thêm tổ tiên chỉ để không đứt gãy cây, KHÔNG được phép bấm chọn. */
  private directlyAllowedIds = computed(() => {
    const allInfra = new Set(this.infrastructureIds());
    const allEquip = new Set(this.equipmentIds());
    const ids = new Set<string>();
    for (const n of this.flatNodes()) {
      const isAllowed =
        ((n.nodeType === 'substation' || n.nodeType === 'line') && allInfra.has(n.id.replace('infra_', '')))
        || (n.nodeType === 'equipment' && allEquip.has(n.id.replace('equipment_', '')));
      if (isAllowed) ids.add(n.id);
    }
    return ids;
  });

  /** Chỉ giữ node Đơn vị/Trạm/Đường dây/Thiết bị nằm trên đường dẫn tới 1 node được phép chọn — ẩn hẳn
   * nhánh không liên quan tới hồ sơ này, không phải chỉ làm mờ. Tổ tiên được GIỮ LẠI trong cây (để
   * không đứt gãy đường dẫn) nhưng KHÔNG được phép tự bấm chọn (xem canSelect()/directlyAllowedIds). */
  allowedTree = computed(() => {
    const directlyAllowedIds = this.directlyAllowedIds();
    const nodes = this.flatNodes();

    const byId = new Map(nodes.map((n) => [n.id, n]));
    const keepIds = new Set<string>();
    for (const id of directlyAllowedIds) {
      // Giữ lại chính node này + toàn bộ tổ tiên (để cây không bị đứt gãy tới gốc).
      let current: PmisCatalogNode | undefined = byId.get(id);
      while (current) {
        keepIds.add(current.id);
        current = current.parentId ? byId.get(current.parentId) : undefined;
      }
    }

    return convertPmisFlatToTree(nodes.filter((n) => keepIds.has(n.id)));
  });

  canSelect(node: PmisCatalogNode): boolean {
    return this.directlyAllowedIds().has(node.id);
  }

  docSearch = signal('');
  documents = signal<PmisDocumentItem[]>([]);
  loadingDocuments = signal(false);
  copying = signal(false);
  page = signal(1);
  pageSize = signal(10);
  totalDocuments = signal(0);
  selectedDocIds = signal<Set<string>>(new Set());

  documentTypes = signal<DocumentTypeLookupItem[]>([]);
  loadingDocTypes = signal(false);
  selectedDocumentTypeId = signal('');

  totalPages = computed(() => {
    const total = this.totalDocuments();
    const size = this.pageSize();
    return total > 0 ? Math.ceil(total / size) : 0;
  });

  formatSize = formatDocumentFileSize;
  formatDate = formatDocumentDate;

  onShow(): void {
    this.phase.set('pick');
    this.selectedDocIds.set(new Set());
    this.docSearch.set('');
    this.page.set(1);
    this.selectedDocumentTypeId.set('');
    this.selectedNode.set(null);
    this.documents.set([]);
    this.totalDocuments.set(0);
    this.loadTree();
  }

  close(): void {
    if (this.copying()) return;
    this.visibleChange.emit(false);
  }

  backToPick(): void {
    if (this.copying()) return;
    this.phase.set('pick');
  }

  private loadTree(): void {
    this.loadingTree.set(true);
    this.catalogService
      .getCatalogTree()
      .pipe(finalize(() => this.loadingTree.set(false)))
      .subscribe({
        next: (nodes) => {
          this.flatNodes.set(nodes || []);
          this.expandedNodeIds.set(new Set((nodes || []).map((n) => n.id)));
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
    // Node chỉ được giữ trong cây vì là tổ tiên của 1 node được phép (vd. Trạm cha của 1 Thiết bị đã
    // gắn, nhưng bản thân Trạm đó KHÔNG được gắn với hồ sơ) — không cho xem tài liệu riêng của nó
    // (tài liệu OwnerType=INFRASTRUCTURE của Trạm đó không liên quan tới hồ sơ này).
    if (node.nodeType === 'unit' || !this.canSelect(node)) return;
    this.selectedNode.set(node);
    this.page.set(1);
    this.selectedDocIds.set(new Set());
    this.loadDocuments();
  }

  onDocSearchChange(value: string): void {
    this.docSearch.set(value);
    this.page.set(1);
    this.loadDocuments();
  }

  private loadDocuments(): void {
    const node = this.selectedNode();
    if (!node) return;

    this.loadingDocuments.set(true);
    this.catalogService
      .getCatalogDocuments(node.id, this.docSearch().trim() || null, this.page(), this.pageSize())
      .pipe(finalize(() => this.loadingDocuments.set(false)))
      .subscribe({
        next: (res) => {
          this.documents.set(res.items ?? []);
          this.totalDocuments.set(res.totalCount ?? 0);
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải tài liệu.' });
          this.documents.set([]);
          this.totalDocuments.set(0);
        },
      });
  }

  prevPage(): void {
    if (this.page() > 1) {
      this.page.update((p) => p - 1);
      this.loadDocuments();
    }
  }

  nextPage(): void {
    if (this.page() < this.totalPages()) {
      this.page.update((p) => p + 1);
      this.loadDocuments();
    }
  }

  isSelected(docId: string): boolean {
    return this.selectedDocIds().has(docId);
  }

  toggleSelect(doc: PmisDocumentItem, event: Event): void {
    event.stopPropagation();
    const next = new Set(this.selectedDocIds());
    if (next.has(doc.id)) next.delete(doc.id);
    else next.add(doc.id);
    this.selectedDocIds.set(next);
  }

  toggleSelectAll(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedDocIds.set(checked ? new Set(this.documents().map((d) => d.id)) : new Set());
  }

  allPageSelected(): boolean {
    const docs = this.documents();
    return docs.length > 0 && docs.every((d) => this.selectedDocIds().has(d.id));
  }

  selectedCount(): number {
    return this.selectedDocIds().size;
  }

  goToConfigure(): void {
    if (this.selectedCount() === 0) {
      this.messageService.add({ severity: 'warn', summary: 'Chưa chọn tài liệu', detail: 'Vui lòng chọn ít nhất một tài liệu' });
      return;
    }
    this.phase.set('configure');
    if (this.documentTypes().length === 0) this.loadDocumentTypes();
  }

  private loadDocumentTypes(): void {
    this.loadingDocTypes.set(true);
    this.dossierDocumentService
      .getDocumentTypesForDossier(this.dossierId)
      .pipe(finalize(() => this.loadingDocTypes.set(false)))
      .subscribe({
        next: (items) => this.documentTypes.set(items),
        error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh mục loại văn bản' }),
      });
  }

  confirmCopy(): void {
    const ids = Array.from(this.selectedDocIds());
    const documentTypeId = this.selectedDocumentTypeId();
    if (!documentTypeId) {
      this.messageService.add({ severity: 'warn', summary: 'Thiếu loại văn bản', detail: 'Vui lòng chọn loại văn bản (bắt buộc)' });
      return;
    }

    this.copying.set(true);
    this.dossierDocumentService
      .copyFromPmis(this.dossierId, ids, documentTypeId)
      .pipe(finalize(() => this.copying.set(false)))
      .subscribe({
        next: (res) => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: `Đã thêm ${res.movedCount} tài liệu từ kho PMIS vào hồ sơ` });
          this.documentsAdded.emit();
          this.selectedDocIds.set(new Set());
          this.visibleChange.emit(false);
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err?.error?.message || 'Không thể thêm tài liệu vào hồ sơ' });
        },
      });
  }

  trackByNodeId(_index: number, node: PmisCatalogNode): string {
    return node.id;
  }

  trackByDocumentId(_index: number, doc: PmisDocumentItem): string {
    return doc.id;
  }
}
