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
import { DossierManagementService } from '../../data-access/dossier-management.service';
import { OcrMode } from '../../utils/dossier-digitization.util';
import { MultiSelectModule } from 'primeng/multiselect';



interface UploadedFileItem {

  fileName: string;

  versionId: string;

}



@Component({

  selector: 'app-dossier-direct-upload-dialog',

  standalone: true,

  imports: [CommonModule, FormsModule, DialogModule, ButtonModule, MultiSelectModule, FileUploadZoneComponent, ScannerPanelComponent],

  templateUrl: './dossier-direct-upload-dialog.component.html',

  styleUrl: './dossier-direct-upload-dialog.component.scss',

})

export class DossierDirectUploadDialogComponent implements OnInit {

  private dossierDocumentService = inject(DossierDocumentService);
  private dossierManagementService = inject(DossierManagementService);
  private messageService = inject(MessageService);

  /** Hồ sơ chỉ nhận file PDF và ảnh (JPG/PNG/TIFF) — khớp với whitelist mime-type backend đang cho phép
   * cho luồng upload trực tiếp vào hồ sơ (FileUploadService.UploadFileToDossierDirectAsync). */
  readonly dossierAllowedExtensions = ['.pdf', '.jpg', '.jpeg', '.png', '.tiff'];

  @ViewChild(FileUploadZoneComponent) uploadZone?: FileUploadZoneComponent;

  @ViewChild('scannerPanel') scannerPanel?: ScannerPanelComponent;



  @Input({ required: true }) dossierId!: string;

  /** Trạm/đường dây đã chọn của hồ sơ — dùng để giới hạn danh sách thiết bị có thể gắn vào tài liệu. */
  @Input() infrastructureIds: string[] = [];

  @Input() visible = false;

  /** 2 = Scan, 3 = Upload web */

  @Input() uploadSource = 3;

  @Input() dialogTitle = 'Upload trực tiếp vào hồ sơ';

  /** Loại văn bản đang chọn ở cây thư mục bên ngoài (tab cha) — thay cho select "Bước 1" cũ đã bỏ.
   * null = chưa chọn nhánh nào, upload sẽ bị chặn (tab cha đã chặn mở dialog trong trường hợp này,
   * đây là lớp bảo vệ thứ 2 phòng khi dialog được mở bằng cách khác).
   *
   * Dùng setter ghi vào signal riêng thay vì đọc thẳng property trong computed() — computed() chỉ
   * theo dõi được signal, một @Input thường (property thuần) không bao giờ kích hoạt nó tính lại, nên
   * selectedDocumentTypeId từng bị đứng im ở giá trị lúc khởi tạo (luôn rỗng) dù tab cha đã truyền
   * đúng documentTypeId khi người dùng chọn nhánh cây — đây chính là lỗi "Chưa xác định được loại văn
   * bản" dù đã chọn loại tài liệu trước khi bấm Thêm mới. */
  private readonly documentTypeIdSignal = signal<string | null>(null);
  @Input() set documentTypeId(value: string | null) {
    this.documentTypeIdSignal.set(value);
  }
  get documentTypeId(): string | null {
    return this.documentTypeIdSignal();
  }

  @Output() visibleChange = new EventEmitter<boolean>();

  @Output() documentsAdded = new EventEmitter<void>();

  @Output() digitizationStarted = new EventEmitter<string>();



  documentTypes = signal<DocumentTypeLookupItem[]>([]);

  loadingDocTypes = signal(false);

  /** thay cho signal set() thủ công cũ — nguồn giờ là @Input() documentTypeId do tab cha truyền vào
   * (lấy từ nhánh cây thư mục đang chọn), không còn select trong dialog này nữa. */
  readonly selectedDocumentTypeId = computed(() => this.documentTypeIdSignal() ?? '');

  /** Thiết bị để phân loại tài liệu — chỉ chọn được thiết bị thuộc Trạm/đường dây đã chọn của hồ sơ.
   * Danh sách có thể rất lớn (toàn bộ thiết bị của 1 Trạm/Đường dây) nên KHÔNG tải hết 1 lần — dùng
   * virtual scroll + lazy load của PrimeNG: mảng equipmentOptions có độ dài đúng bằng tổng số thiết bị
   * thực tế (equipmentTotalCount), các ô chưa tải để `undefined` (PrimeNG tự hiện loaderTemplate cho
   * các ô này); cuộn tới đâu mới gọi API tải trang tương ứng tới đó (xem onEquipmentLazyLoad). Gõ tìm
   * kiếm (onEquipmentFilter) cũng gọi lại API theo từ khoá thay vì lọc client-side trên dữ liệu đã tải
   * — vì dữ liệu đã tải chỉ là 1 phần nhỏ, lọc client-side sẽ bỏ sót thiết bị chưa được tải tới. */
  equipmentOptions = signal<({ id: string; code: string; name: string } | undefined)[]>([]);
  equipmentTotalCount = signal(0);
  loadingEquipments = signal(false);
  selectedEquipmentIds = signal<string[]>([]);
  private readonly EQUIPMENT_PAGE_SIZE = 50;
  private equipmentLoadedPages = new Set<number>();
  private equipmentFilterKeyword = '';

