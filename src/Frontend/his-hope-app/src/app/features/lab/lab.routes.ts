import { Routes } from '@angular/router';
import { LabOrderListComponent } from './lab-order-list/lab-order-list.component';
import { LabOrderFormComponent } from './lab-order-form/lab-order-form.component';
import { LabOrderDetailComponent } from './lab-order-detail/lab-order-detail.component';

export const LAB_ROUTES: Routes = [
  { path: '', component: LabOrderListComponent },
  { path: 'new', component: LabOrderFormComponent },
  { path: ':id', component: LabOrderDetailComponent },
];
