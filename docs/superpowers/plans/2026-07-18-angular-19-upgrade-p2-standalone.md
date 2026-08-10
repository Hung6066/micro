# Phase 2: Standalone Migration — Angular 19 Upgrade

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert 100% of components, directives, and pipes to standalone. Eliminate all NgModules (SharedModule, AppModule, 10 feature modules). Migrate bootstrap to `bootstrapApplication()`.

**Architecture:** Bottom-up migration — shared components first (no dependencies), then feature modules (simple to complex), finally AppModule. Each batch independently testable. Functional guards and interceptors replace class-based where applicable.

**Working directory:** `D:\AI\micro\src\Frontend\his-hope-app`

## Global Constraints

- Angular 19.2.x (from Phase 1)
- TypeScript 5.8.x with `moduleResolution: bundler`, `isolatedModules: true`
- Build: `@angular-devkit/build-angular:application` (esbuild)
- All 451 tests must pass after each batch
- Zero `*.module.ts` files in `src/app/features/` and `src/app/shared/` after completion
- Auth, guards, interceptors must maintain identical behavior
- i18n: Vietnamese build must still work
- Commit after each batch with conventional commit format

---

## File Structure — Before vs After

```
BEFORE:                              AFTER:
src/app/                             src/app/
├── app.module.ts                    ├── app.config.ts          ← NEW
├── app-routing.module.ts            ├── app.routes.ts          ← REWRITTEN
├── app.component.ts                 ├── app.component.ts       ← standalone: true
├── main.ts                          ├── main.ts                ← bootstrapApplication
├── shared/                          ├── shared/
│   └── shared.module.ts             │   └── components/
│       (26 Material re-exports,         (each component standalone, self-imports)
│        5 declared components)
├── features/                        ├── features/
│   ├── auth/                        │   ├── auth/
│   │   └── auth.module.ts           │   │   └── auth.routes.ts
│   ├── dashboard/                   │   │   (components standalone)
│   │   └── dashboard.module.ts      │   └── ...
│   └── ...                          │
└── core/                            └── core/
    └── guards/                          └── guards/
        (class-based)                        (functional: CanActivateFn)
```

---

### Task 1: Create branch + snapshot

- [ ] **Step 1: Create branch**
```pwsh
git checkout main && git pull origin main && git checkout -b chore/angular-19-upgrade-p2
```

- [ ] **Step 2: Snapshot baseline**
```pwsh
npm run build -- --configuration production && npm test -- --no-watch --browsers ChromeHeadless
```

Expected: Build passes, 451 tests pass.

- [ ] **Step 3: Commit baseline**
```pwsh
git add -A && git commit -m "chore: snapshot baseline before standalone migration"
```

---

### Task 2: Convert shared components to standalone

**Files:**
- Modify: `src/app/shared/components/sidebar/sidebar.component.ts`
- Modify: `src/app/shared/components/loading-spinner/loading-spinner.component.ts`
- Modify: `src/app/shared/components/empty-state/empty-state.component.ts`
- Modify: `src/app/shared/components/confirm-dialog/confirm-dialog.component.ts`
- Modify: `src/app/shared/components/error-bar/error-bar.component.ts`

All 5 shared components must become `standalone: true` with explicit `imports` arrays containing exactly the Material + Angular modules they individually need.

- [ ] **Step 1: Convert SidebarComponent to standalone**

Read `src/app/shared/components/sidebar/sidebar.component.ts`. Add `standalone: true` and `imports: [...]`.

The SidebarComponent uses: RouterModule, MatListModule, MatIconModule, MatBadgeModule, MatTooltipModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatAutocompleteModule, ReactiveFormsModule, CommonModule, HasPermissionDirective, HasRoleDirective.

Edit the `@Component` decorator:
```typescript
@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [
    CommonModule, RouterModule, ReactiveFormsModule,
    MatListModule, MatIconModule, MatBadgeModule, MatTooltipModule,
    MatButtonModule, MatFormFieldModule, MatInputModule, MatAutocompleteModule,
    HasPermissionDirective, HasRoleDirective,
  ],
  // ... rest unchanged
})
```

- [ ] **Step 2: Convert LoadingSpinnerComponent to standalone**

