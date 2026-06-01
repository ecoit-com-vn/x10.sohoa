// apps/admin-portal/src/app/features/error/global-error.component.ts
import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';

@Component({
  selector: 'app-global-error',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="error-container">
      <div class="error-card">
        <div class="error-icon">
          <i class="pi" [ngClass]="errorIcon"></i>
        </div>
        <h1 class="error-code">{{ errorCode }}</h1>
        <h2 class="error-title">{{ errorTitle }}</h2>
        <p class="error-message">{{ errorMessage }}</p>
        
        <div class="error-actions">
          <button class="btn-primary" (click)="goHome()">
            <i class="pi pi-home mr-1"></i> Quay về Trang chủ
          </button>
          <button class="btn-outlined" (click)="goBack()">
            <i class="pi pi-arrow-left mr-1"></i> Trở về trang trước
          </button>
        </div>
      </div>
    </div>
  `,
  styles: `
    .error-container {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 80vh;
      padding: 20px;
      font-family: 'Inter', system-ui, -apple-system, sans-serif;
    }
    .error-card {
      background: #ffffff;
      border-radius: 12px;
      box-shadow: 0 10px 25px rgba(0, 45, 114, 0.08);
      padding: 40px 30px;
      text-align: center;
      max-width: 500px;
      width: 100%;
      border: 1px solid #e2e8f0;
      transition: transform 0.3s;
    }
    :global(html.dark-mode) .error-card {
      background: #1e293b;
      border-color: #334155;
      box-shadow: 0 10px 25px rgba(0, 0, 0, 0.3);
    }
    .error-icon {
      font-size: 4rem;
      color: #ea580c;
      margin-bottom: 20px;
    }
    .error-code {
      font-size: 6rem;
      font-weight: 800;
      color: #002D72;
      line-height: 1;
      margin: 0 0 10px 0;
    }
    :global(html.dark-mode) .error-code {
      color: #3b82f6;
    }
    .error-title {
      font-size: 1.5rem;
      font-weight: 700;
      color: #1e293b;
      margin: 0 0 15px 0;
    }
    :global(html.dark-mode) .error-title {
      color: #f1f5f9;
    }
    .error-message {
      font-size: 0.95rem;
      color: #64748b;
      margin: 0 0 30px 0;
      line-height: 1.6;
    }
    :global(html.dark-mode) .error-message {
      color: #94a3b8;
    }
    .error-actions {
      display: flex;
      gap: 15px;
      justify-content: center;
      flex-wrap: wrap;
    }
    .btn-primary {
      height: 42px;
      padding: 0 20px;
      background: #002D72;
      color: #ffffff;
      border: none;
      border-radius: 6px;
      font-size: 0.9rem;
      font-weight: 600;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      gap: 8px;
      transition: background 0.2s;
    }
    .btn-primary:hover {
      background: #001f4d;
    }
    :global(html.dark-mode) .btn-primary {
      background: #2563eb;
    }
    :global(html.dark-mode) .btn-primary:hover {
      background: #1d4ed8;
    }
    .btn-outlined {
      height: 42px;
      padding: 0 20px;
      background: transparent;
      color: #475569;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.9rem;
      font-weight: 600;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      gap: 8px;
      transition: all 0.2s;
    }
    .btn-outlined:hover {
      background: #f8fafc;
      border-color: #94a3b8;
    }
    :global(html.dark-mode) .btn-outlined {
      color: #94a3b8;
      border-color: #475569;
    }
    :global(html.dark-mode) .btn-outlined:hover {
      background: #334155;
      border-color: #64748b;
    }
    .mr-1 {
      margin-right: 4px;
    }
  `
})
export class GlobalErrorComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  errorCode = '404';
  errorTitle = 'Không tìm thấy trang';
  errorMessage = 'Đường dẫn bạn truy cập không tồn tại hoặc đã bị di chuyển.';
  errorIcon = 'pi-exclamation-circle';

  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      const code = params['code'] || '404';
      this.errorCode = code;
      this.updateErrorDetails(code);
    });
  }

  private updateErrorDetails(code: string) {
    switch (code) {
      case '403':
        this.errorTitle = 'Truy cập bị từ chối';
        this.errorMessage = 'Tài khoản của bạn không có đủ quyền hạn để truy cập tài nguyên này.';
        this.errorIcon = 'pi-shield';
        break;
      case '500':
        this.errorTitle = 'Lỗi hệ thống máy chủ';
        this.errorMessage = 'Đã có lỗi xảy ra từ phía hệ thống. Quản trị viên đã được thông báo.';
        this.errorIcon = 'pi-server';
        break;
      case '404':
      default:
        this.errorCode = '404';
        this.errorTitle = 'Không tìm thấy trang';
        this.errorMessage = 'Đường dẫn bạn truy cập không tồn tại hoặc đã bị di chuyển khỏi hệ thống.';
        this.errorIcon = 'pi-exclamation-circle';
        break;
    }
  }

  goHome() {
    this.router.navigate(['/dashboard']);
  }

  goBack() {
    if (typeof window !== 'undefined' && window.history.length > 1) {
      window.history.back();
    } else {
      this.router.navigate(['/']);
    }
  }
}
