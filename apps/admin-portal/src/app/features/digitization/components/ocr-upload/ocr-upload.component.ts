import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FileUploadModule, FileUploadHandlerEvent } from 'primeng/fileupload';
import { CardModule } from 'primeng/card';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-ocr-upload',
  standalone: true,
  imports: [CommonModule, FileUploadModule, CardModule, ToastModule],
  providers: [MessageService],
  templateUrl: './ocr-upload.component.html',
  styleUrls: ['./ocr-upload.component.scss']
})
export class OcrUploadComponent implements OnInit {
  
  allowedExtensions = 'pdf,png,jpg,jpeg';
  maxFileSizeMb = 20;
  acceptTypes = 'image/*,application/pdf';

  private messageService = inject(MessageService);
  private http = inject(HttpClient);

  ngOnInit() {
    this.loadUploadConfig();
  }

  loadUploadConfig() {
    this.http.get<any>(`${environment.apiGatewayUrl}/api/v1/upload-configs/module/OCR`).subscribe({
      next: (config) => {
        if (config) {
          this.allowedExtensions = config.allowedExtensions;
          this.maxFileSizeMb = config.maxFileSizeMb;
          this.acceptTypes = config.allowedExtensions.split(',').map((ext: string) => '.' + ext.trim()).join(',');
        }
      },
      error: (err) => {
        console.warn('Cannot load upload configuration for OCR, using defaults', err);
      }
    });
  }

  onUpload(event: FileUploadHandlerEvent) {
    const allowedList = this.allowedExtensions.split(',').map(e => e.trim().toLowerCase());
    
    // Validate file extension and size
    for (const file of event.files) {
      const ext = file.name.split('.').pop()?.toLowerCase() || '';
      if (!allowedList.includes(ext)) {
        this.messageService.add({
          severity: 'error', 
          summary: 'Định dạng không được phép', 
          detail: `Tệp ${file.name} không đúng định dạng cho phép (${this.allowedExtensions.toUpperCase()}).`
        });
        return;
      }
      
      const fileSizeMb = file.size / (1024 * 1024);
      if (fileSizeMb > this.maxFileSizeMb) {
        this.messageService.add({
          severity: 'error', 
          summary: 'Dung lượng quá lớn', 
          detail: `Tệp ${file.name} có kích thước ${fileSizeMb.toFixed(2)}MB vượt quá giới hạn tối đa ${this.maxFileSizeMb}MB.`
        });
        return;
      }
    }

    const formData = new FormData();
    for (const file of event.files) {
      formData.append('files', file);
    }

    this.http.post(`${environment.apiGatewayUrl}/api/v1/digitization/upload`, formData).subscribe({
      next: () => {
        this.messageService.add({severity: 'info', summary: 'Thành công', detail: 'Đã tải lên tệp để xử lý OCR'});
      },
      error: (err) => {
        this.messageService.add({severity: 'error', summary: 'Lỗi', detail: 'Tải lên tệp thất bại'});
        console.error('Upload error', err);
      }
    });
  }
}
