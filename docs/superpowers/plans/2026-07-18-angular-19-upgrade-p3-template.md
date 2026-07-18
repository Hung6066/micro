# Phase 3: Template Modernization — Angular 19 Upgrade

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace all legacy template syntax with Angular 19 modern patterns: `@if/@for/@switch`, `@defer`, `inject()`, signal inputs/outputs. No application logic changes — only syntax modernization.

**Architecture:** 4 parallel tracks: (A) control flow syntax, (B) deferrable views, (C) inject() DI, (D) signal inputs. Each track independently testable. Tracks A and C are mechanical (can be script-assisted). Tracks B and D require manual judgment.

**Working directory:** `D:\AI\micro\src\Frontend\his-hope-app`

## Global Constraints

- Angular 19.2.x, esbuild, standalone components (from Phase 1+2)
- All 451 tests must pass after each batch
- Build must pass after each batch
- Zero `*ngIf`, `*ngFor`, `*ngSwitch` remaining
- `inject()` replaces constructor DI in all components/services/guards
- Signal inputs/outputs where applicable (not forced on every component)
- Vietnamese i18n still works
- Commit after each batch

---

### Task 1: Branch + Snapshot

- [ ] **Step 1: Create branch**
```pwsh
git checkout main && git pull origin main && git checkout -b chore/angular-19-upgrade-p3
```

- [ ] **Step 2: Baseline**
```pwsh
npm run build -- --configuration production && npm test -- --no-watch --browsers ChromeHeadless
```

Expected: Build passes, 451 tests pass.

- [ ] **Step 3: Commit**
```pwsh
git add -A && git commit -m "chore: snapshot baseline before template modernization"
```

---

### Task 2: Control Flow — `*ngIf` → `@if`, `*ngFor` → `@for`, `*ngSwitch` → `@switch`

**Files:** All `.html` template files and their corresponding `.ts` files.

This is the largest batch. Angular 19 CLI has a migration schematic but we do it manually for precision.

**Pattern:**
```html
<!-- BEFORE -->
<div *ngIf="user$ | async as user; else loading">Welcome, {{ user.fullName }}</div>
<ng-template #loading><app-spinner></app-spinner></ng-template>

<!-- AFTER -->
@if (user$ | async; as user) {
  <div>Welcome, {{ user.fullName }}</div>
} @else {
  <app-spinner></app-spinner>
}
```

```html
<!-- BEFORE -->
<tr *ngFor="let p of patients; trackBy: trackById; let i = index">
<div *ngIf="!patients.length">No patients found</div>

<!-- AFTER -->
@for (p of patients; track p.id; let i = $index) {
  <tr>...</tr>
} @empty {
  <div>No patients found</div>
}
```

```html
<!-- BEFORE -->
<div [ngSwitch]="status">
  <span *ngSwitchCase="'active'">Active</span>
  <span *ngSwitchCase="'pending'">Pending</span>
  <span *ngSwitchDefault>Unknown</span>
</div>

<!-- AFTER -->
@switch (status) {
  @case ('active') { <span>Active</span> }
  @case ('pending') { <span>Pending</span> }
  @default { <span>Unknown</span> }
}
```

**Migration rules:**
1. `*ngIf="expr; else tpl"` → `@if (expr) { ... } @else { <ng-container [ngTemplateOutlet]="tpl"></ng-container> }` — or inline the else content
2. `*ngFor="let x of list; trackBy: fn; let i = index"` → `@for (x of list; track fn(i, x); let i = $index)` — note: track expression must use the actual tracking value, not a method reference
3. `*ngSwitch` with `*ngSwitchCase`/`*ngSwitchDefault` → `@switch/@case/@default`
4. Async pipe `data$ | async` still works inside `@if` blocks
5. Remove `<ng-template #xxx>` that were used only for `else` blocks

**Files to migrate (priority order):**

**Batch A — Shared components (5 files):**
- `src/app/shared/components/sidebar/sidebar.component.ts`
- `src/app/shared/components/loading-spinner/loading-spinner.component.ts`
- `src/app/shared/components/empty-state/empty-state.component.ts`
- `src/app/shared/components/confirm-dialog/confirm-dialog.component.ts`
- `src/app/shared/components/error-bar/error-bar.component.ts`

**Batch B — Simple features (8 files):**
- `src/app/features/auth/login/login.component.ts`
- `src/app/features/auth/register/register.component.ts`
- `src/app/features/auth/forgot-password/forgot-password.component.ts`
- `src/app/features/dashboard/dashboard.component.ts`
- `src/app/features/reports/reports.component.ts`
- `src/app/features/clinical/encounter-list/encounter-list.component.ts`
- `src/app/features/clinical/encounter-detail/encounter-detail.component.ts`

