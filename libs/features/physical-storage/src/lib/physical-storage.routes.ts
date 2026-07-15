import { PhysicalStorageComponent } from './components/physical-storage/physical-storage.component';
import { Route } from '@angular/router';

export const PHYSICAL_STORAGE_ROUTES: Route[] = [
  {
    path: '',
    component: PhysicalStorageComponent,
    data: { title: 'Quản lý kho vật lý' }
  }
];
