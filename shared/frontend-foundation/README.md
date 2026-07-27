# His.Hope Frontend Foundation

The shared frontend foundation is the visual and interaction seam for the three Angular applications.

The project-wide visual contract lives in [`DESIGN.md`](../../DESIGN.md). Read it before adding a new screen or component; this package implements its tokens and primitives.

## What belongs here

- `src/styles/_tokens.scss`: colors, typography, spacing, radii and layout variables.
- `src/styles/_material-theme.scss`: the single Angular Material theme.
- `src/styles/_foundation.scss`: reset, component states, tables, forms and responsive utilities.
- `src/ui/`: small standalone primitives used by feature pages.
- `src/auth/` and `src/http/`: shared cross-app behavior and response normalization.

## Create a new page

Feature pages should own only domain data and actions. Compose the shared primitives:

```html
<hh-page-header title="Patients" subtitle="Manage the hospital patient registry">
  <button mat-flat-button color="primary" (click)="create()">
    <span class="material-icons">add</span>
    New patient
  </button>
</hh-page-header>

<mat-card>
  @if (loading) {
    <hh-state kind="loading" message="Loading patients..." />
  } @else if (error) {
    <hh-state kind="error" icon="error" [message]="error">
      <button mat-stroked-button (click)="reload()">Retry</button>
    </hh-state>
  } @else if (patients.length === 0) {
    <hh-state icon="person_search" message="No patients found." />
  } @else {
    <!-- Domain-specific table only. Shared table styling is global. -->
  }
</mat-card>
```

Use composition for UI. Use inheritance only for genuinely shared behavior, such as a small `LoadablePage` state model. Do not create a base component for markup or CSS.

## Rules

1. New UI gets tokens and shared primitives first; feature CSS is for domain layout only.
2. No app-specific Material palette, font stack or button reset.
3. Every data page has loading, error, empty and mobile states.
4. Use standalone imports from `@his-hope/frontend-foundation`.
5. Keep the foundation dependency-light: primitives use native HTML and CSS where Material is not needed.

The shared UX primitives include `hh-filter-toolbar`, `hh-skeleton` and `hh-confirm-dialog`. Prefer these over browser alerts, ad-hoc loading text and page-specific filter wrappers.

For complete integration examples, API contracts, accessibility rules, theme/i18n setup, DataTable adapters, and CI commands, read [`docs/INTEGRATION.md`](./docs/INTEGRATION.md).

### Enterprise interaction contracts

- `hh-confirm-dialog` is an accessible destructive-action dialog with Escape handling, focus trapping and focus restoration.
- `hh-data-table` supports loading/error/empty states and, in configured data mode, sorting, pagination, row selection and column visibility.
- `hh-filter-toolbar` supports a debounced search field and a clear action while still accepting projected filters.
- `hh-form-field` standardizes labels, required markers, hints, validation errors and dirty/disabled states. Feature controls should bind `aria-describedby` to the field's hint/error id.
- `hh-toast-outlet` and `HisHopeToastService` provide consistent transient notifications.
- `hh-drawer`, `hh-popover`, `hh-tabs`, `hh-breadcrumb` and `hh-permission-button` provide shared shell/action contracts for feature modules.

The package is versioned in `shared/frontend-foundation/package.json` and follows the API rules in [`COMPATIBILITY.md`](./COMPATIBILITY.md). Applications currently consume the workspace package source for local development; release builds must run `npm run release:check` before publishing the restricted package.

## Mobile component set

The mobile components are standalone, Ionic-inspired primitives with His.Hope tokens and no Ionic runtime dependency:

- `hh-mobile-infinite-list`: feed semantics, load-more action, end state and loading skeleton.
- `hh-mobile-refresher`: touch pull-to-refresh contract for native and mobile web shells.
- `hh-mobile-searchbar`: 250ms debounced search, clear action and accessible label.
- `hh-mobile-action-sheet`: bottom action menu with backdrop and Escape dismissal.
- `hh-mobile-bottom-sheet`: detail/filter surface with safe-area padding and Escape dismissal.
- `hh-mobile-segment`: accessible tab-like view switcher.
- `hh-mobile-accordion`: compact expandable information section.
- `hh-mobile-avatar`: image or initials avatar with accessible name.
- `hh-mobile-date-time`: native date or datetime-local control with shared field styling.
- `hh-mobile-otp`: numeric MFA/verification code input with auto-advance and paste handling.

Import them from `@his-hope/frontend-foundation`. Sensitive healthcare actions belong in the action sheet or detail surface and must not use swipe-to-delete.

## Enterprise interaction primitives

- `hh-confirm-dialog` provides `alertdialog` semantics, labelled description, focus trapping, Escape-to-cancel and focus restoration.
- `hh-data-table` owns loading, error, empty and compact-density states while feature pages project the domain table.
- `hh-form-field` owns label, required marker, hint and validation error presentation.
- `hh-toast-outlet` renders notifications from `HisHopeToastService`; call `toast.success(...)`, `toast.error(...)` or the other semantic methods from feature services.

Example:

```html
<hh-form-field controlId="email" label="Email" [required] error="Email is required">
  <input id="email" type="email" />
</hh-form-field>

<hh-data-table [loading]="loading" [empty]="!rows.length" [error]="error" (retry)="reload()">
  <table mat-table [dataSource]="rows"></table>
</hh-data-table>
```