Uses: CommonModule, MatProgressSpinnerModule.
```typescript
@Component({
  selector: 'app-loading-spinner',
  standalone: true,
  imports: [CommonModule, MatProgressSpinnerModule],
  // ... rest unchanged
})
```

- [ ] **Step 3: Convert EmptyStateComponent to standalone**

Uses: CommonModule, MatIconModule, MatButtonModule.
```typescript
@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule],
  // ... rest unchanged
})
```

- [ ] **Step 4: Convert ConfirmDialogComponent to standalone**

Uses: CommonModule, MatDialogModule, MatButtonModule.
```typescript
@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule],
  // ... rest unchanged
})
```

- [ ] **Step 5: Convert ErrorBarComponent to standalone**

Uses: CommonModule, MatProgressBarModule, MatButtonModule, MatIconModule, MatSnackBarModule. Also uses NgRx store.

```typescript
@Component({
  selector: 'app-error-bar',
  standalone: true,
  imports: [CommonModule, MatProgressBarModule, MatButtonModule, MatIconModule, MatSnackBarModule],
  // ... rest unchanged
})
```

- [ ] **Step 6: Verify build after shared components**

```pwsh
npm run build -- --configuration production
```

Expected: Build passes. No errors about missing imports.

- [ ] **Step 7: Run tests**

```pwsh
npm test -- --no-watch --browsers ChromeHeadless
```

Expected: Tests pass (may need to update test imports if they reference SharedModule).

- [ ] **Step 8: Commit**

```pwsh
git add src/app/shared/components/ && git commit -m "refactor: convert shared components to standalone"
```

---

### Task 3: Convert feature batch 1 — Auth, Reports, Dashboard (simple)

These are the 3 simplest feature modules: few components, no complex state interactions.

- [ ] **Step 1: Convert AuthModule → standalone + auth.routes.ts**

Create `src/app/features/auth/auth.routes.ts`:
```typescript
import { Routes } from '@angular/router';
import { LoginComponent } from './login/login.component';
import { RegisterComponent } from './register/register.component';
import { ForgotPasswordComponent } from './forgot-password/forgot-password.component';

export const AUTH_ROUTES: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  { path: 'forgot-password', component: ForgotPasswordComponent },
];
```

For each of the 3 components (LoginComponent, RegisterComponent, ForgotPasswordComponent):

Add `standalone: true` and explicit `imports` containing exactly what they need:
- CommonModule, ReactiveFormsModule, RouterModule
- MatCardModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule

Read each component file and add the appropriate imports.

Delete `src/app/features/auth/auth.module.ts`.

- [ ] **Step 2: Convert DashboardModule → standalone + dashboard.routes.ts**

Create `src/app/features/dashboard/dashboard.routes.ts`:
```typescript
import { Routes } from '@angular/router';
import { DashboardComponent } from './dashboard.component';

export const DASHBOARD_ROUTES: Routes = [
  { path: '', component: DashboardComponent },
];
```

Add `standalone: true` and imports to `DashboardComponent`. The DashboardComponent needs:
- CommonModule, RouterModule, ReactiveFormsModule
- MatCardModule, MatInputModule, MatFormFieldModule, MatIconModule, MatButtonModule
- MatProgressSpinnerModule, MatTableModule, MatChipsModule

Delete `src/app/features/dashboard/dashboard.module.ts`.

- [ ] **Step 3: Convert ReportsModule → standalone + reports.routes.ts**

Create `src/app/features/reports/reports.routes.ts`:
```typescript
import { Routes } from '@angular/router';
import { ReportsComponent } from './reports.component';

export const REPORTS_ROUTES: Routes = [
  { path: '', component: ReportsComponent },
];
```

Add `standalone: true` to `ReportsComponent`. Imports: CommonModule, MatCardModule, MatIconModule.

Delete `src/app/features/reports/reports.module.ts`.

- [ ] **Step 4: Update app-routing.module.ts for batch 1**

Edit `src/app/app-routing.module.ts` — change the loadChildren for these 3 routes:

```typescript
// Auth
{ path: 'auth', loadChildren: () => import('@features/auth/auth.routes').then(m => m.AUTH_ROUTES) },

// Dashboard
{ path: 'dashboard', canActivate: [AuthGuard],
  loadChildren: () => import('@features/dashboard/dashboard.routes').then(m => m.DASHBOARD_ROUTES) },

// Reports
{ path: 'reports', canActivate: [AuthGuard, PermissionGuard], data: { permissions: ['reports.view'] },
  loadChildren: () => import('@features/reports/reports.routes').then(m => m.REPORTS_ROUTES) },
```

