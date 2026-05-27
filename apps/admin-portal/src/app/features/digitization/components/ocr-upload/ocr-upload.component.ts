import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FileUploadModule } from 'primeng/fileupload';
import { CardModule } from 'primeng/card';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';

@Component({
  selector: 'app-ocr-upload',
  standalone: true,
  imports: [CommonModule, FileUploadModule, CardModule, ToastModule],
  providers: [MessageService],
  templateUrl: './ocr-upload.component.html',
  styleUrls: ['./ocr-upload.component.scss']
})
export class OcrUploadComponent {
  
  constructor(private messageService: MessageService) {}

  onUpload(event: any) {
    this.messageService.add({severity: 'info', summary: 'Success', detail: 'File Uploaded for OCR Processing'});
  }
}
