# Phase 4: Design System — Material 3 Migration

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Migrate Angular Material 2 theme to Material 3 token system while preserving the clinical green aesthetic. Convert M2 palette to M3 color roles. Ensure WCAG 2.1 AA accessibility.

**Working directory:** `D:\AI\micro\src\Frontend\his-hope-app`

## Key Files

- `src/styles/_theme.scss` — M2 theme currently using `m2-define-palette` (from P1). Must convert to full M3.
- `src/styles/styles.scss` — Global overrides with custom CSS properties (--bg-warm, --color-primary, etc.)
- `src/index.html` — Material Icons CDN link
- All component `@Component({ styles: [...] })` — inline component styles

## Task 1: Branch + Snapshot

- [ ] `git checkout main && git pull && git checkout -b chore/angular-19-upgrade-p4`
- [ ] Baseline build + test (451/451)
- [ ] Commit snapshot

## Task 2: Convert M2 Theme to M3

Agent: @dotnet

Replace `src/styles/_theme.scss` with full M3 token-based theme.

**Current state:** M2 theme using `mat.m2-define-palette()` and `mat.m2-define-light-theme()`.

**Target:** M3 theme using `mat.define-theme()` with color roles:

```scss
@use '@angular/material' as mat;

$clinical-theme: mat.define-theme((
  color: (
    theme-type: light,
    primary: mat.$green-palette,     // or custom palette
    // ... adjust for clinical green
  ),
  typography: (
    plain-family: 'Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
    brand-family: 'Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
    bold-weight: 700,
    medium-weight: 500,
    regular-weight: 400,
  ),
  density: (
    scale: -2,
  ),
));

html {
  @include mat.all-component-themes($clinical-theme);
  @include mat.system-level-colors($clinical-theme);
  @include mat.system-level-typography($clinical-theme);
}
```

**Key changes:**
1. `mat.m2-define-palette()` → custom M3 color roles or use `mat.$green-palette`
2. `mat.m2-define-light-theme()` → `mat.define-theme()`
3. `mat.elevation-classes() + mat.app-background()` → `mat.all-component-themes()`
4. Typography: `m2-define-typography-level` → M3 typography config
5. Density: preserve `-2` (compact for clinical)

**Important:** The clinical green `#2F6B4A` must be preserved. If `mat.$green-palette` doesn't match, define a custom palette using M3 color roles.

- [ ] **Step 1: Write new `_theme.scss` with M3 tokens**
- [ ] **Step 2: Verify build passes**
- [ ] **Step 3: Visual check — all Material components render correctly**
- [ ] **Step 4: Button variants correct (filled/outlined/text instead of raised/stroked/basic)**
- [ ] **Step 5: Commit**

## Task 3: Update Component-Level Styles

Agent: @dotnet

Many components have inline styles using M2-specific CSS selectors or custom properties. Need to sync with M3 token system.

**Patterns to update:**

1. **Card styles:** Remove custom `box-shadow: none; border: 1px solid` — M3 cards have proper elevation tokens now
2. **Status badges:** Replace custom `background: #xxx` with `background: var(--mat-sys-primary-container)`
3. **Custom CSS properties:** Replace `--color-primary` with `var(--mat-sys-primary)`, `--bg-warm` with `var(--mat-sys-surface)`
4. **Form fields:** M3 default is `outlined` — update any explicit `appearance="fill"` to `appearance="outlined"`

**Key CSS token replacements in `styles.scss`:**

| Before (custom) | After (M3 token) |
|---|---|
| `--bg-warm: #F7F6F3` | `var(--mat-sys-surface)` |
| `--surface-white: #FFFFFF` | `var(--mat-sys-surface-container-lowest)` |
| `--color-primary: #2F6B4A` | `var(--mat-sys-primary)` |
| `--color-warn: #C25450` | `var(--mat-sys-error)` |
| `--shadow-dropdown: 0 4px 12px rgba(...)` | `var(--mat-sys-level1)` |
| `--shadow-modal: 0 8px 32px rgba(...)` | `var(--mat-sys-level3)` |

**Status badge color mapping:**

| Status | Before | After (M3) |
|---|---|---|
| active/confirmed | `background: #E8F5E9; color: #2F6B4A` | `var(--mat-sys-primary-container) / var(--mat-sys-on-primary-container)` |
| pending/scheduled | `background: #E3F2FD; color: #1565C0` | `var(--mat-sys-tertiary-container) / var(--mat-sys-on-tertiary-container)` |
| cancelled/voided | `background: #FFEBEE; color: #C62828` | `var(--mat-sys-error-container) / var(--mat-sys-on-error-container)` |
| completed/filled | `background: #E8F5E9; color: #2F6B4A` | `var(--mat-sys-secondary-container) / var(--mat-sys-on-secondary-container)` |

- [ ] **Step 1: Update `styles.scss` — replace custom properties with M3 tokens**
- [ ] **Step 2: Update component inline styles — card, button, table overrides**
- [ ] **Step 3: Update status badge CSS in all components**
- [ ] **Step 4: Build + test**
- [ ] **Step 5: Commit**

## Task 4: Accessibility Audit

Agent: @check-ui

- [ ] Run axe DevTools on all key pages: login, dashboard, patient list, patient detail, appointments, admin
- [ ] Verify color contrast ratios ≥ 4.5:1 for text, ≥ 3:1 for large text
- [ ] Verify focus indicators visible on all interactive elements
- [ ] Test keyboard navigation: Tab, Enter, Escape, Arrow keys
- [ ] Verify skip-to-content link works
- [ ] Test with `prefers-reduced-motion`
- [ ] Fix any WCAG 2.1 AA violations

## Task 5: Self-Host Material Icons

Remove Google Fonts CDN dependency. Self-host Material Icons.

- [ ] Install `material-icons` npm package: `npm install material-icons@latest`
- [ ] Update `angular.json`: add `"node_modules/material-icons/iconfont/material-icons.css"` to styles
- [ ] Remove `<link href="https://fonts.googleapis.com/icon?family=Material+Icons">` from `index.html`
- [ ] Verify icons render correctly
- [ ] Commit

## Task 6: Final Verification

- [ ] `npm run build -- --configuration production` — SUCCESS
- [ ] `npm run build -- --configuration production-vi` — SUCCESS
- [ ] `npm test -- --no-watch --browsers ChromeHeadless` — 451 PASS
- [ ] No M2 API calls remaining (`grep -r "m2-define" src/styles/`)
- [ ] Visual: all components render with M3 design
- [ ] Accessibility: WCAG AA compliant (axe scan clean)
- [ ] Icons self-hosted (no Google Fonts CDN)
- [ ] Vietnamese i18n renders correctly

- [ ] Push + merge