- [ ] **Step 5: Verify batch 1**

```pwsh
npm run build -- --configuration production && npm test -- --no-watch --browsers ChromeHeadless
```

Expected: Build passes. Tests pass (some may need TestBed imports updated).

- [ ] **Step 6: Commit**

```pwsh
git add src/app/features/auth/ src/app/features/dashboard/ src/app/features/reports/ src/app/app-routing.module.ts && git commit -m "refactor: convert Auth, Dashboard, Reports modules to standalone"
```

---

### Task 4: Convert feature batch 2 — Lab, Billing, Clinical (medium)

These 3 modules have similar CRUD patterns (list, detail, form).

- [ ] **Step 1: Convert LabModule → standalone + lab.routes.ts**

Create `src/app/features/lab/lab.routes.ts`:
```typescript
import { Routes } from '@angular/router';
import { LabOrderListComponent } from './lab-order-list/lab-order-list.component';
import { LabOrderFormComponent } from './lab-order-form/lab-order-form.component';
import { LabOrderDetailComponent } from './lab-order-detail/lab-order-detail.component';

export const LAB_ROUTES: Routes = [
  { path: '', component: LabOrderListComponent },
  { path: 'new', component: LabOrderFormComponent },
  { path: ':id', component: LabOrderDetailComponent },
];
```

Add `standalone: true` to each of the 3 lab components. Each needs:
- CommonModule, ReactiveFormsModule, RouterModule
- MatTableModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule
- MatCardModule, MatChipsModule, MatProgressSpinnerModule, MatPaginatorModule, MatSortModule
- MatSelectModule, MatDatepickerModule, MatNativeDateModule, MatTooltipModule
- MatDialogModule (for confirmation)

Read each component and add the specific imports it uses.

Delete `src/app/features/lab/lab.module.ts`.

- [ ] **Step 2: Convert BillingModule → standalone + billing.routes.ts**

Create `src/app/features/billing/billing.routes.ts`:
```typescript
import { Routes } from '@angular/router';
import { InvoiceListComponent } from './invoice-list/invoice-list.component';
import { InvoiceFormComponent } from './invoice-form/invoice-form.component';
import { InvoiceDetailComponent } from './invoice-detail/invoice-detail.component';

export const BILLING_ROUTES: Routes = [
  { path: '', component: InvoiceListComponent },
  { path: 'new', component: InvoiceFormComponent },
  { path: ':id', component: InvoiceDetailComponent },
];
```

Add `standalone: true` to each billing component with appropriate imports.

Delete `src/app/features/billing/billing.module.ts`.

- [ ] **Step 3: Convert ClinicalModule → standalone + clinical.routes.ts**

Create `src/app/features/clinical/clinical.routes.ts`:
```typescript
import { Routes } from '@angular/router';
import { EncounterListComponent } from './encounter-list/encounter-list.component';
import { EncounterDetailComponent } from './encounter-detail/encounter-detail.component';

export const CLINICAL_ROUTES: Routes = [
  { path: '', component: EncounterListComponent },
  { path: ':id', component: EncounterDetailComponent },
];
```

Add `standalone: true` to each clinical component with appropriate imports.

Delete `src/app/features/clinical/clinical.module.ts`.

- [ ] **Step 4: Convert PharmacyModule → standalone + pharmacy.routes.ts**

Pharmacy is the largest in batch 2 (6 components: medication-list/detail/form + prescription-list/detail/form).

Create `src/app/features/pharmacy/pharmacy.routes.ts`:
```typescript
import { Routes } from '@angular/router';
import { MedicationListComponent } from './medication-list/medication-list.component';
import { MedicationFormComponent } from './medication-form/medication-form.component';
import { MedicationDetailComponent } from './medication-detail/medication-detail.component';
import { PrescriptionListComponent } from './prescription-list/prescription-list.component';
import { PrescriptionFormComponent } from './prescription-form/prescription-form.component';
import { PrescriptionDetailComponent } from './prescription-detail/prescription-detail.component';

export const PHARMACY_ROUTES: Routes = [
  { path: 'medications', component: MedicationListComponent },
  { path: 'medications/new', component: MedicationFormComponent },
  { path: 'medications/:id', component: MedicationDetailComponent },
  { path: 'prescriptions', component: PrescriptionListComponent },
  { path: 'prescriptions/new', component: PrescriptionFormComponent },
  { path: 'prescriptions/:id', component: PrescriptionDetailComponent },
  { path: '', redirectTo: 'medications', pathMatch: 'full' },
];
```

