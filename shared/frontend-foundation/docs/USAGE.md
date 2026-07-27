# Shared Foundation Usage

The foundation is the product UI contract for His.Hope applications. Feature code owns domain data and routing; the foundation owns layout, states, controls, accessibility semantics, and tokens.

## Do

- Use `hh-page-layout`, `hh-page-header`, `hh-filter-toolbar`, `hh-data-table`, and `hh-state` for page structure.
- Pass stable `id` or `key` values to table rows.
- Use tokens from `_tokens.scss` for color, typography, spacing, and control dimensions.
- Keep destructive actions behind confirmation and emit audit feedback after the server result is known.
- Provide a loading, empty, error, and unavailable state for every remote workflow.

## Do not

- Add feature-level hex colors or arbitrary font sizes.
- Rebuild tables, buttons, badges, drawers, or dialogs inside a feature page.
- Treat a successful build as accessibility evidence. Run the Storybook a11y gate and keyboard checks.
- Hide a permission-denied action without an explicit product decision. Prefer a disabled action with an explanation when discoverability matters.

## DataTable contract

Use `mode="server"` with `totalItems`, `pageChange`, `pageSizeChange`, and `sortChange`. The application owns the HTTP request and replaces `rows`; the component never silently performs a second request. Inline editors support text, number, date, select, and autocomplete. Use `editValidator` for synchronous field validation and `savingRowKey` while the save request is pending.

`exportRequested`, `bulkActionRequested`, and `rowEditUndo` are explicit domain events. The consuming application must implement authorization, persistence, audit logging, and undo policy.
