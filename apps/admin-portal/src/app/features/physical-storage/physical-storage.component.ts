import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TabsModule } from 'primeng/tabs';
import { DialogModule } from 'primeng/dialog';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';

@Component({
  selector: 'app-physical-storage',
  standalone: true,
  imports: [
    CommonModule,
    TabsModule,
    DialogModule,
    FormsModule,
    ToastModule
  ],
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
          <span class="bc-text">Lưu trữ Vật lý & OCR</span>
          <span class="bc-sep">/</span>
          <span class="bc-current">Quản lý Lưu trữ</span>
        </div>

        <p class="text-muted mb-4">
          Thiết lập sơ đồ kho vật lý và danh mục hồ sơ của EVNHANOI để định vị chính xác vị trí lưu trữ thực tế của tài liệu kỹ thuật gốc sau khi quét số hóa.
        </p>

        <!-- PrimeNG Tabs styled premium -->
        <p-tabs value="0" styleClass="evn-tabs">
          <p-tablist styleClass="tab-bar-custom">
            <p-tab value="0" styleClass="tab-item-custom"><i class="pi pi-server mr-1"></i> Kệ lưu trữ (Shelf)</p-tab>
            <p-tab value="1" styleClass="tab-item-custom"><i class="pi pi-list mr-1"></i> Tầng kệ (Floor)</p-tab>
            <p-tab value="2" styleClass="tab-item-custom"><i class="pi pi-box mr-1"></i> Hộp hồ sơ (Box)</p-tab>
            <p-tab value="3" styleClass="tab-item-custom"><i class="pi pi-bookmark mr-1"></i> Danh mục (Category)</p-tab>
          </p-tablist>

          <p-tabpanels styleClass="tab-panels-custom">
            
            <!-- ── TAB KỆ LƯU TRỮ ── -->
            <p-tabpanel value="0">
              <div class="list-toolbar">
                <div class="toolbar-left">
                  <h3 class="font-bold m-0" style="color: #002D72; font-size: 1.05rem;">Danh sách kệ hồ sơ</h3>
                </div>
                <div class="toolbar-right">
                  <button class="btn-green btn-small" (click)="showDialog('shelf')">
                    <i class="pi pi-plus"></i> Thêm kệ mới
                  </button>
                </div>
              </div>
              
              <div class="wf-table-wrap">
                <table class="wf-table">
                  <thead>
                    <tr>
                      <th style="width: 100px;">STT</th>
                      <th>Mã số kệ</th>
                      <th>Tên kệ lưu trữ</th>
                      <th>Vị trí kho thực tế</th>
                      <th class="col-hd">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr *ngFor="let item of shelves; let idx = index">
                      <td class="col-stt text-muted">{{ idx + 1 }}</td>
                      <td><code>{{ item.code }}</code></td>
                      <td><b class="wf-name-link" (click)="editItem('shelf', item)">{{ item.name }}</b></td>
                      <td><span class="mota-text">{{ item.location }}</span></td>
                      <td class="col-hd">
                        <button class="act-btn act-edit" (click)="editItem('shelf', item)" title="Sửa"><i class="pi pi-pencil"></i></button>
                        <button class="act-btn act-delete" (click)="deleteItem('shelf', item)" title="Xóa"><i class="pi pi-trash"></i></button>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </p-tabpanel>

            <!-- ── TAB TẦNG KỆ ── -->
            <p-tabpanel value="1">
              <div class="list-toolbar">
                <div class="toolbar-left">
                  <h3 class="font-bold m-0" style="color: #002D72; font-size: 1.05rem;">Danh sách tầng trong kệ</h3>
                </div>
                <div class="toolbar-right">
                  <button class="btn-green btn-small" (click)="showDialog('floor')">
                    <i class="pi pi-plus"></i> Thêm tầng mới
                  </button>
                </div>
              </div>

              <div class="wf-table-wrap">
                <table class="wf-table">
                  <thead>
                    <tr>
                      <th style="width: 100px;">STT</th>
                      <th>Mã số tầng</th>
                      <th>Tên tầng kệ</th>
                      <th>Thuộc mã kệ</th>
                      <th class="col-hd">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr *ngFor="let item of floors; let idx = index">
                      <td class="col-stt text-muted">{{ idx + 1 }}</td>
                      <td><code>{{ item.code }}</code></td>
                      <td><b class="wf-name-link" (click)="editItem('floor', item)">{{ item.name }}</b></td>
                      <td><span class="status-pill status-pending">{{ item.shelfCode }}</span></td>
                      <td class="col-hd">
                        <button class="act-btn act-edit" (click)="editItem('floor', item)" title="Sửa"><i class="pi pi-pencil"></i></button>
                        <button class="act-btn act-delete" (click)="deleteItem('floor', item)" title="Xóa"><i class="pi pi-trash"></i></button>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </p-tabpanel>

            <!-- ── TAB HỘP HỒ SƠ ── -->
            <p-tabpanel value="2">
              <div class="list-toolbar">
                <div class="toolbar-left">
                  <h3 class="font-bold m-0" style="color: #002D72; font-size: 1.05rem;">Danh sách hộp/thùng hồ sơ</h3>
                </div>
                <div class="toolbar-right">
                  <button class="btn-green btn-small" (click)="showDialog('box')">
                    <i class="pi pi-plus"></i> Thêm hộp mới
                  </button>
                </div>
              </div>

              <div class="wf-table-wrap">
                <table class="wf-table">
                  <thead>
                    <tr>
                      <th style="width: 100px;">STT</th>
                      <th>Mã số hộp</th>
                      <th>Tên hộp hồ sơ</th>
                      <th>Sức chứa (tối đa bản ghi)</th>
                      <th class="col-hd">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr *ngFor="let item of boxes; let idx = index">
                      <td class="col-stt text-muted">{{ idx + 1 }}</td>
                      <td><code>{{ item.code }}</code></td>
                      <td><b class="wf-name-link" (click)="editItem('box', item)">{{ item.name }}</b></td>
                      <td>{{ item.capacity }} tài liệu</td>
                      <td class="col-hd">
                        <button class="act-btn act-edit" (click)="editItem('box', item)" title="Sửa"><i class="pi pi-pencil"></i></button>
                        <button class="act-btn act-delete" (click)="deleteItem('box', item)" title="Xóa"><i class="pi pi-trash"></i></button>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </p-tabpanel>

            <!-- ── TAB DANH MỤC ── -->
            <p-tabpanel value="3">
              <div class="list-toolbar">
                <div class="toolbar-left">
                  <h3 class="font-bold m-0" style="color: #002D72; font-size: 1.05rem;">Phân loại danh mục hồ sơ kỹ thuật</h3>
                </div>
                <div class="toolbar-right">
                  <button class="btn-green btn-small" (click)="showDialog('category')">
                    <i class="pi pi-plus"></i> Thêm danh mục mới
                  </button>
                </div>
              </div>

              <div class="wf-table-wrap">
                <table class="wf-table">
                  <thead>
                    <tr>
                      <th style="width: 100px;">STT</th>
                      <th>Mã danh mục</th>
                      <th>Tên danh mục</th>
                      <th>Mô tả chi tiết</th>
                      <th class="col-hd">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr *ngFor="let item of categories; let idx = index">
                      <td class="col-stt text-muted">{{ idx + 1 }}</td>
                      <td><code>{{ item.code }}</code></td>
                      <td><b class="wf-name-link" (click)="editItem('category', item)">{{ item.name }}</b></td>
                      <td><span class="mota-text">{{ item.description }}</span></td>
                      <td class="col-hd">
                        <button class="act-btn act-edit" (click)="editItem('category', item)" title="Sửa"><i class="pi pi-pencil"></i></button>
                        <button class="act-btn act-delete" (click)="deleteItem('category', item)" title="Xóa"><i class="pi pi-trash"></i></button>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </p-tabpanel>

          </p-tabpanels>
        </p-tabs>

      </div>
    </div>

    <!-- Dialog Thêm/Sửa Cấu hình lưu trữ -->
    <p-dialog [(visible)]="displayDialog" [header]="dialogHeader" [modal]="true" [style]="{ width: '450px' }" styleClass="evn-dialog-custom">
      <div style="display: flex; flex-direction: column; gap: 14px; padding-top: 10px;">
        
        <div class="form-group">
          <label class="form-label">Mã định danh <span class="required">*</span></label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentData.code" placeholder="Ví dụ: K03, T05, H10..." />
        </div>
        
        <div class="form-group">
          <label class="form-label">Tên gọi thực tế <span class="required">*</span></label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentData.name" placeholder="Tên kệ, tên tầng, tên hộp..." />
        </div>
        
        <!-- Shelf field -->
        <div class="form-group" *ngIf="currentType === 'shelf'">
          <label class="form-label">Vị trí kho thực tế</label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentData.location" placeholder="Ví dụ: Kho A, Tầng 2..." />
        </div>
        
        <!-- Floor field -->
        <div class="form-group" *ngIf="currentType === 'floor'">
          <label class="form-label">Thuộc mã kệ lưu trữ</label>
          <select class="wf-select w-full" [(ngModel)]="currentData.shelfCode">
            <option *ngFor="let s of shelves" [value]="s.code">{{ s.name }} ({{ s.code }})</option>
          </select>
        </div>
        
        <!-- Box field -->
        <div class="form-group" *ngIf="currentType === 'box'">
          <label class="form-label">Sức chứa tối đa (tài liệu)</label>
          <input type="number" class="wf-input w-full" [(ngModel)]="currentData.capacity" placeholder="Ví dụ: 50, 100..." />
        </div>
        
        <!-- Category field -->
        <div class="form-group" *ngIf="currentType === 'category'">
          <label class="form-label">Mô tả phân loại hồ sơ</label>
          <textarea class="wf-textarea" rows="3" [(ngModel)]="currentData.description" placeholder="Ghi chú phân loại hồ sơ..."></textarea>
        </div>
        
      </div>
      
      <ng-template #footer>
        <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayDialog = false">Hủy</button>
          <button class="btn-save btn-small" (click)="saveData()">Lưu</button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    /* Tabs custom overrides to look like modern GENCO1 */
    ::ng-deep .tab-bar-custom {
      display: flex;
      border-bottom: 2px solid #e2e8f0 !important;
      margin-bottom: 20px !important;
      background: transparent !important;
    }
    ::ng-deep .tab-item-custom {
      padding: 12px 20px !important;
      background: none !important;
      border: none !important;
      font-size: 0.88rem !important;
      color: #6b7280 !important;
      cursor: pointer !important;
      font-weight: 500 !important;
      transition: all 0.15s !important;
      border-bottom: 2px solid transparent !important;
      margin-bottom: -2px !important;
    }
    ::ng-deep .tab-item-custom:hover {
      color: #002D72 !important;
    }
    ::ng-deep .p-tab-active {
      color: #002D72 !important;
      border-bottom: 2px solid #002D72 !important;
      font-weight: 600 !important;
    }
    ::ng-deep .tab-panels-custom {
      background: transparent !important;
      padding: 0 !important;
      border: none !important;
    }
  `]
})
export class PhysicalStorageComponent {
  shelves = [
    { id: 1, code: 'K01', name: 'Kệ Hồ Sơ Bản Vẽ A1', location: 'Nhà Kho A - Khu vực Phía Bắc' },
    { id: 2, code: 'K02', name: 'Kệ Hồ Sơ Thiết Kế Trạm', location: 'Nhà Kho B - Tầng 1' }
  ];
  floors = [
    { id: 1, code: 'T01', name: 'Tầng 1 (Kệ K01)', shelfCode: 'K01' },
    { id: 2, code: 'T02', name: 'Tầng 2 (Kệ K01)', shelfCode: 'K01' }
  ];
  boxes = [
    { id: 1, code: 'H01', name: 'Hộp chứa Hồ sơ TBA Nghĩa Đô', capacity: 50 },
    { id: 2, code: 'H02', name: 'Hộp chứa Hồ sơ ĐZ Hà Đông', capacity: 30 }
  ];
  categories = [
    { id: 1, code: 'DM01', name: 'Bản vẽ thiết kế TBA', description: 'Bản vẽ kỹ thuật nhị thứ, nhất thứ của trạm' },
    { id: 2, code: 'DM02', name: 'Lý lịch thiết bị Máy biến áp', description: 'Tài liệu vận hành, biên bản thí nghiệm máy biến áp' }
  ];

  displayDialog = false;
  dialogHeader = '';
  currentType = '';
  currentData: any = {};
  isEdit = false;

  constructor(private messageService: MessageService) {}

  showDialog(type: string) {
    this.currentType = type;
    this.isEdit = false;
    this.currentData = {
      code: '',
      name: '',
      location: '',
      shelfCode: this.shelves.length > 0 ? this.shelves[0].code : '',
      capacity: 50,
      description: ''
    };
    this.dialogHeader = 'Thêm mới ' + this.getTypeName(type);
    this.displayDialog = true;
  }

  editItem(type: string, item: any) {
    this.currentType = type;
    this.isEdit = true;
    this.currentData = { ...item };
    this.dialogHeader = 'Chỉnh sửa ' + this.getTypeName(type);
    this.displayDialog = true;
  }

  deleteItem(type: string, item: any) {
    if (confirm(`Bạn có chắc chắn muốn xóa bản ghi này khỏi danh mục không?`)) {
      const arr = this.getArray(type);
      const index = arr.findIndex((x: any) => x.id === item.id);
      if (index > -1) {
        arr.splice(index, 1);
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xóa bản ghi thành công!' });
      }
    }
  }

  saveData() {
    if (!this.currentData.code || !this.currentData.name) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng điền đầy đủ Mã và Tên bắt buộc!' });
      return;
    }

    const arr = this.getArray(this.currentType);
    if (this.isEdit) {
      const index = arr.findIndex((x: any) => x.id === this.currentData.id);
      if (index > -1) {
        arr[index] = { ...this.currentData };
        this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Đã lưu thay đổi thông tin thành công!' });
      }
    } else {
      this.currentData.id = Date.now();
      arr.push({ ...this.currentData });
      this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Đã thêm mới bản ghi thành công!' });
    }
    this.displayDialog = false;
  }

  private getTypeName(type: string) {
    switch (type) {
      case 'shelf': return 'Kệ lưu trữ';
      case 'floor': return 'Tầng kệ';
      case 'box': return 'Hộp hồ sơ';
      case 'category': return 'Danh mục hồ sơ';
      default: return '';
    }
  }

  private getArray(type: string): any {
    switch (type) {
      case 'shelf': return this.shelves;
      case 'floor': return this.floors;
      case 'box': return this.boxes;
      case 'category': return this.categories;
      default: return [];
    }
  }
}
