// sohoa.frontend/libs/features/reports/src/lib/components/reports/reports.component.ts
import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { AuthService } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [
    CommonModule, 
    ToastModule,
    WfBreadcrumbComponent
  ],
  providers: [MessageService],
  templateUrl: './reports.component.html'
})
export class ReportsComponent {
  private router = inject(Router);
  public authService = inject(AuthService);

  // Danh sách các báo cáo hệ thống tĩnh hiện có link trên FE
  reportsList = [
    {
      name: 'Báo cáo thống kê hồ sơ thiết bị theo lưới điện áp',
      icon: 'pi pi-bolt',
      url: '/reports/dossier-by-grid-type',
      permission: 'REPORT_DOSSIER_BY_GRIDTYPE_VIEW',
      description: 'Thống kê chi tiết hồ sơ thiết bị phân theo các cấp lưới điện áp EVNHANOI.'
    },
    {
      name: 'Báo cáo thống kê hồ sơ thiết bị theo thiết bị',
      icon: 'pi pi-server',
      url: '/reports/dossier-by-equipment',
      permission: 'REPORT_DOSSIER_BY_EQUIPMENT_VIEW',
      description: 'Thống kê số lượng và thông tin chi tiết hồ sơ phân bổ theo từng loại thiết bị.'
    },
    {
      name: 'Báo cáo hồ sơ thiết bị theo đường dây',
      icon: 'pi pi-share-alt',
      url: '/reports/dossier-by-line',
      permission: 'REPORT_DOSSIER_BY_LINE_VIEW',
      description: 'Xem báo cáo chi tiết và thống kê hồ sơ thiết bị theo các tuyến đường dây tải điện.'
    }
  ];

  navigateToReport(url: string) {
    this.router.navigate([url]);
  }
}
