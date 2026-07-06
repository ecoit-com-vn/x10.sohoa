import { Routes } from '@angular/router';
import { ReportsComponent } from './components/reports/reports.component';
import { reportDossierGuard } from './guards/report-dossier.guard';
import { ReportDossierType } from './data-access/report-dossier.config';

const reportListRoute = (reportType: ReportDossierType, permission: string) => ({
  path: reportType,
  loadComponent: () =>
    import('./components/report-dossier-list/report-dossier-list.component').then(
      (m) => m.ReportDossierListComponent
    ),
  canActivate: [reportDossierGuard(permission)],
  data: { reportType }
});

const reportDetailRoute = (reportType: ReportDossierType, permission: string) => ({
  path: `${reportType}/:id`,
  loadComponent: () =>
    import('./components/report-dossier-detail/report-dossier-detail.component').then(
      (m) => m.ReportDossierDetailComponent
    ),
  canActivate: [reportDossierGuard(permission)],
  data: { reportType }
});

export const REPORTS_ROUTES: Routes = [
  {
    path: '',
    component: ReportsComponent
  },
  reportListRoute('dossier-by-grid-type', 'REPORT_DOSSIER_BY_GRIDTYPE_VIEW'),
  reportDetailRoute('dossier-by-grid-type', 'REPORT_DOSSIER_BY_GRIDTYPE_VIEW'),
  reportListRoute('dossier-by-equipment', 'REPORT_DOSSIER_BY_EQUIPMENT_VIEW'),
  reportDetailRoute('dossier-by-equipment', 'REPORT_DOSSIER_BY_EQUIPMENT_VIEW'),
  reportListRoute('dossier-by-station', 'REPORT_DOSSIER_BY_STATION_VIEW'),
  reportDetailRoute('dossier-by-station', 'REPORT_DOSSIER_BY_STATION_VIEW'),
  reportListRoute('dossier-by-line', 'REPORT_DOSSIER_BY_LINE_VIEW'),
  reportDetailRoute('dossier-by-line', 'REPORT_DOSSIER_BY_LINE_VIEW')
];
