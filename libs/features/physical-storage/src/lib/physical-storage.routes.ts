import { Route } from '@angular/router';
import { PhysicalStorageComponent } from './components/physical-storage/physical-storage.component';
import { PhysicalDossierWarehouseComponent } from './components/physical-dossier-warehouse/physical-dossier-warehouse.component';

export const PHYSICAL_STORAGE_ROUTES: Route[] = [
  {
    path: '',
    component: PhysicalDossierWarehouseComponent,
    data: { title: 'Kho lưu trữ vật lý' }
  },
  {
    path: 'warehouse',
    component: PhysicalDossierWarehouseComponent,
    data: { title: 'Kho lưu trữ vật lý' }
  },
  {
    path: 'management',
    component: PhysicalStorageComponent,
    data: { title: 'Cấu hình kho lưu trữ vật lý' }
  }
];
