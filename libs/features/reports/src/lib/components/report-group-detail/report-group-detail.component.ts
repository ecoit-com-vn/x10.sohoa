// sohoa.frontend/libs/features/reports/src/lib/components/report-group-detail/report-group-detail.component.ts
import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { TreeSelectModule } from 'primeng/treeselect';
import { MessageService, TreeNode } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { environment } from '@env/environment';
import { finalize } from 'rxjs';
import { AuthService } from '@sohoa.frontend/shared/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { Report, ReportGroup } from '../report-groups/report-groups.component';

@Component({
  selector: 'app-report-group-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    TreeSelectModule,
    ToastModule,
    WfBreadcrumbComponent
  ],
  providers: [MessageService],
  templateUrl: './report-group-detail.component.html',
  styleUrls: ['./report-group-detail.component.scss']
})
export class ReportGroupDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private http = inject(HttpClient);
  private messageService = inject(MessageService);
  public authService = inject(AuthService);

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/report-groups`;

  // Path variables
  groupId = signal<number | null>(null);
  isEdit = signal<boolean>(false);
  activeTab = signal<number>(0); // 0: Thông tin chung, 1: Danh sách báo cáo

  // Form states
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);
  formSubmitted = false;

  currentGroup: ReportGroup = {
    code: '',
    name: '',
    sortOrder: 1,
    description: '',
    isActive: true,
    reportIds: [],
    unitIds: []
  };

  // Lookup data
  systemReports = signal<Report[]>([]);
  organizationUnits = signal<any[]>([]);

  // Tree variables for p-treeSelect
  selectedUnitNodes = signal<TreeNode[]>([]);
  selectedOrganizationUnitIds = signal<number[]>([]);
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

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    
    // Read query parameters to set active tab
    this.route.queryParams.subscribe(params => {
      const tabVal = Number(params['tab']);
      if (!isNaN(tabVal) && (tabVal === 0 || tabVal === 1)) {
        this.activeTab.set(tabVal);
      }
    });

    if (idParam === 'new') {
      this.isEdit.set(false);
      this.currentGroup = {
        code: '',
        name: '',
        sortOrder: 1,
        description: '',
        isActive: true,
        reportIds: [],
        unitIds: []
      };
      this.loadSystemReports();
      this.loadOrganizationUnits();
    } else {
      this.isEdit.set(true);
      const idVal = Number(idParam);
      if (isNaN(idVal)) {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'ID nhóm báo cáo không hợp lệ.' });
        this.router.navigate(['/reports/groups']);
        return;
      }
      this.groupId.set(idVal);
      this.loadSystemReports();
      this.loadOrganizationUnits();
      this.loadGroupDetail(idVal);
    }
  }

  loadGroupDetail(id: number) {
    this.loading.set(true);
    this.http.get<ReportGroup>(`${this.apiUrl}/${id}`)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (data) => {
          this.currentGroup = {
            id: data.id,
            code: data.code,
            name: data.name,
            sortOrder: data.sortOrder,
            description: data.description || '',
            isActive: data.isActive,
            reportIds: data.reportIds || [],
            unitIds: data.unitIds || []
          };
          this.selectedOrganizationUnitIds.set(data.unitIds || []);
          this.syncSelectedUnitNodes(data.unitIds || []);
        },
        error: (err) => {
          console.error('Lỗi tải chi tiết nhóm báo cáo:', err);
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải thông tin nhóm báo cáo.' });
          this.router.navigate(['/reports/groups']);
        }
      });
  }

  loadSystemReports() {
    this.http.get<Report[]>(`${this.apiUrl}/reports`).subscribe({
      next: (data) => {
        this.systemReports.set(data);
      },
      error: (err) => {
        console.error('Lỗi tải danh mục báo cáo hệ thống:', err);
      }
    });
  }

  loadOrganizationUnits() {
    this.http.get<any[]>(`${environment.apiGatewayUrl}/api/v1/organization-units/lookup`).subscribe({
      next: (res) => {
        this.organizationUnits.set(res || []);
        if (this.currentGroup.unitIds.length > 0) {
          this.syncSelectedUnitNodes(this.currentGroup.unitIds);
        }
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

  // Tree Node Selection Events
  onUnitNodesChange(nodes: TreeNode[] | TreeNode | null) {
    const list = Array.isArray(nodes) ? nodes : nodes ? [nodes] : [];
    this.selectedUnitNodes.set(list);
    const ids = list
      .map((n) => Number(n?.key ?? (n as any)?.data?.id))
      .filter((id) => !isNaN(id) && id > 0);
    this.selectedOrganizationUnitIds.set(ids);
    this.currentGroup.unitIds = ids;
  }

  private syncSelectedUnitNodes(unitIds: number[]) {
    const findNode = (nodes: TreeNode[], key: string): TreeNode | null => {
      for (const node of nodes) {
        if (node.key === key) return node;
        if (node.children?.length) {
          const found = findNode(node.children, key);
          if (found) return found;
        }
      }
      return null;
    };
    const tree = this.primengOrgUnitTree();
    if (!tree.length) return;
    this.selectedUnitNodes.set(
      unitIds.map((id) => findNode(tree, String(id))).filter((n): n is TreeNode => !!n)
    );
  }

  // Checkbox report events
  isReportSelected(reportId: number): boolean {
    return this.currentGroup.reportIds.includes(reportId);
  }

  toggleReportSelection(reportId: number) {
    const ids = [...this.currentGroup.reportIds];
    const index = ids.indexOf(reportId);
    if (index > -1) {
      ids.splice(index, 1);
    } else {
      ids.push(reportId);
    }
    this.currentGroup.reportIds = ids;
  }

  goBack() {
    this.router.navigate(['/reports/groups']);
  }

  saveGroup() {
    this.formSubmitted = true;
    if (!this.currentGroup.code || !this.currentGroup.name) {
      this.activeTab.set(0); // Switch to General tab to show errors
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Vui lòng điền đầy đủ các thông tin bắt buộc.' });
      return;
    }

    this.saving.set(true);
    if (this.isEdit()) {
      this.http.put(`${this.apiUrl}/${this.currentGroup.id}`, this.currentGroup)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã cập nhật thông tin nhóm báo cáo.' });
            setTimeout(() => this.goBack(), 1000);
          },
          error: (err) => {
            console.error('Cập nhật nhóm báo cáo lỗi:', err);
            const msg = err?.error?.message || err?.message || 'Cập nhật thất bại.';
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: msg });
          }
        });
    } else {
      this.http.post(this.apiUrl, this.currentGroup)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã thêm mới nhóm báo cáo hệ thống.' });
            setTimeout(() => this.goBack(), 1000);
          },
          error: (err) => {
            console.error('Thêm mới nhóm báo cáo lỗi:', err);
            const msg = err?.error?.message || err?.message || 'Thêm mới thất bại.';
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: msg });
          }
        });
    }
  }
}
