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
} from '../../../../../equipment/src/lib/components/file-upload/file-upload-zone.component';
import {
  ScannerPanelComponent,
} from '../../../../../equipment/src/lib/components/scanner/scanner-panel.component';
import {
  UPLOAD_SOURCE,
} from '../../../../../equipment/src/lib/constants/upload-source.constants';

import {
  DossierDocumentService,
  DocumentTypeLookupItem,
  DigitizationProcessOption,
  DigitizationExtractionScope,
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

  @ViewChild('scannerPanel') scannerPanel?: ScannerPanelComponent;



  @Input({ required: true }) dossierId!: string;

  @Input() visible = false;

  /** 2 = Scan, 3 = Upload web */

  @Input() uploadSource = 3;

  @Input() dialogTitle = 'Upload trực tiếp vào hồ sơ';

  /** Chỉ hiển thị loại văn bản lý lịch thiết bị (IsEquipmentProfile). */

  @Output() visibleChange = new EventEmitter<boolean>();

  @Output() documentsAdded = new EventEmitter<void>();

  @Output() digitizationStarted = new EventEmitter<string>();



  documentTypes = signal<DocumentTypeLookupItem[]>([]);

  loadingDocTypes = signal(false);

  selectedDocumentTypeId = signal('');

  uploadedFiles = signal<UploadedFileItem[]>([]);

  submitting = signal(false);

  scanInProgress = signal(false);

  ocrMode: OcrMode = 'none';

  /**
   * Phạm vi trang cần bóc tách. Mặc định 'FirstAndLastPage' vì với biểu mẫu ngành điện, dữ liệu cần
   * lấy hầu như chỉ nằm ở trang đầu và trang cuối — bóc tách mọi trang tốn thêm ~30-60 giây mỗi
   * trang mà phần lớn không dùng tới. Bước OCR không bị ảnh hưởng, vẫn chạy đủ trang.
   */
  extractionScope: DigitizationExtractionScope = 'FirstAndLastPage';



  selectedDocumentType = computed(() =>

    this.documentTypes().find((t) => t.id === this.selectedDocumentTypeId()) ?? null

  );



  hasDocumentTypeForm = computed(() => !!this.selectedDocumentType()?.formId);

  canUpload = computed(() => !!this.selectedDocumentTypeId());

  queuedCount = computed(() => this.uploadZone?.hasQueuedUploads() ?? false);



  readonly UPLOAD_SOURCE = UPLOAD_SOURCE;



  get isScanMode(): boolean {

    return this.uploadSource === UPLOAD_SOURCE.SCAN;

  }



  get dialogWidthStyle(): Record<string, string> {

    return this.isScanMode

      ? { width: '560px', maxWidth: '95vw' }

      : { width: '640px', maxWidth: '95vw' };

  }



  get dialogStyleClass(): string {

    return this.isScanMode

      ? 'evn-dialog-custom dossier-scan-upload-dialog'

      : 'evn-dialog-custom';

  }



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

    if (this.isScanningActive()) {

      this.messageService.add({

        severity: 'warn',

        summary: 'Đang quét',

        detail: 'Vui lòng hoàn tất hoặc hủy quét trên EcoScanner trước khi đóng',

      });

      return;

    }

    this.uploadZone?.clearQueuedUploads();

    this.uploadedFiles.set([]);

    this.visibleChange.emit(false);

  }



  onVisibleChange(visible: boolean): void {

    if (visible) {

      this.visibleChange.emit(true);

      return;

    }

    this.close();

  }



  onScanInProgress(inProgress: boolean): void {

    this.scanInProgress.set(inProgress);

  }



  private isScanningActive(): boolean {

    return this.scanInProgress() || !!this.scannerPanel?.isScanning();

  }



  private loadDocumentTypes(): void {
    this.loadingDocTypes.set(true);
    this.dossierDocumentService
      .getDocumentTypesForDossier(this.dossierId)
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

    this.ocrMode = 'none';
    this.extractionScope = 'FirstAndLastPage';

    this.submitting.set(false);

    this.scanInProgress.set(false);

    this.selectedDocumentTypeId.set('');

    this.uploadZone?.clearQueuedUploads();

    if (this.documentTypes().length === 0) {

      this.loadDocumentTypes();

    }

  }



  async done(): Promise<void> {

    if (this.submitting()) return;



    if (this.isScanningActive()) {

      this.messageService.add({

        severity: 'warn',

        summary: 'Đang quét',

        detail: 'Vui lòng hoàn tất quét trước khi bấm Xong',

      });

      return;

    }



    const hasQueued = this.uploadZone?.hasQueuedUploads() ?? false;

    if (!hasQueued && this.uploadedFiles().length === 0) {

      this.visibleChange.emit(false);

      return;

    }



    this.submitting.set(true);



    try {

      let files = this.uploadedFiles();



      if (hasQueued) {

        const results = await this.uploadZone!.flushQueuedUploads();

        files = results.map((r) => ({

          fileName: r.fileName,

          versionId: r.response.documentVersionId,

        }));

        this.uploadedFiles.set(files);

      }



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

        await this.submitDigitizationForUploaded(files);

        return;

      }



      this.finishAndClose(files.length);

    } catch {

      // flushQueuedUploads đã báo lỗi chi tiết

    } finally {

      this.submitting.set(false);

    }

  }



  private submitDigitizationForUploaded(files: UploadedFileItem[]): Promise<void> {

    const processOption = this.ocrMode as DigitizationProcessOption;
    const extractionScope = this.extractionScope;



    const requests = files.map((file) =>

      this.dossierDocumentService

        .submitDigitization(this.dossierId, file.versionId, { processOption, extractionScope })

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



    return new Promise((resolve) => {

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

          resolve();

        });

    });

  }



  private finishAndClose(uploadCount: number): void {

    if (uploadCount > 0) {

      this.documentsAdded.emit();

      this.messageService.add({

        severity: 'success',

        summary: 'Hoàn tất',

        detail: `Đã thêm ${uploadCount} tài liệu vào hồ sơ`,

      });

    }

    this.uploadedFiles.set([]);

    this.uploadZone?.clearQueuedUploads();

    this.visibleChange.emit(false);

  }

}


