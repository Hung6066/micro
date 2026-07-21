# P1 accessibility and responsive shell implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the logged-in Angular shell usable on mobile and keyboard-friendly across the dashboard and primary list screens, while keeping global errors visible but non-blocking.

**Architecture:** Keep the changes surgical. The app shell owns viewport-aware sidenav behavior; dashboard and list views own their own interactive cards and tables; the global error handler owns transient snackbars while the shared error bar remains the persistent, non-blocking surface. Do not introduce new abstraction layers.

**Tech Stack:** Angular 19 standalone components, Angular Material 19, RxJS 7, Jest, Playwright.

## Global Constraints

- Angular standalone components only; preserve existing OnPush change detection.
- Use warm clinical monochrome styling and keep borders subtle; no new shadows or gradients.
- Mobile layout must collapse below 768px.
- Keyboard accessibility must work with Enter and Space for every clickable card/row.
- Global error presentation must not block the UI; snackbars stay transient while the shared error bar remains dismissible.

---

### Task 1: Shell sidenav becomes mobile-aware

**Files:**
- Modify: `src/Frontend/his-hope-app/src/app/app.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/app.component.spec.ts`

**Interfaces:**
- Consumes: `BreakpointObserver` or equivalent viewport signal, `AuthService.isLoggedIn()`, `Router.events`
- Produces: responsive `mat-sidenav` mode/open state and a testable mobile/desktop toggle path

- [ ] **Step 1: Add a failing expectation** that a narrow viewport switches the sidenav to `over` mode and closes it by default.
- [ ] **Step 2: Implement the minimal viewport-aware state** and keep the current desktop behavior unchanged.
- [ ] **Step 3: Add route-change close behavior** for the mobile drawer so navigation does not leave the drawer open.
- [ ] **Step 4: Run the app component spec** and confirm the new responsive assertion passes.

### Task 2: Clickable cards and rows become keyboard-operable

**Files:**
- Modify: `src/Frontend/his-hope-app/src/app/features/dashboard/dashboard.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/admin/admin-dashboard/admin-dashboard.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/clinical/encounter-list/encounter-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/appointments/appointment-list/appointment-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/pharmacy/medication-list/medication-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/pharmacy/prescription-list/prescription-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/billing/invoice-list/invoice-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/admin/audit-logs/audit-logs.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/admin/manage-users/manage-users.component.ts`

**Interfaces:**
- Consumes: existing navigation methods such as `viewDetail()`, `viewEncounter()`, `viewAppointment()`, `openPatientWorkspace()`, and router links
- Produces: `tabindex`, `role`, and keydown handlers for clickable cards/rows plus visible focus states

- [ ] **Step 1: Add failing keyboard-access expectations** for one representative card and one representative table row.
- [ ] **Step 2: Make the card/row targets focusable and operable** with Enter and Space, without changing their navigation destinations.
- [ ] **Step 3: Add visible focus styling** that matches the existing warm monochrome design.
- [ ] **Step 4: Run the affected component specs** and confirm keyboard interaction tests pass.

### Task 3: Tables wrap cleanly on small screens

**Files:**
- Modify: `src/Frontend/his-hope-app/src/app/features/dashboard/dashboard.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/clinical/encounter-list/encounter-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/appointments/appointment-list/appointment-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/pharmacy/medication-list/medication-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/pharmacy/prescription-list/prescription-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/billing/invoice-list/invoice-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/admin/audit-logs/audit-logs.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/admin/manage-users/manage-users.component.ts`

**Interfaces:**
- Consumes: existing table markup
- Produces: consistent overflow-x wrappers and responsive width rules

- [ ] **Step 1: Add a failing responsive assertion** for one wrapped table container.
- [ ] **Step 2: Add explicit scroll wrappers** around each wide table and keep the table width at 100%.
- [ ] **Step 3: Normalize the mobile overflow CSS** so the wrapper scrolls instead of the page.
- [ ] **Step 4: Run the affected specs** and confirm the wrappers render correctly.

### Task 4: Global errors stay visible but do not block the UI

**Files:**
- Modify: `src/Frontend/his-hope-app/src/app/core/errors/global-error-handler.ts`
- Modify: `src/Frontend/his-hope-app/src/app/core/errors/global-error-handler.spec.ts`
- Modify: `src/Frontend/his-hope-app/src/app/shared/components/error-bar/error-bar.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/shared/components/error-bar/error-bar.component.spec.ts`

**Interfaces:**
- Consumes: `ErrorService`, `MatSnackBar`, NgRx error store
- Produces: transient snackbar notifications, persistent error bar updates, and dismissal behavior

- [ ] **Step 1: Add a failing test** that verifies the snackbar is transient and not sticky for fatal errors.
- [ ] **Step 2: Update the handler** so the snackbar never blocks the UI and the error bar remains the primary visible surface.
- [ ] **Step 3: Ensure the error bar can still dismiss errors** and clears itself on the existing timer path.
- [ ] **Step 4: Run the error handler and error bar specs** to confirm the new presentation flow.

### Task 5: Build and browser verification

**Files:**
- None

**Interfaces:**
- Consumes: the updated Angular app
- Produces: verified build output and browser evidence

- [ ] **Step 1: Run `npm run build`** from `src/Frontend/his-hope-app` and confirm success.
- [ ] **Step 2: Open the app in the browser** and spot-check mobile sidenav, keyboard row/card focus, wrapped tables, and dismissible errors.
- [ ] **Step 3: Capture verification notes** with the routes tested and any visual or interaction regressions.

---

## Self-review

- Scope stays inside shell/sidebar/dashboard/list components plus the global error handler.
- No new design system or shared abstraction is introduced.
- Each task has its own test target and can be reviewed independently.
