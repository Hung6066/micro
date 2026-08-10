# Lab Critical Value Alert Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a critical value alert workflow to Lab Service with realtime in-app notifications, acknowledgment/audit history, and a lab UI inbox/rule editor.

**Architecture:** Keep alert detection inside the existing Lab Service application layer when lab results are recorded or edited. Persist alerts and audit entries in the same Lab database so the database remains the source of truth, then fan out realtime updates through a SignalR hub inside the existing Lab Service API process. The Angular lab area consumes the new alert APIs and socket stream without changing the broader app shell.

**Tech Stack:** .NET 8, ASP.NET Core minimal APIs, SignalR, EF Core, CockroachDB/PostgreSQL-compatible storage, xUnit, Testcontainers, Angular 19, Angular Material, RxJS, Jest, Playwright, Docker Compose.

## Global Constraints

- No new external service for analytics.
- No replacement of the current pipeline engine.
- No autonomous gate bypassing.
- No production model training system.
- No UI redesign.
- Metrics must be derived from existing persisted records; do not invent new runtime telemetry paths when the data already exists.
- Eval results must be reproducible from stored suite definitions.
- Learning must not auto-apply destructive changes.
- Adaptive gates may recommend thresholds, but human review remains required for critical paths.
- Security-sensitive tasks must continue to use redaction and existing guardrails.

---

### Task 1: Backend domain model, persistence, and alert evaluation

**Files:**
- Create: `src/Services/LabService/LabService.Domain/Entities/CriticalAlertRule.cs`
- Create: `src/Services/LabService/LabService.Domain/Entities/CriticalAlert.cs`
- Create: `src/Services/LabService/LabService.Domain/Entities/CriticalAlertAuditEntry.cs`
- Create: `src/Services/LabService/LabService.Domain/ValueObjects/CriticalAlertStatus.cs`
- Create: `src/Services/LabService/LabService.Domain/ValueObjects/CriticalAlertTriggerType.cs`
- Create: `src/Services/LabService/LabService.Domain/Repositories/ICriticalAlertRuleRepository.cs`
- Create: `src/Services/LabService/LabService.Domain/Repositories/ICriticalAlertRepository.cs`
- Create: `src/Services/LabService/LabService.Application/Common/Abstractions/ICurrentUserContext.cs`
- Create: `src/Services/LabService/LabService.Application/DTOs/CriticalAlertDtos.cs`
- Create: `src/Services/LabService/LabService.Application/Services/CriticalAlertEvaluator.cs`
- Modify: `src/Services/LabService/LabService.Infrastructure/Persistence/LabDbContext.cs`
- Create: `src/Services/LabService/LabService.Infrastructure/Persistence/Configurations/CriticalAlertRuleConfiguration.cs`
- Create: `src/Services/LabService/LabService.Infrastructure/Persistence/Configurations/CriticalAlertConfiguration.cs`
- Create: `src/Services/LabService/LabService.Infrastructure/Persistence/Configurations/CriticalAlertAuditEntryConfiguration.cs`
- Create: `src/Services/LabService/LabService.Infrastructure/Persistence/Repositories/CriticalAlertRuleRepository.cs`
- Create: `src/Services/LabService/LabService.Infrastructure/Persistence/Repositories/CriticalAlertRepository.cs`
- Modify: `src/Services/LabService/LabService.Infrastructure/DependencyInjection.cs`
- Test: `tests/Services/LabService/LabService.Domain.Tests/CriticalAlertRuleTests.cs`
- Test: `tests/Services/LabService/LabService.Domain.Tests/CriticalAlertTests.cs`
- Test: `tests/Services/LabService/LabService.Application.Tests/CriticalAlertEvaluatorTests.cs`

**Interfaces:**
- Consumes: `LabOrder`, `LabTest`, `LabResult`, current-user identity (`sub`, `fullName` claims via `ICurrentUserContext`), and active `CriticalAlertRule` rows.
- Produces: `CriticalAlertEvaluator.EvaluateAsync(...)`, `CriticalAlertEvaluator.ResolveAsync(...)`, `CriticalAlertRuleDto`, `CriticalAlertDto`, and `CriticalAlertAuditEntryDto`.

