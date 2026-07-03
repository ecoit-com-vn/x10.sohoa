import { Component, Input, Output, EventEmitter, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { TreeSelectModule } from 'primeng/treeselect';
import { MessageService } from 'primeng/api';
import { Observable, finalize } from 'rxjs';
import { FolderAllocationService, FolderLookupItem, UserLookupItem } from '../../data-access/folder-allocation.service';

interface PrimeNGTreeNode {
  key: string;
  label: string;
  data: string;
  expanded?: boolean;
  children?: PrimeNGTreeNode[];
}

@Component({
  selector: 'app-folder-allocation-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ButtonModule, TreeSelectModule],
  templateUrl: './folder-allocation-dialog.component.html',
  styleUrl: './folder-allocation-dialog.component.scss'
})
export class FolderAllocationDialogComponent {
  private service = inject(FolderAllocationService);
  private messageService = inject(MessageService);

  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Input() allocationId: string | null = null;
  @Output() saved = new EventEmitter<void>();

  users = signal<UserLookupItem[]>([]);
  folders = signal<FolderLookupItem[]>([]);

  selectedUserId = signal<string>('');
  selectedFolderNode = signal<PrimeNGTreeNode | null>(null);

  loading = signal<boolean>(false);
  saving = signal<boolean>(false);
  isDataReady = signal<boolean>(false); // Cờ kiểm soát việc render cây thư mục

  get title(): string {
    return this.allocationId ? 'Cập nhật phân bổ nhập liệu' : 'Thêm mới phân bổ nhập liệu';
  }

  // Map users lookup
  userOptions = computed(() => {
    return this.users().map(u => ({
      label: `${u.full_name} (${u.user_name}) - ${u.organization_unit_name}`,
      value: u.id
    })).sort((a, b) => a.label.localeCompare(b.label));
  });

  // Map folders lookup sang dạng cây TreeNode[] của PrimeNG
  folderTreeOptions = computed<PrimeNGTreeNode[]>(() => {
    const list = this.folders();
    const map = new Map<string, PrimeNGTreeNode>();
    const roots: PrimeNGTreeNode[] = [];

    // Tạo các nodes (mặc định collapsed)
    list.forEach(f => {
      map.set(f.id, {
        key: f.id,
        label: f.name,
        data: f.id,
        expanded: false,
        children: []
      });
    });

    // Liên kết cha con
    list.forEach(f => {
      const node = map.get(f.id)!;
      if (f.parent_id) {
        const parent = map.get(f.parent_id);
        if (parent) {
          parent.children = parent.children || [];
          parent.children.push(node);
        } else {
          roots.push(node);
        }
      } else {
        roots.push(node);
      }
    });

    // Sắp xếp con theo tên
    const sortNodes = (node: PrimeNGTreeNode) => {
      if (node.children) {
        node.children.sort((a, b) => a.label.localeCompare(b.label));
        node.children.forEach(sortNodes);
      }
    };
    roots.forEach(sortNodes);
    roots.sort((a, b) => a.label.localeCompare(b.label));

    return roots;
  });

  onShow(): void {
    this.selectedUserId.set('');
    this.selectedFolderNode.set(null);
    this.isDataReady.set(false);
    this.loadLookups();
  }

  loadLookups(): void {
    this.loading.set(true);
    this.service.getUsersLookup().subscribe({
      next: (users) => this.users.set(users),
      error: () => this.showError('Không thể tải danh sách người dùng lookup.')
    });

    this.service.getFoldersLookup().subscribe({
      next: (folders) => {
        this.folders.set(folders);
        if (this.allocationId) {
          this.loadDetail();
        } else {
          this.loading.set(false);
          this.isDataReady.set(true);
        }
      },
      error: () => {
        this.showError('Không thể tải danh sách thư mục lookup.');
        this.loading.set(false);
      }
    });
  }

  loadDetail(): void {
    if (!this.allocationId) return;
    this.loading.set(true);
    this.service.getById(this.allocationId).pipe(
      finalize(() => {
        this.loading.set(false);
        this.isDataReady.set(true); // Chỉ bật data ready khi đã load xong detail và gán model
      })
    ).subscribe({
      next: (item) => {
        this.selectedUserId.set(item.user_id);
        
        const tree = this.folderTreeOptions();
        this.collapseAllNodes(tree);

        const matchedNode = this.findNodeByKey(tree, item.folder_id);
        if (matchedNode) {
          this.selectedFolderNode.set(matchedNode);
          // Tự động mở rộng các cấp cha chứa node được chọn
          this.expandAncestors(tree, item.folder_id);
        }
      },
      error: () => {
        this.showError('Không thể tải thông tin chi tiết phân bổ.');
        this.close();
      }
    });
  }

  private collapseAllNodes(nodes: PrimeNGTreeNode[]): void {
    nodes.forEach(node => {
      node.expanded = false;
      if (node.children && node.children.length > 0) {
        this.collapseAllNodes(node.children);
      }
    });
  }

  private expandAncestors(nodes: PrimeNGTreeNode[], targetKey: string): boolean {
    for (const node of nodes) {
      if (node.key === targetKey) {
        return true;
      }
      if (node.children && node.children.length > 0) {
        const foundInChild = this.expandAncestors(node.children, targetKey);
        if (foundInChild) {
          node.expanded = true;
          return true;
        }
      }
    }
    return false;
  }

  private findNodeByKey(nodes: PrimeNGTreeNode[], key: string): PrimeNGTreeNode | null {
    for (const node of nodes) {
      if (node.key === key) return node;
      if (node.children && node.children.length > 0) {
        const found = this.findNodeByKey(node.children, key);
        if (found) return found;
      }
    }
    return null;
  }

  save(): void {
    const folderNode = this.selectedFolderNode();
    const folderId = folderNode?.key;

    if (!folderId || !this.selectedUserId()) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Cảnh báo',
        detail: 'Vui lòng chọn đầy đủ Thư mục và Người xử lý.'
      });
      return;
    }

    this.saving.set(true);
    const req = {
      folder_id: folderId,
      user_id: this.selectedUserId()
    };

    const action$: Observable<any> = this.allocationId 
      ? (this.service.update(this.allocationId, req) as Observable<any>)
      : (this.service.create(req) as Observable<any>);

    action$.pipe(
      finalize(() => this.saving.set(false))
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: this.allocationId ? 'Cập nhật phân bổ thành công.' : 'Thêm mới phân bổ thành công.'
        });
        this.saved.emit();
        this.close();
      },
      error: (err: any) => {
        const msg = err?.error?.message || 'Có lỗi xảy ra trong quá trình lưu dữ liệu.';
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: msg
        });
      }
    });
  }

  close(): void {
    this.visibleChange.emit(false);
  }

  private showError(msg: string): void {
    this.messageService.add({
      severity: 'error',
      summary: 'Lỗi',
      detail: msg
    });
  }
}
