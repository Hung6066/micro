# His.Hope UI Patterns

This is the implementation contract for all three Angular applications. New screens should compose these primitives before adding local CSS.

## UX Presets

The data/API contract is independent from the visual UX. The default preset is `expo` and can be changed without touching feature components. Available presets are `default`, `expo`, `linear`, and `intercom`:

```text
/users?ui=expo
/users?ui=default
/users?ui=linear
/users?ui=intercom
```

For a persistent local preview, set `localStorage.setItem('hh-ui-preset', 'expo')` and reload. Presets own tokens, shell treatment, card depth, button geometry, and responsive density; feature screens keep the same `hh-*` component contracts.

## Page Recipe

Use one predictable structure:

```html
<hh-toolbar label="User management">
  <h1 hhToolbarTitle class="page-title">Users</h1>
  <button hh-toolbar-actions mat-raised-button color="primary">
    <mat-icon>add</mat-icon>
    New user
  </button>
</hh-toolbar>

<hh-table-shell label="Users">
  <table mat-table [dataSource]="rows">...</table>
</hh-table-shell>
```

For non-table pages, use `hh-page-header` followed by a full-width section or `hh-state`.

## State Rules

- Loading: use `hh-state kind="loading"`; do not duplicate spinners per page.
- Empty: explain what is missing and provide one next action when possible.
- Error: show a short human message and a visible Retry action.
- Success: use semantic green only for positive status, never as a decorative accent.

## Responsive Rules

- Page content may use the shared `--max-width-container` token, but repeated data tables must use `hh-table-shell`.
- Tables are fluid on desktop and horizontally scrollable on narrow screens. Never shrink technical IDs into unreadable columns.
- Toolbars wrap actions below the title at narrow widths; actions remain at least `--touch-target` high.
- Form fields use full available width on mobile and never set a fixed width without a responsive constraint.
- Do not hide primary actions on mobile; move them below the title or into a menu.

## Visual Rules

- Use shared tokens from `shared/frontend-foundation/src/styles/_tokens.scss`.
- Use `--font-size-title`, `--font-size-section`, `--font-size-body`, and `--font-size-caption` instead of local values.
- Use semantic tokens `--color-success`, `--color-info`, `--color-warning`, and `--color-danger` for meaning.
- Keep cards flat and restrained. Avoid nested cards and decorative gradients.
- Use Material icons inside buttons and provide an accessible label for icon-only actions.

## Reuse Checklist

Before adding a component, check whether the screen can be composed from `hh-brand`, `hh-page-header`, `hh-toolbar`, `hh-state`, `hh-table-shell`, `hh-status-badge`, `hh-metric-card`, and `hh-confirm-dialog`. Local CSS should describe domain layout only, not global typography, color, spacing, or table behavior.

For enterprise interactions, also prefer `hh-data-table`, `hh-form-field` and `hh-toast-outlet`. Every destructive action uses `hh-confirm-dialog`; every async table has loading, empty, error and retry states; every form control has a visible label and validation message.
