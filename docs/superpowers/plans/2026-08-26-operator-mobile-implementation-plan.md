# Operator Mobile Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a secure, permission-aware, offline-capable Capacitor application for Manufacturing production, quality, and maintenance field workflows.

**Architecture:** `operator-mobile` is an independent Angular host using the shared mobile and frontend foundations. Typed feature services submit commands to one encrypted offline queue; Manufacturing API policies enforce tenant, permission, idempotency and version checks.

**Tech Stack:** Angular 21, Capacitor 7, TypeScript, RxJS, ASP.NET Core 8, EF Core, xUnit, Jasmine/Karma, Playwright.

**Spec:** `docs/superpowers/specs/2026-08-26-operator-mobile-design.md`

## Global Constraints

- Do not import desktop feature components into `operator-mobile`.
- Use external templates/styles, functional guards, i18n, and foundation interceptors.
- Use PKCE plus device-backed secure storage; never web storage for tokens.
- Enforce permission and tenant scope in the API; UI visibility is not authorisation.
- Queue only active-shift operational commands, encrypted and bound to tenant and subject.
- Do not retry business 4xx responses; synchronise serially with bounded transient retries.
- Preserve Manufacturing Application-port and domain-workflow boundaries.

---

## Planned file structure

| Path | Responsibility |
| --- | --- |
| `operator-mobile/` | Independent Angular/Capacitor host. |
| `operator-mobile/src/app/core/authorization/operator-mobile-permissions.ts` | Route/action permission constants. |
| `operator-mobile/src/app/core/operator-mobile-tenant-context.service.ts` | Tenant selection and scope reset. |
| `operator-mobile/src/app/core/offline/operation-queue.service.ts` | Encrypt, persist, replay, and resolve commands. |
| `operator-mobile/src/app/core/services/operator-mobile-api.service.ts` | Typed Manufacturing transport. |
| `operator-mobile/src/app/features/` | Production, traceability, quality, maintenance mobile screens. |
| `src/Services/ManufacturingService/ManufacturingService.Api/Program.cs` | Endpoint policy, tenant, replay, and version gates. |
| `src/Services/ManufacturingService/ManufacturingService.Application/Ports/IManufacturingMobileOperationStore.cs` | Operation-ledger port. |
| `src/Services/ManufacturingService/ManufacturingService.Infrastructure/Persistence/ManufacturingMobileOperations.cs` | Durable operation ledger. |
| `tests/Services/ManufacturingService/ManufacturingService.Integration.Tests/MobileOperationAuthorizationTests.cs` | API policy/replay/conflict tests. |
| `tests/e2e/operator-mobile-ui-tests.mjs` | Role and offline E2E tests. |

### Task 1: Authorise field mutations in Manufacturing API

**Files:**
- Modify: `src/Services/ManufacturingService/ManufacturingService.Api/Program.cs`
- Modify: existing shared authorization registration file that owns policy definitions.
- Create: `tests/Services/ManufacturingService/ManufacturingService.Integration.Tests/MobileOperationAuthorizationTests.cs`

**Produces:** endpoint policies `manufacturing.production.execute`, `manufacturing.quality.inspect`, and `manufacturing.maintenance.complete` with effective-tenant validation.

- [ ] **Step 1: Write failing authorization tests.**

