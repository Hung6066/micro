# Shared Component Contract

## Accessibility

- Every error state uses `role="alert"`; loading and neutral state use `role="status"`.
- Dialogs must trap Tab focus, close on Escape, and restore focus to the trigger.
- Status badges expose a text label and `role="status"`; color is never the only state signal.
- Interactive controls expose visible focus and a minimum 44px touch target.

## Data workflows

Use `hh-data-table` for collection screens. The configured data mode accepts column metadata and rows, emits sort/page/selection events, and exposes loading, error, empty and retry states. Domain screens own server-side fetching and persist the emitted query state.

## Theming

Use shared tokens for color, typography, spacing and controls. `data-ui-preset` changes visual direction; `data-theme="dark"` and `prefers-contrast: more` provide the shared accessibility variants.
