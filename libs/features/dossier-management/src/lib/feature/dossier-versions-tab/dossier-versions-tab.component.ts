import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { catchError, finalize, of } from 'rxjs';
import { DossierManagementService } from '../../data-access/dossier-management.service';

@Component({
  selector: 'app-dossier-versions-tab',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div style="display: flex; flex-direction: column; gap: 12px;">
      <div *ngIf="loading()" style="display: flex; align-items: center; gap: 8px; color: #6b7280; padding: 12px 0;">
        <i class="pi pi-spin pi-spinner"></i> Đang tải lịch sử...
      </div>

      <div *ngIf="!loading() && versions().length === 0"
           style="padding: 32px; text-align: center; color: #9ca3af; background: #f8fafc; border-radius: 8px; border: 1px dashed #e2e8f0; font-size: 0.85rem;">
        Chưa có phiên bản nào được lưu.
      </div>

      <div *ngFor="let v of versions()" style="border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;">
        <div style="background: #f8fafc; padding: 10px 16px; border-bottom: 1px solid #e2e8f0; display: flex; justify-content: space-between; align-items: center;">
          <span style="font-weight: 600; color: #374151;">Phiên bản #{{ v.versionNumber }}</span>
          <span style="font-size: 0.78rem; color: #6b7280;">{{ v.createdDate | date:'dd/MM/yyyy HH:mm' }} — {{ v.createdBy }}</span>
        </div>
        <div style="padding: 12px 16px;">
          <div *ngIf="v.changeNote" style="font-size: 0.85rem; color: #374151; margin-bottom: 8px;">
            <i class="pi pi-comment" style="margin-right: 4px; color: #6b7280;"></i>{{ v.changeNote }}
          </div>
          <div *ngIf="v.documentsSnapshotJson" style="font-size: 0.82rem; color: #475569; margin-bottom: 8px;">
            <i class="pi pi-paperclip" style="margin-right: 4px; color: #6b7280;"></i>
            <span>Tài liệu tại thời điểm này: {{ parseDocumentSnapshotCount(v.documentsSnapshotJson) }}</span>
          </div>
          <details style="cursor: pointer;">
            <summary style="font-size: 0.8rem; color: #6b7280;">Xem dữ liệu JSON</summary>
            <pre style="font-size: 0.76rem; background: #f1f5f9; padding: 10px; border-radius: 6px; margin-top: 8px; overflow-x: auto; white-space: pre-wrap; word-break: break-all;">{{ v.formDataJson | json }}</pre>
          </details>
        </div>
      </div>
    </div>
  `,
})
export class DossierVersionsTabComponent implements OnInit {
  @Input({ required: true }) dossierId!: string;

  private service = inject(DossierManagementService);

  versions = signal<any[]>([]);
  loading = signal(false);

  ngOnInit(): void {
    this.loadVersions();
  }

  loadVersions(): void {
    this.loading.set(true);
    this.service.getVersions(this.dossierId).pipe(
      catchError(() => of([] as any[])),
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: (res) => this.versions.set(Array.isArray(res) ? res : []),
    });
  }

  parseDocumentSnapshotCount(json?: string | null): string {
    if (!json?.trim()) return '0 tài liệu';
    try {
      const arr = JSON.parse(json) as unknown[];
      const count = Array.isArray(arr) ? arr.length : 0;
      return `${count} tài liệu`;
    } catch {
      return '—';
    }
  }
}
