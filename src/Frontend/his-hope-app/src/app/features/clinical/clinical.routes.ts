import { Routes } from '@angular/router';
import { EncounterListComponent } from './encounter-list/encounter-list.component';
import { EncounterDetailComponent } from './encounter-detail/encounter-detail.component';

export const CLINICAL_ROUTES: Routes = [
  { path: '', component: EncounterListComponent },
  { path: ':id', component: EncounterDetailComponent },
];
