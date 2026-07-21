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
  {
    path: 'groups',
    loadComponent: () =>
      import('./components/report-groups/report-groups.component').then(
        (m) => m.ReportGroupsComponent
      ),
    canActivate: [reportDossierGuard('REPORT_GROUP_VIEW')]
  },
  {
    path: 'groups/:id',
    loadComponent: () =>
      import('./components/report-group-detail/report-group-detail.component').then(
        (m) => m.ReportGroupDetailComponent
      ),
    canActivate: [reportDossierGuard('REPORT_GROUP_VIEW')]
  },
  {
    path: 'unit-publish',
    loadComponent: () =>
      import('./components/report-unit-publish/report-unit-publish.component').then(
        (m) => m.ReportUnitPublishComponent
      ),
    canActivate: [reportDossierGuard('REPORT_UNIT_PUBLISH_VIEW')]
  },
  {
    path: 'statistics',
    loadComponent: () =>
      import('./components/report-statistics/report-statistics.component').then(
        (m) => m.ReportStatisticsComponent
      ),
    canActivate: [reportDossierGuard('REPORT_STATISTICS_VIEW')]
  },
  reportListRoute('dossier-by-grid-type', 'REPORT_DOSSIER_BY_GRIDTYPE_VIEW'),
  reportDetailRoute('dossier-by-grid-type', 'REPORT_DOSSIER_BY_GRIDTYPE_VIEW'),
  reportListRoute('dossier-by-equipment', 'REPORT_DOSSIER_BY_EQUIPMENT_VIEW'),
  reportDetailRoute('dossier-by-equipment', 'REPORT_DOSSIER_BY_EQUIPMENT_VIEW'),
  {
    path: 'dossier-by-year',
    loadComponent: () =>
      import('./components/report-dossier-by-year/report-dossier-by-year.component').then(
        (m) => m.ReportDossierByYearComponent
      ),
    canActivate: [reportDossierGuard('REPORT_STATISTICS_VIEW')]
  },
  {
    path: 'dossier-by-month',
    loadComponent: () =>
      import('./components/report-dossier-by-month/report-dossier-by-month.component').then(
        (m) => m.ReportDossierByMonthComponent
      ),
    canActivate: [reportDossierGuard('REPORT_STATISTICS_VIEW')]
  },
  {
    path: 'dossier-by-voltage-grid',
    loadComponent: () =>
      import('./components/report-dossier-by-voltage-grid/report-dossier-by-voltage-grid.component').then(
        (m) => m.ReportDossierByVoltageGridComponent
      ),
    canActivate: [reportDossierGuard('REPORT_STATISTICS_VIEW')]
  },
  {
    path: 'dossier-by-equipment-type',
    loadComponent: () =>
      import('./components/report-dossier-by-equipment-type/report-dossier-by-equipment-type.component').then(
        (m) => m.ReportDossierByEquipmentTypeComponent
      ),
    canActivate: [reportDossierGuard('REPORT_STATISTICS_VIEW')]
  },
  {
    path: 'dossier-by-allocation',
    loadComponent: () =>
      import('./components/report-dossier-by-allocation/report-dossier-by-allocation.component').then(
        (m) => m.ReportDossierByAllocationComponent
      ),
    canActivate: [reportDossierGuard('REPORT_STATISTICS_VIEW')]
  },
  {
    path: 'dossier-by-dossier-type',
    loadComponent: () =>
      import('./components/report-dossier-by-dossier-type/report-dossier-by-dossier-type.component').then(
        (m) => m.ReportDossierByDossierTypeComponent
      ),
    canActivate: [reportDossierGuard('REPORT_STATISTICS_VIEW')]
  },
  {
    path: 'dossier-by-shelf',
    loadComponent: () =>
      import('./components/report-dossier-by-shelf/report-dossier-by-shelf.component').then(
        (m) => m.ReportDossierByShelfComponent
      ),
    canActivate: [reportDossierGuard('REPORT_STATISTICS_VIEW')]
  },
  {
    path: 'dossier-by-box',
    loadComponent: () =>
      import('./components/report-dossier-by-box/report-dossier-by-box.component').then(
        (m) => m.ReportDossierByBoxComponent
      ),
    canActivate: [reportDossierGuard('REPORT_STATISTICS_VIEW')]
  },
  {
    path: 'dossier-by-floor',
    loadComponent: () =>
      import('./components/report-dossier-by-floor/report-dossier-by-floor.component').then(
        (m) => m.ReportDossierByFloorComponent
      ),
    canActivate: [reportDossierGuard('REPORT_STATISTICS_VIEW')]
  },
  {
    path: 'dossier-by-document-type',
    loadComponent: () =>
      import('./components/report-dossier-by-document-type/report-dossier-by-document-type.component').then(
        (m) => m.ReportDossierByDocumentTypeComponent
      ),
    canActivate: [reportDossierGuard('REPORT_STATISTICS_VIEW')]
  },
  {
    path: 'dossier-by-station',
    loadComponent: () =>
      import('./components/report-dossier-by-station/report-dossier-by-station.component').then(
        (m) => m.ReportDossierByStationComponent
      ),
    canActivate: [reportDossierGuard('REPORT_STATISTICS_VIEW')]
  },
  {
    path: 'dossier-by-line',
    loadComponent: () =>
      import('./components/report-dossier-by-line/report-dossier-by-line.component').then(
        (m) => m.ReportDossierByLineComponent
      ),
    canActivate: [reportDossierGuard('REPORT_STATISTICS_VIEW')]
  }
];