```csharp
[Fact]
public async Task Production_start_rejects_subject_without_execute_permission()
{
    using var client = CreateClient("qc-user", "factory-a", ["manufacturing.quality.inspect"]);
    var response = await client.PostAsync($"/api/v1/manufacturing/production-batches/{BatchId}/start", JsonContent.Create(new { }));
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}

[Fact]
public async Task Quality_inspection_rejects_out_of_scope_tenant()
{
    using var client = CreateClient("qc-user", "factory-a", ["manufacturing.quality.inspect"]);
    var response = await client.PostAsJsonAsync("/api/v1/manufacturing/quality-inspections?tenantKey=factory-b", ValidInspection);
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

- [ ] **Step 2: Run focused test; expect failure because policy/scope enforcement is missing.**

Run: `dotnet test tests/Services/ManufacturingService/ManufacturingService.Integration.Tests/ManufacturingService.Integration.Tests.csproj --filter FullyQualifiedName~MobileOperationAuthorizationTests`

- [ ] **Step 3: Add policies and require them only for corresponding field mutations.**

```csharp
api.MapPost("/production-batches/{batchId:guid}/start", StartBatch).RequireAuthorization("manufacturing.production.execute");
api.MapPost("/quality-inspections", CreateInspection).RequireAuthorization("manufacturing.quality.inspect");
api.MapPost("/machines/{machineId:guid}/maintenance-work-orders/{workOrderId:guid}/complete", CompleteWorkOrder).RequireAuthorization("manufacturing.maintenance.complete");
```

Resolve subject/effective tenant before invoking an Application store and return `Forbid()` for a mismatch.

- [ ] **Step 4: Re-run focused test; expect PASS.**
- [ ] **Step 5: Commit only Task 1 files with `git add src/Services/ManufacturingService tests/Services/ManufacturingService/ManufacturingService.Integration.Tests && git commit -m "feat(manufacturing): authorise field operations"`.**

### Task 2: Make queued field mutations replay-safe

**Files:**
- Create: `src/Services/ManufacturingService/ManufacturingService.Application/Ports/IManufacturingMobileOperationStore.cs`
- Create: `src/Services/ManufacturingService/ManufacturingService.Infrastructure/Persistence/ManufacturingMobileOperations.cs`
- Modify: `src/Services/ManufacturingService/ManufacturingService.Infrastructure/ManufacturingInfrastructureServiceCollectionExtensions.cs`
- Modify: Task 1 test file and `Program.cs`.

**Produces:** `IManufacturingMobileOperationStore.ExecuteAsync(tenantKey, subjectId, endpoint, operationId, expectedVersion, apply, cancellationToken)`; replay returns original success and stale version returns 409 state/version.

- [ ] **Step 1: Add two failing integration tests.**

```csharp
[Fact]
public async Task Same_operation_id_is_applied_once_and_replay_is_marked()
{
    var first = await StartBatch("f99a2b3e-1c5d-4c4e-8d0d-111111111111");
    var replay = await StartBatch("f99a2b3e-1c5d-4c4e-8d0d-111111111111");
    first.StatusCode.Should().Be(HttpStatusCode.OK);
    replay.Headers.GetValues("X-HisHope-Operation-Replay").Single().Should().Be("true");
}

[Fact]
public async Task Stale_version_returns_conflict_without_mutation()
{
    (await StartBatch(Guid.NewGuid().ToString(), "W/\"stale\"")).StatusCode.Should().Be(HttpStatusCode.Conflict);
}
```

- [ ] **Step 2: Run focused test; expect replay/version assertions to fail.**
- [ ] **Step 3: Persist a unique tenant+subject+endpoint+operation ledger record; return stored success on replay and structured 409 before apply for stale state.**
- [ ] **Step 4: Run focused test and full Manufacturing integration project; expect PASS.**

Run: `dotnet test tests/Services/ManufacturingService/ManufacturingService.Integration.Tests/ManufacturingService.Integration.Tests.csproj`

- [ ] **Step 5: Commit Task 2 files with `feat(manufacturing): support offline operation replay`.**

### Task 3: Scaffold a secure standalone Operator Mobile host

**Files:**
- Create: `operator-mobile/package.json`, `operator-mobile/angular.json`, `operator-mobile/capacitor.config.ts`
- Create: `operator-mobile/src/app/app.config.ts`, `app.routes.ts`, and `operator-mobile-shell.component.{ts,html,scss}`
- Create: `operator-mobile/src/app/core/authorization/operator-mobile-permissions.{ts,spec.ts}`

**Produces:** application id `com.hishope.operator.mobile`; routes `/operations/production`, `/operations/quality`, and `/operations/maintenance`; constants consumed by all feature guards.

- [ ] **Step 1: Write the failing permission-map spec.**

```ts
it('maps every mobile mutation to its narrow permission', () => {
  expect(operatorMobilePermissions.production.execute).toBe('manufacturing.production.execute');
  expect(operatorMobilePermissions.quality.inspect).toBe('manufacturing.quality.inspect');
  expect(operatorMobilePermissions.maintenance.complete).toBe('manufacturing.maintenance.complete');
});
```

- [ ] **Step 2: Run `npm --prefix operator-mobile test -- --include='**/operator-mobile-permissions.spec.ts'`; expect host/module-not-found failure.**
- [ ] **Step 3: Create host by adapting only host/security configuration from `mobile-app`: foundation providers, secure OIDC storage, native HTTP/session interceptors, native certificate settings and external shell. Use a distinct public OIDC client and configured redirect URI.**
- [ ] **Step 4: Run focused spec, `npm --prefix operator-mobile run lint`, and `npm --prefix operator-mobile run build`; expect PASS.**
- [ ] **Step 5: Commit with `feat: scaffold operator mobile host`.**

### Task 4: Add tenant-safe encrypted operation queue

**Files:**
- Create: `operator-mobile/src/app/core/offline/operation-queue.models.ts`
- Create: `operator-mobile/src/app/core/offline/operation-queue.service.{ts,spec.ts}`
- Create: `operator-mobile/src/app/core/operator-mobile-tenant-context.service.{ts,spec.ts}`
- Modify: `operator-mobile/src/app/app.config.ts`

**Produces:** `QueuedOperation` and `OperationQueueService.submit()`, `sync()`, `entries()`, `discard()`; each operation has `operationId`, tenant, subject, expectedVersion, endpoint, payload, time and terminal status.

- [ ] **Step 1: Write failing queue specs.**

```ts
it('stores a network-failed mutation as pending with one stable operation id', async () => {
  const result = await queue.submit(command, offlineTransport);
  expect(result.status).toBe('pending');
  expect((await queue.entries())[0].operationId).toMatch(uuidPattern);
});

