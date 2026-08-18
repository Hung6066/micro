# Admin UI Action Contract

All admin feature actions use `hh-action-button` from
`@his-hope/frontend-foundation/ui`.

| Kind         | Placement                        | Mode                   | Examples                    |
| ------------ | -------------------------------- | ---------------------- | --------------------------- |
| `primary`    | Page header or toolbar           | `label`                | Create, add, save           |
| `secondary`  | Toolbar or page header           | `label`                | Refresh, cancel, clear      |
| `diagnostic` | Toolbar or a diagnostics section | `label`                | Diagnose, inspect, simulate |
| `row`        | Data-table action cell           | `icon-only`            | Edit, rotate, view          |
| `danger`     | Row or confirmation surface      | `label` or `icon-only` | Delete, revoke              |

Rules:

- Page-level create/add actions are always `primary` with an icon and visible translated label.
- Row actions are always `row` or `danger` with `mode="icon-only"`; `label` is still required for accessibility.
- Diagnostic actions are explicit `diagnostic` actions and do not share the primary create slot.
- Refresh, clear, and cancel are secondary labelled actions.
- New raw `mat-*` action buttons and `hh-button`/`hh-icon-button` CSS classes are rejected by the audit command.

Run `npm run audit:admin-ui` to inspect the migration backlog. Use
`scripts/audit-admin-ui-actions.ps1 -FailOnViolation` as the CI gate after the
legacy action backlog has been migrated.
