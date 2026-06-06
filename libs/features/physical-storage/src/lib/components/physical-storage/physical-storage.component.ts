import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TabsModule } from 'primeng/tabs';
import { DialogModule } from 'primeng/dialog';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService, ConfirmationService } from 'primeng/api';
import { HttpClient } from '@angular/common/http';
import { environment } from '@env/environment';

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
  templateUrl: './physical-storage.component.html',
  styleUrl: './physical-storage.component.scss'
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

  private apiUrl = `${environment.apiGatewayUrl}/api/physicalstorage`;

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
