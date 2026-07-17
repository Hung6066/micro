// @ts-nocheck
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '@shared/shared.module';
import { LabOrderListComponent } from './lab-order-list/lab-order-list.component';
import { LabOrderDetailComponent } from './lab-order-detail/lab-order-detail.component';
import { LabOrderFormComponent } from './lab-order-form/lab-order-form.component';

const routes: Routes = [
  { path: '', component: LabOrderListComponent },
  { path: 'new', component: LabOrderFormComponent },
  { path: ':id', component: LabOrderDetailComponent },
];

@NgModule({
  declarations: [LabOrderListComponent, LabOrderDetailComponent, LabOrderFormComponent],
  imports: [SharedModule, RouterModule.forChild(routes)],
})
export class LabModule {}