Add `standalone: true` to each of the 6 pharmacy components.

Delete `src/app/features/pharmacy/pharmacy.module.ts`.

- [ ] **Step 5: Update app-routing.module.ts for batch 2**

```typescript
{ path: 'lab', canActivate: [AuthGuard, PermissionGuard], data: { permissions: ['lab.view'] },
  loadChildren: () => import('@features/lab/lab.routes').then(m => m.LAB_ROUTES) },
{ path: 'billing', canActivate: [AuthGuard, PermissionGuard], data: { permissions: ['billing.view'] },
  loadChildren: () => import('@features/billing/billing.routes').then(m => m.BILLING_ROUTES) },
{ path: 'clinical', canActivate: [AuthGuard, PermissionGuard], data: { permissions: ['clinical.view'] },
  loadChildren: () => import('@features/clinical/clinical.routes').then(m => m.CLINICAL_ROUTES) },
{ path: 'pharmacy', canActivate: [AuthGuard, PermissionGuard], data: { permissions: ['pharmacy.view'] },
  loadChildren: () => import('@features/pharmacy/pharmacy.routes').then(m => m.PHARMACY_ROUTES) },
```

- [ ] **Step 6: Build + test batch 2**

```pwsh
npm run build -- --configuration production && npm test -- --no-watch --browsers ChromeHeadless
```

Expected: Build passes. Tests pass.

- [ ] **Step 7: Commit**

```pwsh
git add src/app/features/lab/ src/app/features/billing/ src/app/features/clinical/ src/app/features/pharmacy/ src/app/app-routing.module.ts && git commit -m "refactor: convert Lab, Billing, Clinical, Pharmacy modules to standalone"
```

---

### Task 5: Convert feature batch 3 — Appointments, Admin, Patients (complex)

These have the most components, dialogs, and complex state interactions.

- [ ] **Step 1: Convert AppointmentsModule → standalone + appointments.routes.ts**

Create `src/app/features/appointments/appointments.routes.ts`:
```typescript
import { Routes } from '@angular/router';
import { AppointmentListComponent } from './appointment-list/appointment-list.component';
import { AppointmentFormComponent } from './appointment-form/appointment-form.component';
import { AppointmentDetailComponent } from './appointment-detail/appointment-detail.component';

export const APPOINTMENT_ROUTES: Routes = [
  { path: '', component: AppointmentListComponent },
  { path: 'new', component: AppointmentFormComponent },
  { path: ':id', component: AppointmentDetailComponent },
];
```

Add `standalone: true` to each appointment component. These use patient service for autocomplete.

Delete `src/app/features/appointments/appointments.module.ts`.

- [ ] **Step 2: Convert AdminModule → standalone + admin.routes.ts**

Create `src/app/features/admin/admin.routes.ts`:
```typescript
import { Routes } from '@angular/router';
import { AdminDashboardComponent } from './admin-dashboard/admin-dashboard.component';
import { ManageUsersComponent } from './manage-users/manage-users.component';
import { ManageRolesComponent } from './manage-roles/manage-roles.component';
import { AuditLogsComponent } from './audit-logs/audit-logs.component';
import { SettingsComponent } from './settings/settings.component';

export const ADMIN_ROUTES: Routes = [
  { path: '', component: AdminDashboardComponent },
  { path: 'users', component: ManageUsersComponent },
  { path: 'roles', component: ManageRolesComponent },
  { path: 'audit-logs', component: AuditLogsComponent },
  { path: 'settings', component: SettingsComponent },
];
```

Add `standalone: true` to all 5 admin components + 3 dialogs (RoleFormDialog, UserFormDialog, AssignRolesDialog). These dialogs are already standalone.

Delete `src/app/features/admin/admin.module.ts`.

