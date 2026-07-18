import { Routes } from '@angular/router';
import { AppointmentListComponent } from './appointment-list/appointment-list.component';
import { AppointmentFormComponent } from './appointment-form/appointment-form.component';
import { AppointmentDetailComponent } from './appointment-detail/appointment-detail.component';

export const APPOINTMENT_ROUTES: Routes = [
  { path: '', component: AppointmentListComponent },
  { path: 'new', component: AppointmentFormComponent },
  { path: ':id', component: AppointmentDetailComponent },
];