- [ ] **Step 1: Write failing domain and evaluator tests**

Create tests that assert:
- a critical flag creates a single open alert,
- a threshold match creates a single open alert,
- a second save of the same critical result updates the existing alert instead of duplicating it,
- a correction to a noncritical value resolves the alert,
- every state change writes an audit entry with actor information.

Run:
`dotnet test tests/Services/LabService/LabService.Domain.Tests/LabService.Domain.Tests.csproj --filter "CriticalAlertRuleTests|CriticalAlertTests" -v normal`

Run:
`dotnet test tests/Services/LabService/LabService.Application.Tests/LabService.Application.Tests.csproj --filter "CriticalAlertEvaluatorTests" -v normal`

Expected: fail because the alert types and evaluator do not exist yet.

- [ ] **Step 2: Implement the alert model and evaluator**

Implement the new alert entities and value objects, wire them into `LabDbContext`, add repository implementations, and register the evaluator and current-user abstraction in dependency injection.

Keep the storage model additive and compatible with the existing `EnsureCreated` bootstrap path; do not introduce a migration system in this iteration.

- [ ] **Step 3: Rerun the backend unit tests**

Run:
`dotnet test tests/Services/LabService/LabService.Domain.Tests/LabService.Domain.Tests.csproj --filter "CriticalAlertRuleTests|CriticalAlertTests" -v normal`

Run:
`dotnet test tests/Services/LabService/LabService.Application.Tests/LabService.Application.Tests.csproj --filter "CriticalAlertEvaluatorTests" -v normal`

Expected: pass.

- [ ] **Step 4: Commit**

Commit message: `feat(lab): add critical alert domain and evaluator`

---

### Task 2: Lab Service API, realtime hub, and alert endpoints

**Files:**
- Modify: `src/Services/LabService/LabService.Api/Program.cs`
- Create: `src/Services/LabService/LabService.Api/Hubs/LabCriticalAlertHub.cs`
- Create: `src/Services/LabService/LabService.Api/Services/HttpCurrentUserContext.cs`
- Create: `src/Services/LabService/LabService.Api/Services/CriticalAlertRealtimePublisher.cs`
- Create: `src/Services/LabService/LabService.Api/Endpoints/CriticalAlertEndpoints.cs`
- Modify: `src/Services/LabService/LabService.Application/UseCases/LabOrders/Commands/RecordLabResultCommand.cs`
- Modify: `src/Services/LabService/LabService.Application/UseCases/LabOrders/Commands/RecordLabOrderResultCommand.cs`
- Create: `src/Services/LabService/LabService.Application/UseCases/CriticalAlerts/Commands/AcknowledgeCriticalAlertCommand.cs`
- Create: `src/Services/LabService/LabService.Application/UseCases/CriticalAlerts/Commands/ResolveCriticalAlertCommand.cs`
- Create: `src/Services/LabService/LabService.Application/UseCases/CriticalAlerts/Queries/GetCriticalAlertsQuery.cs`
- Create: `src/Services/LabService/LabService.Application/UseCases/CriticalAlerts/Queries/GetCriticalAlertRulesQuery.cs`
- Create: `src/Services/LabService/LabService.Application/UseCases/CriticalAlerts/Commands/UpsertCriticalAlertRuleCommand.cs`
- Create: `src/Services/LabService/LabService.Application/UseCases/CriticalAlerts/Commands/DeleteCriticalAlertRuleCommand.cs`
- Create: `tests/Services/LabService/LabService.Integration.Tests/CriticalAlertEndpointsTests.cs`

**Interfaces:**
- Consumes: `ICurrentUserContext`, `CriticalAlertEvaluator`, `IHubContext<LabCriticalAlertHub>`, and the existing lab order/result command handlers.
- Produces: alert rule CRUD endpoints, alert inbox endpoints, `criticalAlertCreated` / `criticalAlertUpdated` / `criticalAlertAcknowledged` / `criticalAlertResolved` socket events, and alert-aware result-recording behavior.

- [ ] **Step 1: Write failing integration tests for the API and hub behavior**

Create tests that assert:
- alert rules can be created and listed,
- recording a critical result creates exactly one persisted alert,
- acknowledging an alert stores the actor and timestamp,
- resolving an alert keeps audit history,
- the realtime publisher emits the expected payload shape for open/acknowledged/resolved updates.

