# Changelog

## Unreleased

- Added ten Ionic-inspired mobile primitives for list loading, refresh, search, sheets, segments, accordion, avatar, date/time and OTP flows.
- Added centralized `HisHopePermissionService` for permission-aware controls.
- Added vendor-neutral `HisHopePerformanceTelemetryService` for frontend performance metrics.
- Added design-token lint enforcement to the foundation CI gates.
- Wired preset `components.button/table/navigation` variants to `data-ui-*` attributes and generic `_presets.scss` rules, so every registry preset drives a full button/table/navigation look, not just color tokens. Added `<hh-preset-switcher />` to swap the active design system at runtime.

## 1.1.0 - 2026-07-25

- Published an Angular package artifact through ng-packagr with public Sass and type entrypoints.
- Added cross-tab locale/theme synchronization and dark/high-contrast token coverage.
- Added cursor pagination, virtualized table windows, bulk-job status and inline conflict resolution contracts.
- Added multi-column sort, URL query synchronization, unified debounced query service, keyboard column reorder, CVA table editors, row retry/undo events, and async export-job contracts.

## 1.0.0 - 2026-07-25

- First versioned release of the His.Hope frontend foundation.
- Shared page layout, navigation, state, status, form, table and notification contracts.
- DataTable sorting, pagination, selection, column visibility and optional inline editing.
- DataTable bulk/export events emit stable row keys instead of full row payloads to reduce sensitive-data exposure.
- Auth coordinator validates same-origin return URLs and reduces background session polling when the tab is hidden.
- Accessibility contracts for dialogs, status messages and keyboard-visible controls.

Release policy: breaking public API changes require a major version; additive inputs/outputs are minor releases; styling fixes and documentation are patch releases.
