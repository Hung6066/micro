import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '@shared/shared.module';
import { PatientListComponent } from './patient-list/patient-list.component';
import { PatientFormComponent } from './patient-form/patient-form.component';
import { PatientDetailComponent } from './patient-detail/patient-detail.component';

const routes: Routes = [
  { path: '', component: PatientListComponent },
  { path: 'new', component: PatientFormComponent },
  { path: ':id', component: PatientDetailComponent },
  { path: ':id/edit', component: PatientFormComponent },
];

@NgModule({
  declarations: [PatientListComponent, PatientFormComponent, PatientDetailComponent],
  imports: [SharedModule, RouterModule.forChild(routes)],
})
export class PatientsModule {}
