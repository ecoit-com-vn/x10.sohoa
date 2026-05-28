import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TabsModule } from 'primeng/tabs';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-physical-storage',
  standalone: true,
  imports: [
    CommonModule,
    TabsModule,
    TableModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    FormsModule
  ],
  template: `
    <div class="card">
      <p-tabs value="0">
        <p-tablist>
            <p-tab value="0">Kệ (Shelf)</p-tab>
            <p-tab value="1">Tầng (Floor)</p-tab>
            <p-tab value="2">Hộp (Box)</p-tab>
            <p-tab value="3">Danh mục (Category)</p-tab>
        </p-tablist>

        <p-tabpanels>
            <!-- Tầng / Kệ / Hộp / Danh mục logic can be repeated -->
            <!-- Tab Kệ -->
            <p-tabpanel value="0">
                <div class="flex justify-between mb-4 items-center">
                    <h2 class="text-xl font-bold">Quản lý Kệ</h2>
                    <p-button label="Thêm Kệ" icon="pi pi-plus" (onClick)="showDialog('shelf')"></p-button>
                </div>
                <p-table [value]="shelves" [tableStyle]="{ 'min-width': '50rem' }">
                    <ng-template #header>
                        <tr>
                            <th>Mã Kệ</th>
                            <th>Tên Kệ</th>
                            <th>Vị trí</th>
                            <th>Thao tác</th>
                        </tr>
                    </ng-template>
                    <ng-template #body let-item>
                        <tr>
                            <td>{{ item.code }}</td>
                            <td>{{ item.name }}</td>
                            <td>{{ item.location }}</td>
                            <td>
                                <p-button icon="pi pi-pencil" class="mr-2" severity="info" size="small" (onClick)="editItem('shelf', item)"></p-button>
                                <p-button icon="pi pi-trash" severity="danger" size="small" (onClick)="deleteItem('shelf', item)"></p-button>
                            </td>
                        </tr>
                    </ng-template>
                </p-table>
            </p-tabpanel>

            <!-- Tab Tầng -->
            <p-tabpanel value="1">
                <div class="flex justify-between mb-4 items-center">
                    <h2 class="text-xl font-bold">Quản lý Tầng</h2>
                    <p-button label="Thêm Tầng" icon="pi pi-plus" (onClick)="showDialog('floor')"></p-button>
                </div>
                <p-table [value]="floors" [tableStyle]="{ 'min-width': '50rem' }">
                    <ng-template #header>
                        <tr>
                            <th>Mã Tầng</th>
                            <th>Tên Tầng</th>
                            <th>Thuộc Kệ</th>
                            <th>Thao tác</th>
                        </tr>
                    </ng-template>
                    <ng-template #body let-item>
                        <tr>
                            <td>{{ item.code }}</td>
                            <td>{{ item.name }}</td>
                            <td>{{ item.shelfCode }}</td>
                            <td>
                                <p-button icon="pi pi-pencil" class="mr-2" severity="info" size="small" (onClick)="editItem('floor', item)"></p-button>
                                <p-button icon="pi pi-trash" severity="danger" size="small" (onClick)="deleteItem('floor', item)"></p-button>
                            </td>
                        </tr>
                    </ng-template>
                </p-table>
            </p-tabpanel>

            <!-- Tab Hộp -->
            <p-tabpanel value="2">
                <div class="flex justify-between mb-4 items-center">
                    <h2 class="text-xl font-bold">Quản lý Hộp</h2>
                    <p-button label="Thêm Hộp" icon="pi pi-plus" (onClick)="showDialog('box')"></p-button>
                </div>
                <p-table [value]="boxes" [tableStyle]="{ 'min-width': '50rem' }">
                    <ng-template #header>
                        <tr>
                            <th>Mã Hộp</th>
                            <th>Tên Hộp</th>
                            <th>Sức chứa</th>
                            <th>Thao tác</th>
                        </tr>
                    </ng-template>
                    <ng-template #body let-item>
                        <tr>
                            <td>{{ item.code }}</td>
                            <td>{{ item.name }}</td>
                            <td>{{ item.capacity }}</td>
                            <td>
                                <p-button icon="pi pi-pencil" class="mr-2" severity="info" size="small" (onClick)="editItem('box', item)"></p-button>
                                <p-button icon="pi pi-trash" severity="danger" size="small" (onClick)="deleteItem('box', item)"></p-button>
                            </td>
                        </tr>
                    </ng-template>
                </p-table>
            </p-tabpanel>

            <!-- Tab Danh mục -->
            <p-tabpanel value="3">
                <div class="flex justify-between mb-4 items-center">
                    <h2 class="text-xl font-bold">Quản lý Danh mục (Hồ sơ)</h2>
                    <p-button label="Thêm Danh mục" icon="pi pi-plus" (onClick)="showDialog('category')"></p-button>
                </div>
                <p-table [value]="categories" [tableStyle]="{ 'min-width': '50rem' }">
                    <ng-template #header>
                        <tr>
                            <th>Mã DM</th>
                            <th>Tên DM</th>
                            <th>Mô tả</th>
                            <th>Thao tác</th>
                        </tr>
                    </ng-template>
                    <ng-template #body let-item>
                        <tr>
                            <td>{{ item.code }}</td>
                            <td>{{ item.name }}</td>
                            <td>{{ item.description }}</td>
                            <td>
                                <p-button icon="pi pi-pencil" class="mr-2" severity="info" size="small" (onClick)="editItem('category', item)"></p-button>
                                <p-button icon="pi pi-trash" severity="danger" size="small" (onClick)="deleteItem('category', item)"></p-button>
                            </td>
                        </tr>
                    </ng-template>
                </p-table>
            </p-tabpanel>
        </p-tabpanels>
      </p-tabs>
    </div>

    <!-- Generic Dialog for add/edit -->
    <p-dialog [(visible)]="displayDialog" [header]="dialogHeader" [modal]="true" [style]="{ width: '400px' }">
        <div class="flex flex-col gap-4 mt-2">
            <div class="flex flex-col gap-2">
                <label>Mã</label>
                <input pInputText [(ngModel)]="currentData.code" />
            </div>
            <div class="flex flex-col gap-2">
                <label>Tên</label>
                <input pInputText [(ngModel)]="currentData.name" />
            </div>
            <div class="flex flex-col gap-2" *ngIf="currentType === 'shelf'">
                <label>Vị trí</label>
                <input pInputText [(ngModel)]="currentData.location" />
            </div>
            <div class="flex flex-col gap-2" *ngIf="currentType === 'floor'">
                <label>Thuộc Kệ (Mã)</label>
                <input pInputText [(ngModel)]="currentData.shelfCode" />
            </div>
            <div class="flex flex-col gap-2" *ngIf="currentType === 'box'">
                <label>Sức chứa</label>
                <input pInputText [(ngModel)]="currentData.capacity" />
            </div>
            <div class="flex flex-col gap-2" *ngIf="currentType === 'category'">
                <label>Mô tả</label>
                <input pInputText [(ngModel)]="currentData.description" />
            </div>
        </div>
        <ng-template #footer>
            <p-button label="Hủy" icon="pi pi-times" [text]="true" severity="secondary" (onClick)="displayDialog = false"></p-button>
            <p-button label="Lưu" icon="pi pi-check" (onClick)="saveData()"></p-button>
        </ng-template>
    </p-dialog>
  `
})
export class PhysicalStorageComponent {
  shelves = [
    { id: 1, code: 'K01', name: 'Kệ Hồ Sơ 1', location: 'Kho A' },
    { id: 2, code: 'K02', name: 'Kệ Hồ Sơ 2', location: 'Kho B' }
  ];
  floors = [
    { id: 1, code: 'T01', name: 'Tầng 1', shelfCode: 'K01' },
    { id: 2, code: 'T02', name: 'Tầng 2', shelfCode: 'K01' }
  ];
  boxes = [
    { id: 1, code: 'H01', name: 'Hộp 1', capacity: '50' },
    { id: 2, code: 'H02', name: 'Hộp 2', capacity: '50' }
  ];
  categories = [
    { id: 1, code: 'DM01', name: 'Hồ sơ nhân sự', description: 'HSNS' },
    { id: 2, code: 'DM02', name: 'Hồ sơ kế toán', description: 'HSKT' }
  ];

