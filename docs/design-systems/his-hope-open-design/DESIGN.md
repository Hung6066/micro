# His.Hope Open Design System

> A calm, precise and production-oriented interface system for hospital operations.

## Product Context

His.Hope serves clinical staff, operations engineers and identity administrators. Interfaces must support repeated work, safe decisions and rapid scanning. Use this system for web prototypes, dashboard concepts and production Angular implementation.

## Direction

- Quiet enterprise surfaces inspired by Linear and HashiCorp.
- Clear next actions and forgiving states inspired by Intercom.
- Technical clarity and restrained emerald identity inspired by Supabase.
- No gradients, decorative blobs, serif defaults or marketing-style hero layouts in operational screens.

## Typography

- Body and display: `Aptos`, `Segoe UI Variable`, `Segoe UI`, `Noto Sans`, `Arial`, sans-serif.
- Technical values: `Cascadia Mono`, `Consolas`, `SFMono-Regular`, `Menlo`, monospace.
- Body: `14px / 1.5`.
- Page title: `24px / 1.25 / 700`.
- Section title: `20px / 1.35 / 700`.
- Card title: `16px / 1.5 / 650`.
- Caption: `12px / 1.4 / 500`.
- Never use browser default typography or negative tracking in application UI.

## Layout

- 8px spacing grid.
- 64px shell header and 264px desktop sidebar.
- 1200px content maximum with 28px by 32px desktop page padding.
- 8px cards, 6px buttons, 4px inputs.
- 44px minimum interactive target.
- Use full-width workspace bands; cards are reserved for repeated records, metrics, dialogs and framed tools.

## Interaction

- One primary action per surface.
- Every async data surface has loading, empty, error with retry and permission-denied states.
- Status is expressed with text and semantic color, never color alone.
- Use 130ms to 200ms transitions with `cubic-bezier(0.2, 0, 0, 1)`.
- Preserve filters, entered form data and URL state after recoverable failures.

## Accessibility

- Visible labels, keyboard focus, disabled states and readable contrast are mandatory.
- Icons must have text labels or accessible names.
- Do not allow labels, controls or status values to overflow their parent.

## Runtime Contract

The Angular apps consume the same contract through `shared/frontend-foundation/src/styles/_tokens.scss`, `shared/frontend-foundation/src/styles/_foundation.scss` and the exported `hh-*` components. This file is the Open Design authoring source; it must stay aligned with those runtime tokens.
