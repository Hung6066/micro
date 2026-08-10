# Identity and Frontend Contract Synchronization

## Goal

Make Identity Service the authoritative source for authentication, authorization, session state, audit, and administrative data while the Angular applications consume typed, least-privilege contracts.

## Decisions

1. Identity Service remains the security boundary. UI permission controls are discoverability only; every mutation is authorized server-side.
2. Admin list endpoints use one paged response contract and whitelist query fields.
3. Bulk and export requests contain stable row keys and query state, never full row payloads.
4. OIDC return URLs are validated both in the browser coordinator and by the identity server/client registration.
5. Frontend uses one authenticated HTTP error contract for 401, 403, 409, and validation failures.
6. Security and contract tests are release gates; Docker health alone is not release evidence.

## Workstreams

- Identity API: verify and harden session, authorization, paging, audit, and ProblemDetails behavior.
- Frontend adapters: centralize admin API query/bulk/export/error handling and bind pages to shared contracts.
- Foundation: keep permission, DataTable, auth, audit, and i18n primitives framework-agnostic and token-based.
- Delivery: add contract/security checks, dependency scanning, CSP/header probes, and responsive/authenticated browser checks.

## Non-goals

- Moving domain authorization into Angular.
- Replacing OpenIddict or introducing a second identity provider.
- Sending or persisting patient data in the shared UI package.
