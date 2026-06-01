import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TabsModule } from 'primeng/tabs';
import { DialogModule } from 'primeng/dialog';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService, ConfirmationService } from 'primeng/api';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

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
                      <th>Thuộc danh mục</th>
                      <th>Vị trí kho thực tế</th>
                      <th class="col-hd">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    <!-- skeleton loading rows -->
                    <tr *ngIf="loading">
                      <td colspan="6" class="empty-row" style="padding: 0;">
                        <div *ngFor="let item of [1,2,3]" style="display: flex; gap: 10px; padding: 12px; border-bottom: 1px solid #f1f5f9;">
                          <div class="skeleton-shimmer" style="height: 16px; flex: 1; border-radius: 4px;"></div>
                          <div class="skeleton-shimmer" style="height: 16px; flex: 2; border-radius: 4px;"></div>
                          <div class="skeleton-shimmer" style="height: 16px; flex: 1.5; border-radius: 4px;"></div>
                        </div>
                      </td>
                    </tr>

                    <ng-container *ngIf="!loading">
                      <tr *ngFor="let item of shelves; let idx = index">
                        <td class="col-stt text-muted">{{ idx + 1 }}</td>
                        <td><code>{{ item.code }}</code></td>
                        <td><b class="wf-name-link" (click)="editItem('shelf', item)">{{ item.name }}</b></td>
                        <td><span class="status-pill status-active">{{ getFondName(item.fondsId) }}</span></td>
                        <td><span class="mota-text">{{ item.location }}</span></td>
                        <td class="col-hd">
                          <button class="act-btn act-edit" (click)="editItem('shelf', item)" title="Sửa"><i class="pi pi-pencil"></i></button>
                          <button class="act-btn act-delete" (click)="deleteItem('shelf', item)" title="Xóa"><i class="pi pi-trash"></i></button>
                        </td>
                      </tr>
                      <tr *ngIf="shelves.length === 0">
                        <td colspan="6" class="empty-row">
                          <i class="pi pi-inbox"></i>
                          <div>Không có dữ liệu kệ lưu trữ.</div>
                        </td>
                      </tr>
                    </ng-container>
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
                      <th>Thuộc kệ</th>
                      <th>Mô tả</th>
                      <th class="col-hd">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    <!-- skeleton loading rows -->
                    <tr *ngIf="loading">
                      <td colspan="6" class="empty-row" style="padding: 0;">
                        <div *ngFor="let item of [1,2,3]" style="display: flex; gap: 10px; padding: 12px; border-bottom: 1px solid #f1f5f9;">
                          <div class="skeleton-shimmer" style="height: 16px; flex: 1; border-radius: 4px;"></div>
                          <div class="skeleton-shimmer" style="height: 16px; flex: 2; border-radius: 4px;"></div>
                          <div class="skeleton-shimmer" style="height: 16px; flex: 1.5; border-radius: 4px;"></div>
                        </div>
                      </td>
                    </tr>

                    <ng-container *ngIf="!loading">
                      <tr *ngFor="let item of floors; let idx = index">
                        <td class="col-stt text-muted">{{ idx + 1 }}</td>
                        <td><code>{{ item.code }}</code></td>
                        <td><b class="wf-name-link" (click)="editItem('floor', item)">{{ item.name }}</b></td>
                        <td><span class="status-pill status-pending">{{ getShelfName(item.shelfId) }}</span></td>
                        <td><span class="text-muted">{{ item.description }}</span></td>
                        <td class="col-hd">
                          <button class="act-btn act-edit" (click)="editItem('floor', item)" title="Sửa"><i class="pi pi-pencil"></i></button>
                          <button class="act-btn act-delete" (click)="deleteItem('floor', item)" title="Xóa"><i class="pi pi-trash"></i></button>
                        </td>
                      </tr>
                      <tr *ngIf="floors.length === 0">
                        <td colspan="6" class="empty-row">
                          <i class="pi pi-inbox"></i>
                          <div>Không có dữ liệu tầng kệ.</div>
                        </td>
                      </tr>
                    </ng-container>
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
                      <th>Thuộc tầng kệ</th>
                      <th>Sức chứa (tối đa bản ghi)</th>
                      <th class="col-hd">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    <!-- skeleton loading rows -->
                    <tr *ngIf="loading">
                      <td colspan="6" class="empty-row" style="padding: 0;">
                        <div *ngFor="let item of [1,2,3]" style="display: flex; gap: 10px; padding: 12px; border-bottom: 1px solid #f1f5f9;">
                          <div class="skeleton-shimmer" style="height: 16px; flex: 1; border-radius: 4px;"></div>
                          <div class="skeleton-shimmer" style="height: 16px; flex: 2; border-radius: 4px;"></div>
                          <div class="skeleton-shimmer" style="height: 16px; flex: 1.5; border-radius: 4px;"></div>
                        </div>
                      </td>
                    </tr>

                    <ng-container *ngIf="!loading">
                      <tr *ngFor="let item of boxes; let idx = index">
                        <td class="col-stt text-muted">{{ idx + 1 }}</td>
                        <td><code>{{ item.code }}</code></td>
                        <td><b class="wf-name-link" (click)="editItem('box', item)">{{ item.name }}</b></td>
                        <td><span class="status-pill status-active">{{ getFloorName(item.floorId) }}</span></td>
                        <td>{{ item.capacity }} tài liệu</td>
                        <td class="col-hd">
                          <button class="act-btn act-edit" (click)="editItem('box', item)" title="Sửa"><i class="pi pi-pencil"></i></button>
                          <button class="act-btn act-delete" (click)="deleteItem('box', item)" title="Xóa"><i class="pi pi-trash"></i></button>
                        </td>
                      </tr>
                      <tr *ngIf="boxes.length === 0">
                        <td colspan="6" class="empty-row">
                          <i class="pi pi-inbox"></i>
                          <div>Không có dữ liệu hộp hồ sơ.</div>
                        </td>
                      </tr>
                    </ng-container>
                  </tbody>
                </table>
              </div>
            </p-tabpanel>

            <!-- ── TAB DANH MỤC ── -->
            <p-tabpanel value="3">
              <div class="list-toolbar">
                <div class="toolbar-left">
                  <h3 class="font-bold m-0" style="color: #002D72; font-size: 1.05rem;">Phân loại danh mục hồ sơ kỹ thuật (Fonds)</h3>
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
                    <!-- skeleton loading rows -->
                    <tr *ngIf="loading">
                      <td colspan="5" class="empty-row" style="padding: 0;">
                        <div *ngFor="let item of [1,2,3]" style="display: flex; gap: 10px; padding: 12px; border-bottom: 1px solid #f1f5f9;">
                          <div class="skeleton-shimmer" style="height: 16px; flex: 1; border-radius: 4px;"></div>
                          <div class="skeleton-shimmer" style="height: 16px; flex: 2; border-radius: 4px;"></div>
                          <div class="skeleton-shimmer" style="height: 16px; flex: 1.5; border-radius: 4px;"></div>
                        </div>
                      </td>
                    </tr>

                    <ng-container *ngIf="!loading">
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
                      <tr *ngIf="categories.length === 0">
                        <td colspan="5" class="empty-row">
                          <i class="pi pi-inbox"></i>
                          <div>Không có dữ liệu danh mục.</div>
                        </td>
                      </tr>
                    </ng-container>
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
          <input type="text" class="wf-input w-full" [(ngModel)]="currentData.code" placeholder="Ví dụ: K03, T05, H10..." [disabled]="saving" />
        </div>
        
        <div class="form-group">
          <label class="form-label">Tên gọi thực tế <span class="required">*</span></label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentData.name" placeholder="Tên kệ, tên tầng, tên hộp..." [disabled]="saving" />
        </div>
        
        <!-- Shelf field -->
        <div class="form-group" *ngIf="currentType === 'shelf'">
          <label class="form-label">Thuộc danh mục (Fonds) <span class="required">*</span></label>
          <select class="wf-select w-full" [(ngModel)]="currentData.fondsId" [disabled]="saving">
            <option *ngFor="let c of categories" [value]="c.id">{{ c.name }} ({{ c.code }})</option>
          </select>
          <label class="form-label mt-2">Vị trí kho thực tế</label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentData.location" placeholder="Ví dụ: Kho A, Tầng 2..." [disabled]="saving" />
        </div>
        
        <!-- Floor field -->
        <div class="form-group" *ngIf="currentType === 'floor'">
          <label class="form-label">Thuộc kệ lưu trữ <span class="required">*</span></label>
          <select class="wf-select w-full" [(ngModel)]="currentData.shelfId" [disabled]="saving">
            <option *ngFor="let s of shelves" [value]="s.id">{{ s.name }} ({{ s.code }})</option>
          </select>
          <label class="form-label mt-2">Mô tả / Vị trí</label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentData.description" placeholder="Vị trí trong kệ..." [disabled]="saving" />
        </div>
        
        <!-- Box field -->
        <div class="form-group" *ngIf="currentType === 'box'">
          <label class="form-label">Thuộc tầng kệ <span class="required">*</span></label>
          <select class="wf-select w-full" [(ngModel)]="currentData.floorId" [disabled]="saving">
            <option *ngFor="let f of floors" [value]="f.id">{{ f.name }} ({{ f.code }})</option>
          </select>
          <label class="form-label mt-2">Sức chứa tối đa (tài liệu)</label>
          <input type="number" class="wf-input w-full" [(ngModel)]="currentData.capacity" placeholder="Ví dụ: 50, 100..." [disabled]="saving" />
        </div>
        
        <!-- Category field -->
        <div class="form-group" *ngIf="currentType === 'category'">
          <label class="form-label">Mô tả phân loại hồ sơ</label>
          <textarea class="wf-textarea" rows="3" [(ngModel)]="currentData.description" placeholder="Ghi chú phân loại hồ sơ..." [disabled]="saving"></textarea>
        </div>
        
      </div>
      
      <ng-template #footer>
        <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayDialog = false" [disabled]="saving">Hủy</button>
          <button class="btn-save btn-small" (click)="saveData()" [disabled]="saving">
            <i class="pi pi-spin pi-spinner" *ngIf="saving" style="margin-right: 4px;"></i>
            Lưu
          </button>
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
    @keyframes shimmer {
      0% { background-position: -200% 0; }
      100% { background-position: 200% 0; }
    }
    .skeleton-shimmer {
      background: linear-gradient(90deg, #f3f4f6 25%, #e5e7eb 50%, #f3f4f6 75%);
      background-size: 200% 100%;
      animation: shimmer 1.5s infinite;
      width: 100%;
    }
    :global(html.dark-mode) .skeleton-shimmer {
      background: linear-gradient(90deg, #1e293b 25%, #334155 50%, #1e293b 75%);
      background-size: 200% 100%;
    }
  `]
})
export class PhysicalStorageComponent implements OnInit {
  shelves: any[] = [];
  floors: any[] = [];
  boxes: any[] = [];
  categories: any[] = [];

  displayDialog = false;
  dialogHeader = '';
  currentType = '';
  currentData: any = {};
  isEdit = false;
  
  loading = false;
  saving = false;

  private apiUrl = `${environment.apiGatewayUrl}/api/PhysicalStorage`;

  constructor(
    private http: HttpClient,
    private messageService: MessageService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit() {
    this.loadAllData();
  }

  getFondName(id: number): string {
    const fond = this.categories.find(c => c.id === id);
    return fond ? fond.name : `Danh mục #${id}`;
  }

  getShelfName(id: number): string {
    const shelf = this.shelves.find(s => s.id === id);
    return shelf ? shelf.name : `Kệ #${id}`;
  }

  getFloorName(id: number): string {
    const floor = this.floors.find(f => f.id === id);
    return floor ? floor.name : `Tầng #${id}`;
  }

  loadAllData() {
    this.loading = true;
    this.http.get<any[]>(`${this.apiUrl}/fonds`).subscribe({
      next: (fondsData) => {
        this.categories = fondsData;
        this.shelves = [];
        this.floors = [];
        this.boxes = [];

        if (fondsData.length === 0) {
          this.loading = false;
          return;
        }

        let loadedShelvesCount = 0;
        fondsData.forEach(fond => {
          this.http.get<any[]>(`${this.apiUrl}/fonds/${fond.id}/shelves`).subscribe({
            next: (shelvesData) => {
              shelvesData.forEach(s => {
                s.location = s.description;
              });
              this.shelves = [...this.shelves, ...shelvesData];
              loadedShelvesCount++;

              if (loadedShelvesCount === fondsData.length) {
                if (this.shelves.length === 0) {
                  this.loading = false;
                  return;
                }
                let loadedFloorsCount = 0;
                const currentShelves = [...this.shelves];
                currentShelves.forEach(shelf => {
                  this.http.get<any[]>(`${this.apiUrl}/shelves/${shelf.id}/floors`).subscribe({
                    next: (floorsData) => {
                      this.floors = [...this.floors, ...floorsData];
                      loadedFloorsCount++;

                      if (loadedFloorsCount === currentShelves.length) {
                        if (this.floors.length === 0) {
                          this.loading = false;
                          return;
                        }
                        const currentFloors = [...this.floors];
                        let loadedBoxesCount = 0;
                        currentFloors.forEach(floor => {
                          this.http.get<any[]>(`${this.apiUrl}/floors/${floor.id}/boxes`).subscribe({
                            next: (boxesData) => {
                              boxesData.forEach(b => {
                                b.capacity = b.description ? parseInt(b.description) || 50 : 50;
                              });
                              this.boxes = [...this.boxes, ...boxesData];
                              loadedBoxesCount++;
                              if (loadedBoxesCount === currentFloors.length) {
                                this.loading = false;
                              }
                            },
                            error: (err) => {
                              console.error(err);
                              loadedBoxesCount++;
                              if (loadedBoxesCount === currentFloors.length) {
                                this.loading = false;
                              }
                            }
                          });
                        });
                      }
                    },
                    error: (err) => {
                      console.error(err);
                      loadedFloorsCount++;
                      if (loadedFloorsCount === currentShelves.length) {
                        this.loading = false;
                      }
                    }
                  });
                });
              }
            },
            error: (err) => {
              console.error(err);
              loadedShelvesCount++;
              if (loadedShelvesCount === fondsData.length) {
                this.loading = false;
              }
            }
          });
        });
      },
      error: (err) => {
        this.loading = false;
        this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải sơ đồ lưu trữ.' });
      }
    });
  }

  showDialog(type: string) {
    this.currentType = type;
    this.isEdit = false;
    this.currentData = {
      code: '',
      name: '',
      location: '',
      description: '',
      fondsId: this.categories.length > 0 ? this.categories[0].id : null,
      shelfId: this.shelves.length > 0 ? this.shelves[0].id : null,
      floorId: this.floors.length > 0 ? this.floors[0].id : null,
      capacity: 50
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
    this.confirmationService.confirm({
      message: `Bạn có chắc chắn muốn xóa bản ghi này khỏi danh mục không?`,
      header: 'Xác nhận xóa',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Đồng ý',
      rejectLabel: 'Hủy',
      accept: () => {
        let deleteUrl = '';
        switch (type) {
          case 'category': deleteUrl = `${this.apiUrl}/fonds/${item.id}`; break;
          case 'shelf': deleteUrl = `${this.apiUrl}/shelves/${item.id}`; break;
          case 'floor': deleteUrl = `${this.apiUrl}/floors/${item.id}`; break;
          case 'box': deleteUrl = `${this.apiUrl}/boxes/${item.id}`; break;
        }
        this.http.delete(deleteUrl).subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xóa bản ghi thành công!' });
            this.loadAllData();
          },
          error: (err) => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Xóa bản ghi thất bại.' });
          }
        });
      }
    });
  }

  saveData() {
    if (!this.currentData.code || !this.currentData.name) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng điền đầy đủ Mã và Tên bắt buộc!' });
      return;
    }

    if (this.currentType === 'shelf' && !this.currentData.fondsId) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng chọn danh mục quản lý.' });
      return;
    }
    if (this.currentType === 'floor' && !this.currentData.shelfId) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng chọn kệ lưu trữ.' });
      return;
    }
    if (this.currentType === 'box' && !this.currentData.floorId) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng chọn tầng kệ.' });
      return;
    }

    this.saving = true;
    let saveObs;
    if (this.isEdit) {
      switch (this.currentType) {
        case 'category':
          saveObs = this.http.put(`${this.apiUrl}/fonds/${this.currentData.id}`, this.currentData);
          break;
        case 'shelf':
          this.currentData.description = this.currentData.location;
          saveObs = this.http.put(`${this.apiUrl}/shelves/${this.currentData.id}`, this.currentData);
          break;
        case 'floor':
          saveObs = this.http.put(`${this.apiUrl}/floors/${this.currentData.id}`, this.currentData);
          break;
        case 'box':
          this.currentData.description = String(this.currentData.capacity);
          saveObs = this.http.put(`${this.apiUrl}/boxes/${this.currentData.id}`, this.currentData);
          break;
      }
    } else {
      switch (this.currentType) {
        case 'category':
          saveObs = this.http.post(`${this.apiUrl}/fonds`, this.currentData);
          break;
        case 'shelf':
          this.currentData.description = this.currentData.location;
          saveObs = this.http.post(`${this.apiUrl}/shelves`, this.currentData);
          break;
        case 'floor':
          saveObs = this.http.post(`${this.apiUrl}/floors`, this.currentData);
          break;
        case 'box':
          this.currentData.description = String(this.currentData.capacity);
          saveObs = this.http.post(`${this.apiUrl}/boxes`, this.currentData);
          break;
      }
    }

    saveObs?.subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu thay đổi thông tin thành công!' });
        this.loadAllData();
        this.displayDialog = false;
        this.saving = false;
      },
      error: (err) => {
        this.saving = false;
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Lưu thông tin thất bại.' });
      }
    });
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
}
