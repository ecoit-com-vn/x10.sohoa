import { Route } from '@angular/router';
import { PhysicalStorageComponent } from './components/physical-storage/physical-storage.component';
import { PhysicalDossierWarehouseComponent } from './components/physical-dossier-warehouse/physical-dossier-warehouse.component';

export const PHYSICAL_STORAGE_ROUTES: Route[] = [
  {
    path: '',
    component: PhysicalDossierWarehouseComponent,
    data: { title: 'Kho hồ sơ kỹ thuật' }
  },
  {
    path: 'warehouse',
    component: PhysicalDossierWarehouseComponent,
    data: { title: 'Kho hồ sơ kỹ thuật' }
  },
  {
    path: 'management',
    component: PhysicalStorageComponent,
    data: { title: 'Quản lý kho vật lý' }
  }
];
