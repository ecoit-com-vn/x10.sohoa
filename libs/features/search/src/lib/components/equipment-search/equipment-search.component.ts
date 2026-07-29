import { Component, OnInit, inject } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { environment } from '@env/environment';
import { VirtualFoldersComponent } from '@sohoa.frontend/features/digitization';

@Component({
  selector: 'app-equipment-search',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, ButtonModule, VirtualFoldersComponent, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './equipment-search.component.html',
  styleUrl: './equipment-search.component.scss'
})
export class EquipmentSearchComponent implements OnInit {
  searchQuery: string = '';
  results: any[] = [];
  loading: boolean = false;

  currentPage = 1;
  pageSize = 10;
  totalRecords = 0;

  // Detail Dialog states
  displayDetailDialog = false;
  selectedEquipment: any = null;
  selectedEquipmentId: string | null = null;

  // Excel Import/Template states
  displayTemplateDialog = false;
  displayImportDialog = false;
  equipmentTypes: any[] = [];
  selectedTypeIdForTemplate = '';
  selectedTypeIdForImport = '';
  selectedFileToImport: File | null = null;
  importing = false;
  importResult: any = null;

  private http = inject(HttpClient);
  private messageService = inject(MessageService);

  ngOnInit() {
    this.loadEquipmentTypes();
  }

  loadEquipmentTypes() {
    this.http.get<any[]>(`${environment.apiGatewayUrl}/api/v1/equipmenttype`).subscribe({
      next: (res) => {
        this.equipmentTypes = res || [];
      },
      error: (err) => console.error('Lỗi tải loại thiết bị:', err)
    });
  }

  openEquipmentDetail(item: any) {
    this.selectedEquipment = item;
    this.selectedEquipmentId = item.id || item.code;
    this.displayDetailDialog = true;
  }

  openTemplateDialog() {
    this.loadEquipmentTypes();
    this.displayTemplateDialog = true;
  }

  downloadTemplate() {
    if (!this.selectedTypeIdForTemplate) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Vui lòng chọn loại thiết bị.' });
      return;
    }
    const url = `${environment.apiGatewayUrl}/api/v1/equipment/import-template/${this.selectedTypeIdForTemplate}`;
    this.http.get(url, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        const urlObj = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = urlObj;
        const selectedType = this.equipmentTypes.find(t => t.id === this.selectedTypeIdForTemplate);
        a.download = `Import_Template_${selectedType?.code || 'Equipment'}.xlsx`;
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(urlObj);
        a.remove();
        this.displayTemplateDialog = false;
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã tải xuống file Excel mẫu.' });
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Tải file mẫu thất bại.' });
      }
    });
  }

  openImportDialog() {
    this.loadEquipmentTypes();
    this.selectedFileToImport = null;
    this.importResult = null;
    this.displayImportDialog = true;
  }

  onFileChange(event: any) {
    const file = event.target.files[0];
    if (file) {
      this.selectedFileToImport = file;
    }
  }

  executeImport() {
    if (!this.selectedTypeIdForImport) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Vui lòng chọn loại thiết bị.' });
      return;
    }
    if (!this.selectedFileToImport) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Vui lòng chọn file Excel.' });
      return;
    }

    const formData = new FormData();
    formData.append('file', this.selectedFileToImport);

    this.importing = true;
    this.importResult = null;
    const url = `${environment.apiGatewayUrl}/api/v1/equipment/import?equipmentTypeId=${this.selectedTypeIdForImport}`;

    this.http.post<any>(url, formData).subscribe({
      next: (res) => {
        this.importing = false;
        this.importResult = res;
        if (res.successCount > 0) {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: res.message });
          this.onSearch(); // Tải lại danh sách thiết bị
        } else {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không có dòng dữ liệu nào được nhập thành công.' });
        }
      },
      error: (err) => {
        this.importing = false;
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Nhập dữ liệu từ Excel thất bại.' });
      }
    });
  }

  onSearch() {
    if (!this.searchQuery.trim()) {
      this.results = [];
      this.totalRecords = 0;
      return;
    }

    this.loading = true;
    const url = `${environment.apiGatewayUrl}/api/v1/equipment/search`;
    this.http.get<any>(url, {
      params: {
        query: this.searchQuery,
        page: this.currentPage.toString(),
        pageSize: this.pageSize.toString()
      }
    }).subscribe({
      next: (res) => {
        if (Array.isArray(res)) {
          this.results = res;
          this.totalRecords = res.length;
        } else {
          this.results = res.items || res.results || [];
          this.totalRecords = res.totalCount || res.total || this.results.length;
        }
        this.loading = false;
      },
      error: (err) => {
        console.error('Lỗi tìm kiếm thiết bị:', err);
        this.results = [];
        this.totalRecords = 0;
        this.loading = false;
      }
    });
  }

  nextPage() {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
      this.onSearch();
    }
  }

  prevPage() {
    if (this.currentPage > 1) {
      this.currentPage--;
      this.onSearch();
    }
  }

  goToPage(page: any) {
    const p = Number(page);
    if (p >= 1 && p <= this.totalPages) {
      this.currentPage = p;
      this.onSearch();
    }
  }

  onPageSizeChange(event: any) {
    this.pageSize = Number(event.target.value);
    this.currentPage = 1;
    this.onSearch();
  }

  get totalPages(): number {
    return Math.ceil(this.totalRecords / this.pageSize);
  }
}
