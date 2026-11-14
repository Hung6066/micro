# Operator Mobile Design

## Goal

Build `operator-mobile`, a dedicated Angular and Capacitor application for
manufacturing field operations. It must provide production, quality control,
maintenance, and traceability workflows on a phone, while retaining the
existing Manufacturing domain workflows, operator tenant isolation, and native
mobile security boundaries.

The application supports all three operational roles from the first
architecture. Delivery is incremental by module; it does not turn the desktop
operator application into a mobile replica.

## Scope and delivery order

1. Foundation: native OIDC, biometric lock, tenant selection, permission-aware
   navigation, encrypted local storage, and a visible sync queue.
2. Production and traceability: assigned batches, start/pause/resume/complete,
   operation results, yield and loss, barcode/QR lot lookup, and consumption of
   authorised lots.
3. Quality: lot/specification review, inspection result capture, and deviation
   creation.
4. Maintenance: assigned work orders, checklists/evidence, telemetry and
   downtime recording, and completion.
5. Push notifications, personal dashboard, and workflow optimisation.

Master-data management, procurement, organisation-wide analytics, recipe
authoring, and global approvals remain in `internal-operator-app`.

## Roles and permissions

Mobile navigation and actions use narrow permissions. The Manufacturing API
must enforce the same permissions and tenant scope; client-side hiding is only
an experience improvement, never an authorisation boundary.

| Role | Mobile capabilities |
| --- | --- |
| Production operator | Execute assigned batches, record operations, quantities and loss, and scan/use authorised lots. |
| Quality controller | Review lots/specifications, capture inspections, and create deviations. |
| Maintenance technician | Complete assigned maintenance, record checklist/evidence/telemetry/downtime. |
| Supervisor | View progress and resolve sync conflicts; may perform specifically granted approvals. |
| Administrator | Uses the web operator application for administration. |

Example permission names are `manufacturing.production.execute`,
`manufacturing.quality.inspect`, and `manufacturing.maintenance.complete`.
Routes require the relevant read or mutation permission and the server applies
the equivalent policy to every endpoint. Every request is scoped to the active
tenant and authenticated subject.

## Application architecture

`operator-mobile` is a separate Angular host application. It reuses
`@his-hope/mobile-foundation` for native adapters and
`@his-hope/frontend-foundation` for UI, i18n, error handling and HTTP
interceptors. It does not import desktop feature components from
`internal-operator-app`.

The host provides:

- secure Authorization Code + PKCE OIDC storage, biometric lock, native camera
  and lifecycle/network adapters;
- `OperatorMobileTenantContextService`, which loads the operator's permitted
  tenant list and changes the active scope safely;
- permission guards and a compact bottom-navigation shell whose entries depend
  on the active permission set;
- `OperatorMobileApiService`, the typed Manufacturing API boundary;
- modules for Production, Traceability, Quality and Maintenance; and
- `OperationQueueService`, the sole entry point for offline mutations.

Feature components submit typed commands only. They never import Capacitor
plugins, write browser storage, or decide retry policy. The queue encrypts each
command and records its tenant, authenticated subject, idempotency key,
creation time, entity version and status.

## Online and offline data flow

On selecting a tenant, the application refreshes the operator's assigned work
and caches only the active-shift data required for the three operational
modules. While online, a mutation is sent immediately. On a network failure,
the same command is stored in the encrypted queue and becomes visible as
pending.

When connectivity returns, the queue synchronises one command at a time using
its original idempotency key. The Manufacturing API validates subject,
permission, tenant, workflow state and expected version before applying the
command. A confirmed response marks the command synced. Business validation
failures and version conflicts become actionable queue entries; the client
does not overwrite server state automatically.

Changing tenant, logging out, a revoked permission, or an expired session stops
sync and removes cached commands/data outside the currently valid scope. High
risk actions (final approvals, tenant changes and master-data changes) are
online-only.

## Backend changes

The existing Manufacturing API already exposes the core production, lot,
quality and maintenance workflows. It requires additive cross-cutting changes:

- permission policies for field mutations;
- tenant and authenticated-subject validation on every mutation;
- an idempotency record keyed by tenant, subject, endpoint and operation key;
- expected-version/state conflict responses suitable for queue resolution; and
- audit events that retain actor, tenant, device operation ID and source.

Domain workflows remain inside existing Application ports and Infrastructure
adapters. The mobile client does not introduce a second workflow engine.

## Failure handling and security

Network and transient server errors retry with bounded backoff. A business 4xx
error never retries. An unauthorised, forbidden, tenant mismatch, or expired
session response pauses sync and requires a safe refresh of permissions or
login. The Sync screen separates pending, synced, failed and conflicted
commands; a user can inspect, reload context, or discard only the local command.

Cached operating data and queued command payloads use device-backed secure
storage. Access tokens never use web storage. The app follows the existing
native certificate pinning, deep-link allow-list and secure OIDC callbacks
provided by the mobile foundation.

## Verification and acceptance gates

- Unit tests cover permission guards, navigation visibility, queue encoding,
  retry decisions, idempotency keys and conflict transitions.
- Manufacturing integration tests prove each field permission, tenant
  isolation, idempotency replay and expected-version conflict.
- Playwright tests cover production, QC and maintenance roles; denied actions;
  tenant switching; and offline-to-online success, failure and conflict paths.
- Android/iOS runtime gates verify OIDC login/callback, secure storage,
  biometrics and QR/barcode scan on real emulator/device environments.

The first release is complete only when each of Production, Quality and
Maintenance has an authorised field workflow, all queue terminal states are
visible and recoverable, the API rejects wrong tenant/permission attempts, and
the applicable build, lint, unit, integration and E2E gates have evidence.
