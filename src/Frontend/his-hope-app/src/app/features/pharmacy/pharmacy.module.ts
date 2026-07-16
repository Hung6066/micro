import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '@shared/shared.module';
import { MedicationListComponent } from './medication-list/medication-list.component';
import { MedicationDetailComponent } from './medication-detail/medication-detail.component';
import { MedicationFormComponent } from './medication-form/medication-form.component';
import { PrescriptionListComponent } from './prescription-list/prescription-list.component';
import { PrescriptionDetailComponent } from './prescription-detail/prescription-detail.component';
import { PrescriptionFormComponent } from './prescription-form/prescription-form.component';

const routes: Routes = [
  { path: '', redirectTo: 'medications', pathMatch: 'full' },
  { path: 'medications', component: MedicationListComponent },
  { path: 'medications/new', component: MedicationFormComponent },
  { path: 'medications/:id', component: MedicationDetailComponent },
  { path: 'medications/:id/edit', component: MedicationFormComponent },
  { path: 'prescriptions', component: PrescriptionListComponent },
  { path: 'prescriptions/new', component: PrescriptionFormComponent },
  { path: 'prescriptions/:id', component: PrescriptionDetailComponent },
];

@NgModule({
  declarations: [
    MedicationListComponent,
    MedicationDetailComponent,
    MedicationFormComponent,
    PrescriptionListComponent,
    PrescriptionDetailComponent,
    PrescriptionFormComponent,
  ],
  imports: [SharedModule, RouterModule.forChild(routes)],
})
export class PharmacyModule {}
