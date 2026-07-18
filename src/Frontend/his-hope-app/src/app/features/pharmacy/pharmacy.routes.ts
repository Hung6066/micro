import { Routes } from '@angular/router';
import { MedicationListComponent } from './medication-list/medication-list.component';
import { MedicationFormComponent } from './medication-form/medication-form.component';
import { MedicationDetailComponent } from './medication-detail/medication-detail.component';
import { PrescriptionListComponent } from './prescription-list/prescription-list.component';
import { PrescriptionFormComponent } from './prescription-form/prescription-form.component';
import { PrescriptionDetailComponent } from './prescription-detail/prescription-detail.component';

export const PHARMACY_ROUTES: Routes = [
  { path: '', redirectTo: 'medications', pathMatch: 'full' },
  { path: 'medications', component: MedicationListComponent },
  { path: 'medications/new', component: MedicationFormComponent },
  { path: 'medications/:id', component: MedicationDetailComponent },
  { path: 'prescriptions', component: PrescriptionListComponent },
  { path: 'prescriptions/new', component: PrescriptionFormComponent },
  { path: 'prescriptions/:id', component: PrescriptionDetailComponent },
];
