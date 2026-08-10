# Accessibility Contract

## Interaction contracts

- Dialogs, drawers and command surfaces trap `Tab`, close with `Escape`, and restore focus to the opener.
- Language switching is a vertical listbox: `ArrowUp`/`ArrowDown`, `Home`, `End`, `Escape`, and `Tab` are supported; the selected option is the roving-tabindex item.
- Tabs use roving keyboard focus with `ArrowLeft`/`ArrowRight`, `Home`, and `End`.
- DataTable selection exposes the selected scope (`page`, `all`, or `explicit`) and selected/excluded keys through `selectionStateChange`; bulk actions must preserve that contract server-side.
- Status and error surfaces use semantic `role="status"` and `role="alert"`; permission-denied explanations are referenced with `aria-describedby`.

- Every interactive control has a visible name or an `aria-label`.
- Errors use `role="alert"`; progress/status messages use `role="status"`.
- Dialog and drawer implementations must trap focus, close on Escape, and restore focus to the opener.
- Table sorting is keyboard reachable and exposes a visible direction icon. Selection controls have row and table-level labels.
- Keyboard support is part of the component API: Tab enters controls, Enter activates, Space activates buttons/row actions, and Escape dismisses transient UI.
- Theme tokens must support light, dark, high contrast, and the system preference. Never encode state using color alone.

The Storybook a11y addon is configured as an error gate. Add an interaction story for every new component state before release.
