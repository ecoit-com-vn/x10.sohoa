import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService, ConfirmationService } from 'primeng/api';
import { environment } from '@env/environment';
import { finalize } from 'rxjs';
import { AuthService } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-organization-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule],
  providers: [MessageService],
  templateUrl: './organization-settings.component.html',
  styleUrl: './organization-settings.component.scss'
})
export class OrganizationSettings implements OnInit {
  units = signal<any[]>([]);
  searchKeyword = signal<string>('');

  displayDialog = signal<boolean>(false);
  dialogHeader = signal<string>('');
  isEdit = signal<boolean>(false);
  currentUnit = signal<any>({});
  
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/organization-units`;

  // Computed signal for filteredUnits
  filteredUnits = computed(() => {
    const kw = this.searchKeyword().toLowerCase().trim();
    const allUnits = this.units() || [];
    if (!kw) {
      return this.buildHierarchicalList();
    }
    return allUnits.filter(u => 
      (u.code?.toLowerCase().includes(kw) ?? false) || 
      (u.name?.toLowerCase().includes(kw) ?? false) || 
      (u.description?.toLowerCase().includes(kw) ?? false)
    );
  });

  constructor(
    private http: HttpClient,
    private messageService: MessageService,
    private confirmationService: ConfirmationService,
    public authService: AuthService
  ) {}

  ngOnInit() {
    this.loadUnits();
  }

  loadUnits() {
    this.loading.set(true);
    this.http.get<any[]>(this.apiUrl)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (data) => {
          this.units.set(data || []);
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải sơ đồ cây tổ chức.' });
        }
      });
  }

  onSearch() {
    // Tự động thông qua computed
  }

  // Thuật toán DFS xây dựng danh sách phẳng thụt lề
  buildHierarchicalList(): any[] {
    const result: any[] = [];
    const unitsSafe = this.units() || [];
    const rootNodes = unitsSafe.filter(u => !u.parentId);
    
    const visit = (node: any) => {
      result.push(node);
      const children = unitsSafe.filter(u => u.parentId === node.id);
      children.forEach(visit);
    };

    rootNodes.forEach(visit);

    // Thêm các nút bị mồ côi nếu có lỗi dữ liệu để tránh mất bản ghi hiển thị
    unitsSafe.forEach(u => {
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
      const parent = this.units().find(u => u.id === parentId);
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
    const unit = this.units().find(u => u.id === id);
    return unit ? unit.name : `Đơn vị #${id}`;
  }

  // Danh sách đơn vị cấp trên hợp lệ (loại trừ chính nó và các con của nó để tránh vòng lặp cây)
  getEligibleParents(currentId: number | null): any[] {
    const allUnits = this.units() || [];
    if (!currentId) return allUnits;
    
    // Tìm danh sách ID con trực tiếp và gián tiếp
    const childrenIds = new Set<number>();
    const findChildren = (pid: number) => {
      allUnits.forEach(u => {
        if (u.parentId === pid) {
          childrenIds.add(u.id);
          findChildren(u.id);
        }
      });
    };
    findChildren(currentId);

    return allUnits.filter(u => u.id !== currentId && !childrenIds.has(u.id));
  }

  onAddNew() {
    this.isEdit.set(false);
    this.currentUnit.set({ code: '', name: '', parentId: null, description: '' });
    this.dialogHeader.set('Thêm mới đơn vị phòng ban');
    this.displayDialog.set(true);
  }

  onEdit(unit: any) {
    this.isEdit.set(true);
    this.currentUnit.set({ ...unit });
    this.dialogHeader.set('Chỉnh sửa đơn vị phòng ban');
    this.displayDialog.set(true);
  }

  onSaveUnit() {
    const unitDraft = this.currentUnit();
    if (!unitDraft.code || !unitDraft.name) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập Mã và Tên đơn vị.' });
      return;
    }

    this.saving.set(true);
    // Đảm bảo parentId là null hoặc số
    if (unitDraft.parentId === 'null' || unitDraft.parentId === null) {
      unitDraft.parentId = null;
    } else {
      unitDraft.parentId = Number(unitDraft.parentId);
    }

    if (this.isEdit()) {
      this.http.put(`${this.apiUrl}/${unitDraft.id}`, unitDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Đã cập nhật thông tin phòng ban thành công!' });
            this.loadUnits();
            this.displayDialog.set(false);
          },
          error: (err) => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể chỉnh sửa đơn vị.' });
          }
        });
    } else {
      this.http.post<any>(this.apiUrl, unitDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: (created) => {
            this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Tạo đơn vị phòng ban mới thành công!' });
            this.loadUnits();
            this.displayDialog.set(false);
          },
          error: (err) => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể thêm mới đơn vị.' });
          }
        });
    }
  }

  onDelete(unit: any) {
    // Kiểm tra xem đơn vị này có đơn vị con không trước khi xóa
    const hasChildren = this.units().some(u => u.parentId === unit.id);
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