- [ ] **Step 3: Convert PatientsModule → standalone + patients.routes.ts**

Create `src/app/features/patients/patients.routes.ts`:
```typescript
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
```

Add `standalone: true` to PatientListComponent, PatientFormComponent, PatientDetailComponent. PatientWorkspaceComponent is already standalone.

Delete `src/app/features/patients/patients.module.ts`.

- [ ] **Step 4: Update app-routing.module.ts for batch 3**

```typescript
{ path: 'appointments', canActivate: [AuthGuard, PermissionGuard], data: { permissions: ['appointments.view'] },
  loadChildren: () => import('@features/appointments/appointments.routes').then(m => m.APPOINTMENT_ROUTES) },
{ path: 'admin', canActivate: [AuthGuard, RoleGuard], data: { roles: ['admin'] },
  loadChildren: () => import('@features/admin/admin.routes').then(m => m.ADMIN_ROUTES) },
{ path: 'patients', canActivate: [AuthGuard, PermissionGuard], data: { permissions: ['patients.view'] },
  loadChildren: () => import('@features/patients/patients.routes').then(m => m.PATIENT_ROUTES) },
```

- [ ] **Step 5: Build + test batch 3**

```pwsh
npm run build -- --configuration production && npm test -- --no-watch --browsers ChromeHeadless
```

Expected: Build passes. Tests pass.

- [ ] **Step 6: Commit**

```pwsh
git add src/app/features/appointments/ src/app/features/admin/ src/app/features/patients/ src/app/app-routing.module.ts && git commit -m "refactor: convert Appointments, Admin, Patients modules to standalone"
```

---

### Task 6: Migrate AppModule → bootstrapApplication

This is the most critical task. It creates `app.config.ts`, updates `main.ts`, makes `AppComponent` standalone, and deletes `AppModule`.

**Files:**
- Create: `src/app/app.config.ts`
- Create: `src/app/app.routes.ts`
- Modify: `src/main.ts`
- Modify: `src/app/app.component.ts`
- Delete: `src/app/app.module.ts`
- Delete: `src/app/app-routing.module.ts`
- Delete: `src/app/shared/shared.module.ts`

- [ ] **Step 1: Create app.config.ts**

Read the current `app.module.ts` to understand all providers.

Write to `src/app/app.config.ts`:
```typescript
import { ApplicationConfig, ErrorHandler, importProvidersFrom } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideStore } from '@ngrx/store';
import { provideEffects } from '@ngrx/effects';
import { provideStoreDevtools } from '@ngrx/store-devtools';

import { routes } from './app.routes';
import { authReducer } from '@store/auth/auth.reducer';
import { patientsReducer } from '@store/patients/patients.reducer';
import { errorReducer } from '@store/error/error.reducer';
import { AuthEffects } from '@store/auth/auth.effects';
import { PatientsEffects } from '@store/patients/patients.effects';
import { AuthInterceptor } from '@core/interceptors/auth.interceptor';
import { ErrorInterceptor } from '@core/interceptors/error.interceptor';
import { GlobalErrorHandler } from '@core/errors/global-error-handler';

import { environment } from '@env/environment';
import { mockServiceProviders } from '@core/services/mock/mock-providers';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptorsFromDi()),
    provideAnimations(),
    provideStore({
      auth: authReducer,
      patients: patientsReducer,
      error: errorReducer,
    }),
    provideEffects([AuthEffects, PatientsEffects]),
    provideStoreDevtools({ maxAge: 25 }),
    { provide: ErrorHandler, useClass: GlobalErrorHandler },
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: ErrorInterceptor, multi: true },
    ...(environment.useMockServices ? mockServiceProviders : []),
  ],
};
```

- [ ] **Step 2: Create app.routes.ts**

Read the current `app-routing.module.ts` and extract the Routes array into a standalone file.