Run:
`dotnet test tests/Services/LabService/LabService.Integration.Tests/LabService.Integration.Tests.csproj --filter "CriticalAlertEndpointsTests" -v normal`

Expected: fail because the endpoints, hub, and command handlers are missing.

- [ ] **Step 2: Implement the API, hub, and command wiring**

Wire `AddSignalR()` and map the hub in `Program.cs`, add the new endpoints, and update the result-recording command handlers so alert evaluation runs on every result save and publishes notifications after persistence succeeds.

Use `Permission:lab.manage` for rule management and `Permission:lab.view` plus runtime ownership checks for acknowledgment and resolution.

- [ ] **Step 3: Rerun the integration tests**

Run:
`dotnet test tests/Services/LabService/LabService.Integration.Tests/LabService.Integration.Tests.csproj --filter "CriticalAlertEndpointsTests" -v normal`

Expected: pass.

- [ ] **Step 4: Commit**

Commit message: `feat(lab): add critical alert api and realtime hub`

---

### Task 3: Angular lab inbox, rule editor, and realtime client

**Files:**
- Modify: `src/Frontend/his-hope-app/package.json`
- Create: `src/Frontend/his-hope-app/src/app/core/models/critical-alert.model.ts`
- Create: `src/Frontend/his-hope-app/src/app/core/models/critical-alert-rule.model.ts`
- Create: `src/Frontend/his-hope-app/src/app/core/models/critical-alert-audit.model.ts`
- Create: `src/Frontend/his-hope-app/src/app/core/services/lab-critical-alert.service.ts`
- Create: `src/Frontend/his-hope-app/src/app/core/services/lab-critical-alert-stream.service.ts`
- Modify: `src/Frontend/his-hope-app/src/app/core/services/lab.service.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/lab/lab.routes.ts`
- Create: `src/Frontend/his-hope-app/src/app/features/lab/lab-critical-alerts/lab-critical-alerts.component.ts`
- Create: `src/Frontend/his-hope-app/src/app/features/lab/lab-critical-alert-rule-form/lab-critical-alert-rule-form.component.ts`
- Create: `src/Frontend/his-hope-app/src/app/features/lab/lab-critical-alert-detail/lab-critical-alert-detail.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/lab/lab-order-list/lab-order-list.component.ts`
- Modify: `src/Frontend/his-hope-app/src/app/features/lab/lab-order-detail/lab-order-detail.component.ts`
- Test: `src/Frontend/his-hope-app/src/app/core/services/lab-critical-alert.service.spec.ts`
- Test: `src/Frontend/his-hope-app/src/app/core/services/lab-critical-alert-stream.service.spec.ts`
- Test: `src/Frontend/his-hope-app/src/app/features/lab/lab-critical-alerts/lab-critical-alerts.component.spec.ts`
- Test: `src/Frontend/his-hope-app/src/app/features/lab/lab-critical-alert-rule-form/lab-critical-alert-rule-form.component.spec.ts`
- Test: `src/Frontend/his-hope-app/src/app/features/lab/lab-order-detail/lab-order-detail.component.spec.ts`

**Interfaces:**
- Consumes: the new lab critical alert REST endpoints and the SignalR hub, plus `AuthService.getStoredAccessToken()` for socket authentication.
- Produces: alert inbox UI, rule editor UI, realtime badge/toast updates, and alert-detail acknowledgment controls.

- [ ] **Step 1: Write failing Jest tests for the alert services and components**

Create tests that assert:
- the service calls the new alert endpoints,
- the socket service subscribes to `criticalAlertCreated` and increments unread state,
- the inbox renders open/acknowledged/resolved filters,
- the rule form validates threshold fields,
- the lab order detail page shows critical-state metadata and an acknowledge action.

Run:
`npm test -- --runInBand --testPathPattern=lab-critical-alert`

Expected: fail because the new components, services, and SignalR client do not exist yet.

- [ ] **Step 2: Implement the Angular feature**

Add the SignalR client dependency, create the alert models/services/components, wire the new lab route, and surface the alert badge and detail actions in the existing lab UI.

