// apps/admin-portal/src/app/features/error/global-error.component.ts
import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';

@Component({
  selector: 'app-global-error',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './global-error.component.html',
  styleUrl: './global-error.component.scss'
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
