# Admin-app P0/P1 mutation governance

## Scope

Harden the existing admin-app mutation UX without weakening server authorization. P0 covers permission-aware visibility and confirmation for destructive or privileged actions. P1 adds missing Role create/update lifecycle UI and explicit user lifecycle semantics.

## Decisions

- The Identity Service remains the authorization source of truth; UI guards are presentation and navigation controls only.
- Mutation controls require the matching permission snapshot before rendering/enabling.
- Destructive actions use the existing shared confirmation pattern and require an explicit reason where the API supports it.
- Users have no hard-delete operation: import creates, role assignment/activation updates, and deactivation is the reversible lifecycle end state.
- Roles support create/update through the existing API; delete remains bulk-only and publish/rollback are lifecycle mutations.

## Acceptance criteria

1. No admin mutation button is visible/enabled without its declared permission.
2. Role create/update UI calls the existing API and reports errors through shared UI feedback.
3. Backend authorization contract and endpoint inventory remain unchanged and passing.
4. Admin, clinical Angular, dashboard, and shared-foundation builds/tests pass; Docker admin image restarts healthy and internal smoke remains green.
