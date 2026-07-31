import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'app-eco-paginator',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './eco-paginator.component.html',
  styleUrl: './eco-paginator.component.scss'
})
export class EcoPaginatorComponent {
  @Input() currentPage = 1;
  @Input() pageSize = 10;
  @Input() first: number | null = null;
  @Input() rows: number | null = null;
  @Input() totalRecords = 0;
  @Input() rowsPerPageOptions: readonly number[] = [10, 20, 50];

  @Output() pageChange = new EventEmitter<number>();
  @Output() pageSizeChange = new EventEmitter<number>();
  @Output() onPageChange = new EventEmitter<{
    first: number;
    rows: number;
    page: number;
    pageCount: number;
  }>();

  get effectivePageSize(): number {
    return this.rows ?? this.pageSize;
  }

  get effectiveCurrentPage(): number {
    return this.first === null
      ? this.currentPage
      : Math.floor(this.first / this.effectivePageSize) + 1;
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalRecords / this.effectivePageSize));
  }

  get fromRecord(): number {
    return this.totalRecords === 0 ? 0 : (this.effectiveCurrentPage - 1) * this.effectivePageSize + 1;
  }

  get toRecord(): number {
    return Math.min(this.effectiveCurrentPage * this.effectivePageSize, this.totalRecords);
  }

  get visiblePages(): number[] {
    const maxVisiblePages = 5;
    const halfWindow = Math.floor(maxVisiblePages / 2);
    let start = Math.max(1, this.effectiveCurrentPage - halfWindow);
    const end = Math.min(this.totalPages, start + maxVisiblePages - 1);

    start = Math.max(1, end - maxVisiblePages + 1);
    return Array.from({ length: end - start + 1 }, (_, index) => start + index);
  }

  selectPage(page: number): void {
    if (page < 1 || page > this.totalPages || page === this.effectiveCurrentPage) {
      return;
    }

    this.pageChange.emit(page);
    this.onPageChange.emit({
      first: (page - 1) * this.effectivePageSize,
      rows: this.effectivePageSize,
      page: page - 1,
      pageCount: this.totalPages
    });
  }

  selectPageSize(event: Event): void {
    const selectedPageSize = Number((event.target as HTMLSelectElement | null)?.value);
    if (!this.rowsPerPageOptions.includes(selectedPageSize) || selectedPageSize === this.effectivePageSize) {
      return;
    }

    this.pageSizeChange.emit(selectedPageSize);
    this.onPageChange.emit({
      first: 0,
      rows: selectedPageSize,
      page: 0,
      pageCount: Math.max(1, Math.ceil(this.totalRecords / selectedPageSize))
    });
  }
}
