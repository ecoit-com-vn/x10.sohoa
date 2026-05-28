import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FileUploadModule, FileUploadHandlerEvent } from 'primeng/fileupload';
import { CardModule } from 'primeng/card';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-ocr-upload',
  standalone: true,
  imports: [CommonModule, FileUploadModule, CardModule, ToastModule],
  providers: [MessageService],
  templateUrl: './ocr-upload.component.html',
  styleUrls: ['./ocr-upload.component.scss']
})
export class OcrUploadComponent {
  
  private messageService = inject(MessageService);
  private http = inject(HttpClient);

  onUpload(event: FileUploadHandlerEvent) {
    const formData = new FormData();
    for (const file of event.files) {
      formData.append('files', file);
    }

    this.http.post('http://localhost:5000/api/v1/digitization/upload', formData).subscribe({
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
