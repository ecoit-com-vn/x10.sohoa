import { Component, OnInit, inject, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { environment } from '@env/environment';

interface FolderNode {
  id: number;
  name: string;
  parentId?: number | null;
  unitId?: number | null;
  equipmentId?: string | null;
  createdBy?: string;
  createdDate?: string;
  children: FolderNode[];
  isExpanded?: boolean;
}

@Component({
  selector: 'app-virtual-folders',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    DialogModule,
    ButtonModule,
    InputTextModule,
    TooltipModule
  ],
  providers: [MessageService],
  templateUrl: './virtual-folders.component.html',
  styleUrl: './virtual-folders.component.scss'
})
export class VirtualFoldersComponent implements OnInit {
  @Input() equipmentId: string | null = null;
  @Input() isEmbedded = false;

  folderTree: FolderNode[] = [];
  selectedFolder: FolderNode | null = null;
  files: any[] = [];

  loadingTree = false;
  loadingFiles = false;
  isDraggingFile = false;

  displayFolderDialog = false;
  folderDialogHeader = '';
  folderNameInput = '';
  folderActionTarget: FolderNode | null = null;
  folderActionParent: FolderNode | null = null;
  isEditFolderMode = false;

  draggedFolder: FolderNode | null = null;

  private http = inject(HttpClient);
  private messageService = inject(MessageService);

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/folders`;

  ngOnInit() {
    this.loadFolderTree();
  }

  loadFolderTree() {
    this.loadingTree = true;
    const params: any = {};
    if (this.equipmentId) {
      params.equipmentId = this.equipmentId;
    }

    this.http.get<FolderNode[]>(`${this.apiUrl}/tree`, { params }).subscribe({
      next: (data) => {
        this.folderTree = data || [];
        this.loadingTree = false;
        if (this.selectedFolder) {
          const found = this.findNodeById(this.folderTree, this.selectedFolder.id);
          if (found) {
            this.selectedFolder = found;
          } else {
            this.selectedFolder = null;
          }
        }
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải cây thư mục.' });
        this.loadingTree = false;
      }
    });
  }

  findNodeById(nodes: FolderNode[], id: number): FolderNode | null {
    for (const node of nodes) {
      if (node.id === id) return node;
      const childMatch = this.findNodeById(node.children || [], id);
      if (childMatch) return childMatch;
    }
    return null;
  }

  toggleNode(node: FolderNode) {
    node.isExpanded = !node.isExpanded;
  }

  selectFolder(node: FolderNode) {
    this.selectedFolder = node;
    this.loadFilesInFolder(node.id);
  }

  loadFilesInFolder(folderId: number) {
    this.loadingFiles = true;
    this.http.get<any[]>(`${this.apiUrl}/${folderId}/files`).subscribe({
      next: (data) => {
        this.files = data || [];
        this.loadingFiles = false;
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách tệp tin.' });
        this.loadingFiles = false;
      }
    });
  }

  openAddFolderDialog(parent: FolderNode | null) {
    this.isEditFolderMode = false;
    this.folderActionParent = parent;
    this.folderNameInput = '';
    this.folderDialogHeader = parent ? `Thêm thư mục con vào '${parent.name}'` : 'Thêm thư mục gốc';
    this.displayFolderDialog = true;
  }

  openEditFolderDialog(node: FolderNode) {
    this.isEditFolderMode = true;
    this.folderActionTarget = node;
    this.folderNameInput = node.name;
    this.folderDialogHeader = `Đổi tên thư mục '${node.name}'`;
    this.displayFolderDialog = true;
  }

  saveFolder() {
    if (!this.folderNameInput.trim()) {
      this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Vui lòng nhập tên thư mục.' });
      return;
    }

    if (this.isEditFolderMode && this.folderActionTarget) {
      const body = {
        name: this.folderNameInput.trim(),
        parentId: this.folderActionTarget.parentId,
        equipmentId: this.equipmentId
      };
      this.http.put(`${this.apiUrl}/${this.folderActionTarget.id}`, body).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã đổi tên thư mục!' });
          this.loadFolderTree();
          this.displayFolderDialog = false;
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể đổi tên thư mục.' });
        }
      });
    } else {
      const body = {
        name: this.folderNameInput.trim(),
        parentId: this.folderActionParent ? this.folderActionParent.id : null,
        equipmentId: this.equipmentId
      };
      this.http.post(this.apiUrl, body).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã tạo thư mục thành công!' });
          this.loadFolderTree();
          if (this.folderActionParent) {
            this.folderActionParent.isExpanded = true;
          }
          this.displayFolderDialog = false;
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tạo thư mục.' });
        }
      });
    }
  }

  deleteFolder(node: FolderNode) {
    if (confirm(`Bạn có chắc chắn muốn xóa thư mục '${node.name}'? Mọi thư mục con và liên kết tài liệu sẽ bị xóa.`)) {
      this.http.delete(`${this.apiUrl}/${node.id}`).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xóa thư mục!' });
          if (this.selectedFolder?.id === node.id) {
            this.selectedFolder = null;
            this.files = [];
          }
          this.loadFolderTree();
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể xóa thư mục.' });
        }
      });
    }
  }

  onDragStartFolder(event: DragEvent, node: FolderNode) {
    this.draggedFolder = node;
    event.dataTransfer?.setData('text/plain', node.id.toString());
  }

  onDragOverFolder(event: DragEvent, node: FolderNode) {
    event.preventDefault();
  }

  onDropFolder(event: DragEvent, targetNode: FolderNode) {
    event.preventDefault();
    if (!this.draggedFolder || this.draggedFolder.id === targetNode.id) return;

    if (this.isDescendant(this.draggedFolder, targetNode)) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Không thể di chuyển thư mục cha vào thư mục con của nó.' });
      return;
    }

    const body = {
      name: this.draggedFolder.name,
      parentId: targetNode.id,
      equipmentId: this.equipmentId
    };

    this.http.put(`${this.apiUrl}/${this.draggedFolder.id}`, body).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: `Đã di chuyển thư mục '${this.draggedFolder!.name}'` });
        this.loadFolderTree();
        this.draggedFolder = null;
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể di chuyển thư mục.' });
      }
    });
  }

  isDescendant(parent: FolderNode, child: FolderNode): boolean {
    if (!parent.children) return false;
    for (const node of parent.children) {
      if (node.id === child.id) return true;
      if (this.isDescendant(node, child)) return true;
    }
    return false;
  }

  onDragOverFile(event: DragEvent) {
    event.preventDefault();
    this.isDraggingFile = true;
  }

  onDragLeaveFile(event: DragEvent) {
    event.preventDefault();
    this.isDraggingFile = false;
  }

  onDropFile(event: DragEvent) {
    event.preventDefault();
    this.isDraggingFile = false;
    const filesList = event.dataTransfer?.files;
    if (filesList && filesList.length > 0) {
      this.uploadFiles(filesList);
    }
  }

  onFileSelected(event: any) {
    const filesList = event.target.files;
    if (filesList && filesList.length > 0) {
      this.uploadFiles(filesList);
    }
  }

  uploadFiles(files: FileList) {
    if (!this.selectedFolder) return;

    const formData = new FormData();
    for (let i = 0; i < files.length; i++) {
      formData.append('files', files[i]);
    }

    this.loadingFiles = true;
    this.http.post(`${this.apiUrl}/${this.selectedFolder.id}/files`, formData).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Tải lên', detail: 'Đã tải lên tệp tin thành công!' });
        this.loadFilesInFolder(this.selectedFolder!.id);
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải lên tệp tin.' });
        this.loadingFiles = false;
      }
    });
  }

  removeFile(file: any) {
    if (!this.selectedFolder) return;
    if (confirm(`Bạn có chắc muốn gỡ bỏ tài liệu '${file.fileName}' khỏi thư mục này?`)) {
      this.http.delete(`${this.apiUrl}/${this.selectedFolder.id}/files/${file.id}`).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã gỡ tài liệu khỏi thư mục!' });
          this.loadFilesInFolder(this.selectedFolder!.id);
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể gỡ tài liệu.' });
        }
      });
    }
  }

  downloadSingleFile(file: any) {
    const downloadUrl = `${environment.apiGatewayUrl}/api/v1/digitization/download?filePath=${encodeURIComponent(file.filePath)}`;
    
    this.http.get(downloadUrl, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = file.fileName;
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(url);
        a.remove();
      },
      error: () => {
        window.open(downloadUrl, '_blank');
      }
    });
  }

  downloadFolderZip(folder: FolderNode) {
    this.messageService.add({ severity: 'info', summary: 'Đang chuẩn bị', detail: 'Hệ thống đang nén zip thư mục, vui lòng chờ...' });
    const downloadUrl = `${this.apiUrl}/${folder.id}/download-zip`;
    
    this.http.get(downloadUrl, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${folder.name.replace(/ /g, '_')}.zip`;
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(url);
        a.remove();
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã tải xuống file zip thư mục!' });
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Tạo file nén thất bại.' });
      }
    });
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }
}
