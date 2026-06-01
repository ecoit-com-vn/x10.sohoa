import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService, ConfirmationService } from 'primeng/api';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-organization-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule],
  providers: [MessageService],
  template: `
    <div class="wf-page">
      <p-toast></p-toast>
      <div class="wf-card">
        
        <!-- Breadcrumb -->
        <div class="breadcrumb">
          <i class="pi pi-home bc-icon"></i>
          <span class="bc-text">Trang chủ</span>
          <span class="bc-sep">/</span>
          <span class="bc-text">Quản trị hệ thống</span>
          <span class="bc-sep">/</span>
          <span class="bc-current">Cài đặt tổ chức</span>
        </div>

        <p class="text-muted mb-4">
          Quản lý sơ đồ cơ cấu tổ chức cây phòng ban, đơn vị trực thuộc EVNHANOI để phân cấp quản lý và phân quyền bảo mật truy cập dữ liệu thiết bị, hồ sơ số hóa.
        </p>

        <!-- Toolbar -->
        <div class="list-toolbar">
          <div class="toolbar-left">
            <input type="text" class="wf-search-input"
              placeholder="Tìm nhanh đơn vị..."
              [(ngModel)]="searchKeyword"
              (keyup.enter)="onSearch()" />
            <button class="btn-tim" (click)="onSearch()">
              <i class="pi pi-search"></i> Tìm
            </button>
          </div>
          <div class="toolbar-right">
            <button class="btn-green" (click)="onAddNew()">
              <i class="pi pi-plus"></i> Thêm đơn vị mới
            </button>
          </div>
        </div>

        <!-- Tree List Table -->
        <div class="wf-table-wrap">
          <table class="wf-table">
            <thead>
              <tr>
                <th style="width: 100px;">STT</th>
                <th>Mã đơn vị (Code)</th>
                <th>Cơ cấu Tổ chức / Phòng ban (Name)</th>
                <th>Thuộc đơn vị cấp trên (Parent Unit)</th>
                <th>Mô tả</th>
                <th class="col-hd">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <!-- skeleton loading rows -->
              <ng-container *ngIf="loading">
                <tr *ngFor="let item of [1, 2, 3, 4]">
                  <td class="col-stt"><div class="skeleton-shimmer" style="height: 16px; width: 24px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 100px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 200px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 24px; width: 120px; border-radius: 12px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 180px; border-radius: 4px;"></div></td>
                  <td class="col-hd"><div class="skeleton-shimmer" style="height: 24px; width: 60px; border-radius: 4px;"></div></td>
                </tr>
              </ng-container>

              <ng-container *ngIf="!loading">
                <tr *ngFor="let unit of filteredUnits; let i = index">
                  <td class="col-stt text-muted">{{ i + 1 }}</td>
                  <td><code>{{ unit.code }}</code></td>
                  <td>
                    <div [style.padding-left.px]="getIndentLevel(unit) * 20" style="display: flex; align-items: center; gap: 8px;">
                      <i class="pi" [class.pi-building]="getIndentLevel(unit) === 0" [class.pi-users]="getIndentLevel(unit) > 0" style="color: #002D72;"></i>
                      <b class="wf-name-link" (click)="onEdit(unit)">{{ unit.name }}</b>
                    </div>
                  </td>
                  <td>
                    <span class="status-pill status-pending" *ngIf="unit.parentId" style="background-color: #f1f5f9; color: #0f172a; border-color: #cbd5e1;">
                      {{ getUnitName(unit.parentId) }}
                    </span>
                    <span class="text-muted" *ngIf="!unit.parentId" style="font-size: 0.8rem;">[Đơn vị gốc]</span>
                  </td>
                  <td><span class="text-muted" style="font-size: 0.82rem;">{{ unit.description }}</span></td>
                  <td class="col-hd">
                    <button class="act-btn act-edit" (click)="onEdit(unit)" title="Chỉnh sửa"><i class="pi pi-pencil"></i></button>
                    <button class="act-btn act-delete" (click)="onDelete(unit)" title="Xóa"><i class="pi pi-trash"></i></button>
                  </td>
                </tr>
                <tr *ngIf="filteredUnits.length === 0">
                  <td colspan="6" class="empty-row">
                    <i class="pi pi-inbox"></i>
                    <div>Không tìm thấy đơn vị nào phù hợp.</div>
                  </td>
                </tr>
              </ng-container>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer">
          <span class="record-count">Tổng số: <b>{{ units.length }}</b> phòng ban / đơn vị trực thuộc.</span>
        </div>

      </div>
    </div>

    <!-- Dialog Thêm/Sửa Đơn vị Tổ chức -->
    <p-dialog [(visible)]="displayDialog" [header]="dialogHeader" [modal]="true" [style]="{ width: '450px' }" styleClass="evn-dialog-custom">
      <div style="display: flex; flex-direction: column; gap: 14px; padding-top: 10px;">
        <div class="form-group">
          <label class="form-label">Mã phòng ban / đơn vị <span class="required">*</span></label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentUnit.code" placeholder="Ví dụ: P_KTDY, CT_DLDONGANH..." [disabled]="saving" />
        </div>
        
        <div class="form-group">
          <label class="form-label">Tên phòng ban / đơn vị <span class="required">*</span></label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentUnit.name" placeholder="Ví dụ: Phòng Kỹ thuật dây, Công ty Điện lực..." [disabled]="saving" />
        </div>

        <div class="form-group">
          <label class="form-label">Thuộc đơn vị cấp trên (Parent Unit)</label>
          <select class="wf-select w-full" [(ngModel)]="currentUnit.parentId" [disabled]="saving">
            <option [value]="null">-- Chọn đơn vị gốc / cấp trên --</option>
            <option *ngFor="let u of getEligibleParents(currentUnit.id)" [value]="u.id">{{ u.name }} ({{ u.code }})</option>
          </select>
        </div>
        
        <div class="form-group">
          <label class="form-label">Mô tả chi tiết</label>
          <textarea class="wf-textarea w-full" rows="3" [(ngModel)]="currentUnit.description" placeholder="Ghi chú chi tiết phòng ban này..." [disabled]="saving"></textarea>
        </div>
      </div>
      
      <ng-template #footer>
        <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayDialog = false" [disabled]="saving">Hủy</button>
          <button class="btn-save btn-small" (click)="onSaveUnit()" [disabled]="saving">
            <i class="pi pi-spin pi-spinner" *ngIf="saving" style="margin-right: 4px;"></i>
            Lưu
          </button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styles: `
    @keyframes shimmer {
      0% { background-position: -200% 0; }
      100% { background-position: 200% 0; }
    }
    .skeleton-shimmer {
      background: linear-gradient(90deg, #f3f4f6 25%, #e5e7eb 50%, #f3f4f6 75%);
      background-size: 200% 100%;
      animation: shimmer 1.5s infinite;
    }
    :global(html.dark-mode) .skeleton-shimmer {
      background: linear-gradient(90deg, #1e293b 25%, #334155 50%, #1e293b 75%);
      background-size: 200% 100%;
    }
  `
})
export class OrganizationSettings implements OnInit {
  units: any[] = [];
  filteredUnits: any[] = [];
  searchKeyword = '';

  displayDialog = false;
  dialogHeader = '';
  isEdit = false;
  currentUnit: any = {};
  
  loading = false;
  saving = false;

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/organization-units`;

  constructor(
    private http: HttpClient,
    private messageService: MessageService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit() {
    this.loadUnits();
  }

  loadUnits() {
    this.loading = true;
    this.http.get<any[]>(this.apiUrl).subscribe({
      next: (data) => {
        this.units = data;
        this.onSearch();
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải sơ đồ cây tổ chức.' });
      }
    });
  }

  onSearch() {
    if (this.searchKeyword) {
      const kw = this.searchKeyword.toLowerCase();
      this.filteredUnits = this.units.filter(u => 
        u.code.toLowerCase().includes(kw) || 
        u.name.toLowerCase().includes(kw) || 
        (u.description && u.description.toLowerCase().includes(kw))
      );
    } else {
      // Sắp xếp các đơn vị theo dạng cấu trúc cây (DFS style) để hiển thị thụt lề trực quan
      this.filteredUnits = this.buildHierarchicalList();
    }
  }

  // Thuật toán DFS xây dựng danh sách phẳng thụt lề
  buildHierarchicalList(): any[] {
    const result: any[] = [];
    const rootNodes = this.units.filter(u => !u.parentId);
    
    const visit = (node: any) => {
      result.push(node);
      const children = this.units.filter(u => u.parentId === node.id);
      children.forEach(visit);
    };

    rootNodes.forEach(visit);

    // Thêm các nút bị mồ côi nếu có lỗi dữ liệu để tránh mất bản ghi hiển thị
    this.units.forEach(u => {
      if (!result.includes(u)) {
        result.push(u);
      }
    });

    return result;
  }

  getIndentLevel(unit: any): number {
    let level = 0;
    let parentId = unit.parentId;
    while (parentId) {
      const parent = this.units.find(u => u.id === parentId);
      if (parent && parent.id !== unit.id) {
        level++;
        parentId = parent.parentId;
      } else {
        break;
      }
    }
    return level;
  }

  getUnitName(id: number): string {
    const unit = this.units.find(u => u.id === id);
    return unit ? unit.name : `Đơn vị #${id}`;
  }

  // Danh sách đơn vị cấp trên hợp lệ (loại trừ chính nó và các con của nó để tránh vòng lặp cây)
  getEligibleParents(currentId: number | null): any[] {
    if (!currentId) return this.units;
    
    // Tìm danh sách ID con trực tiếp và gián tiếp
    const childrenIds = new Set<number>();
    const findChildren = (pid: number) => {
      this.units.forEach(u => {
        if (u.parentId === pid) {
          childrenIds.add(u.id);
          findChildren(u.id);
        }
      });
    };
    findChildren(currentId);

    return this.units.filter(u => u.id !== currentId && !childrenIds.has(u.id));
  }

  onAddNew() {
    this.isEdit = false;
    this.currentUnit = { code: '', name: '', parentId: null, description: '' };
    this.dialogHeader = 'Thêm mới đơn vị phòng ban';
    this.displayDialog = true;
  }

  onEdit(unit: any) {
    this.isEdit = true;
    this.currentUnit = { ...unit };
    this.dialogHeader = 'Chỉnh sửa đơn vị phòng ban';
    this.displayDialog = true;
  }

  onSaveUnit() {
    if (!this.currentUnit.code || !this.currentUnit.name) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập Mã và Tên đơn vị.' });
      return;
    }

    this.saving = true;
    // Đảm bảo parentId là null hoặc số
    if (this.currentUnit.parentId === 'null' || this.currentUnit.parentId === null) {
      this.currentUnit.parentId = null;
    } else {
      this.currentUnit.parentId = Number(this.currentUnit.parentId);
    }

    if (this.isEdit) {
      this.http.put(`${this.apiUrl}/${this.currentUnit.id}`, this.currentUnit).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Đã cập nhật thông tin phòng ban thành công!' });
          this.loadUnits();
          this.displayDialog = false;
          this.saving = false;
        },
        error: (err) => {
          this.saving = false;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể chỉnh sửa đơn vị.' });
        }
      });
    } else {
      this.http.post<any>(this.apiUrl, this.currentUnit).subscribe({
        next: (created) => {
          this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Tạo đơn vị phòng ban mới thành công!' });
          this.loadUnits();
          this.displayDialog = false;
          this.saving = false;
        },
        error: (err) => {
          this.saving = false;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể thêm mới đơn vị.' });
        }
      });
    }
  }

  onDelete(unit: any) {
    // Kiểm tra xem đơn vị này có đơn vị con không trước khi xóa
    const hasChildren = this.units.some(u => u.parentId === unit.id);
    if (hasChildren) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Không thể xóa đơn vị này vì có các đơn vị trực thuộc bên dưới!' });
      return;
    }

    this.confirmationService.confirm({
      message: `Bạn có chắc chắn muốn xóa phòng ban/đơn vị ${unit.name} (${unit.code})?`,
      header: 'Xác nhận xóa',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Đồng ý',
      rejectLabel: 'Hủy',
      accept: () => {
        this.http.delete(`${this.apiUrl}/${unit.id}`).subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Xóa thành công', detail: 'Đã xóa đơn vị thành công!' });
            this.loadUnits();
          },
          error: (err) => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Xóa đơn vị thất bại.' });
          }
        });
      }
    });
  }
}