it('marks a 409 as conflict and does not retry it', async () => {
  await queue.enqueue(command);
  await queue.sync(conflictTransport);
  expect((await queue.entries())[0].status).toBe('conflict');
  expect(conflictTransport).toHaveBeenCalledTimes(1);
});
```

- [ ] **Step 2: Run focused spec; expect missing service failure.**
- [ ] **Step 3: Implement encrypted secure-storage persistence, serial sync, one UUID per command, `X-HisHope-Operation-Id`, bounded transient retries, and scope clearing after tenant/subject/permission change.**
- [ ] **Step 4: Run all operator-mobile specs, lint and build; expect PASS.**
- [ ] **Step 5: Commit with `feat(operator-mobile): add secure offline operation queue`.**

### Task 5: Implement Production and Traceability vertical slice

**Files:**
- Create: `operator-mobile/src/app/core/services/operator-mobile-api.service.ts`
- Create: `operator-mobile/src/app/features/production/production-work-page.component.{ts,html,scss,spec.ts}`
- Create: `operator-mobile/src/app/features/traceability/lot-scan-page.component.{ts,html,scss}`
- Modify: `operator-mobile/src/app/app.routes.ts`

**Produces:** typed batch/lot reads and `recordProductionOperation(batchId, command, operationId, expectedVersion)`; pages only use `OperationQueueService.submit()` for mutations.

- [ ] **Step 1: Write a failing page spec.**

```ts
it('queues a production operation while offline and shows pending sync', async () => {
  await component.submitOperation(validOperation);
  expect(queue.submit).toHaveBeenCalledWith(jasmine.objectContaining({ endpoint: '/production-batches/batch-1/operations' }));
  expect(fixture.nativeElement.textContent).toContain('Pending sync');
});
```

- [ ] **Step 2: Run focused spec; expect feature-not-found failure.**
- [ ] **Step 3: Build assigned-batch, operation-result, lot lookup and scanner screens. Use `NativeCapabilityService` for camera scanning with manual-entry fallback; locally validate required quantity/fields.**
- [ ] **Step 4: Run focused spec, lint, build and `MobileOperationAuthorizationTests`; expect PASS.**
- [ ] **Step 5: Commit with `feat(operator-mobile): add production field workflow`.**

### Task 6: Implement Quality Control vertical slice

**Files:**
- Create: `operator-mobile/src/app/features/quality/quality-inspection-page.component.{ts,html,scss,spec.ts}`
- Modify: `operator-mobile/src/app/app.routes.ts`

**Produces:** queued inspection/deviation commands restricted to quality permissions; selected lot version is carried as `expectedVersion`.

- [ ] **Step 1: Write failing specs.**

```ts
it('rejects an inspection without lot, status and inspector before queueing', async () => {
  component.draft = { lotId: '', status: '', inspector: '' };
  await component.submit();
  expect(queue.submit).not.toHaveBeenCalled();
});

