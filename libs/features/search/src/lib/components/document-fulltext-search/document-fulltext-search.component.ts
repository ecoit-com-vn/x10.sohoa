import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import {
  DocumentFulltextSearchItem,
  DocumentFulltextSearchService,
  DocumentFulltextSort
} from '../../data-access/document-fulltext-search.service';

@Component({
  selector: 'app-document-fulltext-search',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './document-fulltext-search.component.html',
  styleUrl: './document-fulltext-search.component.scss'
})
export class DocumentFulltextSearchComponent implements OnInit {
  private searchService = inject(DocumentFulltextSearchService);
  private messageService = inject(MessageService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private sanitizer = inject(DomSanitizer);

  items = signal<DocumentFulltextSearchItem[]>([]);
  loading = signal(false);
  totalCount = signal(0);
  currentPage = signal(1);
  pageSize = signal(10);
  keyword = signal('');
  activeKeyword = signal('');
  sort = signal<DocumentFulltextSort>('newest');

  totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));
  pageSizeOptions = [10, 20, 50];

  ngOnInit() {
    this.route.queryParamMap.subscribe((params) => {
      const q = (params.get('keyword') || params.get('q') || '').trim();
      this.keyword.set(q);
      this.activeKeyword.set(q);
      this.currentPage.set(1);
      this.loadData();
    });
  }

  onSearch() {
    const q = this.keyword().trim();
    this.router.navigate(['/search/documents'], {
      queryParams: { keyword: q || null }
    });
  }

  onSortChange(value: DocumentFulltextSort) {
    this.sort.set(value);
    this.currentPage.set(1);
    this.loadData();
  }

  onPageSizeChange(value: number) {
    this.pageSize.set(value);
    this.currentPage.set(1);
    this.loadData();
  }

  changePage(page: number) {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage.set(page);
    this.loadData();
  }

  loadData() {
    this.loading.set(true);
    this.searchService
      .search({
        keyword: this.activeKeyword() || undefined,
        sort: this.sort(),
        page: this.currentPage(),
        pageSize: this.pageSize()
      })
      .subscribe({
        next: (res) => {
          this.items.set(res.items || []);
          this.totalCount.set(res.totalCount || 0);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không tải được kết quả tìm kiếm tài liệu.'
          });
        }
      });
  }

  highlightHtml(item: DocumentFulltextSearchItem): SafeHtml | null {
    const text = item.highlight?.trim();
    if (!text) return null;
    return this.sanitizer.bypassSecurityTrustHtml(text);
  }

  snippetText(item: DocumentFulltextSearchItem): string {
    if (item.highlight?.trim()) return '';
    return item.dossierTitle || item.documentTypeName || 'Không có đoạn trích.';
  }

  equipmentLabel(item: DocumentFulltextSearchItem): string {
    const parts = [...(item.equipmentNames || [])];
    if (item.dossierTypeName) parts.push(item.dossierTypeName);
    return parts.filter(Boolean).join(' · ') || '—';
  }

  openDetail(item: DocumentFulltextSearchItem) {
    const versionId = (item.documentVersionId
      || (item as DocumentFulltextSearchItem & { DocumentVersionId?: string }).DocumentVersionId
      || '').trim();
    if (!versionId) {
      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi',
        detail: 'Không xác định được phiên bản tài liệu.'
      });
      return;
    }
    this.router.navigate(['/search/documents', versionId], {
      queryParams: { keyword: this.activeKeyword() || null }
    });
  }
}
