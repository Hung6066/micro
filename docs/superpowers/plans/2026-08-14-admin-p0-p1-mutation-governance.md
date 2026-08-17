# Implementation plan

1. Add focused component tests proving mutation controls are disabled for missing permissions.
2. Add minimal permission checks to users, roles, clients, access-management, mobile, security-provider, identity-capability, and database-platform mutation controls.
3. Add a shared-foundation-compatible Role create/update dialog and wire it to `createRole`/`updateRole`.
4. Run admin/shared Angular tests, all three app builds, authorization contract/coverage scripts, Docker rebuild/restart, and compose smoke.
