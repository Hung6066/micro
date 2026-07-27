# His.Hope Design System

version: 1.0
status: active
scope: shared frontend foundation for the main clinical app, operations dashboard, and identity admin app

authoring: Open Design-compatible system at docs/design-systems/his-hope-open-design/

## Design direction

His.Hope is a clinical operations product, not a marketing site. The interface should feel calm, precise, trustworthy, and quick to scan during repeated work.

The visual direction combines four reference qualities:

- **Linear:** dense information architecture, quiet borders, disciplined spacing, precise controls, and restrained decoration.
- **Intercom:** approachable copy, obvious next actions, forgiving empty and error states, and friendly interaction feedback.
- **Supabase:** white and near-white surfaces with a restrained emerald primary action and strong technical clarity.
- **HashiCorp:** enterprise status semantics, explicit system health, product-area accents, and operational confidence.

Use these as design principles only. Do not copy another product's logo, proprietary font, illustrations, branded wording, or exact page composition.

## Product principles

1. **Clinical clarity first.** A user must identify the current workspace, patient or system context, and next safe action immediately.
2. **Scan before read.** Use short labels, stable columns, strong alignment, and predictable density for repeated work.
3. **One primary action.** Each surface has one visually dominant action. Secondary actions stay quiet and tertiary actions use links or icon buttons.
4. **State is explicit.** Loading, empty, warning, error, healthy, and unavailable states are always visible and human-readable.
5. **Trust without drama.** Use green for healthy/success, amber for attention, red for destructive/error, and blue-green for active navigation. Do not use gradients or decorative glow.
6. **Accessible by default.** Keyboard focus, contrast, labels, disabled states, and touch targets are part of the component contract.

## Tokens

### Color

```yaml
canvas: "#F2F6F3"
surface: "#FFFFFF"
surface-muted: "#F7F9F7"
ink: "#1A1A1A"
ink-secondary: "#5D5F5A"
ink-muted: "#7F857F"
border: "#E1E7E2"
border-strong: "#C9D4CC"
brand-deep: "#173F2D"
brand: "#2F6B4A"
brand-hover: "#21583F"
brand-soft: "#E7F1EB"
success: "#237A4B"
warning: "#A66A00"
danger: "#B42318"
info: "#2563A6"
focus: "#79B99A"
```

Use the existing CSS custom properties in `shared/frontend-foundation/src/styles/_tokens.scss`. Add aliases there before introducing a new color. Product areas may use a small accent color for identification, but semantic meaning always wins over branding.

### Typography

Use the shared sans stack everywhere except code, IDs, timestamps, and technical payloads:

```yaml
font-sans: 'Aptos, Segoe UI Variable, Segoe UI, Noto Sans, Arial, sans-serif'
font-mono: 'Cascadia Mono, Consolas, SFMono-Regular, Menlo, Monaco, monospace'
```

```yaml
page-title: 24px / 700 / 1.25
section-title: 20px / 650 / 1.35
card-title: 16px / 650 / 1.35
body: 14px / 400 / 1.5
body-strong: 14px / 600 / 1.5
caption: 12px / 500 / 1.4
```

Do not use browser default serif fonts. Do not use negative letter spacing in application UI. Display typography may be larger on login or marketing-like identity surfaces, but operational pages stay compact.

### Layout and shape

```yaml
shell-header: 64px
shell-sidebar: 264px
content-max: 1200px
page-padding: 28px 32px
grid-gap: 16px
card-radius: 8px
button-radius: 6px
input-radius: 4px
control-height: 40px
touch-target: 44px minimum
```

Prefer full-width workspace bands and constrained inner content. Use cards only for repeated records, metrics, framed tools, and dialogs. Do not place cards inside cards.

## Application shell

All three applications use the same shell contract:

- dark emerald top bar with product name and global session actions;
- white sidebar with brand, workspace caption, active navigation state, and consistent icon alignment;
- warm green-tinted canvas for page content;
- constrained content column with a clear page header;
- responsive collapse below tablet width;
- persistent keyboard-visible focus states.

The main clinical app may expose more navigation groups. The dashboard and admin app may expose fewer, but they must retain the same shell geometry, typography, and active-state treatment.

## Component rules

### Navigation

- Use familiar Lucide or Material icons with text labels.
- Keep icon boxes at a stable 20px and the hit area at least 44px.
- Active navigation uses a light brand-soft background and a clear brand edge or text color.
- Do not use icon-only navigation when the meaning is not universally familiar.

### Buttons

- Primary: brand background, white text, one clear action.
- Secondary: white or transparent surface with a strong border.
- Tertiary: text or icon action with a tooltip when the icon is unfamiliar.
- Destructive: danger styling only for irreversible actions.
- Every async action has disabled/loading feedback and preserves its label where possible.

### Cards and metrics

- Use a 1px border before using a shadow.
- Metrics show label, value, supporting context, and a safe destination/action.
- Status badges include text, not color alone.
- Keep card padding between 16px and 20px and align card content to a common grid.

### Tables and lists

- Use stable column widths, visible row hover, and a clear empty state.
- Keep filters above the table and preserve them during pagination.
- Use monospace only for IDs, request paths, trace IDs, and machine data.

### Forms

- Labels are visible and persistent; placeholders are examples, not labels.
- Group related fields and expose validation beside the field.
- Preserve entered data when a request fails.
- Never hide an authentication or permission error behind a generic toast.

### States

Every data surface supports:

- loading skeleton;
- empty state with a next action;
- error state with retry and a useful message;
- permission denied state with a safe return path;
- healthy/success confirmation;
- offline or unavailable state where relevant.

## UX patterns

- Prefer inline feedback for local actions and toasts for completed background actions.
- Confirm destructive actions with a focused dialog that names the affected resource.
- Keep URL and navigation state shareable and restorable.
- Avoid request loops: data loading is explicit, cancellable, and triggered by route/filter changes only.
- Avoid layout shift by reserving space for loading, validation, and status content.
- Use Vietnamese product copy consistently in the clinical apps; technical identifiers may remain English.

## Responsive behavior

- Desktop: persistent sidebar, 1200px content constraint, 4-column metric grids where useful.
- Tablet: two-column grids, reduced page padding, sidebar may collapse.
- Mobile: one-column content, stacked page header actions, full-width controls, horizontal scrolling only for genuinely tabular data.
- Never allow labels, button text, or status values to overflow their parent.

## Do and do not

Do:

- reuse shared foundation components and tokens;
- compose feature pages from page header, filter toolbar, state, status badge, skeleton, and confirm dialog;
- use semantic color and explicit text states;
- test desktop and mobile screenshots after visual changes.

Do not:

- add a second font stack to a feature page;
- use serif defaults, decorative gradients, glassmorphism, or oversized marketing heroes in operational screens;
- create one-off button, card, or loading patterns when a shared component exists;
- use color as the only indication of health, permission, or severity;
- copy Linear, Intercom, Supabase, or HashiCorp branding verbatim.

## Reference sources

The local reference collection is cloned at `docs/design-references/awesome-design-md/`. Relevant source analyses:

- `design-md/linear.app/DESIGN.md`
- `design-md/intercom/DESIGN.md`
- `design-md/supabase/DESIGN.md`
- `design-md/hashicorp/DESIGN.md`

This file is the project-specific source of truth. The reference files are inspiration and research material, not runtime dependencies.
