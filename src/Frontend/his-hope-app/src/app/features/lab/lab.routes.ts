import { Routes } from '@angular/router';
import { LabOrderListComponent } from './lab-order-list/lab-order-list.component';
import { LabOrderFormComponent } from './lab-order-form/lab-order-form.component';
import { LabOrderDetailComponent } from './lab-order-detail/lab-order-detail.component';
import { LabCriticalAlertsComponent } from './lab-critical-alerts/lab-critical-alerts.component';
import { LabCriticalAlertRuleFormComponent } from './lab-critical-alert-rule-form/lab-critical-alert-rule-form.component';
import { LabCriticalAlertDetailComponent } from './lab-critical-alert-detail/lab-critical-alert-detail.component';

export const LAB_ROUTES: Routes = [
  { path: '', component: LabOrderListComponent },
  { path: 'new', component: LabOrderFormComponent },
  { path: 'critical-alerts', component: LabCriticalAlertsComponent },
  { path: 'critical-alerts/rules', component: LabCriticalAlertRuleFormComponent },
  { path: 'critical-alerts/:id', component: LabCriticalAlertDetailComponent },
  { path: ':id', component: LabOrderDetailComponent },
];