  uploadedFiles = signal<UploadedFileItem[]>([]);

  submitting = signal(false);

  scanInProgress = signal(false);

  ocrMode: OcrMode = 'OcrAndExtract';

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

      onProgress,

      this.selectedEquipmentIds()

    );

  };



  ngOnInit(): void {

    this.loadDocumentTypes();

    this.loadEquipments();

  }

  private loadEquipments(): void {
    this.equipmentOptions.set([]);
    this.equipmentTotalCount.set(0);
    this.equipmentLoadedPages.clear();
    this.equipmentFilterKeyword = '';
    if (!this.infrastructureIds?.length) {
      return;
    }
    this.fetchEquipmentPage(1);
  }

  private fetchEquipmentPage(page: number): void {
    if (!this.infrastructureIds?.length || this.equipmentLoadedPages.has(page)) return;
    this.equipmentLoadedPages.add(page);
    this.loadingEquipments.set(true);
    this.dossierManagementService
      .getEquipmentLookup({
        infrastructureIds: this.infrastructureIds,
        keyword: this.equipmentFilterKeyword || undefined,
        page,
        pageSize: this.EQUIPMENT_PAGE_SIZE,
      })
      .pipe(finalize(() => this.loadingEquipments.set(false)))
      .subscribe({
        next: (res: any) => {
          const items = (res?.items ?? []).map((eq: any) => ({ id: eq.id, code: eq.code, name: eq.name }));
          const total = res?.totalCount ?? items.length;
          const next = this.equipmentOptions().length === total
            ? this.equipmentOptions().slice()
            : new Array(total);
          const offset = (page - 1) * this.EQUIPMENT_PAGE_SIZE;
          for (let i = 0; i < items.length; i++) {
            next[offset + i] = items[i];
          }
          this.equipmentTotalCount.set(total);
          this.equipmentOptions.set(next);
        },
        error: () => {
          this.equipmentLoadedPages.delete(page);
        },
      });
  }

  /** Cuộn tới vùng [first, last) chưa tải — tải (các) trang tương ứng. */
  onEquipmentLazyLoad(event: { first: number; last: number }): void {
    const firstPage = Math.floor(event.first / this.EQUIPMENT_PAGE_SIZE) + 1;
    const lastPage = Math.floor(Math.max(event.last - 1, 0) / this.EQUIPMENT_PAGE_SIZE) + 1;
    this.fetchEquipmentPage(firstPage);
    if (lastPage !== firstPage) {
      this.fetchEquipmentPage(lastPage);
    }
  }

  /** Gõ tìm kiếm trong select — gọi lại API theo từ khoá (dữ liệu đã tải chỉ là 1 phần nhỏ nên không
   * thể lọc client-side như p-multiSelect mặc định làm). */
  onEquipmentFilter(event: { filter: string }): void {
    this.equipmentFilterKeyword = event.filter?.trim() ?? '';
    this.equipmentOptions.set([]);
    this.equipmentTotalCount.set(0);
    this.equipmentLoadedPages.clear();
    this.fetchEquipmentPage(1);
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

    this.ocrMode = 'OcrAndExtract';
    this.extractionScope = 'FirstAndLastPage';

    this.submitting.set(false);

    this.scanInProgress.set(false);

    this.selectedEquipmentIds.set([]);

    this.uploadZone?.clearQueuedUploads();

    if (this.documentTypes().length === 0) {

      this.loadDocumentTypes();

    }

    this.loadEquipments();

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



    // File vừa quét nằm trong panel quét (xem trước) cho tới khi bấm "Lưu tài liệu" mới được đưa vào hàng đợi upload.
    // Người dùng bấm "Xong" luôn mà chưa bấm "Lưu tài liệu" → trước đây không có gì trong hàng đợi nên dialog đóng im lặng,
    // không gọi API nào. Tự lưu file đang chờ vào hàng đợi (saveDocument phát fileReady → onScannedFile → ingestFile đồng bộ).
    if (this.isScanMode && this.scannerPanel?.currentFile()) {
      this.scannerPanel.saveDocument();
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


