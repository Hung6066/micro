// @ts-nocheck
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '@shared/shared.module';
import { InvoiceListComponent } from './invoice-list/invoice-list.component';
import { InvoiceDetailComponent } from './invoice-detail/invoice-detail.component';
import { InvoiceFormComponent } from './invoice-form/invoice-form.component';

const routes: Routes = [
  { path: '', component: InvoiceListComponent },
  { path: 'new', component: InvoiceFormComponent },
  { path: ':id', component: InvoiceDetailComponent },
];

@NgModule({
  declarations: [InvoiceListComponent, InvoiceDetailComponent, InvoiceFormComponent],
  imports: [SharedModule, RouterModule.forChild(routes)],
})
export class BillingModule {}
