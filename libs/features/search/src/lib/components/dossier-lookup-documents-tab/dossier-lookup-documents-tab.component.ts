import { Component, OnInit, Input, signal, computed, inject, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { MenuItem, MessageService } from 'primeng/api';
import { Menu, MenuModule } from 'primeng/menu';
import { DossierDocumentEditDialogComponent } from '@sohoa.frontend/features/dossier-management';
import { DocumentFulltextSearchService } from '../../data-access/document-fulltext-search.service';

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
  imports: [CommonModule, FormsModule, ToastModule, TooltipModule, MenuModule, DossierDocumentEditDialogComponent],
  providers: [MessageService],
  templateUrl: './dossier-lookup-documents-tab.component.html'
})
export class DossierLookupDocumentsTabComponent implements OnInit, OnChanges {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
  private messageService = inject(MessageService);
  private router = inject(Router);
  private fulltextService = inject(DocumentFulltextSearchService);

  @Input({ required: true }) dossierId!: string;
  /** equipment = tra cứu hồ sơ TB; fulltext = tra cứu toàn văn; report = báo cáo hồ sơ TB */
  @Input() apiMode: 'equipment' | 'fulltext' | 'report' = 'equipment';
  @Input() reportApiSegment = '';
  @Input() returnKeyword = '';

  documents = signal<LookupDocumentItem[]>([]);
  loading = signal<boolean>(false);
  totalDocuments = signal<number>(0);
  page = signal<number>(1);
  pageSize = signal<number>(10);
  searchKeyword = signal<string>('');
  showEditDocument = signal<boolean>(false);
  editTarget = signal<LookupDocumentItem | null>(null);
  actionMenuItems: MenuItem[] = [];

  totalPages = computed(() => Math.ceil(this.totalDocuments() / this.pageSize()));

  openActionMenu(doc: LookupDocumentItem, event: MouseEvent, menu: Menu): void {
    this.actionMenuItems = [
      {
        label: 'Xem tài liệu',
        icon: 'pi pi-eye color-teal',
        command: () => this.viewFile(doc),
      },
      {
        label: 'Tải tài liệu',
        icon: 'pi pi-download color-blue',
        disabled: !doc.latestVersionId,
        command: () => this.downloadFile(doc),
      },
    ];
    menu.toggle(event);
  }

  ngOnInit() {
    this.loadDocuments();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['dossierId'] && !changes['dossierId'].firstChange) {
      this.page.set(1);
      this.loadDocuments();
    }
  }

  private documentsBaseUrl(): string {
    if (this.apiMode === 'fulltext') {
      return `${this.config.apiGatewayUrl}/api/v1/document-fulltext-search/dossiers/${this.dossierId}/documents`;
    }
    if (this.apiMode === 'report' && this.reportApiSegment) {
      return `${this.config.apiGatewayUrl}/api/v1/reports/${this.reportApiSegment}/${this.dossierId}/documents`;
    }
    return `${this.config.apiGatewayUrl}/api/v1/dossiers-by-equipment/${this.dossierId}/documents`;
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

    this.http.get<any>(this.documentsBaseUrl(), { params }).subscribe({
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
    if (this.apiMode === 'fulltext') {
      void this.router.navigate(['/search/documents', doc.latestVersionId], {
        queryParams: this.returnKeyword ? { keyword: this.returnKeyword } : {}
      });
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

    if (this.apiMode === 'fulltext') {
      void this.fulltextService.downloadFile(this.dossierId, doc.latestVersionId, doc.name).catch(() => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải tài liệu' });
      });
      return;
    }

    const downloadBase =
      this.apiMode === 'report' && this.reportApiSegment
        ? `${this.config.apiGatewayUrl}/api/v1/reports/${this.reportApiSegment}/${this.dossierId}/documents`
        : `${this.config.apiGatewayUrl}/api/v1/dossiers-by-equipment/${this.dossierId}/documents`;

    this.http.get<any>(`${downloadBase}/${doc.latestVersionId}/download-url`).subscribe({
      next: (res) => {
        const token = res?.token;
        if (token) {
          const downloadUrl = `${this.config.apiGatewayUrl}/api/v1/files/download?token=${encodeURIComponent(token)}`;
          window.open(downloadUrl, '_blank');
        } else {
          const url = res?.downloadUrl || res?.url;
          if (url) {
            window.open(url, '_blank');
          } else {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không lấy được đường dẫn tải tài liệu' });
          }
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
