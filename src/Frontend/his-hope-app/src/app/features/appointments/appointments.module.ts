import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '@shared/shared.module';
import { AppointmentListComponent } from './appointment-list/appointment-list.component';
import { AppointmentFormComponent } from './appointment-form/appointment-form.component';
import { AppointmentDetailComponent } from './appointment-detail/appointment-detail.component';

const routes: Routes = [
  { path: '', component: AppointmentListComponent },
  { path: 'new', component: AppointmentFormComponent },
  { path: ':id', component: AppointmentDetailComponent },
];

@NgModule({
  declarations: [AppointmentListComponent, AppointmentFormComponent, AppointmentDetailComponent],
  imports: [SharedModule, RouterModule.forChild(routes)],
})
export class AppointmentsModule {}
