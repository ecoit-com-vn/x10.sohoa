import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { FormsModule } from '@angular/forms';
import { environment } from '@env/environment';

@Component({
  selector: 'app-borrow-return',
  standalone: true,
  imports: [CommonModule, HttpClientModule, ToastModule, FormsModule],
  providers: [MessageService],
  templateUrl: './borrow-return.component.html',
  styleUrl: './borrow-return.component.scss'
})
export class BorrowReturnComponent implements OnInit {
  requests: any[] = [];
  loading = false;
  
  currentPage = 1;
  pageSize = 10;

  private http = inject(HttpClient);
  private messageService = inject(MessageService);
  private apiUrl = `${environment.apiGatewayUrl}/api/v1/workflows`;

  ngOnInit() {
    this.loadRequests();
  }

  loadRequests() {
    this.loading = true;
    this.http.get<any[]>(this.apiUrl).subscribe({
      next: (data) => {
        this.requests = (data || []).map(item => ({
          id: item.id,
          requester: item.requesterId || 'Chuyên viên kỹ thuật',
          recordName: item.dossierId,
          createdAt: new Date(item.requestDate),
          status: this.mapBackendState(item.state)
        }));
        this.loading = false;
        this.currentPage = 1;
      },
      error: (err) => {
        console.error('Error loading requests via API', err);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi tải dữ liệu',
          detail: 'Không thể kết nối tới máy chủ để tải yêu cầu mượn/trả.'
        });
        this.requests = [];
        this.loading = false;
      }
    });
  }

  mapBackendState(state: any): string {
    switch (state) {
      case 0:
      case 'Requested':
        return 'PENDING';
      case 1:
      case 'Approved':
        return 'APPROVED';
      case 2:
      case 'Borrowed':
        return 'BORROWED';
      case 3:
      case 'Returned':
        return 'RETURNED';
      default:
        return 'PENDING';
    }
  }

  mapStateToBackendEnum(status: string): number {
    switch (status) {
      case 'APPROVED': return 1;
      case 'BORROWED': return 2;
      case 'RETURNED': return 3;
      default: return 0;
    }
  }

  get paginatedRequests(): any[] {
    const startIndex = (this.currentPage - 1) * this.pageSize;
    return this.requests.slice(startIndex, startIndex + this.pageSize);
  }

  get totalPages(): number {
    return Math.ceil(this.requests.length / this.pageSize);
  }

  nextPage() {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
    }
  }

  prevPage() {
    if (this.currentPage > 1) {
      this.currentPage--;
    }
  }

  onPageSizeChange(event: any) {
    this.pageSize = Number(event.target.value);
    this.currentPage = 1;
  }

  updateStatus(id: string, status: string) {
    const backendState = this.mapStateToBackendEnum(status);
    
    this.http.put(`${this.apiUrl}/${id}/state`, backendState).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success', 
          summary: 'Thành công', 
          detail: `Đã phê duyệt cập nhật trạng thái yêu cầu thành công!`
        });
        this.loadRequests();
      },
      error: (err) => {
        console.error('Error updating status via API', err);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể cập nhật trạng thái yêu cầu trên hệ thống.'
        });
      }
    });
  }

  getStatusLabel(status: string): string {
    switch (status) {
      case 'PENDING': return 'Chờ phê duyệt';
      case 'APPROVED': return 'Đã duyệt';
      case 'BORROWED': return 'Đang mượn';
      case 'RETURNED': return 'Đã trả';
      case 'REJECTED': return 'Từ chối';
      default: return status;
    }
  }
}
