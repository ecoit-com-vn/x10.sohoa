// sohoa.frontend/libs/features/reports/src/lib/components/report-groups/report-groups.component.ts
import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router, RouterModule } from '@angular/router';
import { MessageService, TreeNode, MenuItem } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { DialogModule } from 'primeng/dialog';
import { TreeSelectModule } from 'primeng/treeselect';
import { Menu, MenuModule } from 'primeng/menu';
import { environment } from '@env/environment';
import { finalize } from 'rxjs';
import { AuthService } from '@sohoa.frontend/shared/core';
import {
  DeleteConfirmDialogComponent,
  WfBreadcrumbComponent,
  EcoPaginatorComponent,
} from '@sohoa.frontend/shared/layout';

export interface Report {
  id: number;
  code: string;
  name: string;
}

export interface ReportGroup {
  id?: number;
  code: string;
  name: string;
  sortOrder: number;
  description?: string;
  isActive: boolean;
  reportCount?: number;
  unitCount?: number;
  reportIds: number[];
  unitIds: number[];
  reports?: Report[];
}

@Component({
  selector: 'app-report-groups',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    ToastModule,
    DialogModule,
    TreeSelectModule,
    MenuModule,
    EcoPaginatorComponent,
    WfBreadcrumbComponent,
    DeleteConfirmDialogComponent
  ],
  providers: [MessageService],
  templateUrl: './report-groups.component.html',
  styleUrls: ['./report-groups.component.scss']
})
export class ReportGroupsComponent implements OnInit {
  groups = signal<ReportGroup[]>([]);
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);

  // Filter variables
  searchKeyword = signal<string>('');
  filterStatus = signal<string>('ALL'); // ALL, ACTIVE, INACTIVE

  currentPage = signal(1);
  pageSize = signal(10);

  // Action Menu Items
  actionMenuItems: MenuItem[] = [];

  // Computed filtered list client-side
  filteredGroups = computed(() => {
    const kw = this.searchKeyword().toLowerCase().trim();
    const status = this.filterStatus();

    return this.groups().filter(g => {
      const matchKeyword = !kw ||
        g.code.toLowerCase().includes(kw) ||
        g.name.toLowerCase().includes(kw) ||
        (g.description && g.description.toLowerCase().includes(kw));

      const matchStatus = status === 'ALL' ||
        (status === 'ACTIVE' && g.isActive) ||
        (status === 'INACTIVE' && !g.isActive);

      return matchKeyword && matchStatus;
    });
  });

  // Lock Confirmation
  showLockConfirm = signal<boolean>(false);
  lockTarget = signal<ReportGroup | null>(null);
  locking = signal<boolean>(false);

  // Delete Confirmation
  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<ReportGroup | null>(null);
  deleting = signal<boolean>(false);

  // Chuẩn hóa tên nhóm báo cáo hiển thị trong popup dùng chung.
  readonly deleteTargetLabel = computed(() => {
    const group = this.deleteTarget();

    return group ? `${group.name} (${group.code})` : '';
  });

  // Popup Create Variables
  displayCreateDialog = false;
  formSubmitted = false;
  currentNewGroup: ReportGroup = {
    code: '',
    name: '',
    sortOrder: 1,
    description: '',
    isActive: true,
    reportIds: [],
    unitIds: []
  };

  organizationUnits = signal<any[]>([]);
  selectedUnitNodes = signal<TreeNode[]>([]);
  orgUnitTree = computed(() => this.buildOrgTree(this.organizationUnits()));
  primengOrgUnitTree = computed(() => {
    const buildPrimeNGNodes = (nodes: any[]): TreeNode[] =>
      nodes.map(n => {
        const children = n.children?.length ? buildPrimeNGNodes(n.children) : undefined;
        return {
          key: String(n.id),
          label: n.name,
          data: n,
          selectable: true,
          leaf: !children?.length,
          children
        } as TreeNode;
      });
    return buildPrimeNGNodes(this.orgUnitTree());
  });

  private http = inject(HttpClient);
  private router = inject(Router);
  private messageService = inject(MessageService);
  public authService = inject(AuthService);
  private apiUrl = `${environment.apiGatewayUrl}/api/v1/report-groups`;

  ngOnInit(): void {
    this.loadGroups();
    this.loadOrganizationUnits();
  }

  pagedGroups = computed(() => {
    const first = (this.currentPage() - 1) * this.pageSize();
    return this.filteredGroups().slice(first, first + this.pageSize());
  });

  onUnitPageChange(event: { first?: number; rows?: number }) {
    const rows = Number(event.rows) || this.pageSize();
    const first = Number(event.first) || 0;
    this.pageSize.set(rows);
    this.currentPage.set(Math.floor(first / rows) + 1);
  }

  onResetSearch() {
    this.searchKeyword.set('');
    this.filterStatus.set('ALL');
    this.loadOrganizationUnits();
  }
  onSearch() {
    this.loadGroups();
  }

  loadGroups() {
    this.loading.set(true);
    this.http.get<ReportGroup[]>(this.apiUrl)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (data) => {
          this.groups.set(data);
        },
        error: (err) => {
          console.error('Lỗi tải danh sách nhóm báo cáo:', err);
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách nhóm báo cáo.' });
        }
      });
  }

  loadOrganizationUnits() {
    this.http.get<any[]>(`${environment.apiGatewayUrl}/api/v1/organization-units/lookup`).subscribe({
      next: (res) => {
        this.organizationUnits.set(res || []);
      },
      error: (err) => {
        console.error('Lỗi tải cây đơn vị:', err);
      }
    });
  }

  buildOrgTree(units: any[]): any[] {
    const map = new Map<number, any>();
    const roots: any[] = [];
    units.forEach(u => {
      const id = Number(u.id);
      if (!id) return;
      map.set(id, { ...u, id, parentId: u.parentId != null ? Number(u.parentId) : null, children: [] as any[] });
    });
    map.forEach(node => {
      if (node.parentId && map.has(node.parentId)) {
        map.get(node.parentId)!.children.push(node);
      } else {
        roots.push(node);
      }
    });
    return roots;
  }

  onUnitNodesChange(nodes: TreeNode[] | TreeNode | null) {
    const list = Array.isArray(nodes) ? nodes : nodes ? [nodes] : [];
    this.selectedUnitNodes.set(list);
    const ids = list
      .map((n) => Number(n?.key ?? (n as any)?.data?.id))
      .filter((id) => !isNaN(id) && id > 0);
    this.currentNewGroup.unitIds = ids;
  }

  showCreatePopup() {
    this.formSubmitted = false;
    this.selectedUnitNodes.set([]);
    this.currentNewGroup = {
      code: '',
      name: '',
      sortOrder: this.groups().length + 1,
      description: '',
      isActive: true,
      reportIds: [],
      unitIds: []
    };
    this.displayCreateDialog = true;
  }

  hideCreatePopup() {
    this.displayCreateDialog = false;
  }

  saveNewGroup() {
    this.formSubmitted = true;
    if (!this.currentNewGroup.code || !this.currentNewGroup.name) {
      return;
    }

    this.saving.set(true);
    this.http.post(this.apiUrl, this.currentNewGroup)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã thêm mới nhóm báo cáo hệ thống.' });
          this.loadGroups();
          this.displayCreateDialog = false;
        },
        error: (err) => {
          console.error('Thêm mới nhóm báo cáo lỗi:', err);
          const msg = err?.error?.message || err?.message || 'Thêm mới thất bại.';
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: msg });
        }
      });
  }

  openActionMenu(group: ReportGroup, event: Event, menu: Menu): void {
    event.stopPropagation();
    const active = group.isActive === true;
    this.actionMenuItems = [
      ...(this.authService.hasPermission('REPORT_GROUP_EDIT') ? [
        {
          label: 'Cấu hình báo cáo',
          icon: 'pi pi-cog text-sky-600',
          command: () => this.goToEdit(group, 1)
        },
        {
          label: 'Chỉnh sửa thông tin',
          icon: 'pi pi-pencil text-amber-600',
          command: () => this.goToEdit(group, 0)
        }
      ] : []),
      ...([{
        label: active ? 'Khóa nhóm báo cáo' : 'Mở khóa nhóm báo cáo',
        title: active ? 'Khóa nhóm báo cáo' : 'Mở khóa nhóm báo cáo',
        icon: active ? 'pi pi-lock color-red' : 'pi pi-lock-open color-teal',
        command: () => this.onToggleStatus(group)
      }]),
      ...(this.authService.hasPermission('REPORT_GROUP_DELETE') ? [
        {
          label: 'Xóa nhóm',
          icon: 'pi pi-trash text-red-600',
          command: () => this.deleteGroup(group)
        }
      ] : [])
    ];
    menu.toggle(event);
  }

  goToEdit(group: ReportGroup, tabIndex: number) {
    this.router.navigate(['/reports/groups', group.id], { queryParams: { tab: tabIndex } });
  }

  deleteGroup(group: ReportGroup) {
    this.deleteTarget.set(group);
    this.showDeleteConfirm.set(true);
  }

  onCancelDelete(): void {
    // Không cho đóng popup khi request xóa đang được xử lý.
    if (this.deleting()) {
      return;
    }

    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }

  onCancelLock() {
    this.showLockConfirm.set(false);
    this.lockTarget.set(null);
  }

  onConfirmLock() {
    const group = this.lockTarget();
    if (!group) return;

    const isLocking = group.isActive;
    const action = isLocking ? 'lock' : 'unlock';
    const successMessage = isLocking
      ? 'Khóa nhóm báo cáo thành công.'
      : 'Mở khóa nhóm báo cáo thành công.';

    this.locking.set(true);
    this.http
      .patch(`${this.apiUrl}/${group.id}/${action}`, {})
      .pipe(
        finalize(() => {
          this.locking.set(false);
          this.onCancelLock(); // Hide dialog and reset target
        })
      )
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: successMessage,
          });
          this.loadGroups();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail:
              err?.error?.message ??
              'Không thể cập nhật trạng thái nhóm báo cáo.',
          });
        },
      });
  }

  onConfirmDelete(): void {
    const group = this.deleteTarget();

    // Chặn request không hợp lệ hoặc gửi trùng.
    if (!group || this.deleting()) {
      return;
    }

    this.deleting.set(true);
    this.http.delete(`${this.apiUrl}/${group.id}`)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xóa nhóm báo cáo hệ thống.' });
          this.showDeleteConfirm.set(false);
          this.deleteTarget.set(null);
          this.loadGroups();
        },
        error: (err) => {
          const msg = err?.error?.message || err?.message || 'Xóa nhóm báo cáo thất bại.';
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: msg });
        }
      });
  }

  onCodeInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const invalid = input.value.match(/[^a-zA-Z0-9_]/g);
    if (invalid) {
      this.messageService.add({ severity: 'warn', summary: 'Nhập sai', detail: 'Mã nhóm chỉ được chứa chữ cái (A-Z, a-z), chữ số (0-9) và dấu gạch dưới (_).' });
    }
    input.value = input.value.replace(/[^a-zA-Z0-9_]/g, '');
    this.currentNewGroup.code = input.value;
  }

onToggleStatus(group: ReportGroup) {
    this.lockTarget.set(group);
    this.showLockConfirm.set(true);
  }
}
