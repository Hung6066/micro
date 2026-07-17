// @ts-nocheck
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '@shared/shared.module';
import { EncounterListComponent } from './encounter-list/encounter-list.component';
import { EncounterDetailComponent } from './encounter-detail/encounter-detail.component';

const routes: Routes = [
  { path: '', component: EncounterListComponent },
  { path: ':id', component: EncounterDetailComponent },
];

@NgModule({
  declarations: [EncounterListComponent, EncounterDetailComponent],
  imports: [SharedModule, RouterModule.forChild(routes)],
})
export class ClinicalModule {}