Write to `src/app/app.routes.ts`:
```typescript
import { Routes } from '@angular/router';
import { AuthGuard } from '@core/guards/auth.guard';
import { RoleGuard } from '@core/guards/role.guard';
import { PermissionGuard } from '@core/guards/permission.guard';

export const routes: Routes = [
  { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
  {
    path: 'auth',
    loadChildren: () => import('@features/auth/auth.routes').then(m => m.AUTH_ROUTES),
  },
  {
    path: 'access-denied',
    loadComponent: () => import('@shared/pages/access-denied/access-denied.component').then(m => m.AccessDeniedComponent),
  },
  {
    path: 'dashboard',
    canActivate: [AuthGuard],
    loadChildren: () => import('@features/dashboard/dashboard.routes').then(m => m.DASHBOARD_ROUTES),
  },
  {
    path: 'patients',
    canActivate: [AuthGuard, PermissionGuard],
    data: { permissions: ['patients.view'] },
    loadChildren: () => import('@features/patients/patients.routes').then(m => m.PATIENT_ROUTES),
  },
  {
    path: 'appointments',
    canActivate: [AuthGuard, PermissionGuard],
    data: { permissions: ['appointments.view'] },
    loadChildren: () => import('@features/appointments/appointments.routes').then(m => m.APPOINTMENT_ROUTES),
  },
  {
    path: 'clinical',
    canActivate: [AuthGuard, PermissionGuard],
    data: { permissions: ['clinical.view'] },
    loadChildren: () => import('@features/clinical/clinical.routes').then(m => m.CLINICAL_ROUTES),
  },
  {
    path: 'pharmacy',
    canActivate: [AuthGuard, PermissionGuard],
    data: { permissions: ['pharmacy.view'] },
    loadChildren: () => import('@features/pharmacy/pharmacy.routes').then(m => m.PHARMACY_ROUTES),
  },
  {
    path: 'lab',
    canActivate: [AuthGuard, PermissionGuard],
    data: { permissions: ['lab.view'] },
    loadChildren: () => import('@features/lab/lab.routes').then(m => m.LAB_ROUTES),
  },
  {
    path: 'billing',
    canActivate: [AuthGuard, PermissionGuard],
    data: { permissions: ['billing.view'] },
    loadChildren: () => import('@features/billing/billing.routes').then(m => m.BILLING_ROUTES),
  },
  {
    path: 'admin',
    canActivate: [AuthGuard, RoleGuard],
    data: { roles: ['admin'] },
    loadChildren: () => import('@features/admin/admin.routes').then(m => m.ADMIN_ROUTES),
  },
  {
    path: 'reports',
    canActivate: [AuthGuard, PermissionGuard],
    data: { permissions: ['reports.view'] },
    loadChildren: () => import('@features/reports/reports.routes').then(m => m.REPORTS_ROUTES),
  },
  { path: '**', redirectTo: '/dashboard' },
];
```

- [ ] **Step 3: Make AppComponent standalone + update**

Read `src/app/app.component.ts`. Change from `standalone: false` to `standalone: true` and add imports:

```typescript
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule, RouterModule,
    MatSidenavModule,
    SidebarComponent, ErrorBarComponent,
  ],
  // ... rest unchanged
})
```

Note: `MatSidenavModule` and the content module imports. `SidebarComponent` and `ErrorBarComponent` are now standalone. Add `CommonModule` for `*ngIf` (or use `@if` if migrating control flow early).

- [ ] **Step 4: Update main.ts for bootstrapApplication**

Replace entire content of `src/main.ts`:
```typescript
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';

bootstrapApplication(AppComponent, appConfig)
  .catch(err => console.error(err));
```

Remove `import { platformBrowserDynamic } from '@angular/platform-browser-dynamic'` and all AppModule references.

- [ ] **Step 5: Delete old module files**

```pwsh
Remove-Item src/app/app.module.ts
Remove-Item src/app/app-routing.module.ts
Remove-Item src/app/shared/shared.module.ts
```

- [ ] **Step 6: Update all feature component imports to use explicit Material modules**

All feature components were previously importing `SharedModule` which re-exported all Material. Now each component needs its own `imports`. Read each component and add the specific Material modules it uses.

This was already done in Tasks 3-5 above when adding `standalone: true` to each component.

- [ ] **Step 7: Build + test**

```pwsh
npm run build -- --configuration production
npm test -- --no-watch --browsers ChromeHeadless
```

Expected: Build passes. Tests pass.

If tests fail due to missing Material imports in component specs, update the TestBed `imports` arrays to include the specific modules needed.

- [ ] **Step 8: Verify Vietnamese build**

```pwsh
npm run build -- --configuration production-vi
```

Expected: Build passes.

- [ ] **Step 9: Commit**

