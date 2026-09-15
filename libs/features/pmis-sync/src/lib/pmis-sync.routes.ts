import { Routes } from '@angular/router';
import {
  pmisEndpointConfigMenuGuard,
  pmisManualSyncMenuGuard,
  pmisScheduleMenuGuard,
  pmisUnitMappingMenuGuard,
} from '@sohoa.frontend/shared/core';

export const PMIS_SYNC_ROUTES: Routes = [
  {
    path: 'endpoint-config',
    loadComponent: () =>
      import('./feature/pmis-endpoint-config/pmis-endpoint-config.component').then(
        (m) => m.PmisEndpointConfigComponent
      ),
    canActivate: [pmisEndpointConfigMenuGuard],
  },
  {
    path: 'manual-sync',
    loadComponent: () =>
      import('./feature/pmis-manual-sync/pmis-manual-sync.component').then(
        (m) => m.PmisManualSyncComponent
      ),
    canActivate: [pmisManualSyncMenuGuard],
  },
  {
    path: 'schedule',
    loadComponent: () =>
      import('./feature/pmis-schedule/pmis-schedule.component').then(
        (m) => m.PmisScheduleComponent
      ),
    canActivate: [pmisScheduleMenuGuard],
  },
  {
    path: 'unit-mapping',
    loadComponent: () =>
      import('./feature/pmis-unit-mapping/pmis-unit-mapping.component').then(
        (m) => m.PmisUnitMappingComponent
      ),
    canActivate: [pmisUnitMappingMenuGuard],
  },
];
