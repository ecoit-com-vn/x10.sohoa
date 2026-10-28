import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PhysicalStorageService } from '../../physical-storage/physical-storage.service';
import { AuthService } from '@sohoa.frontend/shared/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { ToastModule } from 'primeng/toast';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';

interface StorageTreeNode {
  id: string;
  level: 'root' | 'shelf' | 'floor' | 'box';
  dataId: number;
  name: string;
  code?: string;
  shelfId?: number;
  floorId?: number;
  boxId?: number;
  docCount?: number;
  children?: StorageTreeNode[];
}

@Component({
  selector: 'app-physical-dossier-warehouse',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    WfBreadcrumbComponent,
    ToastModule,
    DialogModule
  ],
  providers: [MessageService],
  templateUrl: './physical-dossier-warehouse.component.html',
  styleUrls: ['./physical-dossier-warehouse.component.scss']
})
export class PhysicalDossierWarehouseComponent implements OnInit {
  private readonly storageService = inject(PhysicalStorageService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  // Tree & State
  treeData = signal<StorageTreeNode[]>([]);
  expandedNodes = signal<Set<string>>(new Set(['root']));
  selectedNode = signal<StorageTreeNode | null>(null);
  breadcrumbPath = signal<StorageTreeNode[]>([]);
  treeSearchKeyword = signal('');

  filteredTreeData = computed(() => {
    const kw = this.treeSearchKeyword().trim().toLowerCase();
    const roots = this.treeData();
    if (!kw) return roots;

    const filterNode = (node: StorageTreeNode): StorageTreeNode | null => {
      const matchSelf = (node.name && node.name.toLowerCase().includes(kw)) ||
                        (node.code && node.code.toLowerCase().includes(kw));

      let filteredChildren: StorageTreeNode[] = [];
      if (node.children && node.children.length > 0) {
        filteredChildren = node.children
          .map(child => filterNode(child))
          .filter((c): c is StorageTreeNode => c !== null);
      }

      if (matchSelf || filteredChildren.length > 0) {
        return {
          ...node,
          children: filteredChildren
        };
      }
      return null;
    };

    return roots
      .map(r => filterNode(r))
      .filter((r): r is StorageTreeNode => r !== null);
  });

  // Units
  organizationUnits = signal<any[]>([]);
  currentUnitId = signal<number | null>(null);

  // Dossiers List & Filter
  dossiers = signal<any[]>([]);
  totalCount = signal(0);
  loading = signal(false);
  loadingTree = signal(false);

  keyword = signal('');
  selectedDossierTypeId = signal<string | null>(null);
  selectedStatusId = signal<number | null>(null);
  dossierTypes = signal<any[]>([]);

  page = signal(1);
  pageSize = signal(10);
  totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));

  // Detail Modal
  detailModalVisible = signal(false);
  selectedDossier = signal<any>(null);

  ngOnInit(): void {
    const userUnitId = this.authService.getUserUnitId();
    if (userUnitId) {
      this.currentUnitId.set(userUnitId);
    }

    this.loadOrganizationUnits();
    this.loadDossierTypes();
    this.loadTree();
  }

  loadOrganizationUnits(): void {
    this.storageService.getOrganizationUnits().subscribe({
      next: (units) => this.organizationUnits.set(units || []),
      error: () => {}
    });
  }

  loadDossierTypes(): void {
    this.storageService.getDossierTypes().subscribe({
      next: (types) => this.dossierTypes.set(types || []),
      error: () => {}
    });
  }

  loadTree(): void {
    this.loadingTree.set(true);
    this.storageService.getPhysicalStorageTree(this.currentUnitId()).subscribe({
      next: (shelves) => {
        this.loadingTree.set(false);
        this.buildTree(shelves || []);
        // Tải danh sách hồ sơ ban đầu (toàn bộ kho của đơn vị)
        this.loadDossiers();
      },
      error: () => {
        this.loadingTree.set(false);
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được cây kho lưu trữ' });
      }
    });
  }

  private buildTree(shelves: any[]): void {
    const rootNode: StorageTreeNode = {
      id: 'root',
      level: 'root',
      dataId: 0,
      name: 'Kho Lưu Trữ Hồ Sơ Kỹ Thuật',
      children: []
    };

    let totalShelvesCount = 0;

    rootNode.children = shelves.map((shelf) => {
      totalShelvesCount++;
      const shelfNode: StorageTreeNode = {
        id: `shelf_${shelf.id}`,
        level: 'shelf',
        dataId: shelf.id,
        shelfId: shelf.id,
        name: shelf.name || `Kệ ${shelf.code}`,
        code: shelf.code,
        children: []
      };

      shelfNode.children = (shelf.floors || []).map((floor: any) => {
        const floorNode: StorageTreeNode = {
          id: `floor_${floor.id}`,
          level: 'floor',
          dataId: floor.id,
          shelfId: shelf.id,
          floorId: floor.id,
          name: floor.name || `Tầng ${floor.code}`,
          code: floor.code,
          children: []
        };

        floorNode.children = (floor.boxes || []).map((box: any) => {
          return {
            id: `box_${box.id}`,
            level: 'box',
            dataId: box.id,
            shelfId: shelf.id,
            floorId: floor.id,
            boxId: box.id,
            name: box.name || `Hộp ${box.code}`,
            code: box.code
          } as StorageTreeNode;
        });

        return floorNode;
      });

      return shelfNode;
    });

    this.treeData.set([rootNode]);
    // Mặc định mở node root và kệ đầu tiên
    const newExpanded = new Set<string>(['root']);
    if (rootNode.children.length > 0) {
      newExpanded.add(rootNode.children[0].id);
    }
    this.expandedNodes.set(newExpanded);
  }

  onTreeSearchChange(val: string): void {
    this.treeSearchKeyword.set(val);
    const kw = val.trim().toLowerCase();
    if (kw) {
      const expanded = new Set(this.expandedNodes());
      const expandMatching = (nodes: StorageTreeNode[]) => {
        for (const n of nodes) {
          if (n.children && n.children.length > 0) {
            expanded.add(n.id);
            expandMatching(n.children);
          }
        }
      };
      expandMatching(this.filteredTreeData());
      this.expandedNodes.set(expanded);
    }
  }

  // Node Selection Handlers
  selectNode(node: StorageTreeNode, path: StorageTreeNode[] = []): void {
    this.selectedNode.set(node);
    this.breadcrumbPath.set(path);
    this.page.set(1);
    this.loadDossiers();
  }

  selectRoot(): void {
    this.selectedNode.set(null);
    this.breadcrumbPath.set([]);
    this.page.set(1);
    this.loadDossiers();
  }

  toggleNode(nodeId: string, event: Event): void {
    event.stopPropagation();
    const set = new Set(this.expandedNodes());
    if (set.has(nodeId)) {
      set.delete(nodeId);
    } else {
      set.add(nodeId);
    }
    this.expandedNodes.set(set);
  }

  isExpanded(nodeId: string): boolean {
    return this.expandedNodes().has(nodeId);
  }

  expandAll(): void {
    const allIds = new Set<string>();
    const traverse = (nodes: StorageTreeNode[]) => {
      for (const n of nodes) {
        allIds.add(n.id);
        if (n.children?.length) traverse(n.children);
      }
    };
    traverse(this.treeData());
    this.expandedNodes.set(allIds);
  }

  collapseAll(): void {
    this.expandedNodes.set(new Set(['root']));
  }

  // Dossiers Data Fetching
  loadDossiers(): void {
    this.loading.set(true);
    const node = this.selectedNode();

    this.storageService.getDossiersByPhysicalStorage({
      shelfId: node?.level === 'shelf' ? node.shelfId : (node?.shelfId ?? null),
      floorId: node?.level === 'floor' ? node.floorId : (node?.floorId ?? null),
      boxId: node?.level === 'box' ? node.boxId : null,
      unitId: this.currentUnitId(),
      keyword: this.keyword().trim() || null,
      dossierTypeId: this.selectedDossierTypeId(),
      statusId: this.selectedStatusId(),
      page: this.page(),
      pageSize: this.pageSize()
    }).subscribe({
      next: (res) => {
        this.loading.set(false);
        this.dossiers.set(res?.items || []);
        this.totalCount.set(res?.totalCount || 0);
      },
      error: () => {
        this.loading.set(false);
        this.dossiers.set([]);
        this.totalCount.set(0);
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách hồ sơ' });
      }
    });
  }

  onSearch(): void {
    this.page.set(1);
    this.loadDossiers();
  }

  onResetFilters(): void {
    this.keyword.set('');
    this.selectedDossierTypeId.set(null);
    this.selectedStatusId.set(null);
    this.page.set(1);
    this.loadDossiers();
  }

  onUnitChange(unitId: number | null): void {
    this.currentUnitId.set(unitId);
    this.selectedNode.set(null);
    this.breadcrumbPath.set([]);
    this.page.set(1);
    this.loadTree();
  }

  goToPage(p: number): void {
    if (p < 1 || p > this.totalPages()) return;
    this.page.set(p);
    this.loadDossiers();
  }

  // Detail Modal & Navigation
  openDetail(dossier: any): void {
    this.selectedDossier.set(dossier);
    this.detailModalVisible.set(true);
  }

  closeDetail(): void {
    this.detailModalVisible.set(false);
    this.selectedDossier.set(null);
  }

  openFullDossier(dossierId: string): void {
    if (dossierId) {
      this.router.navigate(['/dossier-management/my-dossiers', dossierId]);
    }
  }

  formatStorageLocation(dossier: any): string {
    const parts = [];
    if (dossier.shelfName) parts.push(dossier.shelfName);
    if (dossier.floorName) parts.push(dossier.floorName);
    if (dossier.boxName) parts.push(dossier.boxName);
    return parts.length ? parts.join(' / ') : 'Chưa xếp vị trí';
  }

  getStatusBadgeClass(statusId: number): string {
    switch (statusId) {
      case 2: return 'badge-success'; // Hoàn thành / Đã lưu kho
      case 3: return 'badge-warning'; // Chờ duyệt
      case 4: return 'badge-info';    // Đang xử lý
      default: return 'badge-secondary';
    }
  }
}
