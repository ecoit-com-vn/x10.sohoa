// E:\ecoit\sohoax10\sohoa.frontend\apps\admin-portal\src\app\features\reports\reports.component.ts
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { ReportViewerComponent } from '../report-viewer/report-viewer.component';
import { ReportDesignerComponent } from '../report-designer/report-designer.component';
import { ReportGroupManagerComponent } from '../report-group-manager/report-group-manager.component';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [
    CommonModule, 
    ToastModule,
    ReportViewerComponent,
    ReportDesignerComponent,
    ReportGroupManagerComponent
  ],
  providers: [MessageService],
  templateUrl: './reports.component.html'
})
export class ReportsComponent {
  activeTab = 'view';
}
