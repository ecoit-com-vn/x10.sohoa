import {
  Component,
  OnInit,
  signal,
  computed,
  inject,
  input,
  effect,
  OnDestroy,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { FileUploadZoneComponent } from '../file-upload/file-upload-zone.component';
import { FileListComponent } from '../file-upload/file-list.component';
import { ScannerPanelComponent } from '../scanner/scanner-panel.component';
import { UPLOAD_SOURCE } from '../../constants/upload-source.constants';
import { Subject } from 'rxjs';

@Component({
  selector: 'app-folder-detail-view',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    ToastModule,
    FileUploadZoneComponent,
    FileListComponent,
    ScannerPanelComponent,
  ],
  providers: [MessageService],
  templateUrl: './folder-detail-view.component.html',
  styleUrl: './folder-detail-view.component.scss'
})
export class FolderDetailViewComponent implements OnInit, OnDestroy {
  private messageService = inject(MessageService);
  private destroy$ = new Subject<void>();

  @ViewChild('uploadZone') uploadZone?: FileUploadZoneComponent;

  // Inputs
  folderId = input<string>('');
  currentView = signal<'upload' | 'files'>('files');
  refreshTrigger = signal<number>(0);

  ngOnInit() {
    // Auto-refresh on folderId change
    effect(() => {
      const id = this.folderId();
      if (id) {
        this.refreshTrigger.set(Date.now());
      }
    });
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * Handle file uploaded
   */
  onFileUploaded(event: { documentVersionId: string; fileName: string }) {
    this.messageService.add({
      severity: 'success',
      summary: 'Tải lên thành công',
      detail: event.fileName,
      life: 3000
    });

    // Switch to files view
    this.currentView.set('files');

    // Trigger refresh
    this.refreshTrigger.set(Date.now());
  }

  /**
   * Handle upload error
   */
  onUploadError(event: { fileName: string; error: string }) {
    this.messageService.add({
      severity: 'error',
      summary: 'Lỗi tải lên',
      detail: `${event.fileName}: ${event.error}`,
      life: 5000
    });
  }

  onScannedFile(file: File): void {
    this.uploadZone?.ingestFile(file, UPLOAD_SOURCE.SCAN);
  }
}
