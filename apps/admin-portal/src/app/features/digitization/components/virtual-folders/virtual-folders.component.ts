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
import { environment } from '../../../../../environments/environment';

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
  template: `
    <div class="wf-page" [class.embedded]="isEmbedded">
      <p-toast></p-toast>

      <!-- Breadcrumb (Chỉ hiện khi chạy chế độ độc lập) -->
      <div class="breadcrumb mb-3" *ngIf="!isEmbedded">
        <i class="pi pi-home bc-icon"></i>
        <span class="bc-text">Trang chủ</span>
        <span class="bc-sep">/</span>
        <span class="bc-text">Số hóa hồ sơ</span>
        <span class="bc-sep">/</span>
        <span class="bc-current">Cây thư mục ảo Explorer</span>
      </div>

      <div class="explorer-container" [class.embedded-container]="isEmbedded">
        <!-- Cột trái: Cây thư mục ảo -->
        <div class="tree-sidebar">
          <div class="sidebar-header">
            <span class="font-bold text-sm" style="color: #002D72;">Cấu trúc thư mục</span>
            <button class="btn-primary-small" (click)="openAddFolderDialog(null)">
              <i class="pi pi-folder-plus"></i> Gốc
            </button>
          </div>

          <div class="tree-body">
            <div *ngIf="loadingTree" class="loading-state">
              <i class="pi pi-spin pi-spinner"></i> Đang tải dữ liệu...
            </div>
            
            <div *ngIf="!loadingTree && folderTree.length === 0" class="empty-state">
              Chưa có thư mục ảo nào. Hãy nhấn nút thêm thư mục gốc!
            </div>

            <!-- Cây thư mục đệ quy -->
            <div class="tree-nodes" *ngIf="!loadingTree && folderTree.length > 0">
              <ng-container *ngFor="let node of folderTree">
                <ng-container *ngTemplateOutlet="nodeTemplate; context: { $implicit: node, level: 0 }"></ng-container>
              </ng-container>
            </div>
          </div>
        </div>

        <!-- Cột phải: Explorer File Viewer -->
        <div class="file-explorer">
          <div class="explorer-header" *ngIf="selectedFolder">
            <div class="folder-title">
              <i class="pi pi-folder-open text-xl mr-2 text-primary"></i>
              <div>
                <h3 class="m-0 text-base font-bold" style="color: #002D72;">{{ selectedFolder.name }}</h3>
                <span class="text-xs text-muted">
                  Người tạo: {{ selectedFolder.createdBy || 'Hệ thống' }} | Ngày tạo: {{ selectedFolder.createdDate | date:'dd/MM/yyyy' }}
                </span>
              </div>
            </div>
            <div class="folder-actions">
              <button class="btn-outlined mr-2" (click)="downloadFolderZip(selectedFolder)">
                <i class="pi pi-file-excel mr-1"></i> Tải cả Folder (.zip)
              </button>
            </div>
          </div>

          <div class="explorer-body" *ngIf="selectedFolder">
            <!-- Vùng Kéo thả Upload File -->
            <div class="drop-zone" 
                 [class.drag-over]="isDraggingFile"
                 (dragover)="onDragOverFile($event)"
                 (dragleave)="onDragLeaveFile($event)"
                 (drop)="onDropFile($event)"
                 (click)="fileInput.click()">
              
              <input type="file" #fileInput style="display: none;" multiple (change)="onFileSelected($event)" />
              <i class="pi pi-upload text-3xl mb-2 text-muted"></i>
              <p class="font-bold m-0 text-sm">Kéo thả tài liệu vào đây hoặc nhấp chuột để tải lên</p>
              <p class="text-xs text-muted mt-1">Hỗ trợ các định dạng file đính kèm hồ sơ kỹ thuật.</p>
            </div>

            <!-- Danh sách tài liệu đính kèm -->
            <div class="file-list-section mt-4">
              <span class="font-bold text-sm block mb-2" style="color: #002D72; border-bottom: 2px solid #FF6B00; padding-bottom: 4px; display: inline-block;">
                Tài liệu trong thư mục ({{ files.length }} tệp)
              </span>

              <div *ngIf="loadingFiles" class="loading-state py-4 text-center">
                <i class="pi pi-spin pi-spinner"></i> Đang tải danh sách tệp tin...
              </div>

              <div class="table-responsive" *ngIf="!loadingFiles && files.length > 0">
                <table class="file-table">
                  <thead>
                    <tr>
                      <th>Tên tệp</th>
                      <th>Kích thước</th>
                      <th>Định dạng</th>
                      <th>Ngày tải lên</th>
                      <th>Người tải lên</th>
                      <th style="width: 120px; text-align: center;">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr *ngFor="let file of files">
                      <td>
                        <i class="pi pi-file mr-2 text-muted"></i>
                        <b class="text-secondary text-sm">{{ file.fileName }}</b>
                      </td>
                      <td class="text-xs">{{ formatFileSize(file.fileSize) }}</td>
                      <td><span class="badge-mime text-xs">{{ file.contentType }}</span></td>
                      <td class="text-xs">{{ file.uploadedAt | date:'dd/MM/yyyy HH:mm' }}</td>
                      <td class="text-xs">{{ file.uploadedBy }}</td>
                      <td style="text-align: center;">
                        <button class="btn-icon-blue mr-1" (click)="downloadSingleFile(file)" title="Tải xuống tệp">
                          <i class="pi pi-download"></i>
                        </button>
                        <button class="btn-icon-red" (click)="removeFile(file)" title="Gỡ bỏ tài liệu">
                          <i class="pi pi-trash"></i>
                        </button>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>

              <div *ngIf="!loadingFiles && files.length === 0" class="empty-files text-sm">
                Chưa có tài liệu nào trong thư mục này. Hãy kéo thả file để tải lên!
              </div>
            </div>
          </div>

          <div class="explorer-empty-body" *ngIf="!selectedFolder">
            <i class="pi pi-folder text-5xl mb-3 text-muted"></i>
            <h4 class="text-sm">Hãy chọn một thư mục từ cây thư mục bên trái để khám phá tài liệu</h4>
          </div>
        </div>
      </div>

      <!-- Template đệ quy hiển thị các Node cây -->
      <ng-template #nodeTemplate let-node let-level="level">
        <div class="tree-node-wrapper" 
             [style.padding-left.px]="level * 14"
             [class.active]="selectedFolder?.id === node.id"
             draggable="true"
             (dragstart)="onDragStartFolder($event, node)"
             (dragover)="onDragOverFolder($event, node)"
             (drop)="onDropFolder($event, node)">
          
          <span class="expand-icon" (click)="toggleNode(node); $event.stopPropagation();">
            <i class="pi" *ngIf="node.children.length > 0" 
               [ngClass]="node.isExpanded ? 'pi-chevron-down' : 'pi-chevron-right'"></i>
            <i class="pi pi-circle" *ngIf="node.children.length === 0" style="font-size: 0.4rem; color: #94a3b8; opacity: 0.5;"></i>
          </span>

          <span class="node-content" (click)="selectFolder(node)">
            <i class="pi mr-2" 
               [ngClass]="node.children.length > 0 ? (node.isExpanded ? 'pi-folder-open text-primary' : 'pi-folder text-primary') : 'pi-folder text-muted'"></i>
            <span class="node-label text-sm">{{ node.name }}</span>
          </span>

          <span class="node-actions">
            <i class="pi pi-plus-circle text-green" (click)="openAddFolderDialog(node); $event.stopPropagation();" title="Thêm thư mục con"></i>
            <i class="pi pi-pencil text-blue" (click)="openEditFolderDialog(node); $event.stopPropagation();" title="Đổi tên"></i>
            <i class="pi pi-trash text-red" (click)="deleteFolder(node); $event.stopPropagation();" title="Xóa thư mục"></i>
          </span>
        </div>

        <div class="tree-node-children" *ngIf="node.isExpanded && node.children.length > 0">
          <ng-container *ngFor="let child of node.children">
            <ng-container *ngTemplateOutlet="nodeTemplate; context: { $implicit: child, level: level + 1 }"></ng-container>
          </ng-container>
        </div>
      </ng-template>

      <!-- Dialog Thêm/Sửa thư mục -->
      <p-dialog [(visible)]="displayFolderDialog" [header]="folderDialogHeader" [modal]="true" [style]="{ width: '400px' }">
        <div style="display: flex; flex-direction: column; gap: 14px; padding-top: 10px;">
          <div class="form-group">
            <label class="form-label" style="font-weight:600;">Tên thư mục <span class="required">*</span></label>
            <input type="text" pInputText class="w-full" [(ngModel)]="folderNameInput" placeholder="Ví dụ: Hồ sơ Trạm biến áp 110kV, Bản vẽ..." />
          </div>
        </div>
        <ng-template #footer>
          <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
            <button pButton label="Hủy" class="p-button-outlined" (click)="displayFolderDialog = false"></button>
            <button pButton label="Lưu" class="p-button-primary" (click)="saveFolder()"></button>
          </div>
        </ng-template>
      </p-dialog>

    </div>
  `,
  styles: [`
    .explorer-container {
      display: grid;
      grid-template-columns: 280px 1fr;
      gap: 1.5rem;
      background-color: white;
      border-radius: 12px;
      border: 1px solid #e2e8f0;
      min-height: calc(100vh - 200px);
      box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05);
      overflow: hidden;
    }
    .embedded-container {
      min-height: 450px !important;
      border: 1px solid #e2e8f0;
    }
    .tree-sidebar {
      border-right: 1px solid #e2e8f0;
      padding: 1rem;
      background-color: #f8fafc;
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .sidebar-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-bottom: 2px solid #002D72;
      padding-bottom: 10px;
    }
    .tree-body {
      flex: 1;
      overflow-y: auto;
    }
    .tree-nodes {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .tree-node-wrapper {
      display: flex;
      align-items: center;
      padding: 6px 8px;
      border-radius: 6px;
      cursor: pointer;
      user-select: none;
      transition: all 0.2s ease;
      position: relative;
    }
    .tree-node-wrapper:hover {
      background-color: rgba(0, 45, 114, 0.05);
    }
    .tree-node-wrapper:hover .node-actions {
      display: flex;
    }
    .tree-node-wrapper.active {
      background-color: rgba(0, 45, 114, 0.1);
      font-weight: 600;
    }
    .expand-icon {
      width: 20px;
      display: flex;
      align-items: center;
      justify-content: center;
      height: 20px;
    }
    .expand-icon i {
      font-size: 0.75rem;
      color: #64748b;
    }
    .node-content {
      display: flex;
      align-items: center;
      flex: 1;
      min-width: 0;
      padding: 0 4px;
    }
    .node-label {
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      font-size: 0.85rem;
    }
    .node-actions {
      display: none;
      gap: 6px;
      align-items: center;
      padding-left: 8px;
    }
    .node-actions i {
      font-size: 0.95rem;
      cursor: pointer;
      opacity: 0.8;
      transition: opacity 0.2s;
    }
    .node-actions i:hover {
      opacity: 1;
    }
    .text-green { color: #22c55e; }
    .text-blue { color: #3b82f6; }
    .text-red { color: #ef4444; }

    .file-explorer {
      padding: 1rem;
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
    }
    .explorer-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-bottom: 1px solid #e2e8f0;
      padding-bottom: 1rem;
    }
    .folder-title {
      display: flex;
      align-items: center;
    }
    .drop-zone {
      border: 2px dashed #cbd5e1;
      border-radius: 8px;
      padding: 1.5rem;
      text-align: center;
      background-color: #f8fafc;
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .drop-zone.drag-over {
      border-color: #002D72;
      background-color: rgba(0, 45, 114, 0.05);
    }
    .file-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.85rem;
    }
    .file-table th {
      background-color: #f1f5f9;
      color: #475569;
      font-weight: 600;
      text-align: left;
      padding: 10px 12px;
      border-bottom: 1px solid #cbd5e1;
    }
    .file-table td {
      padding: 10px 12px;
      border-bottom: 1px solid #e2e8f0;
    }
    .badge-mime {
      background-color: #f1f5f9;
      color: #475569;
      padding: 2px 6px;
      border-radius: 4px;
      font-family: monospace;
    }
    .explorer-empty-body {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      flex: 1;
      color: #94a3b8;
      padding: 3rem 0;
    }
    .btn-primary-small {
      background-color: #002D72;
      color: white;
      border: none;
      padding: 4px 10px;
      border-radius: 6px;
      font-size: 0.8rem;
      cursor: pointer;
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .btn-icon-blue, .btn-icon-red {
      border: none;
      background: transparent;
      cursor: pointer;
      font-size: 1rem;
      padding: 4px;
      border-radius: 4px;
    }
    .btn-icon-blue { color: #3b82f6; }
    .btn-icon-blue:hover { background-color: rgba(59, 130, 246, 0.1); }
    .btn-icon-red { color: #ef4444; }
    .btn-icon-red:hover { background-color: rgba(239, 68, 68, 0.1); }

    .empty-files {
      text-align: center;
      padding: 2rem;
      color: #94a3b8;
      border: 1px dashed #e2e8f0;
      border-radius: 8px;
    }
    .required {
      color: #ef4444;
    }
  `]
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
