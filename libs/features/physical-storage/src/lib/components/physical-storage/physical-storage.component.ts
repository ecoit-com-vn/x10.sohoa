import { Component, OnInit, signal, computed, inject } from '@angular/core';
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
import { AuthService } from '@sohoa.frontend/shared/core';
import { MessageService, ConfirmationService } from 'primeng/api';
import { forkJoin, of, finalize, catchError } from 'rxjs';

/**
 * Physical Storage – Kệ / Tầng / Hộp.
 * Load theo đơn vị JWT: kệ theo unit → tầng theo kệ unit → hộp theo tầng.
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
  private readonly authService = inject(AuthService);
  private readonly storageCodePattern = /^[A-Za-z0-9_-]{1,50}$/;
  private readonly storageCodeErrorMessage =
    'Mã chỉ được nhập chữ cái không dấu, số, dấu gạch ngang (-), dấu gạch dưới (_), không được có dấu cách.';

  shelves = signal<PhysicalShelfDto[]>([]);
  floors = signal<PhysicalFloorDto[]>([]);
  boxes = signal<PhysicalBoxDto[]>([]);

  /** Đơn vị hiện tại từ JWT (null = admin / không filter phía FE). */
  currentUnitId = signal<number | null>(null);

  organizationUnits = signal<any[]>([]);
  orgUnitTree = computed(() => this.buildOrgTree(this.organizationUnits()));
  formOrgTreeOpen = signal(false);
  expandedFormUnitNodes = signal<Set<number>>(new Set());

  displayDialog = signal(false);
  dialogHeader = signal('');
  currentType = signal('');
  currentData = signal<any>({});
  codeValidationError = signal('');
  isEdit = signal(false);

  loading = signal(false);
  saving = signal(false);

  // ── Bộ lọc + phân trang tab Kệ ──
  shelfSearchKeyword = signal('');
  shelfPage = signal(1);
  shelfPageSize = signal(10);

  // ── Bộ lọc + phân trang tab Tầng ──
  floorSearchKeyword = signal('');
  floorShelfFilterId = signal<number | null>(null);
  floorPage = signal(1);
  floorPageSize = signal(10);

  // ── Bộ lọc + phân trang tab Hộp ──
  boxSearchKeyword = signal('');
  boxFloorFilterId = signal<number | null>(null);
  boxPage = signal(1);
  boxPageSize = signal(10);

  filteredShelves = computed(() => {
    const kw = this.shelfSearchKeyword().trim().toLowerCase();
    if (!kw) return this.shelves();
    return this.shelves().filter(s =>
      (s.code || '').toLowerCase().includes(kw) ||
      (s.name || '').toLowerCase().includes(kw)
    );
  });

  filteredFloors = computed(() => {
    const kw = this.floorSearchKeyword().trim().toLowerCase();
    const shelfId = this.floorShelfFilterId();
    return this.floors().filter(f => {
      if (shelfId != null && f.shelfId !== shelfId) return false;
      if (!kw) return true;
      return (f.code || '').toLowerCase().includes(kw) ||
        (f.name || '').toLowerCase().includes(kw);
    });
  });

  filteredBoxes = computed(() => {
    const kw = this.boxSearchKeyword().trim().toLowerCase();
    const floorId = this.boxFloorFilterId();
    return this.boxes().filter(b => {
      if (floorId != null && b.floorId !== floorId) return false;
      if (!kw) return true;
      return (b.code || '').toLowerCase().includes(kw) ||
        (b.name || '').toLowerCase().includes(kw);
    });
  });

  pagedShelves = computed(() => this.slicePage(this.filteredShelves(), this.shelfPage(), this.shelfPageSize()));
  pagedFloors = computed(() => this.slicePage(this.filteredFloors(), this.floorPage(), this.floorPageSize()));
  pagedBoxes = computed(() => this.slicePage(this.filteredBoxes(), this.boxPage(), this.boxPageSize()));

  shelfTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredShelves().length / this.shelfPageSize())));
  floorTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredFloors().length / this.floorPageSize())));
  boxTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredBoxes().length / this.boxPageSize())));

  constructor(
    private physicalStorageService: PhysicalStorageService,
    private messageService: MessageService,
    private confirmationService: ConfirmationService
  ) {
    if (typeof window !== 'undefined') {
      window.addEventListener('click', () => this.formOrgTreeOpen.set(false));
    }
  }

  ngOnInit() {
    this.currentUnitId.set(this.authService.getUserUnitId());
    this.loadOrganizationUnits();
    this.loadAllData();
  }

  getShelfName(id: number): string {
    const shelf = this.shelves().find(s => s.id === id);
    return shelf ? shelf.name : `Kệ #${id}`;
  }

  getFloorName(id: number): string {
    const floor = this.floors().find(f => f.id === id);
    return floor ? floor.name : `Tầng #${id}`;
  }

  getUnitLabel(unitId: any): string {
    if (!unitId) return '';
    const fromApi = this.shelves().find(s => s.unitId == unitId)?.unitName;
    if (fromApi) return fromApi;
    const u = this.organizationUnits().find(x => x.id == unitId);
    return u ? u.name : `Đơn vị #${unitId}`;
  }

  buildOrgTree(units: any[]): any[] {
    const map = new Map<number, any>();
    const roots: any[] = [];
    (units || []).forEach(u => map.set(u.id, { ...u, children: [] }));
    map.forEach(node => {
      if (node.parentId && map.has(node.parentId)) {
        map.get(node.parentId)!.children.push(node);
      } else {
        roots.push(node);
      }
    });
    return roots;
  }

  loadOrganizationUnits() {
    this.physicalStorageService.getOrganizationUnits().pipe(
      catchError(() => of([]))
    ).subscribe(data => {
      this.organizationUnits.set(Array.isArray(data) ? data : []);
    });
  }

  toggleFormOrgTree(event?: Event) {
    if (event) event.stopPropagation();
    if (this.organizationUnits().length === 0) {
      this.physicalStorageService.getOrganizationUnits().pipe(
        catchError(() => of([]))
      ).subscribe(data => {
        this.organizationUnits.set(Array.isArray(data) ? data : []);
        this.formOrgTreeOpen.update(v => !v);
      });
    } else {
      this.formOrgTreeOpen.update(v => !v);
    }
  }

  toggleFormUnitNode(unitId: number, event?: Event) {
    if (event) event.stopPropagation();
    const current = new Set(this.expandedFormUnitNodes());
    if (current.has(unitId)) {
      current.delete(unitId);
    } else {
      current.add(unitId);
    }
    this.expandedFormUnitNodes.set(current);
  }

  isFormNodeExpanded(unitId: number): boolean {
    return this.expandedFormUnitNodes().has(unitId);
  }

  selectFormOrgUnit(unitId: number) {
    this.currentData.update(d => ({ ...d, unitId }));
    this.formOrgTreeOpen.set(false);
  }

  private slicePage<T>(items: T[], page: number, pageSize: number): T[] {
    const start = (page - 1) * pageSize;
    return items.slice(start, start + pageSize);
  }

  onShelfSearch(value: string) {
    this.shelfSearchKeyword.set(value);
    this.shelfPage.set(1);
  }

  onFloorSearch(value: string) {
    this.floorSearchKeyword.set(value);
    this.floorPage.set(1);
  }

  onFloorShelfFilter(value: number | null) {
    this.floorShelfFilterId.set(value);
    this.floorPage.set(1);
  }

  onBoxSearch(value: string) {
    this.boxSearchKeyword.set(value);
    this.boxPage.set(1);
  }

  onBoxFloorFilter(value: number | null) {
    this.boxFloorFilterId.set(value);
    this.boxPage.set(1);
  }

  prevShelfPage() {
    if (this.shelfPage() > 1) this.shelfPage.update(p => p - 1);
  }

  nextShelfPage() {
    if (this.shelfPage() < this.shelfTotalPages()) this.shelfPage.update(p => p + 1);
  }

  goToShelfPage(page: any) {
    const p = Number(page);
    if (p >= 1 && p <= this.shelfTotalPages()) this.shelfPage.set(p);
  }

  onShelfPageSizeChange(event: Event) {
    this.shelfPageSize.set(Number((event.target as HTMLSelectElement).value));
    this.shelfPage.set(1);
  }

  prevFloorPage() {
    if (this.floorPage() > 1) this.floorPage.update(p => p - 1);
  }

  nextFloorPage() {
    if (this.floorPage() < this.floorTotalPages()) this.floorPage.update(p => p + 1);
  }

  goToFloorPage(page: any) {
    const p = Number(page);
    if (p >= 1 && p <= this.floorTotalPages()) this.floorPage.set(p);
  }

  onFloorPageSizeChange(event: Event) {
    this.floorPageSize.set(Number((event.target as HTMLSelectElement).value));
    this.floorPage.set(1);
  }

  prevBoxPage() {
    if (this.boxPage() > 1) this.boxPage.update(p => p - 1);
  }

  nextBoxPage() {
    if (this.boxPage() < this.boxTotalPages()) this.boxPage.update(p => p + 1);
  }

  goToBoxPage(page: any) {
    const p = Number(page);
    if (p >= 1 && p <= this.boxTotalPages()) this.boxPage.set(p);
  }

  onBoxPageSizeChange(event: Event) {
    this.boxPageSize.set(Number((event.target as HTMLSelectElement).value));
    this.boxPage.set(1);
  }

  loadAllData() {
    this.loading.set(true);
    this.shelves.set([]);
    this.floors.set([]);
    this.boxes.set([]);

    const unitId = this.currentUnitId();

    forkJoin({
      shelves: this.physicalStorageService.getShelves(unitId).pipe(catchError(() => of([] as PhysicalShelfDto[]))),
      floors: this.physicalStorageService.getFloorsByUnit(unitId).pipe(catchError(() => of([] as PhysicalFloorDto[]))),
      boxes: this.physicalStorageService.getBoxesByUnit(unitId).pipe(catchError(() => of([] as PhysicalBoxDto[])))
    }).pipe(
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: ({ shelves, floors, boxes }) => {
        this.shelves.set(Array.isArray(shelves) ? shelves : []);
        this.floors.set(Array.isArray(floors) ? floors : []);
        this.boxes.set(Array.isArray(boxes) ? boxes : []);
        this.shelfPage.set(1);
        this.floorPage.set(1);
        this.boxPage.set(1);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi tải dữ liệu',
          detail: 'Không thể tải sơ đồ lưu trữ.'
        });
      }
    });
  }

  showDialog(type: string) {
    this.currentType.set(type);
    this.isEdit.set(false);
    this.codeValidationError.set('');
    this.formOrgTreeOpen.set(false);
    this.currentData.set({
      code: '',
      name: '',
      description: '',
      priority: null,
      unitId: this.currentUnitId(),
      shelfId: this.shelves().length > 0 ? this.shelves()[0].id : null,
      floorId: this.floors().length > 0 ? this.floors()[0].id : null,
      capacity: 50,
      status: 1
    });
    this.dialogHeader.set('Thêm mới ' + this.getTypeName(type));
    this.displayDialog.set(true);
  }

  editItem(type: string, item: any) {
    this.currentType.set(type);
    this.isEdit.set(true);
    this.codeValidationError.set('');
    this.formOrgTreeOpen.set(false);
    this.currentData.set({ ...item });
    this.dialogHeader.set('Chỉnh sửa ' + this.getTypeName(type));
    this.displayDialog.set(true);
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
    const data = { ...this.currentData() };
    const type = this.currentType();

    if (!this.validateStorageCode(data.code)) {
      return;
    }

    if (!data.name?.trim()) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng điền đầy đủ Mã và Tên bắt buộc!' });
      return;
    }
    if (type === 'shelf' && !data.unitId) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng chọn đơn vị cho kệ lưu trữ.' });
      return;
    }
    if (type === 'floor' && !data.shelfId) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng chọn kệ lưu trữ.' });
      return;
    }
    if (type === 'box' && !data.floorId) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng chọn tầng kệ.' });
      return;
    }

    // Priority không bắt buộc — trống thì mặc định 1
    if (data.priority == null || data.priority === '' || Number.isNaN(Number(data.priority))) {
      data.priority = 1;
    } else {
      data.priority = Number(data.priority);
    }

    this.saving.set(true);
    let saveObs$: import('rxjs').Observable<any> | undefined;

    if (this.isEdit()) {
      switch (type) {
        case 'shelf': saveObs$ = this.physicalStorageService.updateShelf(data.id, data); break;
        case 'floor': saveObs$ = this.physicalStorageService.updateFloor(data.id, data); break;
        case 'box':   saveObs$ = this.physicalStorageService.updateBox(data.id, data);   break;
        default: return;
      }
    } else {
      switch (type) {
        case 'shelf': saveObs$ = this.physicalStorageService.createShelf(data); break;
        case 'floor': saveObs$ = this.physicalStorageService.createFloor(data); break;
        case 'box':   saveObs$ = this.physicalStorageService.createBox(data);   break;
        default: return;
      }
    }

    saveObs$?.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu thông tin thành công!' });
        this.loadAllData();
        this.displayDialog.set(false);
      },
      error: (err) => {
        const codeError = err?.error?.errors?.code;
        if (codeError) {
          this.codeValidationError.set(codeError);
          return;
        }

        const detail = err?.error?.message || 'Lưu thông tin thất bại.';
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail });
      }
    });
  }

  onStorageCodeChange(code: string) {
    this.currentData.update(data => ({ ...data, code }));
    if (this.codeValidationError()) {
      this.validateStorageCode(code);
    }
  }

  private validateStorageCode(code: string | null | undefined): boolean {
    if (!code?.trim()) {
      this.codeValidationError.set('Mã định danh là bắt buộc');
      return false;
    }

    if (!this.storageCodePattern.test(code)) {
      this.codeValidationError.set(this.storageCodeErrorMessage);
      return false;
    }

    this.codeValidationError.set('');
    return true;
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
