import { Component, OnInit } from '@angular/core';
import { PhysicalStorageService, PhysicalShelfDto, PhysicalFloorDto, PhysicalBoxDto } from '../../physical-storage/physical-storage.service';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { ButtonModule } from 'primeng/button';
import { PaginatorModule } from 'primeng/paginator';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { MessageService, ConfirmationService } from 'primeng/api';
import { forkJoin, switchMap, of } from 'rxjs';

/**
 * Physical Storage Component – quản lý Kệ / Tầng / Hộp hồ sơ.
 * API thực tế: /api/physicalstorage/shelves, /floors, /boxes (qua YARP gateway)
 */
@Component({
  selector: 'app-physical-storage',
  standalone: true,
  imports: [
    CommonModule,
    TableModule,
    TabsModule,
    ButtonModule,
    PaginatorModule,
    DialogModule,
    ToastModule,
    ConfirmDialogModule,
    FormsModule,
    WfBreadcrumbComponent
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './physical-storage.component.html',
  styleUrl: './physical-storage.component.scss'
})
export class PhysicalStorageComponent implements OnInit {
  shelves: PhysicalShelfDto[] = [];
  floors: PhysicalFloorDto[] = [];
  boxes: PhysicalBoxDto[] = [];

  displayDialog = false;
  dialogHeader = '';
  currentType = '';
  currentData: any = {};
  isEdit = false;

  loading = false;
  saving = false;

  constructor(
    private physicalStorageService: PhysicalStorageService,
    private messageService: MessageService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit() {
    this.loadAllData();
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
    this.shelves = [];
    this.floors = [];
    this.boxes = [];

    this.physicalStorageService.getShelves().subscribe({
      next: (shelvesData) => {
        this.shelves = shelvesData;
        if (shelvesData.length === 0) {
          this.loading = false;
          return;
        }

        // Load tất cả floors song song cho các kệ
        const floorRequests = shelvesData.map(s =>
          this.physicalStorageService.getFloorsByShelf(s.id)
        );
        forkJoin(floorRequests).subscribe({
          next: (floorArrays) => {
            this.floors = floorArrays.flat();
            if (this.floors.length === 0) {
              this.loading = false;
              return;
            }

            // Load tất cả boxes song song cho các tầng
            const boxRequests = this.floors.map(f =>
              this.physicalStorageService.getBoxesByFloor(f.id)
            );
            forkJoin(boxRequests).subscribe({
              next: (boxArrays) => {
                this.boxes = boxArrays.flat();
                this.loading = false;
              },
              error: () => { this.loading = false; }
            });
          },
          error: () => { this.loading = false; }
        });
      },
      error: () => {
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
      description: '',
      shelfId: this.shelves.length > 0 ? this.shelves[0].id : null,
      floorId: this.floors.length > 0 ? this.floors[0].id : null,
      capacity: 50,
      status: 1
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
      message: `Bạn có chắc chắn muốn xóa bản ghi này không?`,
      header: 'Xác nhận xóa',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Đồng ý',
      rejectLabel: 'Hủy',
      acceptButtonStyleClass: 'btn-save',
      rejectButtonStyleClass: 'btn-cancel',
      accept: () => {
        let obs$;
        switch (type) {
          case 'shelf': obs$ = this.physicalStorageService.deleteShelf(item.id); break;
          case 'floor': obs$ = this.physicalStorageService.deleteFloor(item.id); break;
          case 'box':   obs$ = this.physicalStorageService.deleteBox(item.id);   break;
          default: return;
        }
        obs$.subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xóa thành công.' });
            this.loadAllData();
          },
          error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Xóa thất bại.' })
        });
      }
    });
  }

  saveData() {
    if (!this.currentData.code?.trim() || !this.currentData.name?.trim()) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng điền đầy đủ Mã và Tên bắt buộc!' });
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
    let saveObs$: import('rxjs').Observable<any> | undefined;

    if (this.isEdit) {
      switch (this.currentType) {
        case 'shelf': saveObs$ = this.physicalStorageService.updateShelf(this.currentData.id, this.currentData); break;
        case 'floor': saveObs$ = this.physicalStorageService.updateFloor(this.currentData.id, this.currentData); break;
        case 'box':   saveObs$ = this.physicalStorageService.updateBox(this.currentData.id, this.currentData);   break;
        default: return;
      }
    } else {
      switch (this.currentType) {
        case 'shelf': saveObs$ = this.physicalStorageService.createShelf(this.currentData); break;
        case 'floor': saveObs$ = this.physicalStorageService.createFloor(this.currentData); break;
        case 'box':   saveObs$ = this.physicalStorageService.createBox(this.currentData);   break;
        default: return;
      }
    }

    saveObs$?.subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu thông tin thành công!' });
        this.loadAllData();
        this.displayDialog = false;
        this.saving = false;
      },
      error: () => {
        this.saving = false;
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Lưu thông tin thất bại.' });
      }
    });
  }

  private getTypeName(type: string): string {
    switch (type) {
      case 'shelf': return 'Kệ lưu trữ';
      case 'floor': return 'Tầng kệ';
      case 'box':   return 'Hộp hồ sơ';
      default: return '';
    }
  }
}
