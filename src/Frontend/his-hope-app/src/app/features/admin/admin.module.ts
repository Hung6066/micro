import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '@shared/shared.module';
import { AdminDashboardComponent } from './admin-dashboard/admin-dashboard.component';

const routes: Routes = [{ path: '', component: AdminDashboardComponent }];

@NgModule({
  declarations: [AdminDashboardComponent],
  imports: [SharedModule, RouterModule.forChild(routes)],
})
export class AdminModule {}