**Batch C — CRUD features (18 files):**
- All lab, billing, appointments list/detail/form components
- All pharmacy medication/prescription list/detail/form components

**Batch D — Complex features (8 files):**
- `src/app/features/patients/patient-list/patient-list.component.ts`
- `src/app/features/patients/patient-detail/patient-detail.component.ts`
- `src/app/features/patients/patient-form/patient-form.component.ts`
- `src/app/features/patients/patient-workspace/patient-workspace.component.ts`
- `src/app/features/admin/admin-dashboard/admin-dashboard.component.ts`
- `src/app/features/admin/manage-users/manage-users.component.ts`
- `src/app/features/admin/manage-roles/manage-roles.component.ts`
- `src/app/features/admin/audit-logs/audit-logs.component.ts`
- `src/app/app.component.ts`

**After each batch:**
```pwsh
npm run build -- --configuration production && npm test -- --no-watch --browsers ChromeHeadless
```

- [ ] **Step 1: Migrate Batch A (Shared)**
- [ ] **Step 2: Migrate Batch B (Simple features)**
- [ ] **Step 3: Migrate Batch C (CRUD features)**
- [ ] **Step 4: Migrate Batch D (Complex features)**
- [ ] **Step 5: Verify zero legacy directives**
```pwsh
rg "\*ngIf|\*ngFor|\*ngSwitch|ngSwitch" src/app/ --include="*.ts" --include="*.html"
```
Expected: No results.

- [ ] **Step 6: Commit**
```pwsh
git add src/app/ && git commit -m "refactor: migrate templates from *ngIf/*ngFor/*ngSwitch to @if/@for/@switch"
```

---

### Task 3: Deferrable Views — `@defer`

Add `@defer` blocks for heavy components to improve initial load performance.

**Defer targets:**

| Location | Component | Trigger | Reason |
|---|---|---|---|
| Dashboard | `<app-dashboard-stats>` | `on viewport` | Stats cards below fold |
| Dashboard | Bento grid row 2+ | `on viewport` | Below-fold content |
| Patient workspace | Tab contents | `on interaction` | Tabs not visible until clicked |
| Admin | `<app-audit-logs>` table | `on viewport` | Large data table |
| Lab/Billing lists | Table body | `on viewport` | Below-fold data |
| Reports | Charts | `on viewport` | Heavy rendering |

**Pattern:**
```html
@defer (on viewport) {
  <app-dashboard-stats [stats]="stats()"></app-dashboard-stats>
} @loading (minimum 200ms) {
  <app-loading-spinner message="Đang tải..."></app-loading-spinner>
} @placeholder {
  <div class="stats-skeleton">
    <div class="skeleton-card"></div>
    <div class="skeleton-card"></div>
  </div>
} @error {
  <app-empty-state icon="error" title="Không thể tải dữ liệu"></app-empty-state>
}
```

**Files to modify (6-8 files):**
1. `src/app/features/dashboard/dashboard.component.ts`
2. `src/app/features/patients/patient-workspace/patient-workspace.component.ts`
3. `src/app/features/admin/audit-logs/audit-logs.component.ts`
4. `src/app/features/admin/admin-dashboard/admin-dashboard.component.ts`
5. `src/app/features/lab/lab-order-list/lab-order-list.component.ts`
6. `src/app/features/billing/invoice-list/invoice-list.component.ts`
7. `src/app/features/appointments/appointment-list/appointment-list.component.ts`
8. `src/app/features/reports/reports.component.ts`

**For each:**
- Read the component template
- Identify the heaviest subtree
- Wrap in `@defer (on viewport)` with appropriate `@loading`, `@placeholder`, `@error` blocks
- Use Vietnamese text in placeholder/error messages (app locale is `vi`)

- [ ] **Step 1: Add @defer to Dashboard**
- [ ] **Step 2: Add @defer to Patient Workspace tabs**
- [ ] **Step 3: Add @defer to Admin components**
- [ ] **Step 4: Add @defer to list components**
- [ ] **Step 5: Build + test**
- [ ] **Step 6: Commit**
```pwsh
git add src/app/features/ && git commit -m "perf: add @defer blocks for heavy components"
```

---

### Task 4: Dependency Injection — `constructor()` → `inject()`

Replace all `constructor(private x: Service)` patterns with `private x = inject(Service)`.

**This is a purely mechanical change.** For every `.ts` file that has a constructor:

```typescript
// BEFORE
@Component({...})
export class PatientListComponent implements OnInit {
  patients$ = this.store.select(selectAllPatients);

  constructor(
    private store: Store,
    private patientService: PatientService,
    private cdr: ChangeDetectorRef,
    private router: Router
  ) {}

  ngOnInit() { this.loadPatients(); }
}

// AFTER
@Component({...})
export class PatientListComponent {
  private store = inject(Store);
  private patientService = inject(PatientService);
  private cdr = inject(ChangeDetectorRef);
  private router = inject(Router);

  patients$ = this.store.select(selectAllPatients);

  constructor() { this.loadPatients(); }  // or move to ngOnInit/init block
}
```

