import { Routes } from '@angular/router';
import {
  pmisEndpointConfigMenuGuard,
  pmisManualSyncMenuGuard,
  pmisScheduleMenuGuard,
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
];
