# Enterprise Readiness Matrix

This document is the release truth for the shared foundation. `Implemented` means the package contains a reusable implementation. `Contract` means the package defines the boundary, while the consuming app still owns the domain behavior.

| Area | Status | Evidence / next action |
| --- | --- | --- |
| Versioned package, peer dependencies, changelog, compatibility, pack check | Implemented | `package.json`, `CHANGELOG.md`, `COMPATIBILITY.md`, `RELEASE.md` |
| DataTable client sorting, pagination, selection, column visibility | Implemented | `his-hope-data-table.component.ts` |
| DataTable server query contract | Contract | `mode="server"`, `HisHopePageQuery`, `queryChange`; each feature must connect its API adapter |
| Typed inline editors and synchronous validation | Implemented | text, number, date, select, autocomplete and `editValidator` |
| Save pending/error/dirty/undo | Contract | `savingRowKey`, `rowEditUndo`, `rowEditSave`; persistence and rollback belong to the feature API |
| Bulk actions and export | Contract | `bulkActions`, `bulkActionRequested`, `exportRequested`; authorization and file generation belong to the feature |
| Column resize/reorder | Implemented | Pointer/keyboard resize and drag reorder emit persistence events; the consuming app owns preference storage |
| Command palette, offline banner, toast, audit feedback, i18n | Implemented | exported services/components and usage contracts |
| Permission-aware actions | Implemented | `hh-permission-button`; server authorization remains mandatory |
| Focus trap/Escape/restore | Implemented for confirm dialog; partial elsewhere | Drawer/popover need the same interaction test contract |
| Dark mode, high contrast, theme service | Implemented | `HisHopeThemeService` owns document-root theme/contrast state and restores persisted preferences |
| Axe automation and visual regression | Partial | Storybook a11y gate is configured; CI must add browser axe and screenshot jobs |
| Storybook states and usage/migration/accessibility docs | Implemented for foundation states | Feature-specific stories and product screenshots remain app responsibilities |
| Full migration of every feature table and hard-coded token | Partial | remaining feature pages must be migrated incrementally; `validate:foundation` checks the shared catalog, while app-level token/i18n migration remains required |

P0 runtime contracts now also include `HisHopePermissionService` (populate with `setSnapshot` after `/me/permissions`, clear on logout; wildcard permissions are supported) and `HisHopePerformanceTelemetryService` (vendor-neutral metric reporting with bounded in-memory diagnostics). Design-token lint is enforced for newly added shared component lines in both frontend CI workflows.

The package is production-shaped but is not a claim that all three applications have completed the migration. A release may be called enterprise-ready only when all `Contract`, `Partial`, and `Planned` rows have application-level evidence.
