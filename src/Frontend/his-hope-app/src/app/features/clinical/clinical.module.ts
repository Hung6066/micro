import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '@shared/shared.module';
import { EncounterListComponent } from './encounter-list/encounter-list.component';

const routes: Routes = [{ path: '', component: EncounterListComponent }];

@NgModule({
  declarations: [EncounterListComponent],
  imports: [SharedModule, RouterModule.forChild(routes)],
})
export class ClinicalModule {}
