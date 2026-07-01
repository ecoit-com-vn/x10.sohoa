import { Component, OnInit, Input, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { DossierDocumentEditDialogComponent } from '@sohoa.frontend/features/dossier-management';

export interface LookupDocumentItem {
  id: string;
  name: string;
  documentTypeName: string;
  latestVersionId: string;
  fileSize: number;
  fileSizeFormatted?: string;
  createdDate: string;
  creatorName: string;
  mimeType?: string;
  documentTypeId?: string;
  ocrProgress?: {
    totalPages: number;
  };
  extractionResult?: any;
}

@Component({
  selector: 'app-dossier-lookup-documents-tab',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, TooltipModule, DossierDocumentEditDialogComponent],
  providers: [MessageService],
  templateUrl: './dossier-lookup-documents-tab.component.html'
})
export class DossierLookupDocumentsTabComponent implements OnInit {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
  private messageService = inject(MessageService);

  @Input({ required: true }) dossierId!: string;

  documents = signal<LookupDocumentItem[]>([]);
  loading = signal<boolean>(false);
  totalDocuments = signal<number>(0);
  page = signal<number>(1);
  pageSize = signal<number>(10);
  searchKeyword = signal<string>('');
  showEditDocument = signal<boolean>(false);
  editTarget = signal<LookupDocumentItem | null>(null);

  totalPages = computed(() => Math.ceil(this.totalDocuments() / this.pageSize()));

  ngOnInit() {
    this.loadDocuments();
  }

  onSearch() {
    this.page.set(1);
    this.loadDocuments();
  }

  loadDocuments() {
    if (!this.dossierId) return;

    this.loading.set(true);
    let params: any = {
      page: this.page(),
      pageSize: this.pageSize()
    };
    if (this.searchKeyword().trim()) {
      params.keyword = this.searchKeyword().trim();
    }

    this.http.get<any>(
      `${this.config.apiGatewayUrl}/api/v1/dossiers-by-equipment/${this.dossierId}/documents`,
      { params }
    ).subscribe({
      next: (res) => {
        this.documents.set(res?.items || []);
        this.totalDocuments.set(res?.totalCount || 0);
        this.loading.set(false);
      },
      error: (err) => {
        const msg = err?.error?.message || 'Không thể tải danh sách tài liệu';
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: msg });
        this.loading.set(false);
      }
    });
  }

  viewFile(doc: LookupDocumentItem) {
    if (!doc.latestVersionId) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Tài liệu không có phiên bản để xem trước' });
      return;
    }
    this.editTarget.set(doc);
    this.showEditDocument.set(true);
  }

  downloadFile(doc: LookupDocumentItem) {
    if (!doc.latestVersionId) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Tài liệu không có phiên bản để tải về' });
      return;
    }

    this.http.get<any>(
      `${this.config.apiGatewayUrl}/api/v1/dossiers-by-equipment/${this.dossierId}/documents/${doc.latestVersionId}/download-url`
    ).subscribe({
      next: (res) => {
        const url = res?.downloadUrl || res?.url;
        if (url) {
          window.open(url, '_blank');
        } else {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không lấy được đường dẫn tải tài liệu' });
        }
      },
      error: (err) => {
        const msg = err?.error?.message || 'Không thể tải tài liệu';
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: msg });
      }
    });
  }

  changePage(newPage: number) {
    if (newPage >= 1 && newPage <= this.totalPages()) {
      this.page.set(newPage);
      this.loadDocuments();
    }
  }

  formatSize(bytes: number): string {
    if (!bytes || isNaN(bytes)) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }
}
