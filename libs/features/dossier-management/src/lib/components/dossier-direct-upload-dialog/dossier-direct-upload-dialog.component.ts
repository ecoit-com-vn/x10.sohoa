import {
  Component,
  Input,
  Output,
  EventEmitter,
  inject,
  signal,
  computed,
  OnInit,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { finalize, forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import {
  FileUploadZoneComponent,
  FileUploadHandler,
  ScannerPanelComponent,
  UPLOAD_SOURCE,
} from '@sohoa.frontend/features/equipment';
import {
  DossierDocumentService,
  DocumentTypeLookupItem,
  DigitizationProcessOption,
} from '../../data-access/dossier-document.service';
import { OcrMode } from '../../utils/dossier-digitization.util';

interface UploadedFileItem {
  fileName: string;
  versionId: string;
}

@Component({
  selector: 'app-dossier-direct-upload-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ButtonModule, FileUploadZoneComponent, ScannerPanelComponent],
  templateUrl: './dossier-direct-upload-dialog.component.html',
  styleUrl: './dossier-direct-upload-dialog.component.scss',
})
export class DossierDirectUploadDialogComponent implements OnInit {
  private dossierDocumentService = inject(DossierDocumentService);
  private messageService = inject(MessageService);

  @ViewChild(FileUploadZoneComponent) uploadZone?: FileUploadZoneComponent;

  @Input({ required: true }) dossierId!: string;
  @Input() visible = false;
  /** 2 = Scan, 3 = Upload web */
  @Input() uploadSource = 3;
  @Input() dialogTitle = 'Upload trực tiếp vào hồ sơ';
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() documentsAdded = new EventEmitter<void>();
  @Output() digitizationStarted = new EventEmitter<string>();

  documentTypes = signal<DocumentTypeLookupItem[]>([]);
  loadingDocTypes = signal(false);
  selectedDocumentTypeId = signal('');
  uploadedFiles = signal<UploadedFileItem[]>([]);
  submitting = signal(false);
  ocrMode: OcrMode = 'none';

  selectedDocumentType = computed(() =>
    this.documentTypes().find((t) => t.id === this.selectedDocumentTypeId()) ?? null
  );

  hasDocumentTypeForm = computed(() => !!this.selectedDocumentType()?.formId);
  canUpload = computed(() => !!this.selectedDocumentTypeId());
  uploadedCount = computed(() => this.uploadedFiles().length);
  isScanMode = computed(() => this.uploadSource === UPLOAD_SOURCE.SCAN);

  readonly UPLOAD_SOURCE = UPLOAD_SOURCE;

  uploadHandler: FileUploadHandler = (file, onProgress) => {
    const documentTypeId = this.selectedDocumentTypeId();
    if (!documentTypeId) {
      return Promise.reject(new Error('Vui lòng chọn loại văn bản trước khi upload'));
    }
    return this.dossierDocumentService.uploadFile(
      this.dossierId,
      file,
      documentTypeId,
      this.uploadSource,
      onProgress
    );
  };

  ngOnInit(): void {
    this.loadDocumentTypes();
  }

  close(): void {
    if (this.submitting()) return;
    this.visibleChange.emit(false);
  }

  private loadDocumentTypes(): void {
    this.loadingDocTypes.set(true);
    this.dossierDocumentService
      .lookupDocumentTypes()
      .pipe(finalize(() => this.loadingDocTypes.set(false)))
      .subscribe({
        next: (items) => this.documentTypes.set(items),
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể tải danh mục loại văn bản',
          });
        },
      });
  }

  onFileUploaded(event: { documentVersionId: string; fileName: string }): void {
    this.uploadedFiles.update((list) => [
      ...list,
      { fileName: event.fileName, versionId: event.documentVersionId },
    ]);
    this.documentsAdded.emit();
  }

  onUploadError(event: { fileName: string; error: string }): void {
    this.messageService.add({
      severity: 'error',
      summary: 'Lỗi upload',
      detail: `${event.fileName}: ${event.error}`,
    });
  }

  onScannedFile(file: File): void {
    this.uploadZone?.ingestFile(file);
  }

  onShow(): void {
    this.uploadedFiles.set([]);
    this.uploadZone?.clearUploads();
    this.ocrMode = 'none';
    this.submitting.set(false);
    this.selectedDocumentTypeId.set('');
    if (this.documentTypes().length === 0) {
      this.loadDocumentTypes();
    }
  }

  done(): void {
    if (this.submitting()) return;

    if (this.uploadZone?.isUploading()) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Đang upload',
        detail: 'Vui lòng đợi file upload xong trước khi bấm Xong',
      });
      return;
    }

    const files = this.uploadedFiles();

    if (this.ocrMode !== 'none') {
      if (files.length === 0) {
        this.messageService.add({
          severity: 'warn',
          summary: 'Chưa có file',
          detail: 'Cần upload ít nhất một file thành công trước khi gửi xử lý OCR',
        });
        return;
      }
      if (!this.hasDocumentTypeForm()) {
        this.messageService.add({
          severity: 'warn',
          summary: 'Thiếu form EAV',
          detail: 'Loại văn bản chưa gắn biểu mẫu — không thể bóc tách',
        });
        return;
      }
      this.submitDigitizationForUploaded(files);
      return;
    }

    this.finishAndClose(files.length);
  }

  private submitDigitizationForUploaded(files: UploadedFileItem[]): void {
    const processOption = this.ocrMode as DigitizationProcessOption;
    this.submitting.set(true);

    const requests = files.map((file) =>
      this.dossierDocumentService
        .submitDigitization(this.dossierId, file.versionId, { processOption })
        .pipe(
          map(() => ({ ok: true as const, fileName: file.fileName, versionId: file.versionId })),
          catchError((err) =>
            of({
              ok: false as const,
              fileName: file.fileName,
              versionId: file.versionId,
              error: err?.error?.message || 'Không thể gửi yêu cầu OCR',
            })
          )
        )
    );

    forkJoin(requests)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe((results) => {
        const succeeded = results.filter((r) => r.ok);
        const failed = results.filter((r) => !r.ok);

        for (const item of failed) {
          if (!item.ok) {
            this.messageService.add({
              severity: 'error',
              summary: 'Lỗi OCR',
              detail: `${item.fileName}: ${item.error}`,
            });
          }
        }

        if (succeeded.length > 0) {
          this.digitizationStarted.emit(succeeded[0].versionId);
          this.messageService.add({
            severity: 'success',
            summary: 'Đã gửi OCR',
            detail: `Đã gửi xử lý cho ${succeeded.length} tài liệu`,
          });
        }

        this.finishAndClose(files.length);
      });
  }

  private finishAndClose(uploadCount: number): void {
    if (uploadCount > 0) {
      this.messageService.add({
        severity: 'success',
        summary: 'Hoàn tất',
        detail: `Đã thêm ${uploadCount} tài liệu vào hồ sơ`,
      });
    }
    this.visibleChange.emit(false);
  }
}