Keep the styling aligned with the current Angular Material patterns; do not redesign the lab module shell.

- [ ] **Step 3: Rerun the Jest suite and the Angular build**

Run:
`npm test -- --runInBand --testPathPattern=lab-critical-alert`

Run:
`npm run build`

Expected: both pass.

- [ ] **Step 4: Commit**

Commit message: `feat(lab-ui): add critical alert inbox and realtime updates`

---

### Task 4: End-to-end Playwright, build, and Docker verification

**Files:**
- Create: `tests/e2e/specs/13-critical-alerts.spec.js`
- Update only if needed: `tests/e2e/specs/07-lab.spec.js`
- Update only if needed: `docker/docker-compose.yml`

**Interfaces:**
- Consumes: the implemented Lab Service API, Angular UI, and Docker Compose stack.
- Produces: end-to-end evidence that a rule can be created, a critical result can be recorded, realtime notification appears, and the alert can be acknowledged.

- [ ] **Step 1: Write the failing Playwright flow**

Create a spec that asserts:
- a lab-admin or equivalent user can open the critical alert inbox,
- a critical rule can be created,
- recording a critical result shows a toast/badge update,
- the alert can be acknowledged,
- the audit/history panel shows the acknowledgment.

Run:
`npx playwright test tests/e2e/specs/13-critical-alerts.spec.js --workers=1`

Expected: fail until the feature is implemented.

- [ ] **Step 2: Build and start the Docker stack**

Run:
`docker compose -f docker/docker-compose.yml build labservice frontend`

Run:
`docker compose -f docker/docker-compose.yml up -d postgres redis rabbitmq identityservice patientservice clinicalservice labservice apigateway frontend`

Run:
`docker compose -f docker/docker-compose.yml ps`

Expected: labservice, apigateway, and frontend become healthy and the app is reachable on `http://localhost:8081`.

- [ ] **Step 3: Rerun backend, frontend, and Playwright checks**

Run:
`dotnet test tests/Services/LabService/LabService.Domain.Tests/LabService.Domain.Tests.csproj -v normal`

Run:
`dotnet test tests/Services/LabService/LabService.Application.Tests/LabService.Application.Tests.csproj -v normal`

Run:
`dotnet test tests/Services/LabService/LabService.Integration.Tests/LabService.Integration.Tests.csproj -v normal`

Run:
`npm test -- --runInBand --testPathPattern=lab-critical-alert`

Run:
`npx playwright test tests/e2e/specs/13-critical-alerts.spec.js --workers=1`

Expected: all pass.

- [ ] **Step 4: Commit**

Commit message: `test(lab): add end-to-end critical alert verification`

---

### Task 5: Agent Harness execution and agent coverage evaluation

**Files:**
- Create: `docs/superpowers/reports/2026-07-20-lab-critical-alert-agent-eval.md`
- Inspect: `src/Infrastructure/AgentHarness/**/*`

**Interfaces:**
- Consumes: the completed Lab Service, Angular, test, and Docker work from Tasks 1–4.
- Produces: a short evaluation of whether the agent harness executed the right agents, finished the workflow, and covered the full feature surface.

- [ ] **Step 1: Run the feature through Agent Harness**

Use the harness workflow for the feature with the minimal agent set that actually changed code:
- `dotnet-agent`
- `angular-agent`
- `testing-backend-agent`
- `testing-frontend-agent`
- `devops-agent`
- `qa-agent`

Capture the run id, agent run ids, and quality-gate results.

- [ ] **Step 2: Inspect pipeline status and timeline**

Use the harness status/timeline tooling to verify:
- each expected agent ran,
- no unnecessary agents were dispatched,
- quality gates passed or were fixed,
- the Docker and Playwright verification ran successfully.

- [ ] **Step 3: Write the evaluation report**

Create `docs/superpowers/reports/2026-07-20-lab-critical-alert-agent-eval.md` with:
- the agents that ran,
- the agents that were skipped,
- whether the workflow completed end to end,
- any loop-engine interventions,
- and a yes/no verdict on whether the agent harness fully handled this feature.

- [ ] **Step 4: Commit**

Commit message: `docs(lab): add agent harness evaluation for critical alert feature`