it('queues a valid inspection with selected lot version', async () => {
  component.draft = validInspection;
  await component.submit();
  expect(queue.submit).toHaveBeenCalledWith(jasmine.objectContaining({ expectedVersion: 'W/"lot-v3"' }));
});
```

- [ ] **Step 2: Run focused spec; expect failure.**
- [ ] **Step 3: Implement lot/specification history, validated inspection capture, deviation reporting and queue-state feedback; omit specification approval.**
- [ ] **Step 4: Run focused spec, lint, build and Manufacturing integration tests; expect PASS.**
- [ ] **Step 5: Commit with `feat(operator-mobile): add quality inspection workflow`.**

### Task 7: Implement Maintenance vertical slice

**Files:**
- Create: `operator-mobile/src/app/features/maintenance/maintenance-work-page.component.{ts,html,scss,spec.ts}`
- Modify: `operator-mobile/src/app/app.routes.ts`

**Produces:** queued work-order completion, telemetry and downtime commands; evidence capture remains behind native capability boundary.

- [ ] **Step 1: Write failing specs.**

```ts
it('does not complete an order until every required checklist item is checked', async () => {
  component.checklist = [{ label: 'Isolation', completed: false }];
  await component.complete();
  expect(queue.submit).not.toHaveBeenCalled();
});

it('queues completion with evidence metadata', async () => {
  component.checklist = [{ label: 'Isolation', completed: true }];
  component.evidence = [{ name: 'panel.jpg', contentType: 'image/jpeg', reference: 'secure://evidence/1' }];
  await component.complete();
  expect(queue.submit).toHaveBeenCalled();
});
```

- [ ] **Step 2: Run focused spec; expect failure.**
- [ ] **Step 3: Implement assigned work order, checklist, evidence, telemetry, downtime and completion flow; omit machine creation and bulk planning.**
- [ ] **Step 4: Run focused spec, lint, build and Manufacturing integration tests; expect PASS.**
- [ ] **Step 5: Commit with `feat(operator-mobile): add maintenance field workflow`.**

### Task 8: Prove role, sync, and native acceptance gates

**Files:**
- Create: `tests/e2e/operator-mobile-ui-tests.mjs`
- Create: `tests/e2e/operator-mobile.playwright.config.mjs`
- Modify: `docker/docker-compose.yml` only if a test host service is indispensable.

**Produces:** authenticated E2E coverage for Production, QC, Maintenance, denied permissions, tenant isolation, and pending/synced/failed/conflict queue states.

- [ ] **Step 1: Write failing E2E coverage.**

```js
test('production operator records offline then synchronises online', async ({ page, context }) => {
  await loginAs(page, 'production.operator');
  await context.setOffline(true);
  await page.getByRole('button', { name: 'Record operation' }).click();
  await expect(page.getByText('Pending sync')).toBeVisible();
  await context.setOffline(false);
  await page.getByRole('button', { name: 'Sync now' }).click();
  await expect(page.getByText('Synced')).toBeVisible();
});
```

- [ ] **Step 2: Run `npx playwright test tests/e2e/operator-mobile-ui-tests.mjs --workers=1`; expect failure before feature implementation.**
- [ ] **Step 3: Add fixtures/assertions for authorized workflows, denied route/action, tenant cache clearing, 4xx failure, 409 conflict and replay-safe retry.**
- [ ] **Step 4: Run lint, build, all mobile specs, Manufacturing integration tests, then the Playwright command with one worker; expect PASS.**
- [ ] **Step 5: Verify Android/iOS OIDC callback, secure storage, biometric and QR scan on emulator/device. Mark unavailable hardware as environment-blocked, never passed; commit E2E files with `test: cover operator mobile workflows`.**

## Plan self-review

- Spec coverage: Tasks 1–2 implement server authorisation, scope, idempotency and conflicts; Tasks 3–4 implement host, security, tenant and encrypted queue; Tasks 5–7 implement all three role workflows; Task 8 implements the required automated and native gates.
- Placeholder scan: no deferred or unspecified implementation/test steps remain.
- Type consistency: all feature writes use `OperationQueueService`, which supplies the operation ID and expected version consumed by Task 2.
