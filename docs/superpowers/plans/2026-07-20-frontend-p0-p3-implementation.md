# Frontend P0-P3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the verified frontend P0-P3 issues: admin server error, auth deep-link bounce, keyboard/responsive/accessibility gaps, and UI polish/dead-end screens.

**Architecture:** Keep changes surgical and local to the Angular frontend. Use the existing auth/guard/service flow for P0, the current design system and Material components for P1-P3, and preserve existing route structure unless a small accessibility refactor is required. Verify each area independently, then run end-to-end regression.

**Tech Stack:** Angular 17, RxJS, Angular Material, Jest, Playwright E2E.

## Global Constraints
- Do not hardcode secrets.
- Keep changes surgical; do not refactor unrelated features.
- Preserve current route structure and design system patterns.
- All changed flows must have tests.
- Use the existing admin login: `admin` / `Admin@123` for validation.

---

### Task 1: P0 auth + admin fix

**Files:**
- Modify: `src/Frontend/his-hope-app/src/app/core/services/auth.service.ts`
- Modify: `src/Frontend/his-hope-app/src/app/core/guards/auth.guard.ts`
- Modify: `src/Frontend/his-hope-app/src/app/core/guards/permission.guard.ts`
- Modify: `src/Frontend/his-hope-app/src/app/core/guards/role.guard.ts`
- Modify: `src/Frontend/his-hope-app/src/app/core/services/auth.service.spec.ts`
- Modify: `src/Frontend/his-hope-app/src/app/core/guards/auth.guard.spec.ts`
- Modify: `src/Frontend/his-hope-app/src/app/core/guards/permission.guard.spec.ts`
- Modify: `src/Frontend/his-hope-app/src/app/core/guards/role.guard.spec.ts`

**Interfaces:**
- Consumes: auth token hydration/verification already used by guards.
- Produces: stable guard decisions after hard reload and consistent admin page access.

- [ ] **Step 1: Verify the failing auth/admin flows with existing tests and browser repro**
- [ ] **Step 2: Fix hydration so guards wait for auth readiness before redirecting**
- [ ] **Step 3: Fix the admin manage-users error by tracing the failing API/state path and patching the frontend contract usage**
- [ ] **Step 4: Update specs for hydration and admin access behavior**
- [ ] **Step 5: Run targeted Jest tests and a live browser login/admin check**

### Task 2: P1 accessibility + responsive UI

**Files:**
- Modify: `src/Frontend/his-hope-app/src/app/app.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/shared/components/sidebar/sidebar.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/patients/patient-workspace/patient-workspace.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/dashboard/dashboard.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/appointments/appointment-list/appointment-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/clinical/encounter-list/encounter-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/lab/lab-order-list/lab-order-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/billing/invoice-list/invoice-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/pharmacy/medication-list/medication-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/shared/components/global-error-handler/global-error-handler.ts`

**Interfaces:**
- Consumes: current Material/table/layout components.
- Produces: keyboard-accessible clickable content, safer responsive shell, and less disruptive error presentation.

- [ ] **Step 1: Add/adjust breakpoint handling for sidenav and dense tables**
- [ ] **Step 2: Make click-only rows/cards keyboard-operable with proper semantics**
- [ ] **Step 3: Tone down global error presentation so it does not block workflows**
- [ ] **Step 4: Update/extend UI specs if present for keyboard and responsive behavior**
- [ ] **Step 5: Run UI-focused checks and verify no regressions on major routes**

### Task 3: P2/P3 copy and polish

**Files:**
- Modify: `src/Frontend/his-hope-app/src/app/features/patients/patient-list/patient-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/appointments/appointment-list/appointment-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/clinical/encounter-list/encounter-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/auth/login/login.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/auth/register/register.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/auth/forgot-password/forgot-password.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/pharmacy/prescription-detail/prescription-detail.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/clinical/patient-workspace/patient-workspace.component.ts`
- Modify: `src/Frontend/his-hope-app/src/styles/_theme.scss`
- Modify: `src/Frontend/his-hope-app/src/styles/styles.scss`

**Interfaces:**
- Consumes: existing i18n/copy and theme tokens.
- Produces: consistent locale wording, cleaner login form alignment, and theme-compliant typography/colors.

- [ ] **Step 1: Normalize visible copy and dead-end placeholder screens**
- [ ] **Step 2: Remove emoji/hardcoded color usage from clinical workspace and dialogs**
- [ ] **Step 3: Fix login prefill/label alignment**
- [ ] **Step 4: Align typography tokens with the approved design system**
- [ ] **Step 5: Run the frontend build and spot-check core pages**

### Task 4: Regression verification

**Files:**
- Test: `tests/Frontend/his-hope-e2e/**/*`

**Interfaces:**
- Consumes: the updated frontend build and live Docker environment.
- Produces: pass/fail evidence for login, admin, dashboard, patients, appointments, clinical, lab, billing, pharmacy, and responsive/accessibility checks.

- [ ] **Step 1: Run the existing Playwright/Cypress suite against the live frontend**
- [ ] **Step 2: Recheck the previously failing admin and deep-link flows**
- [ ] **Step 3: Capture screenshots/logs for any remaining UI issues**
- [ ] **Step 4: Report residual failures with exact route and cause**