  displayDialog = false;
  dialogHeader = '';
  currentType = '';
  currentData: any = {};
  isEdit = false;

  showDialog(type: string) {
    this.currentType = type;
    this.isEdit = false;
    this.currentData = {};
    this.dialogHeader = 'Thêm ' + this.getTypeName(type);
    this.displayDialog = true;
  }

  editItem(type: string, item: any) {
    this.currentType = type;
    this.isEdit = true;
    this.currentData = { ...item };
    this.dialogHeader = 'Sửa ' + this.getTypeName(type);
    this.displayDialog = true;
  }

  deleteItem(type: string, item: any) {
    const arr = this.getArray(type);
    const index = arr.findIndex((x: any) => x.id === item.id);
    if (index > -1) {
      arr.splice(index, 1);
    }
  }

  saveData() {
    const arr = this.getArray(this.currentType);
    if (this.isEdit) {
      const index = arr.findIndex((x: any) => x.id === this.currentData.id);
      if (index > -1) {
        arr[index] = { ...this.currentData };
      }
    } else {
      this.currentData.id = Date.now();
      arr.push({ ...this.currentData });
    }
    this.displayDialog = false;
  }

  private getTypeName(type: string) {
    switch (type) {
      case 'shelf': return 'Kệ';
      case 'floor': return 'Tầng';
      case 'box': return 'Hộp';
      case 'category': return 'Danh mục';
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
