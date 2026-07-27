# Shared Foundation Enterprise Roadmap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Make the shared Angular foundation a consistent, accessible, agent-friendly enterprise design system consumed coherently by all three applications.

**Architecture:** Keep domain behavior in each app, but move cross-app interaction contracts into `shared/frontend-foundation`. Theme and locale are single runtime services; DataTable exposes typed query/state events; accessibility behavior is tested at the component boundary; CI enforces tokens, i18n, Storybook and browser contracts.

**Tech Stack:** Angular 19, TypeScript, SCSS design tokens, Storybook 9, Playwright, axe-core, Docker Compose.

## Global Constraints

- Preserve existing user changes and work with the dirty checkout.
- Keep shared components standalone and export public contracts through `src/index.ts`.
- Do not make server authorization a client-only concern.
- Do not claim application-wide adoption until all three app builds and browser checks pass.
- Keep feature/domain API adapters in the consuming app; the foundation owns reusable UI and typed events.

### Task 1: Unified Theme and I18n Runtime

**Files:**
- Modify: `shared/frontend-foundation/src/theme/his-hope-theme.service.ts`
- Modify: `shared/frontend-foundation/src/i18n/his-hope-i18n.service.ts`
- Modify: `shared/frontend-foundation/src/i18n/his-hope-language-switcher.component.ts`
- Modify: `shared/frontend-foundation/src/styles/_tokens.scss`
- Modify: `admin-app/src/app/app.component.ts`, `dashboard-app/src/app/app.component.ts`, `src/Frontend/his-hope-app/src/app/app.component.ts`
- Modify: the three app `index.html` and styles only where required to remove duplicate theme state
- Test: shared service/component tests and shell E2E checks

Deliver one document root contract for `data-theme`, `data-contrast`, `lang`, and locale persistence. Restore persisted values on startup, support system theme changes, and expose locale options with keyboard and pointer behavior.

### Task 2: DataTable Enterprise Contract

**Files:**
- Modify: `shared/frontend-foundation/src/contracts/his-hope-ui-contracts.ts`
- Modify: `shared/frontend-foundation/src/ui/his-hope-data-table.component.ts`
- Modify: `shared/frontend-foundation/src/ui/his-hope-foundation.stories.ts`
- Modify: app page adapters that currently use `hh-data-table`
- Test: DataTable component tests and Playwright interaction coverage

Use typed row/query contracts, explicit server-page selection scope, stable edit save/undo lifecycle, and an adapter-facing export/bulk event contract. Keep column order/width/visibility persistence at the app boundary.

### Task 3: Accessibility and Interaction Contracts

**Files:**
- Modify: `shared/frontend-foundation/src/ui/his-hope-navigation.component.ts`
- Modify: `shared/frontend-foundation/src/ui/his-hope-confirm-dialog.component.ts`
- Modify: `shared/frontend-foundation/src/ui/his-hope-command-palette.component.ts`
- Modify: shared language switcher and status/toast components as needed
- Modify: `tests/e2e/specs/shared-foundation.spec.js`
- Modify: `shared/frontend-foundation/docs/ACCESSIBILITY.md`

Fix duplicate ARIA IDs, preserve native table semantics, define keyboard contracts for drawer/popover/tabs/listbox/command palette, and test focus trap, Escape, focus restore, loading/error announcements, and status semantics.

### Task 4: Agent Readiness and Quality Gates

**Files:**
- Create: `shared/frontend-foundation/docs/component-catalog.json`
- Modify: `shared/frontend-foundation/package.json`, root package scripts, and `tests/e2e/package.json`
- Create or modify: CI workflow and validation scripts for token/i18n contract checks
- Modify: `shared/frontend-foundation/docs/ENTERPRISE-READINESS.md`, `INTEGRATION.md`, `CHANGELOG.md`

Publish machine-readable component metadata, add stories for every shared component and important state, enforce axe/visual/keyboard checks in CI, and prevent hard-coded feature UI tokens or untranslated user-visible strings from entering shared migration paths.

### Task 5: Integration Gate

Run shared build, Storybook build, all three Angular builds, package dry-run, focused unit/component tests, Playwright axe/keyboard/visual tests, Docker Compose rebuild, HTTP probes for ports 8081-8083, then inspect the final diff and update the readiness matrix with evidence.
