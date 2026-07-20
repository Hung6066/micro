# Lab Service Critical Value Alert Design

Date: 2026-07-20

## Goal

Add a critical value alert workflow to Lab Service so that critical lab results can be:
- detected from hardcoded critical flags and configurable thresholds,
- surfaced to the right users in realtime,
- acknowledged with an audit trail,
- and reviewed in the existing Lab UI.

This is an additive design. It stays inside Lab Service and the existing Angular app.

## Scope

### In scope
- Critical alert rule management.
- Result-trigger evaluation when a lab result is recorded or edited.
- Realtime in-app notifications via SignalR-style sockets.
- Alert inbox / detail UI in the lab feature area.
- Acknowledge / resolve workflow with audit history.

### Out of scope
- Email, SMS, or external paging.
- A separate alerting microservice.
- Automatic suppression based on ML.
- Replacing the existing Lab Service API shape.

## Canonical terms

- **Critical value alert**: a clinical alert raised when a lab result crosses a critical condition.
- **Rule**: a configured threshold definition for a lab test.
- **Alert**: the current lifecycle record for one critical result.
- **Acknowledgment**: a user action that records review of the alert.
- **Resolved**: the result is no longer critical, or the alert is closed after correction.

## Trigger model

V1 uses both trigger types:
1. `AbnormalFlag.CriticalHigh` / `AbnormalFlag.CriticalLow`
2. configurable numeric thresholds per test

If both trigger types match, one alert is created and both reasons are stored.

If the same result is saved again or edited, do not create a duplicate alert. Keep one current alert record and append audit history.

## Recommended architecture

### 1) Result evaluation in Lab Service
When `RecordLabResultCommand` / `RecordLabOrderResultCommand` saves a result, Lab Service will:
1. evaluate the result against the active rule set,
2. create or update the current alert row,
3. append an audit entry,
4. broadcast a realtime event to connected clients.

The existing result save path remains the source of truth.

### 2) Realtime delivery
Add a SignalR hub in Lab Service for alert notifications.

Target audiences in V1:
- the assigned clinician for the order,
- all authorized lab-role users.

The hub only delivers notifications; the database remains the source of truth for inbox state.

### 3) UI surfaces
Add three UI surfaces in the Angular lab area:
- an alert badge / count in the lab header,
- an alert inbox list,
- a detail view / drawer for acknowledgment and history.

## Data model

### `CriticalAlertRule`
Represents a test-specific threshold rule.

Suggested fields:
- `Id`
- `TestCode`
- `TestName`
- `Unit`
- `LowCriticalValue`
- `HighCriticalValue`
- `IsActive`
- `CreatedAt`
- `UpdatedAt`
- `CreatedBy`

### `CriticalAlert`
Represents the current alert for one critical result lifecycle.

Suggested fields:
- `Id`
- `LabOrderId`
- `LabTestId`
- `LabResultId`
- `RuleId` (nullable for flag-only triggers)
- `TriggerType` (`CriticalFlag`, `Threshold`, `Both`)
- `Status` (`Open`, `Acknowledged`, `Resolved`)
- `Message`
- `ResultValue`
- `ResultUnit`
- `ThresholdValue`
- `CreatedAt`
- `UpdatedAt`
- `AcknowledgedAt`
- `AcknowledgedByUserId`
- `ResolvedAt`
- `ResolvedByUserId`

### `CriticalAlertAuditEntry`
Stores the lifecycle trail.

Suggested fields:
- `Id`
- `CriticalAlertId`
- `Action` (`Created`, `Acknowledged`, `Updated`, `Resolved`)
- `ActorUserId`
- `ActorDisplayName`
- `Notes`
- `OccurredAt`

## Behavior rules

1. One current alert per lab test result lifecycle.
2. Re-saving the same critical result updates the existing alert instead of creating duplicates.
3. If a critical result is corrected to a noncritical value, the alert is resolved and history is retained.
4. Any authorized lab user or the assigned clinician may acknowledge the alert.
5. All acknowledge actions must be audited.
6. Realtime delivery must not replace persisted state.

## Backend API surface

### Rule management
- `GET /api/v1/lab-critical-alert-rules`
- `POST /api/v1/lab-critical-alert-rules`
- `PUT /api/v1/lab-critical-alert-rules/{id}`
- `DELETE /api/v1/lab-critical-alert-rules/{id}`

### Alert inbox
- `GET /api/v1/lab-critical-alerts`
- `GET /api/v1/lab-critical-alerts/{id}`
- `POST /api/v1/lab-critical-alerts/{id}/acknowledge`
- `POST /api/v1/lab-critical-alerts/{id}/resolve`

### Realtime hub messages
- `criticalAlertCreated`
- `criticalAlertUpdated`
- `criticalAlertAcknowledged`
- `criticalAlertResolved`

## Frontend design

### Lab order detail
Show critical status inline on the test result row.
- critical badge
- current alert state
- acknowledge button
- audit trail snippet

### Lab alert inbox
Add a dedicated inbox page under the lab feature.
- open / acknowledged / resolved filters
- newest-first list
- patient / order / test / result summary
- quick acknowledge action

### Rule editor
Add a simple rule form.
- test code / name
- optional unit
- low critical threshold
- high critical threshold
- active toggle

### Realtime UX
When a new alert arrives:
- show a snackbar/toast,
- increment the alert badge,
- update the inbox without refresh.

## Security and authorization

- Reuse JWT auth and permission policies.
- Rule management requires a lab-admin or equivalent permission.
- Acknowledge / resolve requires lab access plus audit identity.
- All alert actions are auditable.

## Runtime and Docker

- No new service container is required.
- Rebuild Lab Service and the Angular app as part of the feature verification.
- The realtime hub must be enabled in the existing Lab Service API process.

## Testing strategy

### Backend tests
- rule evaluation for flag-based triggers,
- rule evaluation for threshold-based triggers,
- duplicate suppression on re-save,
- acknowledgment audit entries,
- resolve-on-correction behavior.

### Frontend tests
- rule editor validation,
- inbox rendering and acknowledge action,
- alert badge / toast updates.

### End-to-end tests
- create a rule,
- record a critical result,
- observe realtime alert delivery,
- acknowledge the alert,
- verify the audit trail and resolved/open state.

### Build and runtime verification
- build Lab Service,
- build Angular,
- run docker compose for the relevant stack,
- confirm the app starts and websocket delivery works.

## Acceptance criteria

- A critical result creates exactly one alert record.
- The alert is visible in the lab UI and in the alert inbox.
- The assigned clinician and authorized lab users receive a realtime notification.
- Acknowledge actions are stored with actor and timestamp.
- Re-saving the same critical result does not create duplicates.
- Corrected noncritical results resolve the alert and preserve history.
- All new behavior is covered by automated tests.
