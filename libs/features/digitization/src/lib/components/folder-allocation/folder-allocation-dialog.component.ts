import { Component, Input, Output, EventEmitter, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
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
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    SelectModule,
    TreeSelectModule,
  ],
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
  submitted = signal<boolean>(false);

  /** Style đồng bộ với p-select Loại lưới điện — form thêm thiết bị kỹ thuật */
  readonly selectFieldStyle = {
    width: '100%',
    height: '38px',
    'line-height': '24px',
    border: '1px solid #cbd5e1',
    'border-radius': '6px',
    'background-color': '#ffffff',
    color: '#374151',
  };

  get title(): string {
    return this.allocationId ? 'Cập nhật phân bổ nhập liệu' : 'Thêm mới phân bổ nhập liệu';
  }

  userSelectOptions = computed(() =>
    this.users()
      .map(u => ({
        id: u.id,
        name: u.full_name,
      }))
      .sort((a, b) => a.name.localeCompare(b.name, 'vi'))
  );

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
    this.submitted.set(false);
    this.selectedUserId.set('');
    this.selectedFolderNode.set(null);
    this.isDataReady.set(false);
    this.loadLookups();
  }

  onFolderNodeChange(node: PrimeNGTreeNode | null): void {
    this.selectedFolderNode.set(node);
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
        const matchedNode = this.findNodeByKey(this.folderTreeOptions(), item.folder_id);
        this.selectedFolderNode.set(matchedNode);
      },
      error: () => {
        this.showError('Không thể tải thông tin chi tiết phân bổ.');
        this.close();
      }
    });
  }

  private findNodeByKey(nodes: PrimeNGTreeNode[], key: string): PrimeNGTreeNode | null {
    for (const node of nodes) {
      if (node.key === key) return node;
      if (node.children?.length) {
        const found = this.findNodeByKey(node.children, key);
        if (found) return found;
      }
    }
    return null;
  }

  save(): void {
    this.submitted.set(true);
    const folderId = this.selectedFolderNode()?.key;

    if (!folderId || !this.selectedUserId()) {
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