**Rules:**
1. Add `import { inject } from '@angular/core';` if not present
2. Move each constructor parameter to a class field with `inject()`
3. Remove `OnInit` interface if constructor was only used for init — move logic to constructor or leave empty constructor
4. For services: same pattern — `inject(HttpClient)` instead of `constructor(private http: HttpClient)`
5. Keep `constructor()` if it calls init logic (can be empty braces `constructor() { }`)
6. Remove unused imports that were only used in constructor

**Files to migrate (~40 files):**
- All components in features/
- All components in shared/
- AppComponent
- All services in core/services/
- Guards: auth.guard.ts, permission.guard.ts, role.guard.ts
- Interceptors: auth.interceptor.ts, error.interceptor.ts

- [ ] **Step 1: Migrate shared components (5 files)**
- [ ] **Step 2: Migrate AppComponent**
- [ ] **Step 3: Migrate feature components (all 10 modules)**
- [ ] **Step 4: Migrate services (10 files)**
- [ ] **Step 5: Migrate guards + interceptors (5 files)**
- [ ] **Step 6: Build + test**
- [ ] **Step 7: Commit**
```pwsh
git add src/app/ && git commit -m "refactor: replace constructor DI with inject()"
```

---

### Task 5: Signal Inputs & Outputs

Convert `@Input()` and `@Output()` to signal-based `input()` and `output()`.

**Pattern:**
```typescript
// BEFORE
@Input() patient!: Patient;
@Input() showActions = true;
@Output() edit = new EventEmitter<string>();
@Output() delete = new EventEmitter<string>();

// AFTER
patient = input.required<Patient>();
showActions = input(true);
edit = output<string>();
delete = output<string>();
```

**Rules:**
1. `@Input() name!: Type` → `name = input.required<Type>()`
2. `@Input() name = defaultValue` → `name = input(defaultValue)`
3. `@Output() name = new EventEmitter<Type>()` → `name = output<Type>()`
4. Template bindings unchanged: `[patient]="p" (edit)="onEdit($event)"`
5. Reading signal in TS: `this.patient()` instead of `this.patient`
6. Import from `@angular/core`: `input`, `output`

**Files with @Input/@Output (estimate ~15-20 files):**
- `src/app/shared/components/sidebar/sidebar.component.ts`
- `src/app/shared/components/loading-spinner/loading-spinner.component.ts`
- `src/app/shared/components/empty-state/empty-state.component.ts`
- `src/app/shared/components/confirm-dialog/confirm-dialog.component.ts`
- `src/app/shared/components/error-bar/error-bar.component.ts`
- All patient-workspace dialogs (5 files)
- Patient list/detail/form components
- Admin dialogs

**Do NOT convert** if inputs are used in complex ways with setters/getters or two-way binding — leave for Phase 5 with model().

- [ ] **Step 1: Convert shared component inputs/outputs**
- [ ] **Step 2: Convert dialog inputs/outputs**
- [ ] **Step 3: Convert feature component inputs/outputs**
- [ ] **Step 4: Fix all `.ts` accesses: `this.patient` → `this.patient()` where signal**
- [ ] **Step 5: Build + test**
- [ ] **Step 6: Commit**
```pwsh
git add src/app/ && git commit -m "refactor: convert @Input/@Output to signal input()/output()"
```

---

### Task 6: Final Verification Gate

- [ ] `npm run build -- --configuration production` — SUCCESS
- [ ] `npm run build -- --configuration production-vi` — SUCCESS
- [ ] `npm test -- --no-watch --browsers ChromeHeadless` — ALL 451+ PASS
- [ ] `rg "\*ngIf|\*ngFor|\*ngSwitch|ngSwitch" src/app/ --include="*.ts"` — EMPTY (0 results)
- [ ] `rg "constructor\(.*private.*Service" src/app/features/` — EMPTY (all migrated to inject)
- [ ] `@defer` blocks render correctly (visual check on dev server)
- [ ] All routes still navigate correctly
- [ ] Auth flow still works
- [ ] Vietnamese i18n renders correctly

- [ ] **Push**
```pwsh
git push origin chore/angular-19-upgrade-p3
```

---

### Phase 3 Completion Criteria

- Zero `*ngIf`, `*ngFor`, `*ngSwitch` in entire codebase
- `@defer` blocks on 6-8 heavy components
- All constructors use `inject()` instead of parameter DI
- Signal `input()`/`output()` on shared + dialog components
- All 451+ tests pass
- Both English and Vietnamese builds succeed
