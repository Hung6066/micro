import { Routes } from '@angular/router';
import { PatientListComponent } from './patient-list/patient-list.component';
import { PatientFormComponent } from './patient-form/patient-form.component';
import { PatientDetailComponent } from './patient-detail/patient-detail.component';
import { PatientWorkspaceComponent } from './patient-workspace/patient-workspace.component';

export const PATIENT_ROUTES: Routes = [
  { path: '', component: PatientListComponent },
  { path: 'new', component: PatientFormComponent },
  { path: ':id', component: PatientDetailComponent },
  { path: ':id/edit', component: PatientFormComponent },
  { path: ':id/workspace', component: PatientWorkspaceComponent },
];