```pwsh
git add src/app/app.config.ts src/app/app.routes.ts src/main.ts src/app/app.component.ts && git commit -m "refactor: migrate AppModule to bootstrapApplication with standalone bootstrap"

git add -A && git commit -m "refactor: delete AppModule, AppRoutingModule, SharedModule"
```

---

### Task 7: Update test files for standalone

All `.spec.ts` files that used `SharedModule` in their `TestBed.configureTestingModule({ imports: [SharedModule] })` need to be updated to import the specific modules/components needed.

- [ ] **Step 1: Find all tests importing SharedModule**

```pwsh
rg "imports:.*SharedModule" --include="*.spec.ts" src/app/
```

- [ ] **Step 2: Update each spec to import standalone components directly**

Pattern: Replace `imports: [SharedModule]` with the specific imports needed for that test. For most component tests, this means importing:
- `CommonModule`, `ReactiveFormsModule`, `RouterModule`
- The specific Material modules the component uses
- The standalone directives (`HasPermissionDirective`, `HasRoleDirective`) if used in template

Example for a component test using table + form fields:
```typescript
TestBed.configureTestingModule({
  imports: [
    CommonModule, ReactiveFormsModule, RouterModule,
    MatTableModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatIconModule, MatPaginatorModule, MatSortModule,
    MatCardModule, MatProgressSpinnerModule, MatChipsModule,
  ],
  providers: [
    provideHttpClient(withInterceptorsFromDi()),
    provideHttpClientTesting(),
    // ... mock services
  ],
});
```

- [ ] **Step 3: Verify all tests pass**

```pwsh
npm test -- --no-watch --browsers ChromeHeadless
```

Expected: All 451 tests pass.

- [ ] **Step 4: Commit**

```pwsh
git add src/app/ && git commit -m "test: update test files for standalone component imports"
```

---

### Task 8: Phase 2 Verification & Gate Checklist

- [ ] `npm run build -- --configuration production` — SUCCESS
- [ ] `npm run build -- --configuration production-vi` — SUCCESS (i18n)
- [ ] `npm test -- --no-watch --browsers ChromeHeadless` — ALL 451+ PASS
- [ ] `npm run lint` — NO NEW ERRORS
- [ ] No `*.module.ts` files in `src/app/features/` (except already-deleted)
- [ ] No `app.module.ts` or `app-routing.module.ts` or `shared.module.ts`
- [ ] App loads via `bootstrapApplication` (check main.ts)
- [ ] All 26 routes lazy-load correctly:
  - `/auth/login`, `/auth/register`, `/auth/forgot-password`
  - `/dashboard`
  - `/patients`, `/patients/new`, `/patients/:id`, `/patients/:id/edit`, `/patients/:id/workspace`
  - `/appointments`, `/appointments/new`, `/appointments/:id`
  - `/clinical`, `/clinical/:id`
  - `/pharmacy/medications`, `/pharmacy/medications/new`, `/pharmacy/medications/:id`
  - `/pharmacy/prescriptions`, `/pharmacy/prescriptions/new`, `/pharmacy/prescriptions/:id`
  - `/lab`, `/lab/new`, `/lab/:id`
  - `/billing`, `/billing/new`, `/billing/:id`
  - `/admin`, `/admin/users`, `/admin/roles`, `/admin/audit-logs`, `/admin/settings`
  - `/reports`
  - `/access-denied`
- [ ] Auth flow: Login → JWT → navigate → Logout works
- [ ] PermissionGuard: unauthorized → redirect /access-denied
- [ ] RoleGuard: non-admin → redirect /access-denied
- [ ] *hasPermission directive still hides/shows correctly
- [ ] *hasRole directive still hides/shows correctly
- [ ] Store DevTools shows correct state
- [ ] Vietnamese i18n renders correctly

- [ ] **Push branch**

```pwsh
git push origin chore/angular-19-upgrade-p2
```

---

### Phase 2 Completion Criteria

- Zero NgModules in the entire application
- 100% standalone components, directives, pipes
- `bootstrapApplication(AppComponent, appConfig)` in main.ts
- All routes work with `loadChildren: () => import('./feature.routes')`
- All 451+ tests pass
- Auth, guards, interceptors behave identically
- Both English and Vietnamese builds succeed
